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

    // A room standing at a place in the world, drawn with the origin the renderer stands at the
    // window camera's eye, the level of each pixel's light in sRGB's 255ths, bent as the scene's
    // above. Its floor, wall and blocks, a mesh entity, a lamp, the sun, cubes and lines of the
    // immediate pass and particles kept in their emitter's space lie on eighths of a unit, a float's
    // step at 1,140,000, so the room is the same room wherever it stands, and the field's cascades
    // and the probes lie alike about it where it stands at a whole count of 1,024 units. With
    // shadows, the sun and the lamp cast them.
    private static float[] Standing(Vector3 at, bool shadows = false)
    {
        var config = Config.Default.WithWindow("render origin", Width, Height) with
        {
            Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(GlobalIllumination.Low);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 1.5f, castsShadows: shadows);
        CreatePointLight(at + new Vector3(1.5f, 1.25f, 1.25f), new Color(255, 200, 150), 4, range: 6, castsShadows: shadows);
        CreateParticleEmitter(at + new Vector3(0.25f, 1.5f, 0.5f), ParticleEmitter.Default with
        {
            MaxParticles = 40, Emitting = false, Burst = 40, Life = 60, Velocity = Vector3.Zero, Gravity = Vector3.Zero, Local = true,
            Radius = 0.375f, StartSize = 0.25f, EndSize = 0.25f, StartColor = new Color(220, 220, 230), EndColor = new Color(220, 220, 230),
        });
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var ecs = GetApp().World.Resource<EcsWorld>();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Mesh(block.Meshes[0]));
        ecs.Add(entity, new Material(new Vector4(0.4f, 0.8f, 0.5f, 1)));
        ecs.Add(entity, new Transform(at + new Vector3(0.5f, 0.5f, 1)));
        var view = new Camera3D(at + new Vector3(0, 2.5f, 5.5f), at + new Vector3(0, 0.5f, 0), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 4; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(at);
            DrawModelEx(block, at + new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(8, 1, 8), new Color(200, 190, 170));
            DrawModelEx(block, at + new Vector3(0, 1.5f, -2.5f), Vector3.UnitY, 0, new Vector3(8, 4, 1), new Color(180, 120, 90));
            DrawModelEx(block, at + new Vector3(-1.25f, 0.5f, 0.25f), Vector3.UnitY, 0, Vector3.One, Color.White);
            DrawModelEx(block, at + new Vector3(1.25f, 0.25f, -0.5f), Vector3.UnitY, 0, new Vector3(0.5f), new Color(120, 160, 220));
            DrawCube(at + new Vector3(-2, 0.25f, 1.25f), 0.5f, 0.5f, 0.5f, new Color(230, 90, 60));
            DrawCubeWires(at + new Vector3(-2, 0.25f, 1.25f), 0.75f, 0.75f, 0.75f, Color.Black);
            DrawLine3D(at + new Vector3(-3, 0.125f, 2), at + new Vector3(3, 0.125f, 2), Color.Black);
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
        CloseWindow();
        UseApp(null);
        return levels;
    }

    [NeedsVulkanTheory]
    [InlineData(4096f, 1024f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void A_Room_Far_From_The_Worlds_Origin_Reads_As_The_Room_At_It(float x, float z)
    {
        var home = Standing(Vector3.Zero);
        var far = Standing(new Vector3(x, 0, z));
        home.Count(level => level > 8).Should().BeGreaterThan(Width * Height / 2, "the room is lit across most of the window");
        var largest = home.Zip(far, (a, b) => Math.Abs(a - b)).Max();
        // A tenth of a level, short of what a view pushed from its own matrix in floats moves the
        // room by, where the eye it was made from is set aside (RenderOrigin.Seen).
        largest.Should().BeLessThan(0.1f, "the room far out is handed to the GPU in the numbers the room at the origin is");
    }

    // A floor's shader of the program's own that reads the sun's shadow at its world point, as a
    // voxel game's terrain does, and lights the floor with it.
    private const string HandedShader = """
        import modelpass;
        import lights;

        [shader("fragment")]
        float4 fragmentMain(ModelVertexOutput input) : SV_Target
        {
            float4 color = baseColor(input);
            float held = 0.0;
            float shadow = lightReaching(input.world, normalize(input.normal), held);
            LitLight light = litLight(color.rgb, input, shadow, held);
            return float4(toDisplay(light.diffuse + light.specular), color.a);
        }
        """;

    // One that lights each point of the floor as the point a unit toward -z, its world moved before
    // it is handed to litLight, as a game lights a mob's face at a point off it.
    private const string MovedShader = """
        import modelpass;

        [shader("fragment")]
        float4 fragmentMain(ModelVertexOutput input) : SV_Target
        {
            float4 color = baseColor(input);
            ModelVertexOutput moved = input;
            moved.world = input.world - float3(0.0, 0.0, 1.0);
            LitLight light = litLight(color.rgb, moved);
            return float4(toDisplay(light.diffuse + light.specular), color.a);
        }
        """;

    // A post and a block on a floor drawn with the shader given, at a place in the world, the sun's
    // light travelling toward -x and down, the camera standing or, with crossing, moving three units
    // along x from 6.5 to 9.5 once the field settles, past 8, where the origin steps; the level of
    // the floor's light where the post's shadow falls and a unit to its side.
    private static (float Shadowed, float Beside) Posted(Vector3 at, string shader, bool crossing)
    {
        var config = Config.Default.WithWindow("render origin", Width, Height) with
        {
            Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(GlobalIllumination.Low);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -1, 0)), Color.White, 1, castsShadows: true);
        SetAmbientLight(Color.White, 0.1f);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0].Shader = LoadShaderFromMemory(shader);
        var view = new Camera3D();
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 4; frame++)
        {
            var moved = crossing ? Math.Max(0, frame - SceneFieldPlan.SettleFrames + 1) * 0.75f : 0;
            view = new Camera3D(at + new Vector3(6.5f + moved, 3, 8), at + new Vector3(moved, 0.5f, 0), Vector3.UnitY, 60);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(at);
            DrawModelEx(floor, at + new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(16, 1, 12), new Color(200, 200, 200));
            DrawModelEx(block, at + new Vector3(0, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.5f, 3, 0.5f), new Color(200, 200, 200));
            DrawModelEx(block, at + new Vector3(-2, 0.5f, 2.5f), Vector3.UnitY, 0, Vector3.One, new Color(200, 200, 200));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        float Level(Vector3 point)
        {
            var pixel = GetWorldToScreenEx(point, view, Width, Height);
            var i = (int)pixel.Y * Width + (int)pixel.X;
            var l = 0.2126f * linear[i * 4] + 0.7152f * linear[i * 4 + 1] + 0.0722f * linear[i * 4 + 2];
            l /= 1 + l;
            return 255 * Math.Clamp(l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f, 0, 1);
        }
        var (shadowed, beside) = (Level(at + new Vector3(-1.5f, 0, 0)), Level(at + new Vector3(-1.5f, 0, 1)));
        UnloadModel(block);
        UnloadModel(floor);
        CloseWindow();
        UseApp(null);
        return (shadowed, beside);
    }

    [NeedsVulkanTheory]
    [InlineData(0f, false, false)]
    [InlineData(8f, false, false)]
    [InlineData(24f, false, false)]
    [InlineData(300f, false, false)]
    [InlineData(0f, false, true)]
    [InlineData(8f, true, false)]
    [InlineData(300f, true, false)]
    public void A_Posts_Shadow_Stands_Where_A_Programs_Shader_Reads_It(float x, bool moved, bool crossing)
    {
        // A shader of the program's own hands the lighting world points, as input.world, and moves
        // world on a copy of its input before it is lit, both as before the engine drew against an
        // origin, with the eye at the world's origin, 8, 24 and 300 units off, and moving across a
        // step of the origin.
        var (shadowed, beside) = Posted(new Vector3(x, 0, 0), moved ? MovedShader : HandedShader, crossing);
        // The floor in the post's shadow under three fifths of the floor beside it, and for the shader
        // that lights each point as the point a unit off, the other way about, its point beside the
        // shadow lit as a point in it.
        if (moved) beside.Should().BeLessThan(0.6f * shadowed, "the point a program's shader moved world to is the one lit");
        else shadowed.Should().BeLessThan(0.6f * beside, "the post's shadow falls where a program's shader reads it");
    }

    [NeedsVulkanTheory]
    [InlineData(4096f, 1024f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void A_Shadowed_Room_Far_From_The_Worlds_Origin_Reads_As_The_Room_At_It(float x, float z)
    {
        var home = Standing(Vector3.Zero, shadows: true);
        var far = Standing(new Vector3(x, 0, z), shadows: true);
        var differences = home.Zip(far, (a, b) => Math.Abs(a - b)).ToArray();
        // The room far out stands at another phase of the shadow maps' texels, which lie in world
        // numbers and divide no whole count of 1,024 units, so an edge moves by a part of a texel,
        // past a level at a few pixels. A map fitted in world numbers far out moves as a whole.
        differences.Count(d => d > 4).Should().BeLessThanOrEqualTo(16, "only the shadows' edges move, by a part of a texel at the maps' other phase");
        differences.Max().Should().BeLessThan(48, "an edge's pixel reads a part of a texel off, the map's texels at another phase");
    }

    // The room at the world's origin with its shadows at High, the camera standing while the field settles
    // and then moving a quarter of a unit across it each frame for eight frames, from 6.75 to 8.5
    // units along x, with temporal antialiasing and still particles, and the origin the renderer
    // stands at the eye or the one given held, the level of each pixel's light of the last frame
    // and the origin it was drawn against.
    private static (float[] Levels, RenderOrigin Origin) Crossing(RenderOrigin? held)
    {
        RenderOrigin.Override = held;
        var config = Config.Default.WithWindow("render origin", Width, Height) with
        {
            Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTemporalAntialiasing(true);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 1.5f, castsShadows: true);
        CreatePointLight(new Vector3(1.5f, 1.25f, 1.25f), new Color(255, 200, 150), 4, range: 6, castsShadows: true);
        CreateParticleEmitter(new Vector3(0.25f, 1.5f, 0.5f), ParticleEmitter.Default with
        {
            MaxParticles = 40, Emitting = false, Burst = 40, Life = 60, Velocity = Vector3.Zero, Gravity = Vector3.Zero,
            Radius = 0.375f, StartSize = 0.25f, EndSize = 0.25f, StartColor = new Color(220, 220, 230), EndColor = new Color(220, 220, 230),
            Lit = true, Blend = ParticleBlend.Alpha,
        });
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 8; frame++)
        {
            var moved = Math.Max(0, frame - SceneFieldPlan.SettleFrames + 1) * 0.25f;
            var view = new Camera3D(new Vector3(6.5f + moved, 2.5f, 5.5f), new Vector3(moved, 0.5f, 0), Vector3.UnitY, 60);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(Vector3.Zero);
            DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(16, 1, 8), new Color(200, 190, 170));
            DrawModelEx(block, new Vector3(0, 1.5f, -2.5f), Vector3.UnitY, 0, new Vector3(16, 4, 1), new Color(180, 120, 90));
            DrawModelEx(block, new Vector3(-1.25f, 0.5f, 0.25f), Vector3.UnitY, 0, Vector3.One, Color.White);
            DrawModelEx(block, new Vector3(2.25f, 0.25f, -0.5f), Vector3.UnitY, 0, new Vector3(0.5f), new Color(120, 160, 220));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var origin = renderer.RenderWorld.TryGet<RenderOrigin>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        var levels = new float[Width * Height];
        for (int i = 0; i < levels.Length; i++)
        {
            var l = 0.2126f * linear[i * 4] + 0.7152f * linear[i * 4 + 1] + 0.0722f * linear[i * 4 + 2];
            l /= 1 + l;
            levels[i] = 255 * Math.Clamp(l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f, 0, 1);
        }
        UnloadModel(block);
        CloseWindow();
        UseApp(null);
        return (levels, origin);
    }

    [NeedsVulkanFact]
    public void A_Camera_Crossing_A_Step_Of_The_Origin_Draws_As_With_The_Origin_Held_Where_It_Began()
    {
        // What a pass keeps from one frame to the next, the antialiasing's history, the screen's and
        // the world's probes, the shadows' cascades and the particles, is read in the frame after the
        // step against the origin it stepped to.
        var (stepping, steppedTo) = Crossing(null);
        var (held, _) = Crossing(RenderOrigin.Zero);
        steppedTo.Should().Be(new RenderOrigin(16, 0, 0), "the eye passed 8 units along x, where the origin steps to 16");
        var differences = stepping.Zip(held, (a, b) => Math.Abs(a - b)).ToArray();
        // Within a level but at an edge's pixels, which fall the other way as the places round
        // against the other origin; a frame before read against the origin it was drawn against
        // moves the light of whole surfaces.
        differences.Count(d => d > 1).Should().BeLessThanOrEqualTo(32, "only an edge's pixels fall the other way as the places round against the other origin");
        differences.Max().Should().BeLessThan(64, "an edge's pixel falls the other way by the places' rounding, no further");
    }

    // The room at a place in the world at High under temporal antialiasing and motion blur, with
    // the occlusion and a glossy floor reflecting the frame kept the frame before, the camera standing
    // while the field settles and then turning across the room by an eighth of a unit a frame, a
    // float's step at 1,140,736, for eight frames, each pass reading its frame before through its
    // history; the level of each pixel's light in the last frame as the motion blur leaves it. The
    // lights cast no shadows, whose texels fall at another phase against a room moved.
    private static float[] Turning(Vector3 at)
    {
        var config = Config.Default.WithWindow("render origin", Width, Height) with
        {
            Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTemporalAntialiasing(true);
        SetMotionBlur(1);
        SetAmbientOcclusion(1, 1);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 1.5f);
        CreatePointLight(at + new Vector3(1.5f, 1.25f, 1.25f), new Color(255, 200, 150), 4, range: 6);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var floor = block.Materials[0] with { Roughness = 0.125f, Color = new Color(200, 190, 170) };
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 8; frame++)
        {
            var turned = Math.Max(0, frame - SceneFieldPlan.SettleFrames + 1) * 0.125f;
            var view = new Camera3D(at + new Vector3(0, 2.5f, 5.5f), at + new Vector3(turned, 0.5f, 0), Vector3.UnitY, 60);
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(at);
            DrawMesh(block.Meshes[0], floor, Matrix4x4.CreateScale(8, 1, 8) * Matrix4x4.CreateTranslation(at + new Vector3(0, -0.5f, 0)));
            DrawModelEx(block, at + new Vector3(0, 1.5f, -2.5f), Vector3.UnitY, 0, new Vector3(8, 4, 1), new Color(180, 120, 90));
            DrawModelEx(block, at + new Vector3(-1.25f, 0.5f, 0.25f), Vector3.UnitY, 0, Vector3.One, Color.White);
            DrawModelEx(block, at + new Vector3(1.25f, 0.25f, -0.5f), Vector3.UnitY, 0, new Vector3(0.5f), new Color(120, 160, 220));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Blurred!.ColorView.Image);
        var levels = new float[Width * Height];
        for (int i = 0; i < levels.Length; i++)
        {
            var l = 0.2126f * linear[i * 4] + 0.7152f * linear[i * 4 + 1] + 0.0722f * linear[i * 4 + 2];
            l /= 1 + l;
            levels[i] = 255 * Math.Clamp(l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f, 0, 1);
        }
        UnloadModel(block);
        CloseWindow();
        UseApp(null);
        return levels;
    }

    [NeedsVulkanTheory]
    [InlineData(4096f, 1024f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void A_Camera_Turning_Far_From_The_Worlds_Origin_Reads_As_At_It(float x, float z)
    {
        var home = Turning(Vector3.Zero);
        var far = Turning(new Vector3(x, 0, z));
        var largest = home.Zip(far, (a, b) => Math.Abs(a - b)).Max();
        // Each pass reads its frame before from the two cameras' eyes, against this frame's origin
        // where it reads a place, so the frames far out are handed to the GPU in the numbers the
        // frames at the origin are. A history reprojected through views in world numbers moves its
        // pixels far out.
        largest.Should().BeLessThan(0.1f, "every frame before is read in the numbers the frames at the origin are");
    }

    // The room at a place in the world standing in fog that thins upward, lit by the sun and the
    // lamp, through the depth of field's blur, with a block whose light scatters under its surface,
    // the camera standing; the level of each pixel's light in the last frame as the lens passes
    // leave it. The passes read each pixel's distance from the eye from the depth. Every corner lies
    // on a float's step at 1,140,736, as the scene field splats meshes in world numbers.
    private static float[] Hazy(Vector3 at)
    {
        var config = Config.Default.WithWindow("render origin", Width, Height) with
        {
            Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(GlobalIllumination.Low);
        SetSubsurfaceQuality(SubsurfaceQuality.High);
        SetVolumetricFog(0.06f, 1, 0.5f);
        SetDepthOfField(5, 1, 0.01f);
        SetMotionBlur(1);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 1.5f);
        CreatePointLight(at + new Vector3(1.5f, 1.25f, 1.25f), new Color(255, 200, 150), 4, range: 6);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var scattering = block.Materials[0] with { SubsurfaceRadius = 0.3f, SubsurfaceColor = new Color(255, 90, 60) };
        var view = new Camera3D(at + new Vector3(0, 2.5f, 5.5f), at + new Vector3(0, 0.5f, 0), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 4; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(at);
            DrawModelEx(block, at + new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(8, 1, 8), new Color(200, 190, 170));
            DrawModelEx(block, at + new Vector3(0, 1.5f, -2.5f), Vector3.UnitY, 0, new Vector3(8, 4, 1), new Color(180, 120, 90));
            DrawModelEx(block, at + new Vector3(-1.25f, 0.5f, 0.25f), Vector3.UnitY, 0, Vector3.One, Color.White);
            DrawMesh(block.Meshes[0], scattering, Matrix4x4.CreateScale(0.75f) * Matrix4x4.CreateTranslation(at + new Vector3(1.25f, 0.375f, 0.5f)));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Blurred!.ColorView.Image);
        var levels = new float[Width * Height];
        for (int i = 0; i < levels.Length; i++)
        {
            var l = 0.2126f * linear[i * 4] + 0.7152f * linear[i * 4 + 1] + 0.0722f * linear[i * 4 + 2];
            l /= 1 + l;
            levels[i] = 255 * Math.Clamp(l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f, 0, 1);
        }
        UnloadModel(block);
        CloseWindow();
        UseApp(null);
        return levels;
    }

    [NeedsVulkanTheory]
    [InlineData(4096f, 1024f)]
    [InlineData(71680f, 17408f)]
    [InlineData(1140736f, 285696f)]
    public void A_Room_In_Fog_Through_A_Lens_Far_From_The_Worlds_Origin_Reads_As_At_It(float x, float z)
    {
        var home = Hazy(Vector3.Zero);
        var far = Hazy(new Vector3(x, 0, z));
        var largest = home.Zip(far, (a, b) => Math.Abs(a - b)).Max();
        // The fog, the scattering's blur and the depth of field take each pixel's distance from the
        // eye from the depth through the camera against the frame's origin from its eye, so the room
        // far out is handed to the GPU in the numbers the room at the origin is. Through the camera in
        // world numbers a distance far out is rounded to the float's step at the eye.
        largest.Should().BeLessThan(0.1f, "every distance from the eye is read in the numbers the room at the origin is");
    }

    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Scene_Drawn_Against_An_Origin_Away_From_The_Worlds_Reads_As_Against_The_Worlds(GlobalIllumination quality)
    {
        var at = Drawn(RenderOrigin.Zero, quality);
        var away = Drawn(new RenderOrigin(64, -32, 128), quality);
        at.Count(level => level > 8).Should().BeGreaterThan(Width * Height / 2, "the scene is lit across most of the window");
        var differences = at.Zip(away, (a, b) => Math.Abs(a - b)).ToArray();
        // The places round against each origin to their floats' step, so at an edge a tap of a
        // shadow's filter may fall the other side of its map's depth; a place read against the other
        // origin moves all the light it reaches.
        differences.Count(d => d > 1).Should().BeLessThanOrEqualTo(4, "only an edge's tap of a shadow's filter falls the other way as the places round");
        differences.Max().Should().BeLessThan(4, "every pass takes its places against the one origin, a tap at an edge apart");
    }
}
