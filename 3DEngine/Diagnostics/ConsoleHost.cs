namespace Engine;

/// <summary>
/// What a console command runs against: the world lent for the length of the call, and the ways a
/// command can fail with a code or answer later.
/// </summary>
/// <remarks>
/// Commands run on the main thread inside <see cref="Stage.First"/>, between frames, so a command
/// may read and change the world freely. Outside that, <see cref="World"/> is <c>null</c> and the
/// accessors say why.
/// </remarks>
public static class ConsoleHost
{
    /// <summary>Frames a command answering later is given before it times out.</summary>
    public const ulong LaterFrames = 600;

    /// <summary>The world lent to the running command, or <c>null</c> between commands.</summary>
    public static World? World { get; private set; }

    /// <summary>The app whose world is lent, for its schedule, or <c>null</c> between commands.</summary>
    public static App? App { get; private set; }

    internal static CliError? Failure { get; private set; }

    internal static ulong? Held { get; private set; }

    internal static Func<string?>? Pending { get; private set; }

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

    /// <summary>Answers later: <paramref name="poll"/> is asked once a frame until it returns an answer.</summary>
    public static void Later(Func<string?> poll) => Pending = poll ?? throw new ArgumentNullException(nameof(poll));

    /// <summary>Lends <paramref name="world"/>, and the app it belongs to, to the commands run until the scope is disposed.</summary>
    public static Scope Lend(World world, App? app = null) => new(world, app);

    /// <summary>The span a world is lent for.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly World? _previous;
        private readonly App? _previousApp;

        internal Scope(World world, App? app)
        {
            ArgumentNullException.ThrowIfNull(world);
            _previous = World;
            _previousApp = App;
            World = world;
            App = app;
            Failure = null;
            Held = null;
            Pending = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            World = _previous;
            App = _previousApp;
        }
    }
}
