using Diagnostics;
using ImGuiNET;

namespace Engine;

/// <summary>
/// The Diagnostics module's console, debug overlay, settings panel and keys of an app, a world
/// resource, the console and the panel drawn with the module's ImGui drawers and the overlay's
/// lines given by sections the engine fills.
/// </summary>
/// <remarks>
/// <para>
/// The keys are the module's actions, F1 hiding the interface drawn inside
/// <see cref="E3D.BeginUi"/>, F2 asking for a screenshot, F3 showing the overlay and with Shift its
/// detail, F4 opening the settings panel and F5 the console, each a text setting of the process's
/// registry a player rebinds, as <c>keys.console F6</c>. An action is taken as its key comes up
/// where no other key went down meanwhile, so a program's chords on the keys, F3 with F4 among
/// them, stay the program's, and the program sees every press as it sees any key.
/// </para>
/// <para>
/// The registry refuses a name twice, so the keys stand for the app made last. Making an app's
/// binding disposes the keys of the app made before it, which takes their five settings out of
/// the registry and ends them, and an app's keys are disposed as it shuts down. The engine's
/// settings (<see cref="EngineSettings"/>) stand for that app's world between lines in the same
/// way.
/// </para>
/// <para>
/// The console and the panel are drawn in ImGui's frame in PreUpdate with the world lent, so a line
/// entered there runs as a line the e3d client sends does, between the frame's input and its
/// update. A failure a command gives the host is written under its answer, and an answer that
/// comes frames later is asked for once a frame and written to the console when it comes. A line
/// the console or the panel runs whose command threw has the stack of what it threw written to the
/// log, as a line the e3d client sends has.
/// </para>
/// </remarks>
internal sealed partial class DiagnosticsBinding
{
    // The binding whose keys stand in the registry, and the gate that changes it.
    private static readonly Lock Gate = new();
    private static DiagnosticsBinding? _registered;

    private readonly ImGuiConsole _drawnConsole;
    private readonly ImGuiPanel _drawnPanel;
    private readonly HashSet<string> _programSections = new(StringComparer.Ordinal);

    // A command's answer that comes frames later, asked for once a frame until the frame past
    // which it times out.
    private (string Name, Func<string?> Poll, ulong Until, ulong Allowed)? _later;

    private DiagnosticsBinding(World world)
    {
        Console = new ConsoleModel(ConsoleHost.Registry);
        Panel = new PanelModel(ConsoleHost.Registry);
        Console.LineRun += ConsoleHost.Logged;
        Panel.Answered += ConsoleHost.Logged;
        lock (Gate)
        {
            _registered?.Keys.Dispose();
            Keys = new DiagnosticKeys(ConsoleHost.Registry) { Overlay = Overlay, Panel = Panel, Console = Console };
            _registered = this;
            ConsoleHost.Bind(world);
            if (world.TryGetResource<Config>(out var config)) EngineSettings.Started(config);
        }
        Keys.ScreenshotAsked += () => world.GetOrInsertResource(static () => new ScreenshotKey()).Asked(world);
        _drawnConsole = new ImGuiConsole(Console);
        _drawnPanel = new ImGuiPanel(Panel);
        AddSections(world);
    }

    /// <summary>The overlay, its sections the engine's and the program's.</summary>
    public OverlayModel Overlay { get; } = new();

    /// <summary>The console over the process's registry.</summary>
    public ConsoleModel Console { get; }

    /// <summary>The settings panel over the process's registry.</summary>
    public PanelModel Panel { get; }

    /// <summary>The five keys, which turn the overlay, the panel and the console over.</summary>
    public DiagnosticKeys Keys { get; }

    /// <summary>The binding of a world, made the first time it is asked for.</summary>
    public static DiagnosticsBinding Of(World world) => world.GetOrInsertResource(() => new DiagnosticsBinding(world));

    /// <summary>
    /// Whether the console or the settings panel is open, which holds the exit key so that the key
    /// goes to the one open and the window does not see it. The debug overlay takes no input, so
    /// with it alone shown the key passes on.
    /// </summary>
    internal bool HoldsKeyboard => Keys.Panels.Open is { } open && open != OpenPanels.OverlayName;

    /// <summary>Binds an action to an engine key, or to none with <see cref="Key.Null"/>.</summary>
    internal void Bind(KeyAction action, Key key) => Keys.KeyOf(action).TrySet(key == Key.Null ? "" : key.ToString());

    /// <summary>
    /// Adds a section of the program's own under the engine's lines on the left and any added
    /// before, in place of one of the same name, its name over its lines after a gap.
    /// </summary>
    internal void AddSection(string name, Func<IEnumerable<string>> lines)
    {
        _programSections.Add(name);
        Overlay.AddSection(name, OverlayPlace.Left, () => ["", name, .. lines()]);
    }

