using System.Text;

namespace Engine;

/// <summary>
/// The console commands that say what the program holds, in managed memory, in entities, in the
/// asset server's assets and on the GPU.
/// </summary>
internal static class MemoryCommands
{
    [Command("memory", "What the program holds, as name and number pairs: managed memory, the process's resident memory, entities, the asset server's assets, and the GPU's buffers, images, descriptor sets, pipelines and memory")]
    internal static string Memory()
    {
        // Read as the program left them, without a collection, so a climb shows as it happens and
        // a reader can force one with its own spacing between reads.
        var managed = GC.GetTotalMemory(forceFullCollection: false);
        var heap = GC.GetGCMemoryInfo().HeapSizeBytes;
        var line = $"managed {managed} heap {heap} gen2 {GC.CollectionCount(2)} resident {Environment.WorkingSet}";
        if (ConsoleHost.World?.TryGetResource<EcsWorld>(out var ecs) == true)
            line += $" entities {ecs.EntityCount} entityIds {ecs.EntityIdRange}";
        // The files the asset server knows, which a level that lets nothing go climbs by as it streams.
        if (ConsoleHost.World?.TryGetResource<AssetServer>(out var server) == true)
            line += $" assets {server.TrackedAssetCount}";
        if (ConsoleHost.World?.TryGetResource<Renderer>(out var renderer) == true && renderer.Context.Graphics is GraphicsDevice device)
        {
            var usage = device.Usage;
            line += $" buffers {usage.Buffers} images {usage.Images} descriptorSets {usage.DescriptorSets} pipelines {usage.Pipelines}"
                    + $" memoryBlocks {usage.MemoryBlocks} blockBytes {usage.BlockBytes} usedBytes {usage.UsedBytes}";
        }
        return line;
    }

    [Command("memory.buffers", "The GPU's buffers alive by where the engine made each and what it is for, the most first, a line each with how many, their bytes, the file and line and the use: memory.buffers [lines]")]
    internal static string MemoryBuffers(int lines = 40)
    {
        if (ConsoleHost.World?.TryGetResource<Renderer>(out var renderer) != true || renderer.Context.Graphics is not GraphicsDevice device)
            return "no device";
        var census = device.BufferCensus();
        var text = new StringBuilder($"{census.Sum(entry => entry.Count)} buffers from {census.Count} places");
        foreach (var entry in census.Take(lines))
            text.Append($"\n{entry.Count} {entry.Bytes} {Path.GetFileName(entry.Maker.File.Replace('\\', '/'))}:{entry.Maker.Line} {entry.Maker.Usage}");
        return text.ToString();
    }

    [Command("memory.images", "The GPU's images alive by their size, format, use and levels, the most first, a line each with how many: memory.images [lines]")]
    internal static string MemoryImages(int lines = 40)
    {
        if (ConsoleHost.World?.TryGetResource<Renderer>(out var renderer) != true || renderer.Context.Graphics is not GraphicsDevice device)
            return "no device";
        var census = device.ImageCensus();
        var text = new StringBuilder($"{census.Sum(entry => entry.Count)} images of {census.Count} kinds");
        foreach (var (description, count) in census.Take(lines))
            text.Append($"\n{count} {description.Extent.Width}x{description.Extent.Height} {description.Format} {description.Usage} levels {description.MipLevels}");
        return text.ToString();
    }

    [Command("memory.collect", "The same as memory, read after a full garbage collection, so what is held shows apart from what is waiting to be collected")]
    internal static string MemoryCollected()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return Memory();
    }
}
