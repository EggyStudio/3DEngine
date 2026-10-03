namespace Engine;

/// <summary>
/// Reacts to <see cref="AssetEvent{T}.Modified"/> events for <see cref="SceneAsset"/> and
/// re-spawns the affected subtree in place: every entity tracked under the asset's id in
/// <see cref="SpawnedScenes"/> is despawned, then the new <see cref="SceneAsset"/> is
/// translated into a fresh entity set via <see cref="SceneSpawner.Spawn"/> with the
/// originally-recorded <see cref="SceneSpawnSettings"/>. This delivers the OpenUSD
/// "edit, save, see changes live" workflow on top of the asset hot-reload pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope:</b> only assets tracked by <see cref="SpawnedScenes"/> are eligible -
/// in practice that means assets brought in via <see cref="SpawnSceneRequest"/> (driven by
/// <see cref="SceneSpawnSystem"/>) or callers that opted into tracking by hand. A scene
/// loaded but never spawned is a no-op for hot-reload, which matches expectations.
/// </para>
/// <para>
/// <b>Event lifetime:</b> asset events are cleared at <see cref="Stage.Last"/> by the
/// asset plugin, so reading them in <see cref="Stage.PreUpdate"/> sees every event posted
/// on the previous frame plus any posted earlier in this stage. The system uses
/// <c>Read()</c> (a non-draining snapshot) to coexist with other consumers.
/// </para>
/// <para>
/// <b>Idempotence on failure:</b> if the new asset can't be retrieved (transient
/// dependency error, etc.), the system leaves the old entity set intact. The next event
/// for the same asset will retry. We never partially despawn-then-fail.
/// </para>
/// </remarks>
public static class SceneHotReloadSystem
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    /// <summary>One pass: handle every queued <see cref="AssetEvent{T}"/> for <see cref="SceneAsset"/>.</summary>
    public static void Run(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;
        if (!world.TryGetResource<Assets<SceneAsset>>(out var assets)) return;
        if (!world.TryGetResource<Events<AssetEvent<SceneAsset>>>(out var events)) return;
        if (!world.TryGetResource<SpawnedScenes>(out var tracking)) return;
        world.TryGetResource<AssetServer>(out var assetServer);
        world.TryGetResource<MaterialLibrary>(out var materialLibrary);

        // Read (don't drain) - other systems may also consume these events; AssetPlugin
        // clears them in Stage.Last.
        foreach (var evt in events.Read())
        {
            if (evt.Kind != AssetEventKind.Modified) continue;
            if (!tracking.TryGet(evt.Id, out var record)) continue;

            // Resolve the new asset before touching the world: if it isn't ready yet
            // (race with the AssetServer drain order), bail out cleanly so the next pass
            // can retry.
            if (!assets.TryGet(evt.Handle, out var asset))
            {
                Logger.Debug($"SceneHotReloadSystem: asset {evt.Id} not yet in Assets<SceneAsset> after Modified event; deferring.");
                continue;
            }

            // Despawn old entities. EcsWorld.Despawn is safe on already-despawned ids
            // (id reuse since the original spawn is unlikely within a single frame, but
            // tolerated either way).
            for (int i = 0; i < record.Entities.Length; i++)
                ecs.Despawn(record.Entities[i]);

            // Re-spawn with the same settings so the user-visible result matches the
            // initial spawn (basis change, default albedo, marker policy, ...).
            try
            {
                var fresh = SceneSpawner.Spawn(
                    ecs, asset.Scene, record.Settings, evt.Id.Value,
                    assetServer, asset.SourcePath, materialLibrary);
                tracking.Track(evt.Id, fresh, record.Settings);
                Logger.Info($"SceneHotReloadSystem: re-spawned '{asset.SourcePath}' ({record.Entities.Length} -> {fresh.Count} entities).");
            }
            catch (Exception ex)
            {
                // Don't leave a dangling tracking entry for entities we just despawned.
                tracking.Remove(evt.Id, out _);
                Logger.Error($"SceneHotReloadSystem: re-spawn failed for '{asset.SourcePath}': {ex.Message}");
            }
        }
    }
}