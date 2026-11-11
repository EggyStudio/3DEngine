using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using Vortice.Vulkan;

namespace Engine;

/// <remarks>
/// The driver's compiled pipelines are read from a file in the user's cache folder when the device
/// is made and written back when it is let go, so a run compiles no pipeline a run before it
/// compiled, since the driver's own cache, shared by every program, can serve none of a program's
/// runs, and a frame that first draws a pass, as a reflection plane's mirrored one, waits on the
/// driver compiling its pipelines. Kept in the program's folder, the file went with every build
/// that folder was deleted before, so it is kept in the user's cache folder under the program's
/// name, and one the program's folder holds is read into it.
/// </remarks>
internal sealed unsafe partial class GraphicsDevice
{
    private VkPipelineCache _pipelineCache;

    // Shared by each pipeline made through the driver's cache on any thread, which Vulkan lets a
    // cache made without the externally synchronized flag take at once, and held alone over its
    // data read for the write and its release, so the write never runs beside a compile a worker
    // makes ahead, since a driver that fails that freezes the frame that writes. Held alone by
    // every compile, a worker's waited behind one the frame's thread made, as a mirror's behind
    // a shader's that took the driver over a second.
    private readonly ReaderWriterLockSlim _pipelineCacheUse = new();


    /// <summary>
    /// Called while a pipeline is made through the driver's cache, the cache shared by the
    /// compiles, which a test sets to hold a compile in flight.
    /// </summary>
    internal Action? WhileMakingPipeline { get; set; }

    /// <summary>The most bytes of compiled pipelines a file is read with, past which the cache starts empty.</summary>
    internal const int PipelineCacheBound = 256 * 1024 * 1024;

    // The bytes the cache was read with or last written, written again only where it holds more or
    // less, a run that made no pipeline the file lacked leaving it as it was.
    private nuint _pipelineCacheRead;

    /// <summary>
    /// Where the driver's compiled pipelines are kept between runs, a file named for the program in
    /// <c>pipelines</c> under this version of the engine in the user's cache folder.
    /// </summary>
    internal static string PipelineCachePath => Path.Combine(UserCache.Versioned("pipelines"), $"{Assembly.GetEntryAssembly()?.GetName().Name ?? "program"}.bin");

    /// <summary>Where the program's own folder holds compiled pipelines, read into the cache with those of <see cref="PipelineCachePath"/>.</summary>
    internal static string ProgramPipelineCachePath => Path.Combine(AppContext.BaseDirectory, "source", ".slang-cache", "pipelines.bin");

    // Makes the cache, from the file where its header names this GPU and its driver's cache, and
    // empty otherwise, since a driver need not check the data past the header it is given, and
    // merges into it the one the program's folder holds.
    private void CreatePipelineCache()
    {
        var data = ReadPipelineCache(PipelineCachePath);
        // A file past the bound starts the cache empty, since the driver's data cannot be pruned one
        // pipeline at a time and every version of a shader a run compiled stays in it, about 1.9 MB
        // of Sponza's pipelines a version of the model pass, so a program in development grows its
        // file without end.
        if (data.Length > PipelineCacheBound)
        {
            Logger.Info($"The driver's compiled pipelines at {PipelineCachePath} hold {data.Length / 1048576} MB, past {PipelineCacheBound / 1048576} MB, so the cache starts empty and this run's are written in their place.");
            data = [];
        }
        var cache = MakePipelineCache(data);
        _pipelineCache = cache.Cache;
        _pipelineCacheRead = cache.Read;
        if (ReadPipelineCache(ProgramPipelineCachePath) is { Length: > 0 } own)
        {
            var (source, read) = MakePipelineCache(own);
            if (read > 0) _deviceApi.vkMergePipelineCaches(_pipelineCache, 1, &source);
            _deviceApi.vkDestroyPipelineCache(source, null);
        }
        Logger.Info($"The driver's compiled pipelines read from {PipelineCachePath}, {data.Length / 1024} KB.");
    }

    // A cache made from data, or empty where the driver takes none of it, and the bytes it took.
    private (VkPipelineCache Cache, nuint Read) MakePipelineCache(byte[] data)
    {
        fixed (byte* bytes = data)
        {
            var info = new VkPipelineCacheCreateInfo { initialDataSize = (nuint)data.Length, pInitialData = bytes };
            VkPipelineCache cache;
            if (_deviceApi.vkCreatePipelineCache(&info, null, &cache) != VkResult.Success)
            {
                info.initialDataSize = 0;
                info.pInitialData = null;
                _deviceApi.vkCreatePipelineCache(&info, null, &cache).CheckResult();
            }
            return (cache, info.initialDataSize);
        }
    }

