namespace Engine;

/// <summary>
/// Handles application exit, listening for window quit events and requesting closure.
/// Inserts an <see cref="AppExit"/> resource and adds a <see cref="Stage.First"/> system
/// that closes the window when <see cref="AppExit.Requested"/> is set.
/// </summary>
/// <seealso cref="AppExit"/>
/// <seealso cref="AppWindow"/>
public sealed class AppExitPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.AppExit");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("AppExitPlugin: Registering exit handler...");
        // Ensure exit state resource exists.
        app.World.InitResource<AppExit>();

        // When the window signals quit, raise the Requested flag. A headless run has no window.
        if (app.World.TryGetResource<AppWindow>(out var window))
        {
            window.QuitEvent += () =>
            {
                Logger.Info("Quit event received, so the app exits.");
                app.World.Resource<AppExit>().Requested = true;
            };
        }

        // --frames N: ask to close once N frames have run, at the end of the last one.
        var frames = app.World.Resource<Config>().Frames;
        if (frames > 0)
        {
            app.AddSystem(Stage.Last, new SystemDescriptor(world =>
                {
                    if (world.Resource<Time>().FrameCount >= frames && !world.Resource<AppExit>().Requested)
                    {
                        Logger.Info($"Ran the {frames} frame(s) asked for, closing.");
                        world.Resource<AppExit>().Requested = true;
                    }
                }, "AppExitPlugin.Frames")
                .Read<Time>()
                .Write<AppExit>());
        }

        // Early frame: if an exit was requested previously, ask window to close (will break main loop).
        app.AddSystem(Stage.First, new SystemDescriptor(world =>
            {
                if (world.Resource<AppExit>().Requested && world.TryGetResource<AppWindow>(out var appWindow))
                {
                    Logger.Info("Exit requested, closing the window to end the main loop.");
                    appWindow.RequestClose();
                }
            }, "AppExitPlugin.Update")
            .Read<AppExit>()
            .Write<AppWindow>());

        Logger.Info("AppExitPlugin: Exit handler registered.");
    }
}

/// <summary>Resource tracking whether an application exit was requested.</summary>
public sealed class AppExit
{
    /// <summary>True once a quit event was observed, which closes the app.</summary>
    public bool Requested;
}