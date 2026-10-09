using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The views of what the light that bounces holds (<see cref="BounceViewRenderer"/>) and the parts
/// of it that may be left out, drawn offscreen at Low, which a device without ray queries traces.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class BounceViewTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-bounce-view-");
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void Each_View_Draws_Its_Own_Picture_And_Each_Part_Left_Out_Changes_The_Frame()
    {
        var config = Config.Default.WithWindow("bounce view test", 160, 96) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.Low);
        CreatePointLight(new Vector3(0.5f, 2.6f, 0.8f), Color.White, 6, range: 10);
        // And a sky, which the last cascade's rays that meet nothing bring back, so the far
        // cascades give the frame light of their own.
        SetAmbientLight(new Color(160, 190, 255), 1);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // A white floor, a red wall and a white block beside it, as the tint test draws them.
        void Draw()
        {
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(6, 0.3f, 6), Color.White);
            DrawModelEx(slab, new Vector3(-1.6f, 1.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 3, 6), new Color(220, 20, 20));
            DrawModelEx(slab, new Vector3(-0.3f, 0.75f, 0.3f), Vector3.UnitY, 30, new Vector3(1, 1.5f, 1), Color.White);
        }
        var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
        var plain = Capture(Draw, SceneFieldPlan.SettleFrames + 10);

        // A reference of no light at all, against which every lit pixel of the frame is brighter.
        settings.Reference = (new float[160 * 96 * 3], 160, 96, 1);
        var views = Enum.GetValues<BounceView>().Where(v => v != BounceView.None).ToList();
        var shown = new Dictionary<BounceView, Image>();
        foreach (var view in views)
        {
            settings.Shown = view;
            shown[view] = Capture(Draw, 3);
        }
        settings.Shown = BounceView.None;
        foreach (var view in views)
            Apart(shown[view], plain).Should().BeGreaterThan(3, $"{view} is drawn over the frame");
        foreach (var (one, other) in views.SelectMany((v, i) => views.Skip(i + 1).Select(w => (v, w))))
            Apart(shown[one], shown[other]).Should().BeGreaterThan(0.1f, $"{one} and {other} show different things");
        var difference = Mean(shown[BounceView.Difference], 40, 60, 80, 30);
        difference.X.Should().BeGreaterThan(difference.Z + 100, $"the lit floor is brighter than no light, so red, {difference}");

        // Each part left out, the frame drawn a few times over for the light to settle without it.
        void LeftOut(string part, Action leave, Action restore, float least)
        {
            leave();
            var without = Capture(Draw, 12);
            restore();
            Apart(without, plain).Should().BeGreaterThan(least, $"leaving {part} out changes the frame");
        }
        // The sky lost with the merge changes the frame most, by 14.5 of 255 as this test measured
        // it on an RTX 4070, the first cascade's light lost 2.2, the screen's probes 0.58 and
        // their filter 0.11.
        LeftOut("the merge", () => settings.MergeOff = true, () => settings.MergeOff = false, 5);
        LeftOut("every cascade but the last", () => settings.Alone = 1, () => settings.Alone = -1, 0.5f);
        LeftOut("the screen's probes", () => settings.ScreenOff = true, () => settings.ScreenOff = false, 0.2f);
        LeftOut("the screen's filter", () => settings.FilterOff = true, () => settings.FilterOff = false, 0.03f);
        UnloadModel(slab);
    }

    // Draws frames, capturing the last of them.
    private Image Capture(Action draw, int frames)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < frames + 20 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 1.5f, 4.5f), new Vector3(0, 1.2f, 0), Vector3.UnitY, 50));
            draw();
            EndMode3D();
            if (frame == frames) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        return LoadImage(path);
    }

    // The mean difference of two pictures' channels, 0 to 255.
    private static float Apart(Image one, Image other)
    {
        var sum = 0f;
        for (int y = 0; y < one.Height; y++)
            for (int x = 0; x < one.Width; x++)
            {
                var (a, b) = (GetImageColor(one, x, y), GetImageColor(other, x, y));
                sum += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            }
        return sum / (one.Width * one.Height * 3);
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
}
