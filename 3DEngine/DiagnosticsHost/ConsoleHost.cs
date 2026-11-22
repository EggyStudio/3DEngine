using Diagnostics;

namespace Engine;

/// <summary>
/// What a console command runs against, the world lent for the length of the call and the ways a
/// command can fail with a code or answer later, and the running of a line through the Diagnostics
/// module's registry of the process.
/// </summary>
/// <remarks>
/// <para>
/// Commands run on the main thread inside <see cref="Stage.First"/>, between frames, so a command
/// may read and change the world freely. Outside that, <see cref="World"/> is <c>null</c> and the
/// accessors say why.
/// </para>
/// <para>
/// An answer that comes frames later, by <see cref="Hold"/> or <see cref="Later"/>, is kept by the
/// host that ran the line, which writes it to its console when it comes.
/// </para>
/// </remarks>
internal static class ConsoleHost
{
    private static readonly ILogger Logger = Log.Category("Engine.Console");

    /// <summary>Frames a command answering later is given before it times out.</summary>
    public const ulong LaterFrames = 600;

    /// <summary>The world lent to the running command, or <c>null</c> between commands.</summary>
    public static World? World { get; private set; }

    /// <summary>The app whose world is lent, for its schedule, or <c>null</c> between commands.</summary>
    public static App? App { get; private set; }

    /// <summary>Which process sent the running command, as its client describes itself, or <c>null</c> where none said.</summary>
    public static string? Caller { get; private set; }

    internal static CliError? Failure { get; private set; }

    internal static ulong? Held { get; private set; }

    internal static Func<string?>? Pending { get; private set; }

    internal static ulong PendingFrames { get; private set; } = LaterFrames;

    /// <summary>
    /// The registry of the process, which every assembly of the engine has added its commands to
    /// by the time this returns.
    /// </summary>
    public static Registry Registry
    {
        get
        {
            EngineAssemblies.Load();
            return Registry.Process;
        }
    }

    /// <summary>
    /// Runs a line through the registry, its first word a command's name and the rest its words.
    /// </summary>
    /// <returns>
    /// The command's answer, or one failed with the module's code where no command has the name, a
    /// word did not parse or the command threw.
    /// </returns>
    public static Answer Run(string line)
    {
        var answer = Registry.Run(line);
        Logged(line, answer);
        return answer;
    }

    /// <summary>
    /// Writes the stack of what a line's command threw to the log, where its answer carries one,
    /// as the console and the panel run lines of their own.
    /// </summary>
    /// <remarks>
    /// The answer stays one line a person reads, and the stack goes to the log.
    /// </remarks>
    internal static void Logged(string line, Answer answer)
    {
        if (answer.Error is { } error) Logger.Error($"Command '{CommandLine.Parse(line).Name}' failed.", error);
    }

    /// <summary>The lent world's entities.</summary>
    /// <exception cref="InvalidOperationException">No command is running.</exception>
    public static EcsWorld Ecs => Lent.Resource<EcsWorld>();

    /// <summary>The lent world's clock.</summary>
    /// <exception cref="InvalidOperationException">No command is running.</exception>
    public static Time Time => Lent.Resource<Time>();

    private static World Lent => World ?? throw new InvalidOperationException(
        "This touches the world, so it can only run as a console command, which the CLI runs between frames.");

    /// <summary>Reports the running command as failed, with a code the CLI turns into an exit code.</summary>
    public static void Fail(string code, string message) => Failure = new CliError(code, message);

    /// <summary>Holds the command's answer until <paramref name="frame"/> has started, so "act, then look" is one call.</summary>
    public static void Hold(ulong frame) => Held = frame;

    /// <summary>
    /// Answers later, <paramref name="poll"/> asked once a frame until it returns an answer, or
    /// until <paramref name="frames"/> have run, past which the command times out.
    /// </summary>
    public static void Later(Func<string?> poll, ulong frames = LaterFrames)
    {
        Pending = poll ?? throw new ArgumentNullException(nameof(poll));
        PendingFrames = frames;
    }

    /// <summary>
    /// Lends <paramref name="world"/>, and the app it belongs to, to the commands run until the scope
    /// is disposed, with the process that sent them where it is known.
    /// </summary>
    public static Scope Lend(World world, App? app = null, string? caller = null) => new(world, app, caller);

    /// <summary>The span a world is lent for.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly World? _previous;
        private readonly App? _previousApp;
        private readonly string? _previousCaller;

        internal Scope(World world, App? app, string? caller)
        {
            ArgumentNullException.ThrowIfNull(world);
            _previous = World;
            _previousApp = App;
            _previousCaller = Caller;
            World = world;
            App = app;
            Caller = caller;
            Failure = null;
            Held = null;
            Pending = null;
            PendingFrames = LaterFrames;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            World = _previous;
            App = _previousApp;
            Caller = _previousCaller;
        }
    }
}
