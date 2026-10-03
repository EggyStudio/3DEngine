using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine;


/// <summary>
/// Orchestrates offline/background asset processing: reads source assets, runs them through
/// registered <see cref="IAssetProcessor"/>s, and writes optimized outputs to a processed directory.
/// </summary>
/// <remarks>
/// <para>
/// Source assets are read from the <c>sourceDirectory</c>. Processed outputs are written to
/// the <c>processedDirectory</c>. A single <c>.cache.json</c> manifest in the processed directory
/// tracks content hashes to skip reprocessing of unchanged assets. No per-file sidecar files
/// are created - the source directory is never modified.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var pipeline = new AssetProcessorPipeline("assets_source", "assets")
///     .Register(new TextureCompressor())
///     .Register(new MeshOptimizer());
///
/// await pipeline.ProcessAllAsync();
/// </code>
/// </example>
/// <seealso cref="IAssetProcessor"/>
public sealed class AssetProcessorPipeline
{
    private static readonly ILogger Logger = Log.Category("Engine.AssetProcessor");

    private readonly string _sourceDir;
    private readonly string _processedDir;
    private readonly Dictionary<string, IAssetProcessor> _processors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new processing pipeline.
    /// </summary>
    /// <param name="sourceDirectory">Directory containing source assets.</param>
    /// <param name="processedDirectory">Directory to write processed assets to.</param>
    public AssetProcessorPipeline(string sourceDirectory, string processedDirectory)
    {
        _sourceDir = Path.GetFullPath(sourceDirectory);
        _processedDir = Path.GetFullPath(processedDirectory);
    }

    /// <summary>Registers a processor for its declared extensions.</summary>
    /// <param name="processor">The processor to register.</param>
    /// <returns>This pipeline for fluent chaining.</returns>
    public AssetProcessorPipeline Register(IAssetProcessor processor)
    {
        foreach (string ext in processor.Extensions)
        {
            string normalized = ext.StartsWith('.') ? ext : $".{ext}";
            _processors[normalized] = processor;
            Logger.Debug($"Processor registered: {normalized} → {processor.GetType().Name}");
        }
        return this;
    }

    /// <summary>
    /// Processes all source assets that have changed since the last run.
    /// Skips assets whose SHA-256 hash matches the cached value in <c>.cache.json</c>.
    /// The source directory is never modified - only the processed directory is written to.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of assets processed.</returns>
    public async Task<int> ProcessAllAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_sourceDir))
        {
            Logger.Warn($"Source directory not found: {_sourceDir}");
            return 0;
        }

        NativeLibraryLoader.EnsureDirectory(_processedDir);

        var cache = await LoadCacheAsync(ct);
        int processed = 0;

        foreach (string sourceFile in Directory.EnumerateFiles(_sourceDir, "*.*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            string ext = Path.GetExtension(sourceFile);
            if (!_processors.TryGetValue(ext, out var processor)) continue;

            string relativePath = Path.GetRelativePath(_sourceDir, sourceFile).Replace('\\', '/');

            // Check content hash against cache - skip if unchanged
            string hash = ComputeHash(sourceFile);
            if (cache.Hashes.TryGetValue(relativePath, out var cached) && cached == hash)
            {
                Logger.Debug($"Skipping unchanged: {relativePath}");
                continue;
            }

            // Process
            Logger.Info($"Processing: {relativePath}...");
            byte[] sourceBytes = await File.ReadAllBytesAsync(sourceFile, ct);
            var ctx = new ProcessContext(new AssetPath(relativePath), sourceBytes);
            var result = await processor.ProcessAsync(ctx, ct);

            if (!result.Success)
            {
                Logger.Error($"Processing failed: {relativePath} - {result.Error}");
                continue;
            }

            // Determine output path
            string outputRelative = processor.OutputExtension is not null
                ? Path.ChangeExtension(relativePath, processor.OutputExtension)
                : relativePath;
            string outputPath = Path.Combine(_processedDir, outputRelative.Replace('/', Path.DirectorySeparatorChar));

            // Ensure output directory
            string? outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                Directory.CreateDirectory(outputDir);

            await File.WriteAllBytesAsync(outputPath, result.OutputBytes!, ct);

            // Update cache
            cache.Hashes[relativePath] = hash;
            processed++;
            Logger.Info($"Processed: {relativePath} → {outputRelative} ({result.OutputBytes!.Length} bytes)");
        }

        // Write cache manifest once at the end
        if (processed > 0)
            await SaveCacheAsync(cache, ct);

        Logger.Info($"Processing complete: {processed} asset(s) processed.");
        return processed;
    }

    /// <summary>
    /// Processes a single asset by relative path. Does not use cache - always reprocesses.
    /// </summary>
    /// <param name="relativePath">Relative path from the source directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the asset was processed successfully.</returns>
    public async Task<bool> ProcessFileAsync(string relativePath, CancellationToken ct = default)
    {
        string sourceFile = Path.Combine(_sourceDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourceFile))
        {
            Logger.Warn($"Source file not found: {sourceFile}");
            return false;
        }

        string ext = Path.GetExtension(sourceFile);
        if (!_processors.TryGetValue(ext, out var processor))
        {
            Logger.Debug($"No processor for extension '{ext}': {relativePath}");
            return false;
        }

        byte[] sourceBytes = await File.ReadAllBytesAsync(sourceFile, ct);
        var ctx = new ProcessContext(new AssetPath(relativePath), sourceBytes);
        var result = await processor.ProcessAsync(ctx, ct);

        if (!result.Success)
        {
            Logger.Error($"Processing failed: {relativePath} - {result.Error}");
            return false;
        }

        string outputRelative = processor.OutputExtension is not null
            ? Path.ChangeExtension(relativePath, processor.OutputExtension)
            : relativePath;
        string outputPath = Path.Combine(_processedDir, outputRelative.Replace('/', Path.DirectorySeparatorChar));

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        await File.WriteAllBytesAsync(outputPath, result.OutputBytes!, ct);

        // Update cache for this file
        var cache = await LoadCacheAsync(ct);
        cache.Hashes[relativePath] = ComputeHash(sourceFile);
        await SaveCacheAsync(cache, ct);

        return true;
    }

    /// <summary>Deletes the processing cache, forcing a full reprocessing on the next run.</summary>
    public void ClearCache()
    {
        string path = CacheFilePath;
        if (File.Exists(path))
        {
            File.Delete(path);
            Logger.Info("Processing cache cleared.");
        }
    }

    // -- Cache manifest --

    private string CacheFilePath => Path.Combine(_processedDir, ".cache.json");

    private async Task<ProcessingCache> LoadCacheAsync(CancellationToken ct)
    {
        string path = CacheFilePath;
        if (!File.Exists(path))
            return new ProcessingCache();
        try
        {
            string json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<ProcessingCache>(json, ProcessingCache.JsonOptions) ?? new ProcessingCache();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to read processing cache: {ex.Message}");
            return new ProcessingCache();
        }
    }

    private async Task SaveCacheAsync(ProcessingCache cache, CancellationToken ct)
    {
        string path = CacheFilePath;
        string json = JsonSerializer.Serialize(cache, ProcessingCache.JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
        Logger.Debug($"Processing cache written: {cache.Hashes.Count} entries.");
    }

    private static string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = System.Security.Cryptography.SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Internal cache manifest stored as <c>.cache.json</c> in the processed output directory.
/// Maps relative source paths to their SHA-256 content hashes.
/// </summary>
internal sealed class ProcessingCache
{
    [JsonPropertyName("hashes")]
    public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.Ordinal);

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
}
