namespace Engine;

/// <summary>
/// Handles application exit: listens for window quit events and requests closure.
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

        // When the window signals quit, raise the Requested flag.
        var window = app.World.Resource<AppWindow>();
        window.QuitEvent += () =>
        {
            Logger.Info("Quit event received - flagging application exit.");
            app.World.Resource<AppExit>().Requested = true;
        };

        // Early frame: if an exit was requested previously, ask window to close (will break main loop).
        app.AddSystem(Stage.First, new SystemDescriptor(world =>
            {
                if (world.Resource<AppExit>().Requested)
                {
                    Logger.Info("Exit requested - closing window to break main loop.");
                    world.Resource<AppWindow>().RequestClose();
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
    /// <summary>True if a quit event was observed and the app should close.</summary>
    public bool Requested;
}