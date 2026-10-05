namespace Engine;

/// <summary>
/// Lets go of the models, textures and scene files a level loaded through <see cref="ModelRef"/>
/// and <see cref="SceneRef"/> once no entity uses them, so a level streamed in as the player nears
/// and despawned behind holds what is near and not everything it ever loaded.
/// </summary>
/// <remarks>
/// <para>
/// Each model a <see cref="ModelRef"/> asks for is held by its entity, each texture a spawned
/// material names by the entities the model spawned, and each scene file a <see cref="SceneRef"/>
/// read by its entity. When every holder of an asset is gone it waits <see cref="Grace"/> seconds,
/// so a room left and entered again is not read again, and is then let go. A model is removed from
/// the asset server, which reads it again if it is asked for later, a texture is removed with the
/// GPU copy the renderer made of it, and a scene file's parsed copy and the meshes shared between
/// its copies are dropped.
/// </para>
/// <para>
/// The counts are the asset server's own, which each load takes one of. What a program loads
/// itself through <see cref="AssetServer.Load{T}(string)"/> keeps the count it took, so a texture
/// the program holds is never taken from under it, even when a level used it too.
/// </para>
/// </remarks>
public sealed class AssetRelease
{
    /// <summary>Seconds an asset no entity uses waits before it is let go, 10 to begin with.</summary>
    public double Grace { get; set; } = 10;

    /// <summary>How many models, textures and scene files have been let go since the program started.</summary>
    public int Released { get; private set; }

    /// <summary>How many models, textures and scene files are held by entities now.</summary>
    public int Held => _holds.Sum(h => h.Assets.Length) + _files.Count;

    internal enum Kind : byte { Model, Texture }

    // What one or more entities hold, kept while any of them is alive.
    private sealed record Hold(Entity[] Holders, (AssetId Id, Kind Kind)[] Assets, string? File);

    private readonly List<Hold> _holds = [];
    private readonly Dictionary<string, int> _files = [];
    // What no entity holds, with when it was last let go of, waiting out the grace.
    private readonly Dictionary<AssetId, (double Since, Kind Kind)> _unused = [];
    private readonly Dictionary<string, double> _unusedFiles = [];

    /// <summary>Holds a model for an entity, the count its load took being given back once the entity is gone.</summary>
    internal void HoldModel(Entity holder, AssetId model) => Add(new Hold([holder], [(model, Kind.Model)], null));

    /// <summary>Holds the textures a spawn's materials loaded for the entities it spawned.</summary>
    internal void HoldTextures(Entity[] holders, IReadOnlyList<AssetId> textures)
    {
        if (holders.Length == 0 || textures.Count == 0) return;
        Add(new Hold(holders, textures.Select(t => (t, Kind.Texture)).ToArray(), null));
    }

    /// <summary>Holds a parsed scene file for the entity whose <see cref="SceneRef"/> read it.</summary>
    internal void HoldFile(Entity holder, string file)
    {
        Add(new Hold([holder], [], file));
    }

    private void Add(Hold hold)
    {
        _holds.Add(hold);
        if (hold.File is { } file)
        {
            _files[file] = _files.GetValueOrDefault(file) + 1;
            _unusedFiles.Remove(file);
        }
    }

    /// <summary>
    /// Gives back what entities that are gone held, and lets go of what has been unused for longer
    /// than <see cref="Grace"/>. Run once a frame, in <see cref="Stage.PreUpdate"/> after the
    /// spawns, so the renderer reads the textures removed in the same frame.
    /// </summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<AssetRelease>(out var release) || !world.TryGetResource<EcsWorld>(out var ecs)) return;
        var now = world.TryGetResource<Time>(out var time) ? time.ElapsedSeconds : 0;
        world.TryGetResource<AssetServer>(out var server);
        release.Sweep(world, ecs, server, now);
    }

    private void Sweep(World world, EcsWorld ecs, AssetServer? server, double now)
    {
        for (int i = _holds.Count - 1; i >= 0; i--)
        {
            var hold = _holds[i];
            if (hold.Holders.Any(ecs.IsAlive)) continue;
            _holds.RemoveAt(i);
            // Each hold gives back the counts its loads took.
            foreach (var (id, kind) in hold.Assets)
                if (HandleRefCounts.Decrement(id) == 0) _unused[id] = (now, kind);
            if (hold.File is { } file)
            {
                var count = _files.GetValueOrDefault(file) - 1;
                if (count > 0) _files[file] = count;
                else
                {
                    _files.Remove(file);
                    _unusedFiles[file] = now;
                }
            }
        }

        if (_unused.Count > 0)
            foreach (var (id, (since, kind)) in _unused.ToArray())
            {
                if (now - since < Grace) continue;
                // Loaded again since, by a level or by the program.
                if (HandleRefCounts.GetCount(id) > 0)
                {
                    _unused.Remove(id);
                    continue;
                }
                // A load still under way is let go once it has finished, so what it brings is not
                // stored under an id nothing knows.
                if (server is not null && server.GetLoadState(id) == LoadState.Loading) continue;
                _unused.Remove(id);
                var released = kind switch
                {
                    Kind.Model => server?.Unload<SceneAsset>(world, id) ?? false,
                    _ => server?.Unload<TextureAsset>(world, id) ?? false,
                };
                if (released) Released++;
            }

        if (_unusedFiles.Count > 0)
            foreach (var (file, since) in _unusedFiles.ToArray())
            {
                if (now - since < Grace) continue;
                _unusedFiles.Remove(file);
                if (SceneRefSystem.Forget(file)) Released++;
            }
    }
}
