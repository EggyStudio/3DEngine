namespace Engine;

/// <summary>
/// Abstraction for driving the main loop.
/// Implemented by window backends (e.g., SDL) or editors that provide their own frame cadence.
/// </summary>
/// <remarks>
/// The engine calls <see cref="Run"/> once after all plugins have been built and the
/// <see cref="Stage.Startup"/> stage has executed. The implementation is responsible for
/// calling the <c>frameStep</c> delegate once per frame (e.g., inside an SDL event loop)
/// until the application should exit. After the loop ends, <see cref="Shutdown"/> is called
/// to tear down any platform resources the driver owns.
/// </remarks>
/// <seealso cref="App"/>
/// <seealso cref="Stage"/>
public interface IMainLoopDriver
{
    /// <summary>
    /// Runs the application loop, invoking <paramref name="frameStep"/> once per frame
    /// until the loop ends (e.g., window close, editor stop).
    /// </summary>
    /// <param name="frameStep">
    /// Delegate that executes one full frame (all per-frame stages from
    /// <see cref="Stage.First"/> through <see cref="Stage.Last"/>).
    /// </param>
    void Run(Action frameStep);

    /// <summary>
    /// Called after the <see cref="Stage.Cleanup"/> stage has finished to tear down platform
    /// resources (e.g., destroy the SDL window). Override when the driver owns resources that
    /// other Cleanup systems depend on.
    /// </summary>
    void Shutdown() { }
}
