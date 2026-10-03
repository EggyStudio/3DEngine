using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Engine;

/// <summary>
/// Central asset management coordinator. Handles async loading, deduplication, type-safe storage,
/// dependency tracking, and hot-reload via file watching.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a <see cref="World"/> resource by <see cref="AssetPlugin"/>. The server orchestrates:
/// <list type="bullet">
///   <item><description>Pluggable <see cref="IAssetReader"/> sources (filesystem, embedded, network).</description></item>
///   <item><description>Pluggable <see cref="IAssetLoader{T}"/> per file extension.</description></item>
///   <item><description>Background thread pool for async loading via <see cref="Channel{T}"/>.</description></item>
///   <item><description>Per-path deduplication - same path always returns the same <see cref="Handle{T}"/>.</description></item>
///   <item><description>Hot-reload via <see cref="IAssetWatcher"/> when enabled.</description></item>
///   <item><description>Typed <see cref="Assets{T}"/> storage and <see cref="AssetEvent{T}"/> lifecycle events.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Threading model:</b> <see cref="Load{T}(string)"/> can be called from any thread and returns
/// a handle immediately. Actual I/O runs on background threads. The <see cref="ProcessCompleted"/>
/// method is called once per frame on the schedule thread (by <see cref="AssetPlugin"/>) to drain
/// completed loads into <see cref="Assets{T}"/> and fire <see cref="AssetEvent{T}"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In a plugin's Build method:
/// var server = world.Resource&lt;AssetServer&gt;();
/// server.RegisterLoader(new TextureLoader());
///
/// // In a system:
/// var server = world.Resource&lt;AssetServer&gt;();
/// Handle&lt;Texture&gt; tex = server.Load&lt;Texture&gt;("textures/ground.png");
///
/// // Next frame, check if loaded:
/// var assets = world.Resource&lt;Assets&lt;Texture&gt;&gt;();
/// if (assets.TryGet(tex, out var texture))
///     BindTexture(texture);
/// </code>
/// </example>
/// <seealso cref="AssetPlugin"/>
/// <seealso cref="Assets{T}"/>
/// <seealso cref="Handle{T}"/>
/// <seealso cref="IAssetLoader{T}"/>
/// <seealso cref="IAssetReader"/>
public sealed partial class AssetServer : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.AssetServer");

    private readonly List<(string Label, IAssetReader Reader)> _sources = [];
    private readonly List<IAssetWatcher> _watchers = [];

    // Extension → loader (e.g. ".png" → TextureLoader adapter)
    private readonly Dictionary<string, IAssetLoaderUntyped> _loaders = new(StringComparer.OrdinalIgnoreCase);

    // path string → (AssetId, AssetType) - deduplication
    private readonly ConcurrentDictionary<string, (AssetId Id, Type AssetType)> _pathToId = new();
    // AssetId → LoadState
    private readonly ConcurrentDictionary<AssetId, LoadState> _states = new();
    // AssetId → AssetPath (for reload/diagnostics)
    private readonly ConcurrentDictionary<AssetId, AssetPath> _idToPath = new();
    // AssetId → list of dependency AssetIds
    private readonly ConcurrentDictionary<AssetId, HashSet<AssetId>> _dependencies = new();

    private readonly Channel<LoadRequest> _loadQueue = Channel.CreateUnbounded<LoadRequest>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
    private readonly ConcurrentQueue<CompletedLoad> _completedLoads = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task[] _workers;

    private bool _watchEnabled;
    private bool _disposed;

    /// <summary>Whether file watching (hot-reload) is enabled.</summary>
    public bool WatchForChanges => _watchEnabled;

    /// <summary>Number of registered asset sources.</summary>
    public int SourceCount => _sources.Count;

    /// <summary>Number of registered loaders.</summary>
    public int LoaderCount => _loaders.Count;

    /// <summary>Number of assets currently tracked (any state).</summary>
    public int TrackedAssetCount => _pathToId.Count;

    /// <summary>
    /// Creates a new <see cref="AssetServer"/> with the specified number of background worker threads.
    /// </summary>
    /// <param name="workerCount">
    /// Number of background threads for async loading. Defaults to
    /// <c>Math.Max(2, Environment.ProcessorCount / 2)</c>.
    /// </param>
    public AssetServer(int? workerCount = null)
    {
        int count = workerCount ?? Math.Max(2, Environment.ProcessorCount / 2);
        _workers = new Task[count];
        for (int i = 0; i < count; i++)
        {
            int id = i;
            _workers[i] = Task.Factory.StartNew(
                () => WorkerLoop(id, _cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }

        Logger.Info($"AssetServer created with {count} worker thread(s).");
    }

    /// <summary>Returns a snapshot of all tracked asset paths and their load states.</summary>
    public IReadOnlyDictionary<string, LoadState> GetAllStates()
    {
        var result = new Dictionary<string, LoadState>();
        foreach (var kv in _pathToId)
        {
            _states.TryGetValue(kv.Value.Id, out var state);
            result[kv.Key] = state;
        }
        return result;
    }

    /// <summary>
    /// Cancels background workers, stops watchers, and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Logger.Info("AssetServer shutting down...");

        DisableWatching();
        _cts.Cancel();
        _loadQueue.Writer.Complete();

        try
        {
            Task.WaitAll(_workers, TimeSpan.FromSeconds(5));
        }
        catch (AggregateException) { /* workers may throw on cancellation */ }

        _cts.Dispose();
        Logger.Info("AssetServer shut down.");
    }
}
