using System.Numerics;
using FluentAssertions;
using ImGuiNET;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// What F1 and F3 leave in a frame drawn on the device: the interface's 2D shapes and its ImGui
/// window gone under F1 with the world drawn on, and the debug overlay over the world with its
/// lines' light text on their dark backing and the strip's bars.
/// </summary>
/// <remarks>
/// DebugOverlayTests reads the same keys in a headless run, by the draws recorded, and these the
/// pixels, on a mid gray so a backing, an ink and a bar each stand apart from it.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class DebugOverlayPixelTests : IDisposable
{
    private static readonly Color Gray = new(128, 128, 128);
    private readonly TestFolder _folder = new("engine-debug-overlay-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static Input Input => GetApp().World.Resource<Input>();

    // The frame: a blue cube in the middle of the world, and inside the interface's scope a red
    // square at the lower right and an ImGui window at the lower left.
    private static void Draw()
    {
        ClearBackground(Gray);
        BeginMode3D(new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45));
        DrawCube(Vector3.Zero, 1.5f, 1.5f, 1.5f, Color.Blue);
        EndMode3D();
        if (BeginUi())
        {
            ImGui.SetNextWindowPos(new Vector2(40, 340));
            ImGui.SetNextWindowSize(new Vector2(240, 160));
            ImGui.Begin("Inventory");
            ImGui.End();
        }
        DrawRectangle(800, 420, 80, 60, Color.Red);
        EndUi();
    }

    private static void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            BeginDrawing();
            Draw();
            EndDrawing();
        }
    }

    // A key held through a frame and let go in the next, so it is taken as it is let go.
    private static void Tap(Key key)
    {
        Input.SetKey(key, true);
        Frames(1);
        Input.SetKey(key, false);
        Frames(1);
    }

    private Image Capture()
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            Draw();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    private static bool Near(Color seen, Color wanted, int within) =>
        Math.Abs(seen.R - wanted.R) <= within && Math.Abs(seen.G - wanted.G) <= within && Math.Abs(seen.B - wanted.B) <= within;

    [NeedsVulkanFact]
    public void F1_Takes_The_Interface_Away_And_F3_Lays_The_Overlay_Over_The_World()
    {
        var config = Config.Default.WithWindow("debug overlay test", 960, 540) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        Frames(2);

        var shown = Capture();
        Near(GetImageColor(shown, 840, 450), Color.Red, 8).Should().BeTrue("the interface's square is drawn");
        Near(GetImageColor(shown, 160, 440), Gray, 12).Should().BeFalse("the interface's ImGui window is drawn over the gray");
        var cube = GetImageColor(shown, 480, 270);
        (cube.B > cube.R + 40).Should().BeTrue($"the world's cube is in the middle, {cube}");

        Tap(Key.F1);
        var hidden = Capture();
        Near(GetImageColor(hidden, 840, 450), Gray, 2).Should().BeTrue("the square drawn in the hidden scope is dropped");
        Near(GetImageColor(hidden, 160, 440), Gray, 2).Should().BeTrue("the ImGui window is not begun while BeginUi answers false");
        GetImageColor(hidden, 480, 270).Should().Be(cube, "the world is drawn as it was");

        Tap(Key.F3);
        var overlaid = Capture();
        GetImageColor(overlaid, 480, 270).Should().Be(cube, "the overlay's columns leave the middle of the world");
        Near(GetImageColor(overlaid, 840, 450), Gray, 2).Should().BeTrue("the overlay leaves the interface hidden");

        // The first line's backing, black at half over the gray, and its ink, light gray, in the band
        // of the line at the top left.
        var (backing, ink) = (0, 0);
        for (int y = 3; y < 19; y++)
            for (int x = 3; x < 120; x++)
            {
                var seen = GetImageColor(overlaid, x, y);
                if (seen.R is >= 50 and <= 78 && seen.G is >= 50 and <= 78) backing++;
                if (seen.R > 200 && seen.G > 200 && seen.B > 200) ink++;
            }
        backing.Should().BeGreaterThan(200, "the line stands on a dark half-clear backing");
        ink.Should().BeGreaterThan(30, "its text is light gray over the backing");

        // The strip under the two head lines, its newest bar at its right end standing from the
        // strip's floor, green, yellow or red by the frame's time, which the machine sets, a
        // runner's frame slower than a thirtieth of a second drawing it red.
        var bar = GetImageColor(overlaid, 2 + 239, 2 + 2 * 18 + 47);
        new[] { new Color(80, 220, 100), new Color(240, 200, 60), new Color(240, 80, 70) }.Any(color => Near(bar, color, 8))
            .Should().BeTrue($"the newest frame's bar stands in one of the strip's three colors, {bar}");
    }
}
