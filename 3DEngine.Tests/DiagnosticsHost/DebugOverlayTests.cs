using System.Numerics;
using Diagnostics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.DiagnosticsHost;

/// <summary>
/// F1 hiding the interface's scope and F3 the debug overlay, the Diagnostics module's keys and
/// overlay in a headless run, with F1 turning the scope over and the scope's 2D draws dropped while
/// the overlay's stay, F3 let go alone turning the overlay over and a chord of it with F4 turning
/// neither the overlay nor the console, Shift+F3 the profiler, the keys moved and turned off, what
/// the engine's sections say, the strip of the last 240 frames' times, a program's sections and
/// where the lines stand in a narrow window and a wide one.
/// </summary>
/// <remarks>
/// What the frame's pixels show, the scope's draws and ImGui windows gone and the overlay over the
/// world, is read on a device by the render tests the local suite runs.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class DebugOverlayTests : IDisposable
{
    // The vertices the frame's list holds once the overlay has drawn, read before the list is let go
    // at the frame's end, as a headless run lets it go with nothing rendered.
    private int _atEnd;

    public DebugOverlayTests()
    {
        var app = new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins());
        app.AddSystem(Stage.Last, new SystemDescriptor(world => _atEnd = world.Resource<DrawList>().Vertices.Length, "DebugOverlayTests.AtEnd")
            .After("RenderPlugin.DebugOverlay")
            .Before("RenderPlugin.ClearDraws")
            .MainThreadOnly());
        UseApp(app);
    }

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static Input Input => GetApp().World.Resource<Input>();

    private static DiagnosticsBinding Binding => DiagnosticsBinding.Of(GetApp().World);

    private static OverlayModel Overlay => Binding.Overlay;

    // The lines of a place as the sections give them when asked again.
    private static IReadOnlyList<string> Said(OverlayPlace place)
    {
        Overlay.Refresh();
        return Overlay.Lines(place);
    }

    private static DrawList Draws => GetApp().World.Resource<DrawList>();

    private static void Frames(int count, Action? draw = null)
    {
        for (var i = 0; i < count; i++)
        {
            BeginDrawing();
            ClearBackground(Color.DarkBlue);
            draw?.Invoke();
            EndDrawing();
        }
    }

    // A key held through a frame and let go in the next.
    private static void Tap(Key key)
    {
        Input.SetKey(key, true);
        Frames(1);
        Input.SetKey(key, false);
        Frames(1);
    }

    [Fact]
    public void F1_Hides_The_Interface_And_Shows_It_Again_And_The_Scope_Answers_Whether_It_Draws()
    {
        Frames(1);
        IsUiHidden().Should().BeFalse();

        Tap(Key.F1);
        IsUiHidden().Should().BeTrue();
        BeginDrawing();
        BeginUi().Should().BeFalse("a program's ImGui windows go inside an if on the answer");
        EndUi();
        EndDrawing();

        Tap(Key.F1);
        IsUiHidden().Should().BeFalse();
        var unopened = () => EndUi();
        unopened.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void While_Hidden_The_Scopes_Shapes_Are_Dropped_And_Those_Drawn_Outside_It_Kept()
    {
        Frames(1);
        Tap(Key.F1);

        BeginDrawing();
        DrawRectangle(0, 0, 10, 10, Color.Red);
        var outside = Draws.Vertices.Length;
        BeginUi();
        DrawRectangle(20, 0, 10, 10, Color.Green);
        DrawText("Health 100", 40, 0, 20, Color.White);
        EndUi();
        Draws.Vertices.Length.Should().Be(outside, "the HUD's rectangle and text are dropped");
        DrawRectangle(0, 20, 10, 10, Color.Blue);
        Draws.Vertices.Length.Should().Be(outside + 4, "a screen drawn outside the scope stays");
        Draws.Batches.Sum(batch => batch.IndexCount).Should().Be(12, "two rectangles' triangles, and nothing of the scope's");
        EndDrawing();

        // Shown again, the same scope keeps what it draws.
        Tap(Key.F1);
        BeginDrawing();
        var before = Draws.Vertices.Length;
        BeginUi().Should().BeTrue();
        DrawRectangle(20, 0, 10, 10, Color.Green);
        EndUi();
        Draws.Vertices.Length.Should().Be(before + 4);
        EndDrawing();
    }

    [Fact]
    public void F3_Let_Go_Alone_Turns_The_Overlay_Over_And_A_Chord_Of_It_With_F4_Turns_Nothing()
    {
        Frames(1);
        Tap(Key.F3);
        Overlay.Shown.Should().BeTrue();

        // F3 held while F4 goes down is the program's chord.
        Input.SetKey(Key.F3, true);
        Frames(1);
        Input.SetKey(Key.F4, true);
        Frames(1);
        Input.SetKey(Key.F4, false);
        Input.SetKey(Key.F3, false);
        Frames(1);
        Overlay.Shown.Should().BeTrue("a chord on F3 never turns the overlay");
        Binding.Console.Open.Should().BeFalse("and F4 in the chord opens no console");

        // Pressed and not yet let go, F3 has done nothing.
        Input.SetKey(Key.F3, true);
        Frames(1);
        Overlay.Shown.Should().BeTrue();
        Input.SetKey(Key.F3, false);
        Frames(1);
        Overlay.Shown.Should().BeFalse("let go alone, it turns the overlay off");
    }

    [Fact]
    public void The_Keys_Move_And_Turn_Off()
    {
        Frames(1);
        SetDebugOverlayKey(Key.F6);
        SetUiHiddenKey(Key.Null);
        Tap(Key.F3);
        Tap(Key.F1);
        (Overlay.Shown, IsUiHidden()).Should().Be((false, false));

        Tap(Key.F6);
        Overlay.Shown.Should().BeTrue();
    }

    [Fact]
    public void The_Overlay_Is_Drawn_Over_A_Hidden_Interface_And_Says_The_Engine_The_Camera_And_The_World()
    {
        var camera = new Camera3D(new Vector3(1, 2, 3), new Vector3(1, 2, -7), Vector3.UnitY, 60);
        Frames(1);
        Tap(Key.F1);
        Tap(Key.F3);
        GetApp().World.Resource<EcsWorld>().Spawn();

        // The lines are written again four times a second, so the frames run past a quarter of one.
        var (before, after) = (0, 0);
        Frames(20, () =>
        {
            BeginMode3D(camera);
            EndMode3D();
            before = Draws.Vertices.Length;
            BeginUi();
            DrawText("dropped", 0, 0, 20, Color.White);
            EndUi();
            after = Draws.Vertices.Length;
        });
        after.Should().Be(before, "the text drawn in the hidden scope is dropped");
        _atEnd.Should().BeGreaterThan(after, "the overlay's lines are drawn after the program's, over the hidden interface");

        var head = Overlay.Lines(OverlayPlace.Head);
        head[0].Should().MatchRegex(@"^\d+ fps, \d+\.\d ms, \d+\.\d ms average, \d+ fps 1% low$", "the module's own line of the frame's rate comes first");
        head[1].Should().StartWith("3DEngine ").And.Contain("no device");
        head.Should().HaveCount(2);
        Overlay.Lines(OverlayPlace.Left).Should().StartWith(
        [
            "XYZ 1.000 / 2.000 / 3.000",
            "Facing north (towards negative Z), heading 0.0, pitch 0.0",
            "Field of view 60 degrees",
            "",
        ]);
        Overlay.Lines(OverlayPlace.Left).Should().Contain(line => line.StartsWith("Entities ", StringComparison.Ordinal) && line != "Entities 0");
        var right = Overlay.Lines(OverlayPlace.Right);
        right.Should().Contain(line => line.StartsWith("CPU ", StringComparison.Ordinal) && line.EndsWith(" logical cores", StringComparison.Ordinal));
        right.Should().Contain("GPU none, as the run draws nothing").And.Contain("Display none, a headless run").And.Contain("Global illumination off");
        right.Should().Contain(line => line.StartsWith("Lights 0 directional", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Camera_Entity_Is_The_Overlays_Camera_And_Its_Facing_Is_Named_As_Minecraft_Names_It()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var entity = ecs.Spawn();
        // Turned a quarter to the right, looking along plus X, east, and down a little.
        ecs.Add(entity, new Transform(new Vector3(4, 5, 6), Quaternion.CreateFromYawPitchRoll(-MathF.PI / 2, -0.25f, 0), Vector3.One));
        ecs.Add(entity, new Camera { FovY = MathF.PI / 3, Near = 0.1f, Far = 100 });

        var left = Said(OverlayPlace.Left);
        left[0].Should().Be("XYZ 4.000 / 5.000 / 6.000");
        left[1].Should().Be("Facing east (towards positive X), heading 90.0, pitch -14.3");
        left[2].Should().Be("Field of view 60 degrees");

        DiagnosticsBinding.Compass(359).Should().Be("north");
        DiagnosticsBinding.Compass(225).Should().Be("southwest");
    }

    [Fact]
    public void Shift_F3_Shows_The_Profiler_With_The_Overlay_And_F3_Alone_Hides_Both()
    {
        Frames(1);
        Input.SetKey(Key.LeftShift, true);
        Frames(1);
        Tap(Key.F3);
        Input.SetKey(Key.LeftShift, false);
        Frames(1);
        (Overlay.Shown, Overlay.Detailed).Should().Be((true, true));
        Said(OverlayPlace.Right).Should().Contain("Pass timings not measured, as no frame is rendered", "a headless run renders no pass to time");

        Tap(Key.F3);
        (Overlay.Shown, Overlay.Detailed).Should().Be((false, false), "F3 alone hides the profiler with the overlay");
        Tap(Key.F3);
        (Overlay.Shown, Overlay.Detailed).Should().Be((true, false));
        Said(OverlayPlace.Right).Should().NotContain(line => line.StartsWith("Pass timings", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Strip_Holds_The_Last_240_Frames_Times_Whether_Or_Not_The_Overlay_Shows()
    {
        Frames(300);
        Overlay.Shown.Should().BeFalse();

        var times = Overlay.FrameTimes;
        times.Should().HaveCount(OverlayModel.StripFrames);
        times.Should().OnlyContain(ms => MathF.Abs(ms - 1000f / 60) < 1e-3f, "each frame counts a sixtieth of a second");
        Said(OverlayPlace.Head)[0].Should().Be("60 fps, 16.7 ms, 16.7 ms average, 60 fps 1% low");
    }

    [Fact]
    public void A_Programs_Sections_Follow_The_Engines_Lines_In_The_Order_Added_And_Say_Why_They_Failed()
    {
        Frames(1);
        var seed = 42;
        AddDebugOverlaySection("World", () => [$"Seed {seed}", "Chunk 3 -1"]);
        AddDebugOverlaySection("Broken", () => throw new InvalidOperationException("no level is loaded"));
        AddDebugOverlaySection("Enemies", () => ["Alive 7"]);

        Said(OverlayPlace.Left).Should().EndWith(["", "World", "Seed 42", "Chunk 3 -1", "Broken failed. no level is loaded", "", "Enemies", "Alive 7"]);

        // One of the same name takes the place of the first, and one taken away is gone.
        seed = 7;
        AddDebugOverlaySection("World", () => [$"Seed {seed}"]);
        RemoveDebugOverlaySection("Broken").Should().BeTrue();
        RemoveDebugOverlaySection("Broken").Should().BeFalse();
        Said(OverlayPlace.Left).Should().EndWith(["", "World", "Seed 7", "", "Enemies", "Alive 7"]);

        var nameless = () => AddDebugOverlaySection(" ", () => []);
        nameless.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_Overlay_Draws_Its_Strip_And_A_Sections_Lines_In_A_Frame()
    {
        Frames(1);
        AddDebugOverlaySection("World", () => ["Seed 42"]);
        Tap(Key.F3);

        Frames(1);
        Overlay.Lines(OverlayPlace.Left).Should().Contain("Seed 42");
        // The strip's backing, its two rules and a bar a frame, besides every line's backing and
        // text, in a frame that draws nothing of its own.
        _atEnd.Should().BeGreaterThan(4 * (3 + 2));
    }

    // An overlay of the lines the bounce rooms example shows on the RTX machine, the longest of
    // each place, laid out at a width.
    private static (List<OverlayBox> Lines, int Strip) LaidOut(int width)
    {
        var overlay = new OverlayModel();
        overlay.AddSection("head", OverlayPlace.Head, () => ["3DEngine 0.1.0-preview.1 (debug build, Vulkan 1.4.351)"]);
        overlay.AddSection("beside", OverlayPlace.Beside, () => ["GPU 4.29 ms a frame over 21 passes"]);
        overlay.AddSection("left", OverlayPlace.Left, () =>
            ["XYZ 0.000 / 2.500 / 8.000", "Facing north (towards negative Z), heading 0.0, pitch -0.7", "Field of view 45 degrees"]);
        overlay.AddSection("right", OverlayPlace.Right, () =>
        [
            ".NET 10.0.12, x64",
            "Bazzite",
            "CPU Intel(R) Core(TM) i9-14900HX, 32 logical cores",
            "Memory 779 MB of 63984 MB, managed 35 MB",
            "",
            "GPU NVIDIA GeForce RTX 4070 Laptop GPU (discrete gpu)",
            "",
            "Global illumination High: 3 cascades of 512 probes, 256, 256, 256 rays each, 393216 rays a frame",
        ]);
        overlay.Refresh();
        return LayOutOverlay(overlay, width);
    }

    [Fact]
    public void At_800_Wide_The_Right_Column_Goes_Down_Under_What_It_Would_Meet_And_Breaks_A_Line_Wider_Than_The_Window()
    {
        var (lines, strip) = LaidOut(800);
        lines.Should().OnlyContain(line => line.X >= 0 && line.X + line.Wide <= 800, "no line runs past the window's edges");

        // The right column's lines are those whose backing reaches the window's right edge less its
        // margin, and the rest are the head's, the left's and those beside the strip.
        var right = lines.Where(line => line.X + line.Wide == 798).ToList();
        var left = lines.Except(right).Select(line => (line.X, line.Y, line.Wide, High: 18)).Append((X: 2, Y: strip, Wide: 240, High: 48)).ToList();
        foreach (var line in right)
            left.Should().NotContain(other => other.X + other.Wide + 8 > line.X && other.Y < line.Y + 18 && line.Y < other.Y + other.High,
                $"'{line.Text}' keeps eight pixels from the left's lines and the strip");

        right.Select(line => line.Text).Should().ContainInOrder(
            ".NET 10.0.12, x64", "CPU Intel(R) Core(TM) i9-14900HX, 32 logical cores",
            "Global illumination High: 3 cascades of 512 probes, 256, 256, 256 rays each,", "393216 rays a frame");
        right.Should().BeInAscendingOrder(line => line.Y, "the lines keep their order down the column");
        var cpu = right.Single(line => line.Text.StartsWith("CPU ", StringComparison.Ordinal));
        cpu.Y.Should().BeGreaterThan(2 + 2 * 18, "the CPU's line would meet the GPU's time beside the strip at its own row");
    }

    [Fact]
    public void At_1280_Wide_The_Columns_Stand_Side_By_Side_With_Every_Line_At_Its_Row()
    {
        var (lines, _) = LaidOut(1280);
        var right = lines.Where(line => line.X + line.Wide == 1278).ToList();
        right.Select(line => line.Y).Should().Equal([2, 20, 38, 56, 92, 128], "each line stands at its own row, the empty ones gaps");
        right[^1].Text.Should().EndWith("393216 rays a frame", "the longest line is whole where it fits");
    }
}
