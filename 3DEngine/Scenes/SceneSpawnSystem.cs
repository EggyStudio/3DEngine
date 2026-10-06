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
internal static class SceneSpawnSystem
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
        world.TryGetResource<AssetRelease>(out var release);

        // Snapshot first: SceneSpawner.Spawn mutates the world (Spawn + Add), and
        // EcsWorld.Query yields live references; iterating a stale snapshot keeps the
        // semantics simple.
        List<(int Entity, SpawnSceneRequest Request)>? pending = null;
        foreach (var (entity, request) in ecs.Query<SpawnSceneRequest>())
        {
            (pending ??= new()).Add((entity, request));
        }
        if (pending is null) return;

        HashSet<int>? spawnedUnder = null;
        foreach (var (entity, request) in pending)
        {
            if (!assets.TryGet(request.Handle, out var asset))
                continue; // still loading (or load failed - hot-reload may revive it)
            (spawnedUnder ??= []).Add(Root(ecs, entity));

            // A model a ModelRef names that has clips plays its first through an AnimatedModel on
            // a child, where its meshes spawned as entities would stand at rest. The child carries
            // a SceneInstance, so a level saved with the reference is saved without it.
            if (ecs.TryGet<ModelRef>(entity, out var reference) && Engine3D.Holds(world) && Animated(asset.Scene))
            {
                var path = ModelRefSystem.AssetPath(reference.Path);
                var child = ecs.Spawn();
                ecs.Add(child, new AnimatedModel(path));
                ecs.Add(child, new Transform(System.Numerics.Vector3.Zero));
                ecs.Add(child, new SceneInstance { SourcePath = path });
                ecs.SetParent(child, entity);
                ecs.Remove<SpawnSceneRequest>(entity);
                continue;
            }

            try
            {
                var settings = request.Settings ?? SceneSpawnSettings.Default;
                var textures = new List<AssetId>();
                var entities = SceneSpawner.SpawnTaking(
                    ecs, asset.Scene, settings, request.Handle.Id.Value,
                    assetServer, asset.SourcePath, materialLibrary, textures);
                tracking.Track(request.Handle.Id, entities, settings);
                // The textures the materials loaded are held while any entity spawned with them is.
                release?.HoldTextures(entities.Select(ecs.Handle).ToArray(), textures);

                // The scene hangs under the entity that asked for it, so that entity's Transform
                // places it. One with no Transform composes as identity, as before.
                foreach (var spawned in entities)
                    if (ecs.ParentOf(spawned) == 0 && spawned != entity)
                        ecs.SetParent(spawned, entity);
                Logger.Debug($"SceneSpawnSystem: spawned {entities.Count} entit{(entities.Count == 1 ? "y" : "ies")} for '{asset.SourcePath}'.");
            }
            catch (Exception ex)
            {
                Logger.Error($"SceneSpawnSystem: spawn failed for '{asset.SourcePath}': {ex.Message}");
            }

            // The request is removed even on failure, so a broken asset is not tried
            // every frame. Hot-reload produces a new SceneAsset and is
            // handled by SceneHotReloadSystem (no need to re-add the request component).
            ecs.Remove<SpawnSceneRequest>(entity);
        }

        if (spawnedUnder is not null) RecaptureProbes(ecs, assetServer, spawnedUnder);
    }

    // A probe placed with its room, as a prefab places a room's models beside it, was captured in
    // the frame it appeared, before the models had loaded, and reflected the sky where the walls
    // now stand. Once nothing under a root waits for its model, the probes under it capture again.
    private static void RecaptureProbes(EcsWorld ecs, AssetServer? server, HashSet<int> roots)
    {
        foreach (var (entity, request) in ecs.Query<SpawnSceneRequest>())
            if (server?.GetLoadState(request.Handle.Id) != LoadState.Failed) roots.Remove(Root(ecs, entity));
        if (roots.Count == 0) return;
        List<int>? probes = null;
        foreach (var (entity, _) in ecs.Query<ReflectionProbe>())
            if (roots.Contains(Root(ecs, entity))) (probes ??= []).Add(entity);
        if (probes is null) return;
        foreach (var probe in probes) ecs.GetRef<ReflectionProbe>(probe).Capture++;
    }

    private static bool Animated(Scene scene) => SceneBones.Walk(scene).Any(node => node.Components.OfType<SceneAnimationPayload>().Any());

    private static int Root(EcsWorld ecs, int entity)
    {
        while (ecs.ParentOf(entity) is var parent and not 0) entity = parent;
        return entity;
    }
}