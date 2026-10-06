namespace Engine.Files.Compiler;

/// <summary>
/// The file system watches the process holds for the compilers of scripts, one for each directory
/// and filter however many compilers watch it, each let go with the last compiler that watched it.
/// </summary>
/// <remarks>
/// Every app with behaviors compiles the scripts of its program's <c>source/behaviors</c>, so a
/// process that makes app after app, as the suite does, watched the same directory once an app. On
/// macOS a watcher is an FSEvents stream whose managed side lives until the system's thread lets it
/// go, after the app is collected, which a hundred apps showed as 75 KB an app (REVIEW.md, Verdict
/// 27). A change is told to every compiler watching, each of which compiles its own app's scripts.
/// </remarks>
internal static class DirectoryWatches
{
    private sealed record Watcher(FileSystemEventHandler Changed, RenamedEventHandler Renamed);

    // One system watcher and the compilers it tells, in the order they came.
    private sealed class Watch(FileSystemWatcher system)
    {
        public FileSystemWatcher System { get; } = system;
        public List<Watcher> Watchers { get; } = [];
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<(string Directory, string Filter), Watch> Watches = [];

    /// <summary>How many system watchers the process holds on <paramref name="directory"/>, one for each filter, and how many compilers they tell.</summary>
    public static (int Watchers, int Told) Of(string directory)
    {
        var full = Path.GetFullPath(directory);
        lock (Gate)
        {
            var on = Watches.Where(entry => entry.Key.Directory == full).Select(entry => entry.Value).ToArray();
            return (on.Length, on.Sum(watch => watch.Watchers.Count));
        }
    }

    /// <summary>
    /// Tells <paramref name="changed"/> of each file of <paramref name="filter"/> in
    /// <paramref name="directory"/> or below that is made, written or deleted, and
    /// <paramref name="renamed"/> of each renamed, until the lease returned is disposed.
    /// </summary>
    public static IDisposable Start(string directory, string filter, FileSystemEventHandler changed, RenamedEventHandler renamed)
    {
        var key = (Path.GetFullPath(directory), filter);
        var watcher = new Watcher(changed, renamed);
        lock (Gate)
        {
            if (!Watches.TryGetValue(key, out var watch))
            {
                var system = new FileSystemWatcher(key.Item1, filter)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = true,
                };
                watch = new Watch(system);
                var made = watch;
                system.Changed += (sender, e) => Tell(made, w => w.Changed(sender, e));
                system.Created += (sender, e) => Tell(made, w => w.Changed(sender, e));
                system.Deleted += (sender, e) => Tell(made, w => w.Changed(sender, e));
                system.Renamed += (sender, e) => Tell(made, w => w.Renamed(sender, e));
                system.EnableRaisingEvents = true;
                Watches[key] = watch;
            }
            watch.Watchers.Add(watcher);
        }
        return new Lease(key, watcher);
    }

    // Tells each compiler watching, from a copy, so one that stops watching as it is told does not
    // change the list being read.
    private static void Tell(Watch watch, Action<Watcher> tell)
    {
        Watcher[] watchers;
        lock (Gate) watchers = [.. watch.Watchers];
        foreach (var watcher in watchers) tell(watcher);
    }

    // A compiler's place among those watching a directory, which stops the system's watcher when
    // the last lets go.
    private sealed class Lease((string Directory, string Filter) key, Watcher watcher) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            FileSystemWatcher? stopped = null;
            lock (Gate)
            {
                if (_disposed) return;
                _disposed = true;
                if (!Watches.TryGetValue(key, out var watch)) return;
                watch.Watchers.Remove(watcher);
                if (watch.Watchers.Count > 0) return;
                Watches.Remove(key);
                stopped = watch.System;
            }
            stopped.EnableRaisingEvents = false;
            stopped.Dispose();
        }
    }
}
