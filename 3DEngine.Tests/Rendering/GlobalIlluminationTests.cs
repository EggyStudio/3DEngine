using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The light that bounces: how many cascades and directions each quality traces, and frames drawn
/// offscreen in which a red wall tints the block beside it and a glowing panel lights its room.
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
}
