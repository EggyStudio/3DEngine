namespace Engine;

/// <summary>Declares whether a system may run on worker threads or must run on the main thread.</summary>
/// <remarks>
/// Systems that interact with platform APIs (e.g., SDL window, GPU context) should use
/// <see cref="MainThread"/> to prevent cross-thread access violations.
/// </remarks>
public enum ThreadAffinity
{
    /// <summary>The system may run on any thread, including worker threads in parallel batches.</summary>
    Any,
    /// <summary>The system must execute on the main thread. It forms its own sequential batch.</summary>
    MainThread,
}