    /// <summary>
    /// Takes a section of the program's away by its name, and says whether there was one.
    /// </summary>
    internal bool RemoveSection(string name) => _programSections.Remove(name) && Overlay.RemoveSection(name);

    /// <summary>
    /// Gives the keys every key that went down and came up this frame, once a frame in PreUpdate
    /// once the frame's input is in.
    /// </summary>
    internal static void TakeKeys(World world, App app)
    {
        if (!world.TryGetResource<Input>(out var input)) return;
        var binding = Of(world);
        // A field of an ImGui window that has the caret, the console's among them, takes the keys
        // of one character typed into it.
        var typing = binding._drawnConsole.Typing || (ImGui.GetCurrentContext() != IntPtr.Zero && ImGui.GetIO().WantTextInput);
        Borrowing(world, app, () =>
        {
            foreach (var key in input.KeysPressed.ToArray()) binding.Keys.KeyDown(key.ToString(), typing);
            foreach (var key in input.KeysReleased.ToArray()) binding.Keys.KeyUp(key.ToString());
        });
    }

    /// <summary>
    /// Draws the console and the panel where they are open, in ImGui's frame in PreUpdate after the
    /// keys, with the world lent to the lines they run.
    /// </summary>
    internal static void DrawWindows(World world, App app)
    {
        if (ImGui.GetCurrentContext() == IntPtr.Zero || !world.TryGetResource<DiagnosticsBinding>(out var binding)) return;
        binding.AskLater(world, app);
        var size = ImGui.GetIO().DisplaySize;
        using (ConsoleHost.Lend(world, app))
        {
            binding._drawnConsole.Draw(size);
            binding._drawnPanel.Draw(size);
            binding.Keep(world);
        }
    }

    /// <summary>
    /// Keeps the frame's time in the overlay and draws it over everything drawn in 2D where it is
    /// shown, once a frame in <see cref="Stage.Last"/> before the frame is rendered.
    /// </summary>
    internal static void DrawOverlay(World world, App app)
    {
        var binding = Of(world);
        Borrowing(world, app, () =>
        {
            // The first frame, which has no time, is not kept.
            if (world.TryGetResource<Time>(out var time) && time.DeltaSeconds > 0) binding.Overlay.Frame(time.DeltaSeconds);
            if (binding.Overlay.Shown) E3D.DrawDebugOverlay(binding.Overlay);
        });
    }

    /// <summary>
    /// Writes the settings file and disposes the keys as the app shuts down, which takes their
    /// settings out of the registry where they are its own, and the settings stand for no world
    /// where they stood for this one.
    /// </summary>
    internal static void Release(World world)
    {
        if (!world.TryGetResource<DiagnosticsBinding>(out var binding)) return;
        binding.CloseSettings();
        lock (Gate)
        {
            binding.Keys.Dispose();
            if (!ReferenceEquals(_registered, binding)) return;
            _registered = null;
            ConsoleHost.Bind(null);
        }
    }

    // What a command run by the console or the panel left with the host, a failure written under
    // its answer and an answer to come kept for the frames after.
    private void Keep(World world)
    {
        if (ConsoleHost.Failure is { } failure) Console.Write(ConsoleLevel.Error, $"{failure.Code} {failure.Message}");
        if (ConsoleHost.Pending is not { } poll) return;
        var name = Console.History.Count > 0 ? CommandLine.Parse(Console.History[^1]).Name : "";
        _later = (name, poll, Frame(world) + ConsoleHost.PendingFrames, ConsoleHost.PendingFrames);
    }

    // The answer to come, asked for with the world lent and written to the console once it comes,
    // or a line saying it timed out.
    private void AskLater(World world, App app)
    {
        if (_later is not { } later) return;
        string? answer;
        CliError? failure;
        using (ConsoleHost.Lend(world, app))
        {
            answer = later.Poll();
            failure = ConsoleHost.Failure;
        }

        if (answer is not null)
        {
            _later = null;
            if (answer.Length > 0) Console.Write(ConsoleLevel.Info, answer);
            if (failure is { } error) Console.Write(ConsoleLevel.Error, $"{error.Code} {error.Message}");
        }
        else if (Frame(world) >= later.Until)
        {
            _later = null;
            Console.Write(ConsoleLevel.Error, $"'{later.Name}' had no answer after {later.Allowed} frames.");
        }
    }

    private static ulong Frame(World world) => world.TryGetResource<Time>(out var time) ? time.FrameCount : 0;

    // Runs what asks the overlay's sections for their lines with the flat API made to run against
    // the app, where it runs against another or none, since a section reads the window through it.
    private static void Borrowing(World world, App app, Action read)
    {
        var current = E3D.CurrentApp;
        var borrowed = !E3D.Holds(world) && ReferenceEquals(app.World, world);
        if (borrowed) E3D.UseApp(app);
        try
        {
            read();
        }
        finally
        {
            if (borrowed) E3D.UseApp(current);
        }
    }
}
