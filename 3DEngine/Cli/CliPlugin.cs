using System.Reflection;

namespace Engine;

/// <summary>
/// Serves the <c>e3d</c> command line when <see cref="Config.Serve"/> is set: a loopback socket
/// answering console commands between frames, and a session file that tells <c>e3d</c> where it is.
/// </summary>
/// <remarks>
/// <para>
/// Requests are answered at the top of <see cref="Stage.First"/>, so a command sees the world as
/// the frame's systems will, and what it changes is in place before they run. The session file is
/// rewritten every 30 frames as a heartbeat, and removed at <see cref="Stage.Cleanup"/> and at
/// process exit, so a file left behind belongs to a process that died.
/// </para>
/// <para>
/// The plugin is in <see cref="DefaultPlugins"/> and does nothing unless asked, so every program
/// can be served with <c>--serve</c> and none pays for it otherwise.
/// </para>
/// </remarks>
public sealed class CliPlugin : IPlugin
{
    private const ulong Beat = 30;
    private static readonly DateTimeOffset Started = DateTimeOffset.UtcNow;

    private readonly CliQueue _queue = new();
    private CliServer? _server;

    /// <inheritdoc />
    public int Order => PluginOrder.Late;

    /// <inheritdoc />
    public void Build(App app)
    {
        var config = app.World.Resource<Config>();
        if (!config.Serve) return;

        ConsoleLog.TeeConsole();
        _server = new CliServer(_queue);
        CliSessionFile.Write(Describe(app, "starting", 0));
        Console.WriteLine($"[e3d] serving on 127.0.0.1:{_server.Port}. Drive it with e3d status, e3d list, e3d command <name>.");

        // Ready once a frame has run, because nothing can be asked before there is a frame to answer in.
        app.AddSystem(Stage.Startup, new SystemDescriptor(world => CliSessionFile.Write(Describe(app, "ready", CliDispatch.Frame(world))), "Cli.Ready")
            .MainThreadOnly());
        app.AddSystem(Stage.First, new SystemDescriptor(world => Tick(app, world), "Cli.Serve").MainThreadOnly());
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(_ => Close(), "Cli.Close").MainThreadOnly());
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Close();
    }

    private void Tick(App app, World world)
    {
        var frame = CliDispatch.Frame(world);
        ConsoleLog.Frame = frame;
        if (world.TryGetResource<SyntheticInput>(out var synthetic) && world.TryGetResource<Input>(out var input))
            synthetic.Update(input, frame);
        _queue.Pump(world, app);

        if (frame % Beat != 0) return;
        try { CliSessionFile.Write(Describe(app, "ready", frame)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private void Close()
    {
        if (_server is null) return;
        _queue.Abandon();
        _server.Dispose();
        _server = null;
        CliSessionFile.Remove(Environment.ProcessId);
    }

    private CliSession Describe(App app, string state, ulong frame)
    {
        var config = app.World.Resource<Config>();
        return new CliSession(
            Environment.ProcessId,
            _server?.Port ?? 0,
            _server?.Token ?? string.Empty,
            Environment.CurrentDirectory,
            Assembly.GetEntryAssembly()?.GetName().Name ?? "app",
            config.WindowData.Title,
            RunMode.Describe(config),
            Started,
            RunMode.HasRenderer(app.World),
            state,
            frame,
            DateTimeOffset.UtcNow);
    }
}
