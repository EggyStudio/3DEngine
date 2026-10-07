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
    // times, the last of them captured.
    private Image Capture(Action draw)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 40 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
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
