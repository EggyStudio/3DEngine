namespace Engine;

/// <summary>
/// Caches compiled <see cref="IPipeline"/> instances keyed by <see cref="GraphicsPipelineDesc"/>,
/// avoiding redundant pipeline compilation when multiple nodes or draw functions request
/// the same shader/vertex layout/blend state combination.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GraphicsPipelineDesc"/> is a <c>readonly record struct</c> so two descriptors with
/// identical field values (including reference-equal shaders and render passes) share the same
/// compiled pipeline.  Callers should cache and reuse shader/render-pass handles to benefit
/// from this deduplication.
/// </para>
/// <para>
/// The cache is stored as a <see cref="RenderWorld"/> resource so that all render graph nodes
/// and prepare systems can share compiled pipelines.
/// </para>
/// </remarks>
/// <seealso cref="GraphicsPipelineDesc"/>
/// <seealso cref="IPipeline"/>
/// <seealso cref="RenderWorld"/>
internal sealed class PipelineCache : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Renderer.PipelineCache");

    private readonly IGraphicsDevice _device;
    private readonly Dictionary<GraphicsPipelineDesc, IPipeline> _cache = new();

    // The pipelines made on workers for a pass that waits for no compile (GetOrBegin).
    private readonly Dictionary<GraphicsPipelineDesc, Task<IPipeline>> _making = new();

    /// <summary>Number of cached pipelines.</summary>
    public int Count => _cache.Count;

    /// <summary>Creates a new pipeline cache bound to the given graphics device.</summary>
    /// <param name="device">The graphics device used to compile pipelines.</param>
    public PipelineCache(IGraphicsDevice device) =>
        _device = device;

    /// <summary>
    /// Returns an existing compiled pipeline matching <paramref name="desc"/>, or compiles and
    /// caches a new one.
    /// </summary>
    /// <param name="desc">The graphics pipeline descriptor to look up or compile.</param>
    /// <returns>The compiled <see cref="IPipeline"/>.</returns>
    public IPipeline GetOrCreate(GraphicsPipelineDesc desc)
    {
        if (_cache.TryGetValue(desc, out var existing))
            return existing;
        if (_making.Remove(desc, out var made))
            return _cache[desc] = made.Result;

        Logger.Debug($"Compiling new pipeline (cache size {_cache.Count} → {_cache.Count + 1})...");
        var pipeline = _device.CreateGraphicsPipeline(desc);
        _cache[desc] = pipeline;
        return pipeline;
    }

    /// <summary>
    /// Returns the compiled pipeline matching <paramref name="desc"/>, or none while a worker thread
    /// makes it, which this sets going where none has.
    /// </summary>
    /// <remarks>
    /// A pass a program turns on in play draws with what is made and leaves out the rest until it
    /// is, as the window's view mirrored in a plane does. The voxel game's first frame with a
    /// reflection plane took 23.7 to 24.0 s of the wall clock, the driver compiling the mirrored
    /// pass's pipelines on the thread that records the frame.
    /// </remarks>
    public IPipeline? GetOrBegin(GraphicsPipelineDesc desc)
    {
        if (_cache.TryGetValue(desc, out var existing))
            return existing;
        if (_making.TryGetValue(desc, out var making))
        {
            if (!making.IsCompleted) return null;
            _making.Remove(desc);
            return _cache[desc] = making.Result;
        }
        _making[desc] = Task.Run(() => _device.CreateGraphicsPipeline(desc));
        return null;
    }

    /// <summary>How many pipelines worker threads are making.</summary>
    public int Making => _making.Count;

    /// <summary>
    /// Waits for every pipeline worker threads are making and keeps each, so the shaders and
    /// layouts they are made from may be let go after.
    /// </summary>
    public void WaitForMaking()
    {
        foreach (var (desc, making) in _making)
            _cache[desc] = making.Result;
        _making.Clear();
    }

    /// <summary>
    /// Removes a specific pipeline from the cache and disposes its GPU resources.
    /// </summary>
    /// <param name="desc">The descriptor of the pipeline to remove.</param>
    /// <returns><c>true</c> if the pipeline was found and removed; <c>false</c> otherwise.</returns>
    public bool Remove(GraphicsPipelineDesc desc)
    {
        if (_cache.Remove(desc, out var pipeline))
        {
            pipeline.Dispose();
            return true;
        }
        return false;
    }

    /// <summary>Clears all cached pipelines, disposing their GPU resources.</summary>
    public void Clear()
    {
        WaitForMaking();
        foreach (var pipeline in _cache.Values)
            pipeline.Dispose();
        _cache.Clear();
        Logger.Debug("Pipeline cache cleared.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Logger.Debug($"Disposing pipeline cache ({_cache.Count} pipelines)...");
        Clear();
    }
}
