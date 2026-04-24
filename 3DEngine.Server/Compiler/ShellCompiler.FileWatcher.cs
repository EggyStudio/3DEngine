namespace Editor.Shell;

public sealed partial class ShellCompiler
{
    /// <summary>File extensions monitored by the script compiler.</summary>
    private static readonly string[] WatchedExtensions = ["*.cs", "*.razor", "*.css"];

    /// <summary>Handles <see cref="FileSystemWatcher.Changed"/>, <see cref="FileSystemWatcher.Created"/>,
    /// and <see cref="FileSystemWatcher.Deleted"/> events by scheduling a debounced recompile.</summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e) => ScheduleRecompile();

    /// <summary>Handles <see cref="FileSystemWatcher.Renamed"/> events by scheduling a debounced recompile.</summary>
    private void OnFileRenamed(object sender, RenamedEventArgs e) => ScheduleRecompile();

    /// <summary>
    /// Schedules a recompilation after a 300ms debounce period. Repeated calls within
    /// the debounce window reset the timer, coalescing rapid file changes into a single compilation.
    /// </summary>
    private void ScheduleRecompile()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            var result = CompileAndLoad();
            CompilationCompleted?.Invoke(result);
        }, null, 300, Timeout.Infinite);
    }
}
