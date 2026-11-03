using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The fog <c>SetVolumetricFog</c> lays over the window's scene, read from frames drawn offscreen
/// with it and without, as <see cref="FrameEffectsTests"/> reads the effects over the frame.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class VolumetricFogTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-fog-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("fog test", 160, 120) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the first one's capture has been written, as FrameEffectsTests does.
    private Image Capture(Action draw)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    private static float Change(Image a, Image b, int x, int y)
    {
        var (p, q) = (GetImageColor(a, x, y), GetImageColor(b, x, y));
        return (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B)) / 3f;
    }

    [NeedsVulkanFact]
    public void Fog_Hides_What_Is_Far_More_Than_What_Is_Near()
    {
        Open();
        CreateDirectionalLight(new Vector3(0.3f, -1, 0.2f), Color.White);
        var camera = new Camera3D(new Vector3(0, 1, 0), new Vector3(0, 1, -10), Vector3.UnitY, 45);
        Action draw = () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            // A gray block near on the left and a large one far on the right, each a quarter of the frame across.
            DrawCube(new Vector3(-0.9f, 1, -4), 1.2f, 1.2f, 0.2f, Color.Gray);
            DrawCube(new Vector3(18, 1, -80), 24, 24, 1, Color.Gray);
            EndMode3D();
        };
        var clear = Capture(draw);
        SetVolumetricFog(0.02f, 0, 0, 0);
        var fogged = Capture(draw);

        // Through 4 units of fog 92% of the block's light reaches the eye, and through 80 a fifth.
        var near = Change(clear, fogged, 40, 60);
        var far = Change(clear, fogged, 130, 60);
        far.Should().BeGreaterThan(near * 3, "the fog between the eye and a pixel grows with its distance");
        far.Should().BeGreaterThan(20, "80 units of fog take most of the far block's light");
        GetVolumetricFog().Should().Be((0.02f, 0f, 0f, 0f));
    }

    [NeedsVulkanFact]
    public void Fog_In_A_Sealed_Room_Scatters_None_Of_The_Light_Outside_It()
    {
        // A room two units across inside walls a unit thick under a bright sky and a sun, the eye
        // inside it at High, the field's middle 20 units off, so the room lies in the third
        // cascade, whose probes stand 8 units apart, all outside it. The fog's light in the room is
        // the frame's linear light with fog less without, at the wall ahead, against the same room
        // with its roof off. No light enters the sealed room, so its fog has none to scatter. Its
        // cells read as a surface's light is read, the point taken past the roof and the probes
        // outside seeing the sky, the sealed room's fog scattered 0.436 where the open room's
        // scattered 0.428 on the development machine's GPU, and read for the air, 0.0012 where
        // 0.035. With the field around the eye, probes 2 apart, the open room's fog scatters 0.17 to
        // 0.19 either way, so probes 8 apart leave the open room's fog five times too dark, the cost
        // of reading only the probes that see it. Walls 0.3 thick, thinner than the field's cells of
        // a unit there, the probes' own rays pass, and the sealed room's fog scattered 0.269 where
        // the open room's 0.296.
        var config = Config.Default.WithWindow("fog test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.25f, 2);
        SetSceneFieldCenter(new Vector3(0, 1, -20));
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(new Color(170, 200, 255), 2);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.4f, -1, 0.3f)), Color.White, 3, castsShadows: true);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var gray = new Color(150, 150, 150);
        var camera = new Camera3D(new Vector3(0, 1, 0.6f), new Vector3(0, 1, -1), Vector3.UnitY, 60);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        double Wall(bool roofed, bool fogged)
        {
            SetVolumetricFog(fogged ? 0.3f : 0, 0, 0, 0);
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 30; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(30, 0.3f, 30), gray);
                foreach (var side in new[] { -1f, 1f })
                {
                    DrawModelEx(slab, new Vector3(side * 1.5f, 1, 0), Vector3.UnitY, 0, new Vector3(1, 2, 4), gray);
                    DrawModelEx(slab, new Vector3(0, 1, side * 1.5f), Vector3.UnitY, 0, new Vector3(2, 2, 1), gray);
                }
                if (roofed)
                    DrawModelEx(slab, new Vector3(0, 2.5f, 0), Vector3.UnitY, 0, new Vector3(4, 1, 4), gray);
                EndMode3D();
                EndDrawing();
            }
            var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            double sum = 0;
            for (int y = 50; y < 70; y++)
                for (int x = 70; x < 90; x++)
                    sum += 0.2126 * light[(y * 160 + x) * 4] + 0.7152 * light[(y * 160 + x) * 4 + 1] + 0.0722 * light[(y * 160 + x) * 4 + 2];
            return sum / 400;
        }
        var open = Wall(false, true) - Wall(false, false);
        var sealedFog = Wall(true, true) - Wall(true, false);
        open.Should().BeGreaterThan(0, "the fog in the open room scatters the sky's and the sun's light");
        sealedFog.Should().BeLessThan(open * 0.05, $"the sealed room's fog has no light to scatter, {sealedFog:0.00000} where the open room's adds {open:0.00000}");
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    public void Fog_That_Thins_With_Height_Leaves_The_Top_Of_A_Wall_Clearer()
    {
        Open();
        CreateDirectionalLight(new Vector3(0.3f, -1, 0.2f), Color.White);
        var camera = new Camera3D(new Vector3(0, 1, 0), new Vector3(0, 6, -30), Vector3.UnitY, 60);
        Action draw = () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(new Vector3(0, 15, -40), 80, 30, 1, Color.Gray);
            EndMode3D();
        };
        var clear = Capture(draw);
        SetVolumetricFog(0.03f, 1, 0.5f, 0);
        var fogged = Capture(draw);

        // The wall's foot lies in the fog's full density along the way to it, and its top above
        // 20 units, where the fog is e to the tenth thinner.
        var foot = Change(clear, fogged, 80, 78);
        var top = Change(clear, fogged, 80, 12);
        foot.Should().BeGreaterThan(top * 2, "the fog thins above its height, so the way up to the wall's top crosses less of it");
    }
}
