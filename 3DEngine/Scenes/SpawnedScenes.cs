namespace Engine;

/// <summary>
/// World resource that records which ECS entities a given <see cref="SceneAsset"/> spawn
/// produced, so <see cref="SceneHotReloadSystem"/> can despawn-and-respawn the affected
/// subtree when the asset hot-reloads.
/// </summary>
/// <remarks>
/// <para>
/// Inserted by <see cref="ScenesPlugin"/>. Maintained by <see cref="SceneSpawnSystem"/>
/// (which records new spawns) and <see cref="SceneHotReloadSystem"/> (which despawns the
/// old set and re-records the replacement). Direct callers of
/// <see cref="SceneSpawner.Spawn"/> can also <see cref="Track"/> their result manually if
/// they want hot-reload behavior outside the request-driven workflow.
/// </para>
/// <para>
/// One <see cref="AssetId"/> maps to one record - re-spawning the same asset replaces the
/// previous entry. If you need multiple independent instances of the same scene asset in
/// the same world (different placements), spawn them via the synchronous helper and skip
/// tracking; hot-reload will only re-drive the tracked instance.
/// </para>
/// </remarks>
public sealed class SpawnedScenes
{
    private readonly Dictionary<AssetId, SpawnedSceneRecord> _records = new();
    private readonly Lock _lock = new();

    /// <summary>Total number of tracked scene spawns.</summary>
    public int Count { get { lock (_lock) return _records.Count; } }

    /// <summary>Records (or replaces) the entity set spawned for <paramref name="assetId"/>.</summary>
    public void Track(AssetId assetId, IEnumerable<int> entities, SceneSpawnSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_lock)
        {
            _records[assetId] = new SpawnedSceneRecord(assetId, entities.ToArray(), settings);
        }
    }

    /// <summary>Looks up the record for <paramref name="assetId"/>, if any.</summary>
    public bool TryGet(AssetId assetId, out SpawnedSceneRecord record)
    {
        lock (_lock)
            return _records.TryGetValue(assetId, out record!);
    }

    /// <summary>Removes and returns the tracking record for <paramref name="assetId"/>.</summary>
    public bool Remove(AssetId assetId, out SpawnedSceneRecord record)
    {
        lock (_lock)
            return _records.Remove(assetId, out record!);
    }

    /// <summary>Snapshot of all currently tracked records (for diagnostics / editor UI).</summary>
    public IReadOnlyList<SpawnedSceneRecord> Snapshot()
    {
        lock (_lock)
            return _records.Values.ToArray();
    }
}

/// <summary>
/// One entry in <see cref="SpawnedScenes"/>: which entities back a given
/// <see cref="SceneAsset"/> spawn, and the policy that produced them.
/// </summary>
/// <param name="AssetId">Source asset id.</param>
/// <param name="Entities">Entity IDs spawned by <see cref="SceneSpawner.Spawn"/>, in depth-first order.</param>
/// <param name="Settings">Settings used at spawn time; reused verbatim on hot-reload re-spawn.</param>
public sealed record SpawnedSceneRecord(AssetId AssetId, int[] Entities, SceneSpawnSettings Settings);