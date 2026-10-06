namespace Engine.Files.Compiler;

internal abstract partial class RuntimeAssemblyCompiler<TResult>
{
    /// <summary>Performs the initial compilation and starts file watchers across configured directories.</summary>
    /// <returns>The result of the initial compilation.</returns>
    public TResult Start()
    {
        var result = CompileAndLoad();

        // Each directory is watched once a process, shared with the other compilers watching it.
        foreach (var dir in _scriptDirectories)
            foreach (var ext in WatchedExtensions)
                _watchers.Add(DirectoryWatches.Start(dir, ext, OnFileChanged, OnFileRenamed));

        return result;
    }

    /// <summary>Manually triggers an immediate recompile (synchronous, bypasses debounce).</summary>
    public TResult Recompile() => CompileAndLoad();

    /// <summary>
    /// Domain hook invoked from <see cref="Dispose"/> after debounce/watchers/load context are torn down.
    /// Default does nothing.
    /// </summary>
    protected virtual void OnDispose()
    {
    }

    /// <summary>Disposes the debounce timer, every watcher, and the current load context.</summary>
    public void Dispose()
    {
        _debounceTimer?.Dispose();
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        UnloadCurrent();
        OnDispose();
    }
}