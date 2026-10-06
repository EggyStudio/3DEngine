namespace Engine;

/// <summary>
/// Central application object holding the <see cref="World"/> and execution <see cref="Schedule"/>.
/// Supports plugin composition: add plugins, register systems, insert resources,
/// then call <see cref="Run"/> to enter the main loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle:</b> Create an <see cref="App"/>, add plugins via <see cref="AddPlugin"/>,
/// register systems via <see cref="AddSystem(Stage, SystemFn)"/>, insert resources via
/// <see cref="InsertResource{T}"/>, and finally call <see cref="Run"/>.
/// </para>
/// <para>
/// The <see cref="Run"/> method executes three phases:
/// <list type="number">
///   <item><description><see cref="Stage.Startup"/>, the systems that initialize once.</description></item>
///   <item><description>The main loop, the stages of each frame (<see cref="Stage.First"/> through <see cref="Stage.Last"/>), driven by <see cref="IMainLoopDriver"/>.</description></item>
///   <item><description><see cref="Stage.Cleanup"/>, teardown and resource disposal.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var app = new App(Config.Default.WithWindow("My Game", 1280, 720));
/// app.AddPlugin(new TimePlugin())
///    .AddPlugin(new InputPlugin())
///    .AddSystem(Stage.Update, MyGameSystem);
/// app.Run();
/// </code>
/// </example>
/// <seealso cref="World"/>
/// <seealso cref="Schedule"/>
/// <seealso cref="IPlugin"/>
/// <seealso cref="Config"/>
public sealed partial class App : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Application");

    /// <summary>Shared global state and resource container for the application.</summary>
    /// <seealso cref="World"/>
    public World World { get; } = new();

    /// <summary>Holds systems grouped by <see cref="Stage"/> and executes them on demand.</summary>
    /// <seealso cref="Schedule"/>
    internal Schedule Schedule { get; } = new();

    /// <summary>Total frames executed since <see cref="Run"/> was called.</summary>
    public ulong FrameCount => _frameCount;

    private readonly Dictionary<Type, IPlugin> _plugins = new();
    private ulong _frameCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="App"/> instance, configuring the file logger, startup banner,
    /// and inserting the <see cref="Config"/> and <see cref="ScheduleDiagnostics"/> resources into the <see cref="World"/>.
    /// </summary>
    /// <param name="config">
    /// Optional application configuration. When <c>null</c>, <see cref="Config.Default"/> is used
    /// (600×400 window, Vulkan backend, <see cref="WindowCommand.Show"/>).
    /// </param>
    /// <example>
    /// <code>
    /// // Use default configuration
    /// var app = new App();
    /// </code>
    /// <code>
    /// // Use custom configuration
    /// var app = new App(Config.GetDefault(title: "My Game", width: 1920, height: 1080));
    /// </code>
    /// </example>
    public App(Config? config = null)
    {
        CurrentApp.Value = new WeakReference<App>(this);
        Created?.Invoke(this);

        // Initialize file logger early so all subsequent logs are captured to disk.
        var logPath = LogConfig.GetLogFilePath("Engine.log");
        FileLoggerProvider.Initialize(logPath);

        Log.PrintStartupBanner();
        Logger.Info("Creating App instance...");

        var cfg = RunMode.Apply(config ?? Config.Default);
        World.InsertResource(cfg);
        World.InsertResource(Schedule.Diagnostics);

        Logger.Info($"{cfg}");
        Logger.Info("App instance created successfully.");
    }
    
    // The app this flow of execution made last, which the threads and tasks started after it
    // inherit, so a line logged anywhere in an app's work can be laid to the app. It is held
    // weakly, because whatever captures the flow's context keeps what is in it, and a context
    // captured while the app lived can outlive it, as macOS's FileSystemWatcher keeps the one the
    // script compiler's watchers start in until FSEvents lets go of their stream.
    private static readonly AsyncLocal<WeakReference<App>?> CurrentApp = new();

    /// <summary>
    /// The app the current flow of execution made last, which the threads and tasks it starts
    /// inherit, or <see langword="null"/>.
    /// </summary>
    /// <remarks>The test project lays an error the engine logs to the test whose app logged it (N 3.7).</remarks>
    internal static App? Current => CurrentApp.Value is { } made && made.TryGetTarget(out var app) ? app : null;

    /// <summary>Raised on the thread that makes an app, before it builds anything.</summary>
    internal static event Action<App>? Created;

    /// <summary>
    /// Disposes the <see cref="World"/> and all its <see cref="IDisposable"/> resources.
    /// Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        World.Dispose();
        Logger.Trace("App disposed.");
    }
}
