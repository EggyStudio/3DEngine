namespace Engine;

public sealed partial class App
{
    /// <summary>
    /// Runs the application: executes <see cref="Stage.Startup"/> once, enters the per-frame main loop,
    /// then runs <see cref="Stage.Cleanup"/> on exit.
    /// </summary>
    /// <remarks>
    /// <para>The main loop is driven by the <see cref="IMainLoopDriver"/> resource, which must be
    /// present in the <see cref="World"/> (typically inserted by a window plugin such as
    /// <c>AppWindowPlugin</c>).</para>
    /// <para>Each frame executes all stages from <see cref="Stage.First"/> through <see cref="Stage.Last"/>
    /// in fixed order. After the loop exits, <see cref="Stage.Cleanup"/> runs, the driver is shut down,
    /// and all <see cref="IDisposable"/> resources are disposed.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no <see cref="IMainLoopDriver"/> resource has been inserted into the <see cref="World"/>.
    /// </exception>
    /// <seealso cref="IMainLoopDriver"/>
    /// <seealso cref="Stage"/>
    public void Run()
    {
        Logger.Info("App.Run() - Resolving main loop driver...");
        var loop = World.Resource<IMainLoopDriver>();
        Logger.Info($"Main loop driver: {loop.GetType().Name}");

        Logger.Info("Running Startup stage - one-time initialization systems...");
        Schedule.RunStage(Stage.Startup, World);
        Logger.Info("Startup stage complete.");

        Logger.Info("Entering main loop - per-frame execution begins.");
        loop.Run(() =>
        {
            _frameCount++;
            if (_frameCount <= 3 || (_frameCount % 1000 == 0))
                Logger.FrameTrace($"Frame #{_frameCount} begin");

            foreach (var stage in StageOrder.FrameStages())
                Schedule.RunStage(stage, World);
        });

        Logger.Info($"Main loop exited after {_frameCount} frames.");
        Logger.Info("Running Cleanup stage - teardown and resource disposal...");
        Schedule.RunStage(Stage.Cleanup, World);

        // Tear down platform resources (e.g., SDL window) *after* Cleanup systems
        // have released GPU resources that depend on the window/surface.
        Logger.Info("Shutting down main loop driver (platform teardown)...");
        loop.Shutdown();

        // Dispose all IDisposable resources as a safety net.
        World.Dispose();
        Logger.Info("Cleanup stage complete. Application shutdown finished.");
    }
}
