namespace Engine;

/// <summary>
/// Polls every entity holding a <see cref="SpawnSceneRequest"/> and, as soon as its
/// <see cref="Handle{T}"/> resolves in <see cref="Assets{T}"/>, calls
/// <see cref="SceneSpawner.Spawn"/> and removes the request component so the spawn fires
/// exactly once. Registered by <see cref="ScenesPlugin"/> in <see cref="Stage.PreUpdate"/>
/// (after <c>AssetServer</c> has drained completed loads in the same stage).
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotence:</b> removing the component is the marker that "this request was
/// fulfilled". Re-adding the request later (e.g. after a hot-reload event) is a valid way
/// to re-spawn; the system has no internal memory of past requests.
/// </para>
/// <para>
/// <b>Why a system at all:</b> the spawner itself is synchronous and could be invoked
/// from any behavior. The system exists so that asset-driven "auto-spawn on load" works
/// without the gameplay code having to hand-roll a polling <c>OnUpdate</c>.
/// </para>
/// </remarks>
public static class SceneSpawnSystem
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    /// <summary>
    /// One pass over <see cref="SpawnSceneRequest"/> components. Skips silently when
    /// <c>Assets&lt;SceneAsset&gt;</c> doesn't exist yet (no scene asset has finished
    /// loading on this world).
    /// </summary>
    public static void Run(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;
        if (!world.TryGetResource<Assets<SceneAsset>>(out var assets)) return;
        var tracking = world.GetOrInsertResource(() => new SpawnedScenes());
        // AssetServer is optional from the spawner's perspective (legacy / test path),
        // but in the system path it's always present once AssetPlugin has been added.
        world.TryGetResource<AssetServer>(out var assetServer);
        // MaterialLibrary is added by MaterialPlugin (a transitive dependency of
        // most scene-loading apps). Forwarded so the spawner can register every
        // SceneMaterialPayload as a MaterialDescription and stash the resulting
        // handle on Material.Handle for the renderer's per-material pipeline cache.
        world.TryGetResource<MaterialLibrary>(out var materialLibrary);

        // Snapshot first: SceneSpawner.Spawn mutates the world (Spawn + Add), and
        // EcsWorld.Query yields live references; iterating a stale snapshot keeps the
        // semantics simple.
        List<(int Entity, SpawnSceneRequest Request)>? pending = null;
        foreach (var (entity, request) in ecs.Query<SpawnSceneRequest>())
        {
            (pending ??= new()).Add((entity, request));
        }
        if (pending is null) return;

        foreach (var (entity, request) in pending)
        {
            if (!assets.TryGet(request.Handle, out var asset))
                continue; // still loading (or load failed - hot-reload may revive it)

            try
            {
                var settings = request.Settings ?? SceneSpawnSettings.Default;
                var entities = SceneSpawner.Spawn(
                    ecs, asset.Scene, settings, request.Handle.Id.Value,
                    assetServer, asset.SourcePath, materialLibrary);
                tracking.Track(request.Handle.Id, entities, settings);
                Logger.Debug($"SceneSpawnSystem: spawned {entities.Count} entit{(entities.Count == 1 ? "y" : "ies")} for '{asset.SourcePath}'.");
            }
            catch (Exception ex)
            {
                Logger.Error($"SceneSpawnSystem: spawn failed for '{asset.SourcePath}': {ex.Message}");
            }

            // Always remove the request - even on failure - so we don't loop forever on
            // a permanently-broken asset. Hot-reload produces a new SceneAsset and is
            // handled by SceneHotReloadSystem (no need to re-add the request component).
            ecs.Remove<SpawnSceneRequest>(entity);
        }
    }
}