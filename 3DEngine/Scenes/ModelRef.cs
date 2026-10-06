using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>A model file an entity shows, spawned under the entity so its <see cref="Transform"/> places it.</summary>
/// <remarks>
/// What a level names in place of the meshes and materials a model file holds, which
/// <see cref="ModelRefSystem"/> spawns through the asset server. The spawned entities are not saved
/// with the scene, since the file brings them back. A file with animation clips, in the app
/// <c>InitWindow</c> built, is played instead, its first clip on a loop through an
/// <see cref="AnimatedModel"/> on a child of the entity, which is not saved either, so a character a
/// level places moves rather than standing at rest.
/// </remarks>
[SceneComponent]
public struct ModelRef
{
    /// <summary>The model file, from the asset folder (the program's <c>source</c> folder) or, as a <see cref="SceneRef"/>'s, from beside the program or the working directory.</summary>
    public string Path;
}

/// <summary>Spawns the model of every <see cref="ModelRef"/> that has not been, under its entity.</summary>
internal static class ModelRefSystem
{
    /// <summary>The system, for <see cref="Stage.PreUpdate"/>.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs)) return;
        world.TryGetResource<AssetServer>(out var server);
        List<(int Entity, string Path)>? pending = null;
        foreach (var (entity, model) in ecs.Query<ModelRef>())
            if (!ecs.Has<ModelRefSpawned>(entity) && !string.IsNullOrEmpty(model.Path))
                (pending ??= []).Add((entity, AssetPath(model.Path)));
        if (pending is null) return;

        foreach (var (entity, path) in pending)
        {
            ecs.Add(entity, new ModelRefSpawned());

            // The model is read on the asset server's workers, and whether it has clips to play is
            // decided once it has arrived (SceneSpawnSystem), so no file is read on the frame.
            if (server is null) continue;
            try
            {
                var handle = server.Load<SceneAsset>(path);
                ecs.Add(entity, new SpawnSceneRequest { Handle = handle });
                // The model is held while the entity naming it is, and let go some time after.
                if (world.TryGetResource<AssetRelease>(out var release)) release.HoldModel(ecs.Handle(entity), handle.Id);
            }
            catch (InvalidOperationException ex)
            {
                Log.Category("Engine.Scenes").Warn($"ModelRef: '{path}' cannot be loaded: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// A model's path as the asset server takes it, from the program's <c>source</c> folder. A path
    /// that names no file there is looked for as a <see cref="SceneRef"/>'s is, beside the program
    /// or from the working directory, so a level names its models and its prefabs alike, and a file
    /// written with paths from the asset folder loads as it did.
    /// </summary>
    internal static string AssetPath(string path)
    {
        var assets = System.IO.Path.Combine(AppContext.BaseDirectory, "source");
        if (File.Exists(System.IO.Path.Combine(assets, path))) return path;
        return SceneRefSystem.Resolve(path) is { } file ? System.IO.Path.GetRelativePath(assets, System.IO.Path.GetFullPath(file)) : path;
    }
}

/// <summary>Marks a <see cref="ModelRef"/> whose model has been asked for.</summary>
internal struct ModelRefSpawned;
