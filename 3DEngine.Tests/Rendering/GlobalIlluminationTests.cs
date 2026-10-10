using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The light that bounces: how many cascades and directions each quality traces, and frames drawn
/// offscreen in which a red wall tints the block beside it, a glowing panel lights its room, and
/// glossy surfaces reflect what is on the screen and what is behind the camera.
/// </summary>
[Collection("Engine3D")]
public sealed class GlobalIlluminationTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-gi-");
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Each_Quality_Traces_More_Cascades_And_Directions_Than_The_One_Below_As_The_Field_Allows()
    {
        GlobalIlluminationRenderer.TexelsAt(GlobalIllumination.Off).Should().BeEmpty();
        GlobalIlluminationRenderer.TexelsAt(GlobalIllumination.Low).Should().Equal(4, 8);
        GlobalIlluminationRenderer.TexelsAt(GlobalIllumination.Medium).Should().Equal(4, 8, 16);
        GlobalIlluminationRenderer.TexelsAt(GlobalIllumination.High).Should().Equal(8, 16, 16, 16);
        GlobalIlluminationRenderer.TileAt(GlobalIllumination.Low).Should().BeGreaterThan(GlobalIlluminationRenderer.TileAt(GlobalIllumination.High));

        var world = new RenderWorld();
        GlobalIlluminationRenderer.CascadesIn(world).Should().Be(0, "nothing asked for light to bounce");
        world.Set(new GlobalIlluminationSettings { Quality = GlobalIllumination.High });
        GlobalIlluminationRenderer.CascadesIn(world).Should().Be(0, "and there is no field to trace it through");
        world.Set(new SceneFieldSettings { Cascades = 3 });
        GlobalIlluminationRenderer.CascadesIn(world).Should().Be(3, "the field's three cascades cut the four High has short");
        world.Set(new GlobalIlluminationSettings { Quality = GlobalIllumination.Low });
        GlobalIlluminationRenderer.CascadesIn(world).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Asking_For_Light_To_Bounce_Turns_The_Field_On_Where_It_Was_Off()
    {
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));
        SetGlobalIllumination(GlobalIllumination.Medium);
        GetApp().World.Resource<SceneFieldSettings>().Cascades.Should().Be(4);
        SetSceneField(2);
        SetGlobalIllumination(GlobalIllumination.High);
        GetApp().World.Resource<SceneFieldSettings>().Cascades.Should().Be(2, "a field the program set is kept");
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("gi test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.15f, 2);
    }

    // Draws frames until the field has settled and been built and the light has bounced a few
    // times, the last of them captured, through the camera given or one looking at the middle.
    private Image Capture(Action draw, Camera3D? camera = null)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 40 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera ?? new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
            draw();
            EndMode3D();
            if (frame == SceneFieldPlan.SettleFrames + 10) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        return LoadImage(path);
    }

    // The mean of a rectangle's pixels' channels.
    private static Vector3 Mean(Image image, int x, int y, int width, int height)
    {
        var sum = Vector3.Zero;
        for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
            {
                var c = GetImageColor(image, px, py);
                sum += new Vector3(c.R, c.G, c.B);
            }
        return sum / (width * height);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Under_A_Uniform_Sky_Every_Face_Of_A_Probe_Reads_The_Same_Light_At_Each_Quality()
    {
        // A sky of 1 and nothing near the probes in the middle of the first cascade, a block drawn
        // far below them so the window shows a scene, so every face of such a probe gathers pi.
        // Each texel of the probes' octahedrons taken as an equal share of the sphere read the
        // faces along z at 0.82 of that at Low, its first cascade's 4 texels a side, and 0.93 at High.
        foreach (var quality in new[] { GlobalIllumination.Low, GlobalIllumination.High })
        {
            Open();
            SetAmbientLight(Color.White, 1);
            SetGlobalIllumination(quality);
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            Capture(() => DrawModelEx(slab, new Vector3(0, -30, 0), Vector3.UnitY, 0, Vector3.One, Color.White));
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var probes = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Probes!;
            var cubes = ((GraphicsDevice)renderer.Context.Graphics).ReadIlluminationCubes(probes);
            var (p, middle) = (probes.Probes, probes.Probes / 2);
            var faces = Enumerable.Range(0, 6).Select(f => cubes[((middle * p + middle) * 6 * p + f * p + middle) * 4]).ToArray();
            faces.Should().AllSatisfy(face => face.Should().BeApproximately(MathF.PI, MathF.PI * 0.02f, $"each face at {quality} reads the sky alike, {string.Join(", ", faces)}"));
            UnloadModel(slab);
            CloseWindow();
            UseApp(null);
        }
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Floor_Seen_At_A_Slant_Takes_The_Light_Of_The_Screens_Probes_Along_Its_Slope()
    {
        // A wide floor lit only by the light that bounces, from a dim sky and a glowing wall at its
        // far end, the camera low over it, so each row of the screen's probes stands further off
        // than the one below. Weighed by how alike their distances from the eye were, the probes
        // around a pixel of the floor by the wall lay too far apart for any to count, the pixel took
        // the world's probes instead, and the wall's light on the floor ended in a hard line, the
        // light falling by half within four rows, where weighed by how near each lies to the
        // pixel's plane it falls by 18% at most, as this test measured on an RTX 4070.
        var config = Config.Default.WithWindow("gi test", 480, 270) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(new Color(160, 190, 255), 0.2f);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var glow = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        glow.Materials[0].Emissive = Color.White;
        glow.Materials[0].EmissiveIntensity = 6;
        var picture = Capture(() =>
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, -2), Vector3.UnitY, 0, new Vector3(10, 0.3f, 10), Color.White);
            DrawModelEx(glow, new Vector3(0, 1.5f, -6.15f), Vector3.UnitY, 0, new Vector3(10, 3, 0.3f), Color.White);
        }, new Camera3D(new Vector3(0, 0.6f, 2.5f), new Vector3(0, 0.2f, -6), Vector3.UnitY, 60));

        // Each row's mean over the floor's middle, from the wall's foot toward the camera, and the
        // steepest fall between rows four apart.
        var rows = Enumerable.Range(140, 70).Select(y => (double)Mean(picture, 140, y, 200, 1).Y).ToArray();
        var steepest = Enumerable.Range(10, 50).Max(i => (rows[i] - rows[i + 4]) / rows[i]) * 100;
        steepest.Should().BeLessThan(30, $"the wall's light on the floor falls smoothly toward the camera, {steepest:0}% at most within four rows");
        UnloadModel(slab);
        UnloadModel(glow);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Red_Wall_Tints_The_Side_Of_A_White_Block_Facing_It()
    {
        Open();
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // A white floor, a red wall on the left and a white block turned toward it, its side facing
        // the wall away from the lamp, so only the light the wall sends on reaches it.
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
        }

        SetGlobalIllumination(GlobalIllumination.Low);
        var bouncing = Capture(Draw);
        SetGlobalIllumination(GlobalIllumination.Off);
        var still = Capture(Draw);

        // The block's left side, left of its middle and half way up.
        var lit = Mean(bouncing, 58, 48, 6, 10);
        var dark = Mean(still, 58, 48, 6, 10);
        (lit.X - dark.X).Should().BeGreaterThan(20, $"light the wall sends on reaches the side, {lit} against {dark}");
        lit.X.Should().BeGreaterThan(lit.Y + 10, $"and it is red, {lit}");
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Red_Wall_That_Moves_Tints_The_Side_Of_A_White_Block_Facing_It_As_A_Still_One_Does()
    {
        Open();
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // The scene above with the red wall swaying a fiftieth of a unit each frame, which the field
        // holds as a box stamped each frame and never builds from its triangles.
        int drawn = 0;
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f - drawn++ % 2 * 0.02f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
        }

        SetGlobalIllumination(GlobalIllumination.Low);
        var bouncing = Capture(Draw);
        GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<SceneFieldRenderer>()!.Plan!.StillCount
            .Should().Be(2, "the floor and the block are built and the wall is not");
        SetGlobalIllumination(GlobalIllumination.Off);
        var still = Capture(Draw);

        var lit = Mean(bouncing, 58, 48, 6, 10);
        var dark = Mean(still, 58, 48, 6, 10);
        (lit.X - dark.X).Should().BeGreaterThan(20, $"light the wall sends on reaches the side, {lit} against {dark}");
        lit.X.Should().BeGreaterThan(lit.Y + 10, $"and it is red, the box the wall is held as painted its color, {lit}");
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Glowing_Panel_Lights_Its_Room_With_No_Light_In_It()
    {
        Open();
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        panel.Materials[0].Emissive = Color.White;
        panel.Materials[0].EmissiveIntensity = 8;
        // A room of floor, back wall and walls, and a panel on the ceiling that gives off light,
        // with no light entity, so whatever lights the floor bounced off the panel's light.
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), Color.White);
            DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), Color.White);
            DrawModelEx(slab, new Vector3(0, 1.5f, -1.85f), Vector3.UnitY, 0, new Vector3(4, 3, 0.3f), Color.White);
            DrawModelEx(slab, new Vector3(-1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), Color.White);
            DrawModelEx(slab, new Vector3(1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), Color.White);
            DrawModelEx(panel, new Vector3(0, 2.97f, 0), Vector3.UnitY, 0, new Vector3(1.2f, 0.06f, 1.2f), Color.White);
        }

        // With light bouncing a scene is lit only by what lights it, where a scene with no light at
        // all is drawn in its own colors, so the panel dark is the frame to set against.
        SetGlobalIllumination(GlobalIllumination.Low);
        var glowing = Capture(Draw);
        panel.Materials[0].EmissiveIntensity = 0;
        var dark = Capture(Draw);

        // The floor at the room's middle, near the bottom of the picture.
        var lit = Mean(glowing, 60, 80, 40, 8);
        var unlit = Mean(dark, 60, 80, 40, 8);
        lit.X.Should().BeGreaterThan(unlit.X + 40, $"the panel's light reaches the floor by bouncing, {lit} against {unlit}");
        UnloadModel(slab);
        UnloadModel(panel);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Glowing_Panel_Lights_Its_Room_In_A_Render_Texture_Too()
    {
        // The room the panel lights by bouncing alone, drawn into a render texture, which drew it
        // in its own colors as a scene with no light at all, the panel's light or none, and built
        // no field, the window drawing no mesh.
        Open();
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        panel.Materials[0].Emissive = Color.White;
        var target = LoadRenderTexture(160, 96);
        Image Drawn(float glow)
        {
            panel.Materials[0].EmissiveIntensity = glow;
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 10; frame++)
            {
                BeginDrawing();
                BeginTextureMode(target);
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), Color.White);
                DrawModelEx(slab, new Vector3(0, 3.15f, 0), Vector3.UnitY, 0, new Vector3(4, 0.3f, 4), Color.White);
                DrawModelEx(slab, new Vector3(0, 1.5f, -1.85f), Vector3.UnitY, 0, new Vector3(4, 3, 0.3f), Color.White);
                DrawModelEx(slab, new Vector3(-1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), Color.White);
                DrawModelEx(slab, new Vector3(1.85f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 4), Color.White);
                DrawModelEx(panel, new Vector3(0, 2.97f, 0), Vector3.UnitY, 0, new Vector3(1.2f, 0.06f, 1.2f), Color.White);
                EndMode3D();
                EndTextureMode();
                // The window shows the texture alone, as a game drawing its scene at a low size
                // does, so the field follows the texture's camera and holds its meshes.
                ClearBackground(Color.Black);
                DrawTexture(target.Texture, 0, 0, Color.White);
                EndDrawing();
            }
            return LoadImageFromTexture(target.Texture);
        }

        SetGlobalIllumination(GlobalIllumination.Low);
        var glowing = Mean(Drawn(8), 0, 0, 160, 96);
        var dark = Mean(Drawn(0), 0, 0, 160, 96);

        glowing.X.Should().BeGreaterThan(dark.X + 20, $"the panel's light reaches the room in the texture by bouncing, {glowing} against {dark}");
        dark.X.Should().BeLessThan(20, $"and with the panel dark nothing lights it, {dark}");
        UnloadRenderTexture(target);
        UnloadModel(slab);
        UnloadModel(panel);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Light_That_Bounces_Follows_A_Lamp_Brought_Into_The_Room_Within_Two_Frames()
    {
        // The red wall and the block of the test above, the lamp held out of its reach and brought
        // in at frame 30, the camera between the two looking at the block's side facing the wall,
        // which only the wall's light reaches, so the screen's probes stand on it. The side is read
        // each frame from frame 28, and the frames after 30 it takes to come and stay within a
        // tenth of the way from its light before to its light at frame 59 are how far the bounce
        // lags the lamp, seven where each frame blended a fifth of its light.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        var lamp = CreatePointLight(new Vector3(0.5f, 60, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var reads = new List<(int Frame, string Path)>();
        for (int frame = 0; frame < 60; frame++)
        {
            if (frame == 30) SetLightPosition(lamp, new Vector3(0.5f, 2.6f, 0.8f));
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(-1.3f, 0.8f, 0.9f), new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 50));
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
            EndMode3D();
            if (frame >= 28)
            {
                reads.Add((frame, Path.Combine(_folder.Path, $"{_captures++}.png")));
                TakeScreenshot(reads[^1].Path);
            }
            EndDrawing();
        }
        UnloadModel(slab);
        CloseWindow();
        UseApp(null);

        var side = reads.Select(read => (read.Frame, Red: Mean(LoadImage(read.Path), 60, 30, 40, 36).X)).ToList();
        var (before, after) = (side[0].Red, side[^1].Red);
        (after - before).Should().BeGreaterThan(20, $"the lamp brought in lights the wall, whose light reaches the side, {before:0} to {after:0}");
        var lag = side.First(read => read.Frame >= 30 && side.Where(later => later.Frame >= read.Frame)
            .All(later => Math.Abs(later.Red - after) <= 0.1 * (after - before))).Frame - 30;
        lag.Should().BeLessThanOrEqualTo(2, $"the bounce follows the lamp, the side reading {string.Join(", ", side.Select(read => $"{read.Red:0}"))} from frame 28");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Room_Drawn_Into_A_Render_Texture_Bounces_Its_Light_As_The_Window_Does()
    {
        // The red wall and the block, drawn into the window and then into a render texture of the
        // window's size shown over the window, the block's side, which only the wall's light
        // reaches, read in each. The texture's screen probes stand on its own depth as the window's
        // do, in the same frame.
        Open();
        SetGlobalIllumination(GlobalIllumination.Low);
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(-1.3f, 0.8f, 0.9f), new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 50);
        void Room()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
        }
        var window = Capture(Room, camera);

        var texture = LoadRenderTexture(GetScreenWidth(), GetScreenHeight());
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 40 && !File.Exists(path); frame++)
        {
            BeginTextureMode(texture);
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            Room();
            EndMode3D();
            EndTextureMode();
            BeginDrawing();
            ClearBackground(Color.Black);
            DrawTexture(texture.Texture, 0, 0, Color.White);
            if (frame == SceneFieldPlan.SettleFrames + 10) TakeScreenshot(path);
            EndDrawing();
        }
        var drawn = LoadImage(path);

        var (inWindow, inTexture) = (Mean(window, 60, 30, 40, 36), Mean(drawn, 60, 30, 40, 36));
        inWindow.X.Should().BeGreaterThan(60, $"the wall's light reaches the block's side in the window, {inWindow}");
        Vector3.Distance(inWindow, inTexture).Should().BeLessThan(6, $"and reaches it alike in the render texture, {inTexture} against {inWindow}");
        UnloadRenderTexture(texture);
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Light_That_Bounces_Holds_Still_As_The_Camera_Slides()
    {
        // A Cornell box the camera slides across a hundredth of a unit a frame, the picture's change
        // from frame to frame with light bouncing set against its change with none. The screen's
        // probes stand on whatever surface each tile's middle shows, so as the camera slides they
        // slide over the surfaces and their light changes with them, which blending each with the
        // frame before's where its surface was holds still.
        double Change(GlobalIllumination quality, bool held = true)
        {
            Open();
            SetGlobalIllumination(quality);
            GetApp().World.Resource<GlobalIlluminationSettings>().HistoryOff = !held;
            CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            var shots = new List<string>();
            for (int frame = 0; frame < 50; frame++)
            {
                var x = frame < 30 ? 0 : (frame - 30) * 0.01f;
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(new Vector3(x, 2.5f, 8), new Vector3(x, 2.4f, 0), Vector3.UnitY, 45));
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
                DrawModelEx(slab, new Vector3(0, 5.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
                DrawModelEx(slab, new Vector3(0, 2.5f, -3.15f), Vector3.UnitY, 0, new Vector3(6, 5, 0.3f), Color.White);
                DrawModelEx(slab, new Vector3(-3.15f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5, 6), new Color(200, 30, 30));
                DrawModelEx(slab, new Vector3(3.15f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5, 6), new Color(30, 200, 30));
                DrawModelEx(slab, new Vector3(-1, 1, -0.5f), Vector3.UnitY, 20, new Vector3(1.5f, 2, 1.5f), Color.White);
                EndMode3D();
                if (frame >= 36) shots.Add(Path.Combine(_folder.Path, $"{_captures++}.png"));
                if (frame >= 36) TakeScreenshot(shots[^1]);
                EndDrawing();
            }
            UnloadModel(slab);
            CloseWindow();
            UseApp(null);
            var changes = new List<double>();
            for (int i = 1; i < shots.Count - 2; i++)
            {
                var (a, b) = (LoadImage(shots[i - 1]), LoadImage(shots[i]));
                double sum = 0;
                int n = 0;
                for (int y = 8; y < 88; y++)
                    for (int x = 16; x < 144; x++, n += 3)
                    {
                        var (p, q) = (GetImageColor(a, x, y), GetImageColor(b, x, y));
                        sum += Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B);
                    }
                changes.Add(sum / n);
            }
            return changes.Average();
        }

        // What the bounce adds to the change, held by the frame before's light and not, as a share,
        // since a bounce twice as bright moves twice as many levels: 0.97 of 2.91 levels a frame
        // on an RTX 4070, as this test measures them.
        var still = Change(GlobalIllumination.Off);
        var bouncing = Change(GlobalIllumination.Low);
        var unheld = Change(GlobalIllumination.Low, held: false);
        (bouncing - still).Should().BeLessThan((unheld - still) / 2,
            $"the frame before's light takes most of the bounce's crawl away, {bouncing:0.00} levels a frame where {unheld:0.00} unheld and {still:0.00} with no bounce");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Polished_Floor_Reflects_A_Red_Block_On_It_Through_The_Window_Where_Light_Bounces()
    {
        Open();
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.5f)), Color.White, 2);
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0] = new ModelMaterial(new Color(30, 30, 30)) { Metallic = 1, Roughness = 0.05f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // A red block standing on a dark mirror of a floor, the camera low over the floor in front
        // of it, so the floor below the block in the picture shows its reflection.
        void Draw()
        {
            DrawModelEx(floor, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(8, 0.2f, 8), Color.White);
            DrawModelEx(block, new Vector3(0, 0.75f, 0), Vector3.UnitY, 0, new Vector3(1, 1.5f, 1), new Color(220, 30, 30));
        }

        SetGlobalIllumination(GlobalIllumination.Low);
        var reflecting = Capture(Draw);
        SetGlobalIllumination(GlobalIllumination.Off);
        var plain = Capture(Draw);

        // The floor below the block's base in the picture.
        var mirrored = Mean(reflecting, 74, 84, 12, 6);
        var bare = Mean(plain, 74, 84, 12, 6);
        mirrored.X.Should().BeGreaterThan(bare.X + 30, $"the floor reflects the block, {mirrored} against {bare}");
        mirrored.X.Should().BeGreaterThan(mirrored.Y + 20, $"and the reflection is red, {mirrored}");
        UnloadModel(floor);
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Reflection_Of_A_Surface_The_Frame_Before_Hid_Shows_The_Surface_And_Not_What_Hid_It()
    {
        Open();
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.5f)), Color.White, 2);
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0] = new ModelMaterial(new Color(30, 30, 30)) { Metallic = 1, Roughness = 0.05f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // A red block on a mirror of a floor, and a green panel in front of the camera that hides
        // the foot of the block's face the floor reflects, but not the floor, until the frame it
        // is taken away, whose reflection reads the picture of the frame before, which showed the
        // panel there.
        void Draw(bool hidden)
        {
            DrawModelEx(floor, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(8, 0.2f, 8), Color.White);
            DrawModelEx(block, new Vector3(0, 0.75f, 0), Vector3.UnitY, 0, new Vector3(1, 1.5f, 1), new Color(220, 30, 30));
            if (hidden) DrawModelEx(panel, new Vector3(0, 1.15f, 3), Vector3.UnitY, 0, new Vector3(0.6f, 0.4f, 0.05f), new Color(30, 220, 30));
        }

        SetGlobalIllumination(GlobalIllumination.Low);
        var path = Path.Combine(_folder.Path, "uncovered.png");
        for (int frame = 0; frame < 40 && !File.Exists(path); frame++)
        {
            var uncovered = frame >= SceneFieldPlan.SettleFrames + 10;
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
            Draw(!uncovered);
            EndMode3D();
            if (frame == SceneFieldPlan.SettleFrames + 10) TakeScreenshot(path);
            EndDrawing();
        }
        var frameAfter = LoadImage(path);

        var mirrored = Mean(frameAfter, 74, 84, 12, 6);
        mirrored.X.Should().BeGreaterThan(mirrored.Y + 20, $"the floor reflects the red block, not the green panel the frame before showed, {mirrored}");
        UnloadModel(floor);
        UnloadModel(block);
        UnloadModel(panel);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Lamp_That_Casts_Shadows_Bounces_No_Light_Into_A_Room_Its_Walls_Close_It_Out_Of()
    {
        // A closed room of slabs 0.6 thick, the camera inside facing a wall, and a lamp that casts
        // shadows over its roof or under its floor, so none of its light reaches inside. The
        // probes' rays lit the room's walls with it as if the roof were not there, a ray of a probe
        // above the floor that started past it brought back the light under it, and probes beyond
        // the walls lent theirs, which lit the room nearly as brightly as the lamp would unshadowed.
        // Weighed by how far their rays reached, the probes under the floor lend the walls by it
        // 1.6 levels, where they lent 15.3.
        foreach (var lampY in new[] { 5f, -2f })
        {
            Open();
            CreatePointLight(new Vector3(0, lampY, 0), Color.White, 20, range: 14, castsShadows: true);
            var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            const float thick = 0.6f, wide = 4 + 2 * thick;
            void Draw()
            {
                DrawModelEx(slab, new Vector3(0, -thick / 2, 0), Vector3.UnitY, 0, new Vector3(wide, thick, wide), Color.White);
                DrawModelEx(slab, new Vector3(0, 3 + thick / 2, 0), Vector3.UnitY, 0, new Vector3(wide, thick, wide), Color.White);
                DrawModelEx(slab, new Vector3(-2 - thick / 2, 1.5f, 0), Vector3.UnitY, 0, new Vector3(thick, 3, wide), Color.White);
                DrawModelEx(slab, new Vector3(2 + thick / 2, 1.5f, 0), Vector3.UnitY, 0, new Vector3(thick, 3, wide), Color.White);
                DrawModelEx(slab, new Vector3(0, 1.5f, -2 - thick / 2), Vector3.UnitY, 0, new Vector3(4, 3, thick), Color.White);
                DrawModelEx(slab, new Vector3(0, 1.5f, 2 + thick / 2), Vector3.UnitY, 0, new Vector3(4, 3, thick), Color.White);
            }
            SetGlobalIllumination(GlobalIllumination.Medium);
            var room = Mean(Capture(Draw, new Camera3D(new Vector3(0, 1.5f, 1.5f), new Vector3(0, 1.5f, -2), Vector3.UnitY, 70)), 20, 16, 120, 64);

            room.Length().Should().BeLessThan(8, $"none of the lamp's light at {lampY} up reaches the room, bounced or not, {room}");
            UnloadModel(slab);
            CloseWindow();
            UseApp(null);
        }
    }

    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Mirror_Reflects_A_Block_Past_The_Field_Through_The_Devices_Rays_At_High()
    {
        var config = Config.Default.WithWindow("gi test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        // One cascade of the field, which reaches some five units behind the camera, and a green
        // block twenty-five behind it, which a ray through the field never meets, so only the
        // device's own rays, which High traces, find it in the mirror.
        SetSceneField(1, 0.15f, 1);
        SetAmbientLight(Color.White, 0.3f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.2f, -1, 0.3f)), Color.White, 2);
        var mirror = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        mirror.Materials[0] = new ModelMaterial(new Color(240, 240, 240)) { Metallic = 1, Roughness = 0.02f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        void Draw()
        {
            DrawModelEx(mirror, new Vector3(0, 1.2f, -1.5f), Vector3.UnitY, 0, new Vector3(4, 3, 0.2f), Color.White);
            DrawModelEx(block, new Vector3(0, 1.2f, 25), Vector3.UnitY, 0, new Vector3(8, 8, 1), new Color(30, 200, 40));
        }

        SetGlobalIllumination(GlobalIllumination.Medium);
        var field = Capture(Draw);
        SetGlobalIllumination(GlobalIllumination.High);
        var traced = Capture(Draw);

        var middle = Mean(traced, 70, 40, 20, 16);
        var bare = Mean(field, 70, 40, 20, 16);
        (middle.Y - middle.X).Should().BeGreaterThan(60, $"the mirror shows the green block the device's rays found, {middle}");
        (bare.Y - bare.X).Should().BeLessThan(10, $"where the field alone, which never reaches it, shows the gray light from all around, {bare}");
        UnloadModel(mirror);
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Mirror_Shows_A_Block_Its_Lamp_Is_Shut_Away_From_In_The_Dark()
    {
        Open();
        var mirror = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        mirror.Materials[0] = new ModelMaterial(new Color(240, 240, 240)) { Metallic = 1, Roughness = 0.02f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // A mirror the camera faces and a green block behind the camera, which only a ray through
        // the field finds, and beside the camera, out of the picture, a lamp that casts shadows in a
        // box of six walls, so none of its light reaches the block. With the box's wall toward the
        // block left out, its light does.
        var lamp = new Vector3(3, 1.2f, 4.5f);
        CreatePointLight(lamp, Color.White, 8, range: 12, castsShadows: true);
        void Draw(bool shut)
        {
            DrawModelEx(mirror, new Vector3(0, 1.2f, -1.5f), Vector3.UnitY, 0, new Vector3(4, 3, 0.2f), Color.White);
            DrawModelEx(block, new Vector3(0, 1.2f, 6.5f), Vector3.UnitY, 0, new Vector3(3, 3, 1), new Color(30, 200, 40));
            foreach (var (at, size) in new[]
                     {
                         (new Vector3(0, 1.1f, 0), new Vector3(2.4f, 0.2f, 2.4f)), (new Vector3(0, -1.1f, 0), new Vector3(2.4f, 0.2f, 2.4f)),
                         (new Vector3(1.1f, 0, 0), new Vector3(0.2f, 2.4f, 2.4f)), (new Vector3(-1.1f, 0, 0), new Vector3(0.2f, 2.4f, 2.4f)),
                         (new Vector3(0, 0, 1.1f), new Vector3(2.4f, 2.4f, 0.2f)), (new Vector3(0, 0, -1.1f), new Vector3(2.4f, 2.4f, 0.2f)),
                     })
                if (shut || at.X >= 0)
                    DrawModelEx(slab, lamp + at, Vector3.UnitY, 0, size, Color.Gray);
        }

        SetGlobalIllumination(GlobalIllumination.Low);
        var shut = Mean(Capture(() => Draw(true)), 70, 40, 20, 16);
        var open = Mean(Capture(() => Draw(false)), 70, 40, 20, 16);
        shut.Y.Should().BeLessThan(20, $"the block the lamp is shut away from is dark in the mirror, {shut}");
        (open.Y - shut.Y).Should().BeGreaterThan(40, $"and lit green where the box lets the lamp's light out to it, {open} against {shut}");
        UnloadModel(mirror);
        UnloadModel(block);
        UnloadModel(slab);
    }

    [NeedsRayQueryFact]
    [Trait("Category", "Render")]
    public void A_Mirror_Shows_A_Block_Its_Lamp_Is_Shut_Away_From_In_The_Dark_Through_The_Devices_Rays_At_High()
    {
        var config = Config.Default.WithWindow("gi test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        // The scene above with the block twenty-five units behind the camera, past the one cascade
        // of the field, so only the device's own rays, which High traces, find it, and the lamp in
        // its box beside the block.
        SetSceneField(1, 0.15f, 1);
        var mirror = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        mirror.Materials[0] = new ModelMaterial(new Color(240, 240, 240)) { Metallic = 1, Roughness = 0.02f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var lamp = new Vector3(6, 1.2f, 22);
        CreatePointLight(lamp, Color.White, 30, range: 20, castsShadows: true);
        void Draw(bool shut)
        {
            DrawModelEx(mirror, new Vector3(0, 1.2f, -1.5f), Vector3.UnitY, 0, new Vector3(4, 3, 0.2f), Color.White);
            DrawModelEx(block, new Vector3(0, 1.2f, 25), Vector3.UnitY, 0, new Vector3(8, 8, 1), new Color(30, 200, 40));
            foreach (var (at, size) in new[]
                     {
                         (new Vector3(0, 1.1f, 0), new Vector3(2.4f, 0.2f, 2.4f)), (new Vector3(0, -1.1f, 0), new Vector3(2.4f, 0.2f, 2.4f)),
                         (new Vector3(1.1f, 0, 0), new Vector3(0.2f, 2.4f, 2.4f)), (new Vector3(-1.1f, 0, 0), new Vector3(0.2f, 2.4f, 2.4f)),
                         (new Vector3(0, 0, 1.1f), new Vector3(2.4f, 2.4f, 0.2f)), (new Vector3(0, 0, -1.1f), new Vector3(2.4f, 2.4f, 0.2f)),
                     })
                if (shut || at.X >= 0)
                    DrawModelEx(slab, lamp + at, Vector3.UnitY, 0, size, Color.Gray);
        }

        SetGlobalIllumination(GlobalIllumination.High);
        var shut = Mean(Capture(() => Draw(true)), 70, 40, 20, 16);
        var open = Mean(Capture(() => Draw(false)), 70, 40, 20, 16);
        shut.Y.Should().BeLessThan(20, $"the block the lamp is shut away from is dark in the mirror, {shut}");
        (open.Y - shut.Y).Should().BeGreaterThan(40, $"and lit green where the box lets the lamp's light out to it, {open} against {shut}");
        UnloadModel(mirror);
        UnloadModel(block);
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Mirror_Reflects_A_Block_Behind_The_Camera_Through_The_Field()
    {
        Open();
        SetAmbientLight(Color.White, 0.3f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.2f, -1, 0.3f)), Color.White, 2);
        var mirror = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        mirror.Materials[0] = new ModelMaterial(new Color(240, 240, 240)) { Metallic = 1, Roughness = 0.02f };
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // A mirror the camera faces and a green block behind the camera, which the window never
        // shows, so only a ray traced through the field past the picture finds it.
        void Draw()
        {
            DrawModelEx(mirror, new Vector3(0, 1.2f, -1.5f), Vector3.UnitY, 0, new Vector3(4, 3, 0.2f), Color.White);
            DrawModelEx(block, new Vector3(0, 1.2f, 6.5f), Vector3.UnitY, 0, new Vector3(3, 3, 1), new Color(30, 200, 40));
        }

        SetGlobalIllumination(GlobalIllumination.Low);
        var reflecting = Capture(Draw);
        SetGlobalIllumination(GlobalIllumination.Off);
        var plain = Capture(Draw);

        var middle = Mean(reflecting, 70, 40, 20, 16);
        var bare = Mean(plain, 70, 40, 20, 16);
        (middle.Y - middle.X).Should().BeGreaterThan(60, $"the mirror shows the green block, {middle}");
        (bare.Y - bare.X).Should().BeLessThan(10, $"and without the field it shows the gray light from all around, {bare}");
        UnloadModel(mirror);
        UnloadModel(block);
    }
}
