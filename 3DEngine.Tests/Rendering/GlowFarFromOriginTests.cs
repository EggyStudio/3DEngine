using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A glow light's light on a wall beside it far from the world's origin, as a voxel game's world
/// stands, read as the same room reads at the origin.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class GlowFarFromOriginTests : IDisposable
{
    private const int Width = 960, Height = 540;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // The room's blocks by their least corners: a floor, a ceiling and four walls about an inside 8
    // wide, 4 high and 5 deep, and a pocket before the lantern's place in the north wall at the
    // floor, closed at its sides and in front and open upward from 2.
    private static IEnumerable<Vector3> Blocks()
    {
        for (int x = -1; x <= 8; x++)
            for (int z = -1; z <= 5; z++)
            {
                yield return new Vector3(x, -1, z);
                yield return new Vector3(x, 4, z);
            }
        for (int y = 0; y < 4; y++)
        {
            for (int x = -1; x <= 8; x++)
            {
                if (!(x == 5 && y == 0)) yield return new Vector3(x, y, -1);
                yield return new Vector3(x, y, 5);
            }
            for (int z = 0; z < 5; z++)
            {
                yield return new Vector3(-1, y, z);
                yield return new Vector3(8, y, z);
            }
        }
        for (int y = 0; y < 2; y++)
        {
            yield return new Vector3(3, y, 0);
            yield return new Vector3(7, y, 0);
            for (int x = 4; x <= 6; x++) yield return new Vector3(x, y, 1);
        }
    }

    // The mean step in levels between points 1/32 apart along rows of the east wall over the pocket,
    // the room standing at origin, settled at the quality given.
    private static double WallStep(Vector3 origin, GlobalIllumination quality)
    {
        var config = Config.Default.WithWindow("far from origin", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f);
        SetGlobalIllumination(quality);
        SetAmbientLight(Color.Black, 0);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // The lantern as a voxel game's emissive mesh holds it, the one face that shows, a second
        // square far below cutting the mesh into parts, each a light of its own.
        Vector3[] squares =
        [
            new(5, 0, 0), new(6, 0, 0), new(6, 1, 0), new(5, 1, 0),
            new(40, -20, 40), new(41, -20, 40), new(41, -19, 40), new(40, -19, 40),
        ];
        var lantern = LoadModelFromMesh(UploadMesh(squares, [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7], null, null));
        lantern.Materials[0].Emissive = new Color(200, 235, 255);
        lantern.Materials[0].EmissiveIntensity = 6;
        var blocks = Blocks().ToArray();
        var view = new Camera3D(origin + new Vector3(1.5f, 2.6f, 4.3f), origin + new Vector3(8, 2.6f, 0.6f), Vector3.UnitY, 70);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 40; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(origin + new Vector3(4, 2, 2));
            foreach (var b in blocks) DrawModel(block, origin + b + new Vector3(0.5f), 1, new Color(210, 210, 205));
            DrawModelEx(block, origin + new Vector3(5.5f, 0.5f, -0.51f), Vector3.UnitY, 0, new Vector3(1, 1, 0.98f), new Color(210, 210, 205));
            DrawModel(lantern, origin, 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        float Level(int x, int y)
        {
            var i = (y * Width + x) * 4;
            var l = 0.2126f * linear[i] + 0.7152f * linear[i + 1] + 0.0722f * linear[i + 2];
            return 255 * Math.Clamp(l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f, 0, 1);
        }
        var (steps, count) = (0.0, 0);
        for (int row = 0; row < 16; row++)
        {
            float? before = null;
            for (int i = 0; i <= 74; i++)
            {
                var pixel = GetWorldToScreenEx(origin + new Vector3(7.999f, 2.9f + row / 16f, i / 32f), view, Width, Height);
                var (x, y) = ((int)pixel.X, (int)pixel.Y);
                if (x < 0 || y < 0 || x >= Width || y >= Height) { before = null; continue; }
                var level = Level(x, y);
                if (before is { } b) { steps += Math.Abs(level - b); count++; }
                before = level;
            }
        }
        UnloadModel(block);
        UnloadModel(lantern);
        CloseWindow();
        UseApp(null);
        return steps / Math.Max(count, 1);
    }

    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Glow_Lights_Light_On_A_Wall_Far_From_The_Origin_Is_As_Smooth_As_At_The_Origin(GlobalIllumination quality)
    {
        // A lamp flush in a wall at the floor in a pocket open upward, lit through one face, and the
        // wall beside it struck at a low angle. Its points and normals worked out from the window's
        // depth in the world's own numbers lost their last bits far from the origin, so the
        // wall's light ran in stripes there that the same room at the origin has none of.
        var near = WallStep(Vector3.Zero, quality);
        var far = WallStep(new Vector3(4447, 24, 596), quality);
        far.Should().BeLessThan(near * 1.5 + 0.1, $"at {quality} the wall far from the origin steps {far:0.00} levels between points 1/32 apart, as the same wall at the origin does, {near:0.00}");
    }
}
