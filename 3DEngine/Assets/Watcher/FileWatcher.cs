using System.Collections.Concurrent;

namespace Engine;

/// <summary>
/// Describes a single file system change detected by <see cref="FileWatcher"/>.
/// </summary>
/// <param name="FilePath">Absolute path of the changed file.</param>
/// <param name="RelativePath">Path relative to the watched directory.</param>
/// <param name="ChangeType">The kind of change (Created, Modified, Deleted, Renamed).</param>
/// <seealso cref="FileWatcher"/>
public readonly record struct FileChangedEvent(
    string FilePath,
    string RelativePath,
    WatcherChangeTypes ChangeType);

/// <summary>
/// General-purpose debounced file system watcher. Wraps <see cref="FileSystemWatcher"/> with
/// configurable extension filters, recursive watching, and coalescence of rapid changes.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <c>RuntimeShellCompiler.FileWatcher</c> as a reusable utility. Multiple parts of the
/// engine and editor can share this instead of duplicating <see cref="FileSystemWatcher"/> boilerplate.
/// </para>
/// <para>
/// Changes are coalesced over a configurable debounce window (default 300ms). When the debounce
/// timer fires, all accumulated changes are delivered as a batch through the <see cref="Changed"/> event.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var watcher = new FileWatcher("/path/to/watch")
///     .WithExtensions("*.cs", "*.razor")
///     .WithDebounce(TimeSpan.FromMilliseconds(500));
///
/// watcher.Changed += events =>
/// {
///     foreach (var e in events)
///         Console.WriteLine($"{e.ChangeType}: {e.RelativePath}");
/// };
///
/// watcher.Start();
/// </code>
/// </example>
/// <seealso cref="FileAssetWatcher"/>
/// <seealso cref="FileChangedEvent"/>
public sealed class FileWatcher : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.FileWatcher");

    private readonly string _directory;
    private readonly List<string> _extensions = [];
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, WatcherChangeTypes> _pendingChanges = new();
    private TimeSpan _debounce = TimeSpan.FromMilliseconds(300);
    private bool _recursive = true;
    private Timer? _debounceTimer;
    private bool _disposed;

    /// <summary>
    /// Fired when the debounce window expires and accumulated changes are delivered.
    /// The array contains all coalesced <see cref="FileChangedEvent"/>s since the last delivery.
    /// </summary>
    public event Action<FileChangedEvent[]>? Changed;

    /// <summary>The directory being watched.</summary>
    public string Directory => _directory;

    /// <summary>Whether the watcher is currently active.</summary>
    public bool IsWatching => _watchers.Count > 0 && _watchers[0].EnableRaisingEvents;

    /// <summary>
    /// Creates a new file watcher for the specified directory.
    /// Call <see cref="Start"/> to begin watching.
    /// </summary>
    /// <param name="directory">Absolute path to the directory to watch.</param>
    /// <exception cref="ArgumentException">The directory path is null or empty.</exception>
    public FileWatcher(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
    }

    // -- Configuration (fluent) --

    /// <summary>
    /// Sets the file extension filters (e.g. <c>"*.cs"</c>, <c>"*.png"</c>).
    /// If none are set, all files are watched (<c>"*.*"</c>).
    /// </summary>
    /// <param name="patterns">Glob patterns for extensions to watch.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FileWatcher WithExtensions(params string[] patterns)
    {
        _extensions.Clear();
        _extensions.AddRange(patterns);
        return this;
    }

    /// <summary>
    /// Sets the debounce window. Changes within this period are coalesced into a single batch.
    /// Defaults to 300ms.
    /// </summary>
    /// <param name="debounce">The debounce duration.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FileWatcher WithDebounce(TimeSpan debounce)
    {
        _debounce = debounce;
        return this;
    }

    /// <summary>
    /// Sets whether subdirectories are watched recursively. Defaults to <c>true</c>.
    /// </summary>
    /// <param name="recursive">Whether to include subdirectories.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public FileWatcher WithRecursive(bool recursive)
    {
        _recursive = recursive;
        return this;
    }

    // -- Lifecycle --

    /// <summary>Starts watching for file changes. Idempotent.</summary>
    /// <exception cref="DirectoryNotFoundException">The target directory does not exist.</exception>
    public void Start()
    {
        if (_watchers.Count > 0) return;
        if (!System.IO.Directory.Exists(_directory))
            throw new DirectoryNotFoundException($"Watch directory not found: {_directory}");

        var filters = _extensions.Count > 0 ? _extensions : ["*.*"];

        foreach (string filter in filters)
        {
            var fsw = new FileSystemWatcher(_directory, filter)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
                IncludeSubdirectories = _recursive,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536,
            };
            fsw.Changed += OnFswEvent;
            fsw.Created += OnFswEvent;
            fsw.Deleted += OnFswEvent;
            fsw.Renamed += OnFswRenamed;
            fsw.Error += OnFswError;
            _watchers.Add(fsw);
        }

        Logger.Info($"FileWatcher started: {_directory} (filters: [{string.Join(", ", filters)}], recursive: {_recursive}, debounce: {_debounce.TotalMilliseconds}ms)");
    }

    /// <summary>Stops watching. Can be restarted with <see cref="Start"/>.</summary>
    public void Stop()
    {
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _pendingChanges.Clear();
        Logger.Debug($"FileWatcher stopped: {_directory}");
    }

    // -- Event handling --

    private void OnFswEvent(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath is null) return;
        _pendingChanges[e.FullPath] = e.ChangeType;
        ScheduleFlush();
    }

    private void OnFswRenamed(object sender, RenamedEventArgs e)
    {
        if (e.OldFullPath is not null)
            _pendingChanges[e.OldFullPath] = WatcherChangeTypes.Deleted;
        if (e.FullPath is not null)
            _pendingChanges[e.FullPath] = WatcherChangeTypes.Created;
        ScheduleFlush();
    }

    private void OnFswError(object sender, ErrorEventArgs e)
    {
        Logger.Warn($"FileSystemWatcher error in {_directory}: {e.GetException().Message}");
    }

    private void ScheduleFlush()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ => Flush(), null, _debounce, Timeout.InfiniteTimeSpan);
    }

    private void Flush()
    {
        if (_pendingChanges.IsEmpty) return;

        var events = new List<FileChangedEvent>();
        foreach (var kv in _pendingChanges)
        {
            string relative = Path.GetRelativePath(_directory, kv.Key).Replace('\\', '/');
            events.Add(new FileChangedEvent(kv.Key, relative, kv.Value));
        }
        _pendingChanges.Clear();

        Logger.Debug($"FileWatcher flush: {events.Count} change(s) in {_directory}");

        try
        {
            Changed?.Invoke(events.ToArray());
        }
        catch (Exception ex)
        {
            Logger.Error($"FileWatcher callback error: {ex.Message}", ex);
        }
    }

    // -- Disposal --

    /// <summary>Stops watching and releases all resources.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
