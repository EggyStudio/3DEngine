namespace Engine;

public sealed partial class App
{
    private bool _started;
    private bool _shutDown;

    /// <summary>
    /// Runs the application: <see cref="Startup"/>, then <see cref="Frame"/> for as long as the
    /// <see cref="IMainLoopDriver"/> keeps looping, then <see cref="Shutdown"/>.
    /// </summary>
    /// <remarks>
    /// The main loop is driven by the <see cref="IMainLoopDriver"/> resource, which a window plugin
    /// such as <c>AppWindowPlugin</c> inserts. A program that drives its own loop calls the same
    /// steps itself, which is how <see cref="Engine3D"/> runs a frame between
    /// <c>BeginDrawing</c> and <c>EndDrawing</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="IMainLoopDriver"/> resource has been inserted into the <see cref="World"/>.
    /// </exception>
    /// <seealso cref="IMainLoopDriver"/>
    /// <seealso cref="Stage"/>
    public void Run()
    {
        var loop = World.Resource<IMainLoopDriver>();
        Logger.Info($"Main loop driver: {loop.GetType().Name}");

        Startup();
        loop.Run(Frame);
        Logger.Info($"Main loop exited after {_frameCount} frames.");
        Shutdown();
    }

    /// <summary>Runs <see cref="Stage.Startup"/>, once. Later calls do nothing.</summary>
    public void Startup()
    {
        if (_started) return;
        _started = true;

        Logger.Info("Running the Startup stage, the systems that initialize once...");
        Schedule.RunStage(Stage.Startup, World);
        Logger.Info("Startup stage complete.");
    }

    /// <summary>Runs one whole frame, <see cref="Stage.First"/> through <see cref="Stage.Last"/>.</summary>
    internal void Frame()
    {
        BeginFrame();
        EndFrame();
    }

    /// <summary>
    /// Runs the first half of a frame, <see cref="Stage.First"/> through <see cref="Stage.Update"/>,
    /// running <see cref="Startup"/> first if it has not run.
    /// </summary>
    /// <remarks>
    /// <see cref="Stage.FixedUpdate"/> runs between <see cref="Stage.PreUpdate"/> and
    /// <see cref="Stage.Update"/> once per step <see cref="FixedTime.TryStep"/> grants, and not at
    /// all when the world has no <see cref="FixedTime"/>. State transitions are applied before it,
    /// right after <see cref="Stage.PreUpdate"/>, as Bevy applies them.
    /// </remarks>
    /// <remarks>
    /// Whatever the caller does between this and <see cref="EndFrame"/> belongs to the frame, so
    /// draw calls made there are rendered by it and ImGui windows begun there are drawn with it.
    /// </remarks>
    internal void BeginFrame()
    {
        Startup();

        // A script compiled again since the last frame, swapped in before any stage runs a system
        // of either generation.
        if (World.TryGetResource<RuntimeBehaviorCompiler>(out var scripts))
            scripts.ApplyPending();

        _frameCount++;
        if (_frameCount <= 3 || _frameCount % 1000 == 0)
            Logger.FrameTrace($"Frame #{_frameCount} begin");

        Schedule.RunStage(Stage.First, World);
        Schedule.RunStage(Stage.PreUpdate, World);

        if (World.TryGetResource<StateTransitions>(out var transitions))
            transitions.Apply(World);

        if (World.TryGetResource<FixedTime>(out var fixedTime))
            while (fixedTime.TryStep())
                Schedule.RunStage(Stage.FixedUpdate, World);

        Schedule.RunStage(Stage.Update, World);
    }

    /// <summary>
    /// Runs the second half of a frame, <see cref="Stage.PostUpdate"/> through <see cref="Stage.Last"/>,
    /// which applies deferred commands, renders and presents.
    /// </summary>
    internal void EndFrame()
    {
        foreach (var stage in StageOrder.EndFrameStages())
            Schedule.RunStage(stage, World);
    }

    /// <summary>
    /// Runs <see cref="Stage.Cleanup"/>, shuts the main loop driver down and disposes every
    /// disposable resource, once. Later calls do nothing.
    /// </summary>
    /// <remarks>
    /// The driver is shut down after <see cref="Stage.Cleanup"/>, so GPU resources that depend on
    /// the window's surface are released before the window goes away.
    /// </remarks>
    internal void Shutdown()
    {
        if (_shutDown) return;
        _shutDown = true;

        Logger.Info("Running the Cleanup stage, teardown and resource disposal...");
        Schedule.RunStage(Stage.Cleanup, World);
        Schedule.ReportThrownTotals();

        if (World.TryGetResource<IMainLoopDriver>(out var loop))
        {
            Logger.Info("Shutting down main loop driver (platform teardown)...");
            loop.Shutdown();
        }

        World.Dispose();
        Logger.Info("Cleanup stage complete. Application shutdown finished.");
    }
}
