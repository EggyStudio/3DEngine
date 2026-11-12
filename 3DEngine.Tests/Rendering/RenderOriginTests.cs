using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A scene drawn with the frame's places taken against a render origin away from the world's own,
/// read as the same scene drawn against the world's own origin, within the rounding of the places'
/// subtraction, so no pass reads one place against the origin and another as given.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class RenderOriginTests : IDisposable
{
    private const int Width = 160, Height = 90;

    public void Dispose()
    {
        RenderOrigin.Override = null;
        CloseWindow();
        UseApp(null);
    }

    // The scene drawn against the origin given, the same frames for either origin, the level of
    // each pixel's light in sRGB's 255ths, bent as l / (1 + l) first so no light past 1 is lost to
    // the clamp. A floor and a wall, a block, a ball whose light scatters
    // under its surface, still particles lit as surfaces, and a sun, a lamp and a spot light, each
    // casting shadows, at Low as many frames as the field takes to settle and two more. At High as
    // well fog, the occlusion, the floor glossy and mirroring the scene in its plane, a reflection
    // probe and a lantern whose light global illumination carries, ten frames more, the probes'
    // rays traced through the device's rays where it has them (gi.toggle meshes), which read the
    // ray scene's copies.
    private static float[] Drawn(RenderOrigin origin, GlobalIllumination quality)
    {
        RenderOrigin.Override = origin;
        var config = Config.Default.WithWindow("render origin", Width, Height) with
        {
            Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var high = quality == GlobalIllumination.High;
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(quality);
        SetSubsurfaceQuality(SubsurfaceQuality.Low);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 1.5f, castsShadows: true);
        CreatePointLight(new Vector3(1.6f, 1.3f, 1.2f), new Color(255, 200, 150), 4, range: 6, castsShadows: true);
        CreateSpotLight(new Vector3(-2, 2.5f, 1.5f), Vector3.Normalize(new Vector3(0.4f, -1, -0.6f)), new Color(150, 200, 255), 8,
            range: 8, castsShadows: true);
        var still = ParticleEmitter.Default with
        {
            MaxParticles = 40, Emitting = false, Burst = 40, Life = 60, Velocity = Vector3.Zero, Gravity = Vector3.Zero,
            Radius = 0.4f, StartSize = 0.2f, EndSize = 0.2f, StartColor = new Color(220, 220, 230), EndColor = new Color(220, 220, 230),
            Lit = true, Blend = ParticleBlend.Alpha,
        };
        CreateParticleEmitter(new Vector3(0.2f, 1.6f, 0.4f), still);
        if (high)
        {
            SetVolumetricFog(0.04f, 1, 0.5f);
            SetAmbientOcclusion(1, 1);
            SetReflectionPlane(0, Vector3.UnitY);
            CreateReflectionProbe(new Vector3(0, 1.5f, 0), new Vector3(8, 4, 8));
            GetApp().World.Resource<GlobalIlluminationSettings>().MeshesTraced = true;
        }
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var ball = LoadModelFromMesh(GenMeshSphere(0.5f, 16, 16));
        var scattering = ball.Materials[0] with { SubsurfaceRadius = 0.3f, SubsurfaceColor = new Color(255, 90, 60) };
        var floor = block.Materials[0] with { Roughness = high ? 0.1f : 0.5f };
        var lantern = block.Materials[0] with { Emissive = new Color(255, 210, 150), EmissiveIntensity = 6 };
        var view = new Camera3D(new Vector3(0, 3, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + (high ? 12 : 2); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(Vector3.Zero);
            DrawMesh(block.Meshes[0], floor, Matrix4x4.CreateScale(8, 1, 8) * Matrix4x4.CreateTranslation(0, -0.5f, 0));
            DrawModelEx(block, new Vector3(0, 1, -2), Vector3.UnitY, 0, new Vector3(8, 3, 1), new Color(180, 120, 90));
            DrawModelEx(block, new Vector3(-1.2f, 0.5f, 0.3f), Vector3.UnitY, 30, Vector3.One, Color.White);
            DrawMesh(ball.Meshes[0], scattering, Matrix4x4.CreateTranslation(1.3f, 0.5f, 0.6f));
            if (high) DrawMesh(block.Meshes[0], lantern, Matrix4x4.CreateScale(0.2f) * Matrix4x4.CreateTranslation(-2.2f, 0.3f, 0));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        var levels = new float[Width * Height];
        for (int i = 0; i < levels.Length; i++)
        {
            var l = 0.2126f * linear[i * 4] + 0.7152f * linear[i * 4 + 1] + 0.0722f * linear[i * 4 + 2];
            l /= 1 + l;
            levels[i] = 255 * Math.Clamp(l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f, 0, 1);
        }
        UnloadModel(block);
        UnloadModel(ball);
        CloseWindow();
        UseApp(null);
        return levels;
    }

    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Scene_Drawn_Against_An_Origin_Away_From_The_Worlds_Reads_As_Against_The_Worlds(GlobalIllumination quality)
    {
        var at = Drawn(RenderOrigin.Zero, quality);
        var away = Drawn(new RenderOrigin(64, -32, 128), quality);
        at.Count(level => level > 8).Should().BeGreaterThan(Width * Height / 2, "the scene is lit across most of the window");
        var largest = at.Zip(away, (a, b) => Math.Abs(a - b)).Max();
        // A level, past the rounding of the places against the origin to their floats' step and
        // short of what a place read against the other origin moves.
        largest.Should().BeLessThan(1, "every pass takes its places against the one origin");
    }
}
