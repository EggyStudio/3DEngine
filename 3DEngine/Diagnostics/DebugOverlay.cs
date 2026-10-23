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
/// second while it shows, so a number stays still long enough to be read, and a run that never
/// shows it pays the keys' reads.
/// </para>
/// </remarks>
internal sealed partial class DebugOverlay
{
    /// <summary>Whether the interface drawn inside <see cref="E3D.BeginUi"/> is hidden.</summary>
    public bool UiHidden { get; set; }

    /// <summary>Whether the overlay shows.</summary>
    public bool Shown { get; set; }

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

    /// <summary>Takes F1 and F3, once a frame in PreUpdate, once the frame's input is in.</summary>
    internal static void Keys(World world)
    {
        if (!world.TryGetResource<Input>(out var input)) return;
        var keys = world.GetOrInsertResource(static () => new DebugKeys());
        var overlay = Of(world);
        var typing = ImGuiNET.ImGui.GetCurrentContext() != IntPtr.Zero && ImGuiNET.ImGui.GetIO().WantTextInput;

        if (keys.HideUi != Key.Null && input.KeyPressed(keys.HideUi) && !typing) overlay.UiHidden = !overlay.UiHidden;

        if (keys.Overlay == Key.Null) return;
        if (input.KeyDown(keys.Overlay) && input.AnyKeyPressed() && !input.KeyPressed(keys.Overlay)) overlay._chord = true;
        if (!input.KeyReleased(keys.Overlay)) return;
        if (!overlay._chord && !typing) overlay.Shown = !overlay.Shown;
        overlay._chord = false;
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
                overlay._said = Lines(world);
                overlay._written = now;
            }
            E3D.DrawDebugOverlay(overlay._said);
        }
        finally
        {
            if (borrowed) E3D.UseApp(current);
        }
    }
}