    private byte[] ReadPipelineCache(string path)
    {
        byte[] data;
        try
        {
            if (!File.Exists(path)) return [];
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"The pipeline cache at {path} could not be read, so its pipelines are compiled: {ex.Message}");
            return [];
        }
        // The header Vulkan gives a cache's data, its size, its version, the vendor's and the
        // device's ids and the driver's cache id, which a cache from another GPU or driver fails.
        _instanceApi.vkGetPhysicalDeviceProperties(_physicalDevice, out var properties);
        if (data.Length < 32 || BinaryPrimitives.ReadUInt32LittleEndian(data) < 32 || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)) != 1
            || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8)) != properties.vendorID
            || BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(12)) != properties.deviceID
            || !new ReadOnlySpan<byte>(properties.pipelineCacheUUID, 16).SequenceEqual(data.AsSpan(16, 16)))
            return [];
        return data;
    }

    // Whether the cache's file was cleared while the program runs (cache clear), so the pipelines
    // read from it are not written back when the device is let go.
    private bool _pipelineCacheDropped;

    // Whether the device said its driver's cache holds nothing to write.
    private bool _saidNothingToWrite;

    /// <summary>
    /// Whether the driver's cache holds data past the header Vulkan gives it, which a driver that
    /// keeps no pipelines in it, as lavapipe with Mesa's own shader cache off, does not.
    /// </summary>
    internal bool PipelineCacheHolds
    {
        get
        {
            _pipelineCacheUse.EnterWriteLock();
            try
            {
                return PipelineCacheSize() > PipelineCacheHeader;
            }
            finally
            {
                _pipelineCacheUse.ExitWriteLock();
            }
        }
    }

    // The bytes of the header Vulkan gives a cache's data, past which it holds pipelines.
    private const int PipelineCacheHeader = 32;

    // The bytes the driver says the cache's data takes, or none where it cannot say, the cache held.
    private nuint PipelineCacheSize()
    {
        nuint size;
        return !_pipelineCache.IsNull && _deviceApi.vkGetPipelineCacheData(_pipelineCache, &size, null) == VkResult.Success ? size : 0;
    }

    /// <summary>Has the device write none of its pipelines back to <see cref="PipelineCachePath"/> this run, its file cleared while it runs.</summary>
    internal void DropPipelineCache() => _pipelineCacheDropped = true;

    // Writes the cache back where it changed since it was read, after a write on a worker still
    // going, and lets it go.
    private void DestroyPipelineCache()
    {
        _pipelineCacheWriting?.GetAwaiter().GetResult();
        SavePipelineCache();
        _pipelineCacheUse.EnterWriteLock();
        try
        {
            if (_pipelineCache.IsNull) return;
            _deviceApi.vkDestroyPipelineCache(_pipelineCache, null);
            _pipelineCache = default;
        }
        finally
        {
            _pipelineCacheUse.ExitWriteLock();
        }
    }

    // The write a worker makes for the frames, each after the one before it.
    private Task? _pipelineCacheWriting;

    /// <summary>
    /// Writes the cache as <see cref="SavePipelineCache"/> does on a worker, after any write asked
    /// before it, so the frame that asks it goes on while the data is read, which waits for every
    /// compile in flight, and while the file is written.
    /// </summary>
    internal void SavePipelineCacheAside() =>
        _pipelineCacheWriting = (_pipelineCacheWriting ?? Task.CompletedTask).ContinueWith(_ => SavePipelineCache(), TaskScheduler.Default);

    /// <summary>
    /// Writes the cache to <see cref="PipelineCachePath"/> where it changed since it was read or
    /// last written, through a file of its own moved over the old one, so a program started beside
    /// it never reads half of it, and none where its file was cleared while the program runs.
    /// </summary>
    internal void SavePipelineCache()
    {
        byte[]? data;
        // The data read with the cache held, after any pipeline a worker is making through it, and
        // the line before and after it, so a log that stops between them names the read.
        _pipelineCacheUse.EnterWriteLock();
        try
        {
            if (_pipelineCache.IsNull || _pipelineCacheDropped) return;
            Logger.Info("The driver's compiled pipelines are read to be written...");
            var reading = Stopwatch.GetTimestamp();
            data = PipelineCacheData();
            Logger.Info($"The driver's compiled pipelines read, {data?.Length ?? 0} bytes to write, in {Stopwatch.GetElapsedTime(reading).TotalMilliseconds:0} ms.");
        }
        finally
        {
            _pipelineCacheUse.ExitWriteLock();
        }
        if (data is null)
        {
            // Said once, since a driver that keeps no pipelines in the cache, as lavapipe with Mesa's
            // own shader cache off, gives the header alone or less every time it is asked.
            if (!PipelineCacheHolds && !_saidNothingToWrite)
            {
                _saidNothingToWrite = true;
                Logger.Info($"The driver's pipeline cache holds nothing past its header, so no pipelines are written to {PipelineCachePath}.");
            }
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PipelineCachePath)!);
            var written = $"{PipelineCachePath}.{Environment.ProcessId}";
            File.WriteAllBytes(written, data);
            File.Move(written, PipelineCachePath, overwrite: true);
            _pipelineCacheRead = (nuint)data.Length;
            Logger.Info($"The driver's compiled pipelines written to {PipelineCachePath}, {data.Length / 1024} KB.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"The pipeline cache could not be written to {PipelineCachePath}, so the next run compiles its pipelines again: {ex.Message}");
        }
    }

    // The cache's data where it holds pipelines by the rule PipelineCacheHolds answers by, more than
    // its header, and changed since it was read, or none, the cache held. The data is asked for
    // again by the size given, and taken only where what was filled still holds more than the
    // header and the whole header it names, as lavapipe asked for 32 bytes and filled 20 of them.
    private byte[]? PipelineCacheData()
    {
        var size = PipelineCacheSize();
        if (size <= PipelineCacheHeader || size == _pipelineCacheRead)
            return null;
        var data = new byte[(int)size];
        fixed (byte* bytes = data)
            if (_deviceApi.vkGetPipelineCacheData(_pipelineCache, &size, bytes) != VkResult.Success)
                return null;
        return size > PipelineCacheHeader && BinaryPrimitives.ReadUInt32LittleEndian(data) <= size ? data[..(int)size] : null;
    }

    // How many pipelines the device made and the time the driver took, which the renderer logs for
    // the first frame and any frame that waits long on them, and the latest made, each by the
    // shaders it is made from and the time it took, which the renderer takes each frame.
    private long _pipelinesMade, _pipelineTicks;
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pipelinesNamed = new();

    // As many named pipelines as are kept for a frame that does not take them, as a test's.
    private const int NamedKept = 64;

    /// <summary>How many pipelines the device made and how long the driver took to make them, from the start.</summary>
    internal (long Made, TimeSpan Making) PipelinesMade =>
        (Interlocked.Read(ref _pipelinesMade), Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _pipelineTicks)));

    /// <summary>
    /// The pipelines the device made since this was last asked, each as the shaders it is made from
    /// and the milliseconds the driver took, the latest 64 at most.
    /// </summary>
    internal string[] TakePipelinesNamed()
    {
        var named = new List<string>();
        while (_pipelinesNamed.TryDequeue(out var one)) named.Add(one);
        return [.. named];
    }

    // A compute pipeline the driver makes through the cache from the SPIR-V given, counted.
    internal VkPipeline CreateComputePipeline(VkComputePipelineCreateInfo* info, ReadOnlySpan<byte> spirv)
    {
        var making = Stopwatch.GetTimestamp();
        VkPipeline pipeline;
        _pipelineCacheUse.EnterReadLock();
        try
        {
            WhileMakingPipeline?.Invoke();
            _deviceApi.vkCreateComputePipelines(_pipelineCache, 1, info, null, &pipeline).CheckResult();
        }
        finally
        {
            _pipelineCacheUse.ExitReadLock();
        }
        Made(making, ShaderNames.Of(spirv));
        return pipeline;
    }

    // Counts a pipeline the driver made since the timestamp given, from the shaders named.
    private void Made(long since, string shaders)
    {
        var ticks = Stopwatch.GetTimestamp() - since;
        Interlocked.Increment(ref _pipelinesMade);
        Interlocked.Add(ref _pipelineTicks, ticks);
        _pipelinesNamed.Enqueue($"{shaders} in {Stopwatch.GetElapsedTime(0, ticks).TotalMilliseconds:0} ms");
        while (_pipelinesNamed.Count > NamedKept && _pipelinesNamed.TryDequeue(out _)) { }
    }
}
