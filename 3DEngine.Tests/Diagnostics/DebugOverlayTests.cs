using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// F1 hiding the interface's scope and F3 the debug overlay, in a headless run, with F1 turning the
/// scope over and the scope's 2D draws dropped while the overlay's stay, F3 let go alone turning the
/// overlay over and a chord on it not, Shift+F3 the profiler, the keys moved and turned off, what the
/// overlay's lines say, the strip of the last 240 frames' times and a program's sections.
/// </summary>
/// <remarks>
/// What the frame's pixels show, the scope's draws and ImGui windows gone and the overlay over the
/// world, is read on a device by the render tests the local suite runs.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class DebugOverlayTests : IDisposable
{
    public DebugOverlayTests() =>
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static Input Input => GetApp().World.Resource<Input>();

    private static DebugOverlay Overlay => DebugOverlay.Of(GetApp().World);

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
    public void F3_Let_Go_Alone_Turns_The_Overlay_Over_And_A_Chord_On_It_Does_Not()
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
        Draws.Vertices.Length.Should().BeGreaterThan(after, "the overlay's lines are drawn after the program's, over the hidden interface");

        var said = Overlay.Written!;
        said.Head[0].Should().StartWith("3DEngine ").And.Contain("no device");
        said.Head[1].Should().MatchRegex(@"^\d+ fps, \d+\.\d ms a frame, frame \d+$");
        said.Left.Should().StartWith(
        [
            "XYZ 1.000 / 2.000 / 3.000",
            "Facing north (towards negative Z), heading 0.0, pitch 0.0",
            "Field of view 60 degrees",
            "",
        ]);
        said.Left.Should().Contain(line => line.StartsWith("Entities ", StringComparison.Ordinal) && line != "Entities 0");
        said.Right.Should().Contain(line => line.StartsWith("CPU ", StringComparison.Ordinal) && line.EndsWith(" logical cores", StringComparison.Ordinal));
        said.Right.Should().Contain("GPU none, as the run draws nothing").And.Contain("Display none, a headless run").And.Contain("Global illumination off");
        said.Right.Should().Contain(line => line.StartsWith("Lights 0 directional", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Camera_Entity_Is_The_Overlays_Camera_And_Its_Facing_Is_Named_As_Minecraft_Names_It()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var entity = ecs.Spawn();
        // Turned a quarter to the right, looking along plus X, east, and down a little.
        ecs.Add(entity, new Transform(new Vector3(4, 5, 6), Quaternion.CreateFromYawPitchRoll(-MathF.PI / 2, -0.25f, 0), Vector3.One));
        ecs.Add(entity, new Camera { FovY = MathF.PI / 3, Near = 0.1f, Far = 100 });

        var said = Overlay.Lines(GetApp().World);
        said.Left[0].Should().Be("XYZ 4.000 / 5.000 / 6.000");
        said.Left[1].Should().Be("Facing east (towards positive X), heading 90.0, pitch -14.3");
        said.Left[2].Should().Be("Field of view 60 degrees");

        DebugOverlay.Compass(359).Should().Be("north");
        DebugOverlay.Compass(225).Should().Be("southwest");
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
        (Overlay.Shown, Overlay.ProfilerShown).Should().Be((true, true));
        Overlay.Lines(GetApp().World).Right.Should().Contain("Pass timings not measured, as no frame is rendered", "a headless run renders no pass to time");

        Tap(Key.F3);
        (Overlay.Shown, Overlay.ProfilerShown).Should().Be((false, false), "F3 alone hides the profiler with the overlay");
        Tap(Key.F3);
        (Overlay.Shown, Overlay.ProfilerShown).Should().Be((true, false));
        Overlay.Lines(GetApp().World).Right.Should().NotContain(line => line.StartsWith("Pass timings", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Strip_Holds_The_Last_240_Frames_Times_Whether_Or_Not_The_Overlay_Shows()
    {
        Frames(300);
        Overlay.Shown.Should().BeFalse();

        var times = Overlay.FrameTimes();
        times.Should().HaveCount(DebugOverlay.StripFrames);
        times.Should().OnlyContain(ms => MathF.Abs(ms - 1000f / 60) < 1e-3f, "each frame counts a sixtieth of a second");
        Overlay.Lines(GetApp().World).Beside[0].Should().Be("Now 16.7 ms, average 16.7 ms, 1% low 60 fps");
    }

    [Fact]
    public void The_One_Percent_Low_Is_The_Rate_Of_The_Slowest_Hundredth_Of_The_Frames()
    {
        // A frame of 100 milliseconds among 199 of 10, the slowest hundredth of 200 being two frames.
        var times = Enumerable.Repeat(10f, 199).Append(100f).ToArray();
        var (now, average, low) = DebugOverlay.Stats(times);

        (now, average).Should().Be((100f, (199 * 10f + 100) / 200));
        low.Should().BeApproximately(1000 / 55f, 1e-3f, "the two slowest average 55 milliseconds");
        DebugOverlay.Stats([]).Should().Be((0f, 0f, 0f));
    }

    [Fact]
    public void A_Programs_Sections_Follow_The_Engines_Lines_In_The_Order_Added_And_Say_Why_They_Failed()
    {
        Frames(1);
        var seed = 42;
        AddDebugOverlaySection("World", () => [$"Seed {seed}", "Chunk 3 -1"]);
        AddDebugOverlaySection("Broken", () => throw new InvalidOperationException("no level is loaded"));
        AddDebugOverlaySection("Enemies", () => ["Alive 7"]);

        var left = Overlay.Lines(GetApp().World).Left;
        left.Should().EndWith(["", "World", "Seed 42", "Chunk 3 -1", "", "Broken", "Broken: InvalidOperationException, no level is loaded", "", "Enemies", "Alive 7"]);

        // One of the same name takes the place of the first, and one taken away is gone.
        seed = 7;
        AddDebugOverlaySection("World", () => [$"Seed {seed}"]);
        RemoveDebugOverlaySection("Broken").Should().BeTrue();
        RemoveDebugOverlaySection("Broken").Should().BeFalse();
        Overlay.Lines(GetApp().World).Left.Should().EndWith(["", "World", "Seed 7", "", "Enemies", "Alive 7"]);

        var nameless = () => AddDebugOverlaySection(" ", () => []);
        nameless.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_Overlay_Draws_Its_Strip_And_A_Sections_Lines_In_A_Frame()
    {
        Frames(1);
        AddDebugOverlaySection("World", () => ["Seed 42"]);
        Tap(Key.F3);

        var before = Draws.Vertices.Length;
        Frames(1);
        Overlay.Written!.Left.Should().Contain("Seed 42");
        // The strip's backing, its two rules and a bar a frame, besides every line's backing and text.
        (Draws.Vertices.Length - before).Should().BeGreaterThan(4 * (3 + 2));
    }
}
