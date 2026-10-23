namespace Engine;

/// <summary>
/// What F1 and F3 hold, whether the interface is hidden and whether the debug overlay shows, with
/// the lines the overlay last wrote, a world resource.
/// </summary>
/// <remarks>
/// <para>
/// F1 turns <see cref="UiHidden"/> over as it goes down, which <see cref="E3D.BeginUi"/> reads, so
/// the program's interface drawn inside the scope is dropped while the world draws on. F3 turns the
/// overlay over as it is let go, where no other key went down while it was held, so a program's own
/// F3 chords stand and never open it. Neither is taken while a field of an ImGui window has the
/// caret, and the program sees both presses as it sees any key.
/// </para>
/// <para>
/// The overlay is the engine's own text and lines, drawn after everything else the frame draws in
/// 2D and outside the interface's scope, so F1 leaves it. Its lines are written again four times a
/// second while it shows, so a number stays still long enough to be read, and the strip of frame
/// times every frame. Every frame's time is kept whether it shows or not, so the strip it opens on
/// holds the last four seconds, and a run that never shows it pays the keys' reads and a time kept
/// a frame.
/// </para>
/// <para>
/// Shift held as F3 is let go turns the profiler's pass timings over, under the right column, and
/// shows the overlay with them, as Minecraft's Shift+F3 opens its debug screen with its chart. F3
/// alone hiding the overlay hides the profiler with it.
/// </para>
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>Whether the interface drawn inside <see cref="E3D.BeginUi"/> is hidden.</summary>
    public bool UiHidden { get; set; }

    /// <summary>Whether the overlay shows.</summary>
    public bool Shown { get; set; }

    /// <summary>Whether the profiler's pass timings show under the overlay's right column.</summary>
    public bool ProfilerShown { get; set; }

    /// <summary>The frames the strip holds, four seconds at sixty a second, as Minecraft's graph holds.</summary>
    internal const int StripFrames = 240;

    // The last frames' times in milliseconds, a ring, and how many it holds.
    private readonly float[] _times = new float[StripFrames];
    private int _timesNext, _timesHeld;

    // The program's sections, in the order added.
    private readonly List<(string Name, Func<IEnumerable<string>> Lines)> _sections = [];

    // Whether another key went down while the overlay's key was held, which leaves the release to
    // the program's chord.
    private bool _chord;

    // The lines last written and when, written again four times a second while the overlay shows.
    private Said? _said;
    private double _written = double.NegativeInfinity;

    /// <summary>The overlay's lines as they were last written, or null before it first showed.</summary>
    public Said? Written => _said;

    /// <summary>The overlay of a world, made the first time it is asked for.</summary>
    public static DebugOverlay Of(World world) => world.GetOrInsertResource(static () => new DebugOverlay());

    /// <summary>Keeps the frame's time and takes F1 and F3, once a frame in PreUpdate, once the frame's input is in.</summary>
    internal static void Keys(World world)
    {
        var overlay = Of(world);
        // The first frame, which has no time, is not kept.
        if (world.TryGetResource<Time>(out var time) && time.DeltaSeconds > 0) overlay.Keep((float)(time.DeltaSeconds * 1000));

        if (!world.TryGetResource<Input>(out var input)) return;
        var keys = world.GetOrInsertResource(static () => new DebugKeys());
        var typing = ImGuiNET.ImGui.GetCurrentContext() != IntPtr.Zero && ImGuiNET.ImGui.GetIO().WantTextInput;

        if (keys.HideUi != Key.Null && input.KeyPressed(keys.HideUi) && !typing) overlay.UiHidden = !overlay.UiHidden;

        if (keys.Overlay == Key.Null) return;
        if (input.KeyDown(keys.Overlay) && input.AnyKeyPressed() && !input.KeyPressed(keys.Overlay)) overlay._chord = true;
        if (!input.KeyReleased(keys.Overlay)) return;
        if (!overlay._chord && !typing) overlay.Turn(input.KeyDown(Key.LeftShift) || input.KeyDown(Key.RightShift));
        overlay._chord = false;
    }

    /// <summary>
    /// The key let go alone, which shows or hides the overlay, or with Shift the profiler, showing the
    /// overlay with it. The key alone hides the profiler with the overlay, so the overlay shown again
    /// by it alone comes back without it, as Minecraft's does.
    /// </summary>
    private void Turn(bool shift)
    {
        if (shift)
        {
            ProfilerShown = !ProfilerShown;
            if (ProfilerShown) Shown = true;
            return;
        }
        Shown = !Shown;
        if (!Shown) ProfilerShown = false;
    }

    private void Keep(float milliseconds)
    {
        _times[_timesNext] = milliseconds;
        _timesNext = (_timesNext + 1) % StripFrames;
        _timesHeld = Math.Min(_timesHeld + 1, StripFrames);
    }

    /// <summary>The frames' times the strip holds, in milliseconds, the oldest first.</summary>
    internal float[] FrameTimes()
    {
        var times = new float[_timesHeld];
        var first = (_timesNext - _timesHeld + StripFrames) % StripFrames;
        for (int i = 0; i < _timesHeld; i++) times[i] = _times[(first + i) % StripFrames];
        return times;
    }

    /// <summary>
    /// The last frame's time, the frames' average and their one percent low, the rate the slowest
    /// hundredth of them came at, which a game that stutters shows where its average does not.
    /// </summary>
    /// <remarks>
    /// The slowest hundredth is at least one frame, so 240 frames give the slowest three, and their
    /// times are averaged before the rate is taken, as the common tools take it.
    /// </remarks>
    internal static (float Now, float Average, float LowFps) Stats(ReadOnlySpan<float> times)
    {
        if (times.IsEmpty) return (0, 0, 0);
        var sorted = times.ToArray();
        Array.Sort(sorted);
        var slowest = (sorted.Length + 99) / 100;
        var low = 0f;
        for (int i = sorted.Length - slowest; i < sorted.Length; i++) low += sorted[i];
        low /= slowest;
        var sum = 0f;
        foreach (var t in times) sum += t;
        return (times[^1], sum / times.Length, low > 0 ? 1000 / low : 0);
    }

    /// <summary>Adds a section of the program's own under the engine's lines and any added before, in place of one of the same name.</summary>
    public void AddSection(string name, Func<IEnumerable<string>> lines)
    {
        var at = _sections.FindIndex(section => section.Name == name);
        if (at >= 0) _sections[at] = (name, lines);
        else _sections.Add((name, lines));
        _said = null;
    }

    /// <summary>Takes a section away by its name, and says whether there was one.</summary>
    public bool RemoveSection(string name)
    {
        _said = null;
        return _sections.RemoveAll(section => section.Name == name) > 0;
    }

    /// <summary>
    /// The system, for <see cref="Stage.Last"/> before the frame is rendered, which draws the overlay
    /// over everything drawn in 2D, its lines written again four times a second, the flat API made
    /// to run against the app while it draws where it runs against another or none.
    /// </summary>
    internal static void Draw(World world, App app)
    {
        if (!world.TryGetResource<DebugOverlay>(out var overlay) || !overlay.Shown) return;

        var current = E3D.CurrentApp;
        var borrowed = !E3D.Holds(world) && ReferenceEquals(app.World, world);
        if (!borrowed && !E3D.Holds(world)) return;
        if (borrowed) E3D.UseApp(app);
        try
        {
            var now = world.TryGetResource<Time>(out var time) ? time.ElapsedSeconds : 0;
            if (overlay._said is null || now - overlay._written >= 0.25 || now < overlay._written)
            {
                overlay._said = overlay.Lines(world);
                overlay._written = now;
            }
            E3D.DrawDebugOverlay(overlay._said, overlay.FrameTimes());
        }
        finally
        {
            if (borrowed) E3D.UseApp(current);
        }
    }
}
