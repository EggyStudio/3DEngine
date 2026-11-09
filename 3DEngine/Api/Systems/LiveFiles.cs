using System.Collections.Concurrent;
using System.Diagnostics;

namespace Engine;

/// <summary>
/// The files the flat API loaded by name, each with what it was loaded into, watched where the
/// program is live (<see cref="Config.Live"/>), so a file saved while the program runs is loaded
/// again in place at the start of a frame once it has been quiet for <see cref="Quiet"/>.
/// </summary>
/// <remarks>
/// <para>
/// A program run from its own project's build folder watches the project's copy of each file
/// where the project has one at the same place, since that is the copy a person edits and the build
/// copies only at the next build, and the file it read otherwise. A folder's watch is shared by its
/// files and let go with the last of them, or when the program is no longer live.
/// </para>
/// <para>
/// The watch's events arrive on threads of their own and only mark a file changed, and the reload
/// runs on the main thread at the start of the frame, where the stores it writes are written. Each
/// reload is said in the log under <c>Engine.Live</c>, and e3d's <c>live</c> lists the files with
/// the last reload of each.
/// </para>
/// </remarks>
internal sealed class LiveFiles : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Live");

    /// <summary>
    /// How long a file stays unchanged before it is read again, since an editor saves a file in
    /// more than one write, or writes another and moves it over the first.
    /// </summary>
    internal static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(150);

    private static readonly StringComparer Paths = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>One file the flat API loaded by name and what it was loaded into.</summary>
    /// <param name="Kind">What the file is, as <c>shader</c>, <c>texture</c>, <c>model</c> or <c>scene</c>.</param>
    /// <param name="Id">The id it was loaded into, the program's handle to it.</param>
    /// <param name="Path">The file watched, the project's copy where there is one.</param>
    /// <param name="Reload">Loads the file at the path given again into the id, answering null where it did or why it did not.</param>
    internal sealed record Entry(string Kind, int Id, string Path, Func<string, string?> Reload)
    {
        /// <summary>How many times the file was loaded again.</summary>
        public int Reloads { get; set; }

        /// <summary>The frame of the last reload, or null where there was none.</summary>
        public ulong? LastFrame { get; set; }

        /// <summary>Why the last reload failed, or null where it did not or there was none.</summary>
        public string? LastError { get; set; }
    }

    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<string, FileSystemWatcher> _watches = new(Paths);
    private readonly ConcurrentDictionary<string, long> _changed = new(Paths);
    private readonly string? _project;
    private readonly string _program;
    private bool _disposed;

    /// <summary>
    /// Starts with the program live or not, mapping a file under <paramref name="program"/>, the
    /// program's own folder where none is given, to its copy in <paramref name="project"/> where
    /// there is one.
    /// </summary>
    internal LiveFiles(bool on, string? project, string? program = null)
    {
        On = on;
        _project = project;
        _program = Path.GetFullPath(program ?? AppContext.BaseDirectory);
    }

    /// <summary>Whether the files are watched and loaded again.</summary>
    internal bool On { get; private set; }

    /// <summary>The files, in the order they were loaded.</summary>
    internal IReadOnlyList<Entry> Entries
    {
        get { lock (_gate) return [.. _entries]; }
    }

    /// <summary>
    /// Remembers a file loaded by name into <paramref name="id"/>, from <paramref name="loaded"/>,
    /// watching its project's copy where there is one, and gives the file watched. One id may watch
    /// more than one file, each loaded again by its own reload.
    /// </summary>
    internal string Watch(string kind, int id, string loaded, Func<string, string?> reload)
    {
        var path = Watched(loaded);
        lock (_gate)
        {
            _entries.RemoveAll(e => e.Kind == kind && e.Id == id && Paths.Equals(e.Path, path));
            _entries.Add(new Entry(kind, id, path, reload));
            if (On) WatchFolderOf(path);
        }
        return path;
    }

    /// <summary>Forgets every file loaded into <paramref name="id"/>, as an unload does.</summary>
    internal void Forget(string kind, int id)
    {
        lock (_gate)
        {
            _entries.RemoveAll(e => e.Kind == kind && e.Id == id);
            LetGoOfUnwatchedFolders();
        }
    }

    /// <summary>Turns the watch on or off, the files remembered either way.</summary>
    internal void Turn(bool on)
    {
        lock (_gate)
        {
            if (On == on || _disposed) return;
            On = on;
            if (on)
                foreach (var entry in _entries) WatchFolderOf(entry.Path);
            else
            {
                foreach (var watch in _watches.Values) watch.Dispose();
                _watches.Clear();
                _changed.Clear();
            }
        }
        Logger.Info(on ? "The program is live, its files watched." : "The program is no longer live.");
    }

    /// <summary>Loads again each file that changed and has been quiet since, on the main thread at the frame's start.</summary>
    internal void Run(ulong frame)
    {
        if (_changed.IsEmpty) return;
        var now = Stopwatch.GetTimestamp();
        foreach (var (path, at) in _changed)
        {
            if (Stopwatch.GetElapsedTime(at, now) < Quiet) continue;
            if (!_changed.TryRemove(new KeyValuePair<string, long>(path, at))) continue;
            Entry[] loaded;
            lock (_gate) loaded = [.. _entries.Where(e => Paths.Equals(e.Path, path))];
            foreach (var entry in loaded)
            {
                string? error;
                try
                {
                    error = entry.Reload(path);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    error = e.Message;
                }
                entry.Reloads++;
                entry.LastFrame = frame;
                entry.LastError = error;
                if (error is null) Logger.Info($"Loaded {path} again into {entry.Kind} {entry.Id}.");
                else Logger.Warn($"{path} changed and was not loaded into {entry.Kind} {entry.Id} again, which keeps what it held. {error}");
            }
        }
    }

    /// <summary>The file to watch for one loaded from <paramref name="loaded"/>, the project's copy where the program runs from its build folder and the project has one.</summary>
    internal string Watched(string loaded)
    {
        var full = Path.GetFullPath(loaded);
        if (_project is null || !full.StartsWith(_program, StringComparison.Ordinal)) return full;
        var theirs = Path.Combine(_project, Path.GetRelativePath(_program, full));
        return File.Exists(theirs) ? theirs : full;
    }

    // A watch of the folder a file lies in, shared by every file of the folder.
    private void WatchFolderOf(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (folder is null || _watches.ContainsKey(folder) || !Directory.Exists(folder)) return;
        var watch = new FileSystemWatcher(folder)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = false,
        };
        watch.Changed += (_, e) => Mark(e.FullPath);
        watch.Created += (_, e) => Mark(e.FullPath);
        watch.Renamed += (_, e) => Mark(e.FullPath);
        watch.EnableRaisingEvents = true;
        _watches[folder] = watch;
    }

    // Marks a file of a watched folder changed now, where the program loaded it.
    private void Mark(string path)
    {
        lock (_gate)
            if (!_entries.Any(e => Paths.Equals(e.Path, path))) return;
        _changed[path] = Stopwatch.GetTimestamp();
    }

    private void LetGoOfUnwatchedFolders()
    {
        foreach (var folder in _watches.Keys.ToArray())
        {
            if (_entries.Any(e => Paths.Equals(Path.GetDirectoryName(e.Path), folder))) continue;
            _watches[folder].Dispose();
            _watches.Remove(folder);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var watch in _watches.Values) watch.Dispose();
            _watches.Clear();
            _entries.Clear();
        }
        _changed.Clear();
    }
}
