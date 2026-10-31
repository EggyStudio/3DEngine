using System.Buffers.Binary;
using Vortice.Vulkan;

namespace Engine;

/// <remarks>
/// The driver's compiled pipelines are read from a file in the program's cache folder when the
/// device is made and written back when it is let go, so a run compiles no pipeline a run before it
/// compiled. The voxel game's first frame with a reflection plane took 23.7 to 24.0 s of the wall
/// clock, the driver compiling the mirrored pass's pipelines, wherever the driver's own cache missed,
/// which on the development machine held 1.1 GB across six programs and served none of the game's
/// runs.
/// </remarks>
internal sealed unsafe partial class GraphicsDevice
{
    private VkPipelineCache _pipelineCache;

    // The bytes the cache was read with, written back only where it holds more or less, a run that
    // made no pipeline the file lacked leaving it as it was.
    private nuint _pipelineCacheRead;

    /// <summary>Where the driver's compiled pipelines are kept between runs, beside the compiled shaders.</summary>
    internal static string PipelineCachePath => Path.Combine(AppContext.BaseDirectory, "source", ".slang-cache", "pipelines.bin");

    // Makes the cache, from the file where its header names this GPU and its driver's cache, and
    // empty otherwise, since a driver need not check the data past the header it is given.
    private void CreatePipelineCache()
    {
        var data = ReadPipelineCache();
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
            _pipelineCache = cache;
            _pipelineCacheRead = info.initialDataSize;
        }
    }

    private byte[] ReadPipelineCache()
    {
        byte[] data;
        try
        {
            if (!File.Exists(PipelineCachePath)) return [];
            data = File.ReadAllBytes(PipelineCachePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn($"The pipeline cache at {PipelineCachePath} could not be read, so every pipeline is compiled: {ex.Message}");
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
}
