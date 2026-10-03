namespace Engine.Files.Compiler;

public abstract partial class RuntimeAssemblyCompiler<TResult>
{
    /// <summary>Forwards <see cref="FileSystemWatcher.Changed"/>/<see cref="FileSystemWatcher.Created"/>/
    /// <see cref="FileSystemWatcher.Deleted"/> to the debounce timer.</summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e) => ScheduleRecompile();

    /// <summary>Forwards <see cref="FileSystemWatcher.Renamed"/> to the debounce timer.</summary>
    private void OnFileRenamed(object sender, RenamedEventArgs e) => ScheduleRecompile();

    /// <summary>Schedules a recompile after a 300ms debounce window. Repeated calls reset the timer.</summary>
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