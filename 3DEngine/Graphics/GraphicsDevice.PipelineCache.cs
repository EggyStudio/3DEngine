using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using Vortice.Vulkan;

namespace Engine;

/// <remarks>
/// The driver's compiled pipelines are read from a file in the user's cache folder when the device
/// is made and written back when it is let go, so a run compiles no pipeline a run before it
/// compiled. The voxel game's first frame with a reflection plane took 23.7 to 24.0 s of the wall
/// clock, the driver compiling the mirrored pass's pipelines, wherever the driver's own cache missed,
/// which on the development machine held 1.1 GB across six programs and served none of the game's
/// runs. Kept in the program's folder, the file went with every build that folder was deleted
/// before, so it is kept in the user's cache folder under the program's name, and one the
/// program's folder holds is read into it.
/// </remarks>
internal sealed unsafe partial class GraphicsDevice
{
    private VkPipelineCache _pipelineCache;

    // The bytes the cache was read with, written back only where it holds more or less, a run that
    // made no pipeline the file lacked leaving it as it was.
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

    // Writes the cache back where it changed since it was read, through a file of its own moved over
    // the old one, so a program started beside it never reads half of it.
    private void DestroyPipelineCache()
    {
        if (_pipelineCache.IsNull) return;
        if (PipelineCacheData() is { } data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PipelineCachePath)!);
                var written = $"{PipelineCachePath}.{Environment.ProcessId}";
                File.WriteAllBytes(written, data);
                File.Move(written, PipelineCachePath, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn($"The pipeline cache could not be written to {PipelineCachePath}, so the next run compiles its pipelines again: {ex.Message}");
            }
        }
        _deviceApi.vkDestroyPipelineCache(_pipelineCache, null);
        _pipelineCache = default;
    }

    // The cache's data where it changed since it was read and holds the whole header it names, or
    // none, as lavapipe asked for 32 bytes and filled 20 of them.
    private byte[]? PipelineCacheData()
    {
        nuint size;
        if (_deviceApi.vkGetPipelineCacheData(_pipelineCache, &size, null) != VkResult.Success || size < 32 || size == _pipelineCacheRead)
            return null;
        var data = new byte[(int)size];
        fixed (byte* bytes = data)
            if (_deviceApi.vkGetPipelineCacheData(_pipelineCache, &size, bytes) != VkResult.Success)
                return null;
        return size >= 32 && BinaryPrimitives.ReadUInt32LittleEndian(data) <= size ? data[..(int)size] : null;
    }

    // How many pipelines the device made and the time the driver took, which the renderer logs for
    // the first frame and any frame that waits long on them.
    private long _pipelinesMade, _pipelineTicks;

    /// <summary>How many pipelines the device made and how long the driver took to make them, from the start.</summary>
    internal (long Made, TimeSpan Making) PipelinesMade =>
        (Interlocked.Read(ref _pipelinesMade), Stopwatch.GetElapsedTime(0, Interlocked.Read(ref _pipelineTicks)));

    // A compute pipeline the driver makes through the cache, counted.
    internal VkPipeline CreateComputePipeline(VkComputePipelineCreateInfo* info)
    {
        var making = Stopwatch.GetTimestamp();
        VkPipeline pipeline;
        _deviceApi.vkCreateComputePipelines(_pipelineCache, 1, info, null, &pipeline).CheckResult();
        Made(making);
        return pipeline;
    }

    // Counts a pipeline the driver made since the timestamp given.
    private void Made(long since)
    {
        Interlocked.Increment(ref _pipelinesMade);
        Interlocked.Add(ref _pipelineTicks, Stopwatch.GetTimestamp() - since);
    }
}
