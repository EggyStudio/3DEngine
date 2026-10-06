namespace Engine;

/// <summary>
/// The threads an app's own parts start, which <see cref="App"/> joins as it shuts down, so none of
/// a closed app is alive after <c>Shutdown</c> returns.
/// </summary>
/// <remarks>
/// A thread that outlives its app keeps the app's objects it reached, its context and its statics
/// until it ends, which a system that lets threads end late, as macOS may, shows as memory a
/// hundred closed apps still hold. A part stops its threads first, as the console's server closes
/// its listener and its connections, and the join then waits for them to finish.
/// </remarks>
internal sealed class AppThreads
{
    private readonly object _gate = new();
    private readonly List<Thread> _started = [];

    /// <summary>How long a shutdown waits for each thread before naming it in the log.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

    /// <summary>Starts <paramref name="work"/> on a background thread named <paramref name="name"/>, joined as the app shuts down.</summary>
    public Thread Start(string name, Action work)
    {
        var thread = new Thread(() => work()) { IsBackground = true, Name = name };
        lock (_gate)
        {
            // Threads already finished are forgotten, so a long session of short connections
            // keeps no list of them.
            _started.RemoveAll(t => !t.IsAlive);
            _started.Add(thread);
        }
        thread.Start();
        return thread;
    }

    /// <summary>The threads started and not yet seen finished.</summary>
    public IReadOnlyList<Thread> Started
    {
        get { lock (_gate) return [.. _started]; }
    }

    /// <summary>Waits for every thread started to finish, up to <see cref="Patience"/> each, and returns the names of those that did not.</summary>
    public IReadOnlyList<string> JoinAll()
    {
        Thread[] threads;
        lock (_gate) threads = [.. _started];
        var alive = threads.Where(thread => thread.IsAlive && !thread.Join(Patience)).Select(thread => thread.Name ?? "unnamed").ToArray();
        lock (_gate) _started.RemoveAll(t => !t.IsAlive);
        return alive;
    }
}
