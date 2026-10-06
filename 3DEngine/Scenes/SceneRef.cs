using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>
/// A scene file an entity holds a copy of, spawned under the entity so its <see cref="Transform"/>
/// places it, as a prefab: a door, a lamp post or a room made once and placed many times.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SceneRefSystem"/> spawns the file the first frame it sees the component. The spawned
/// entities carry a <see cref="SceneInstance"/>, so a level saved with the reference in it is saved
/// without them, since the file brings them back, and they drop the file's own ids, so two copies
/// of one file name no entity twice. A file whose entities hold a <see cref="SceneRef"/> of their
/// own spawns those too, eight deep at most, so a file that names itself stops.
/// </para>
/// <para>
/// The path is a file beside the program, in its <c>source</c> folder, or from the working
/// directory, as the flat API's <c>Load</c> functions find files.
/// </para>
/// </remarks>
[SceneComponent]
public struct SceneRef
{
    /// <summary>The scene file to spawn under the entity.</summary>
    public string Path;
}

/// <summary>Marks a <see cref="SceneRef"/> whose file has been spawned, or tried, with the file and when it was written.</summary>
internal struct SceneRefSpawned
{
    /// <summary>The file spawned, as it was found, or null when none was.</summary>
    public string? File;

    /// <summary>When the file was last written as it was spawned, which a later write differs from.</summary>
    public DateTime Written;
}

/// <summary>Spawns the scene file of every <see cref="SceneRef"/> that has not been, under its entity.</summary>
internal static class SceneRefSystem
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    /// <summary>How many references deep a file may spawn files of its own.</summary>
    public const int MaxDepth = 8;

    // When the files were last looked at, so a level of many references asks the file system twice
    // a second rather than every frame.
    private static long _lastCheck;

    /// <summary>The system, for <see cref="Stage.PreUpdate"/>.</summary>
    /// <remarks>
    /// A file written since it was spawned, as one saved in another tool or by <c>scene.save</c>, is
    /// spawned again in place of what it spawned before, so a level shows an edited prefab as it
    /// runs. What the program changed of the old copy is lost with it.
    /// </remarks>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<SceneRef>() == 0) return;
        if (System.Diagnostics.Stopwatch.GetElapsedTime(_lastCheck).TotalSeconds >= 0.5)
        {
            _lastCheck = System.Diagnostics.Stopwatch.GetTimestamp();
            ReloadChanged(ecs);
        }
        // Repeated, so the references a spawned file holds spawn in the same frame.
        for (int pass = 0; pass <= MaxDepth; pass++)
        {
            List<(int Entity, string Path)>? pending = null;
            foreach (var (entity, reference) in ecs.Query<SceneRef>())
                if (!ecs.Has<SceneRefSpawned>(entity))
                    (pending ??= []).Add((entity, reference.Path ?? ""));
            if (pending is null) return;
            foreach (var (entity, path) in pending) Spawn(world, ecs, entity, path);
        }
    }

    // Despawns what each reference whose file was written since spawned, and marks it to be
    // spawned again by the pass after.
    internal static void ReloadChanged(EcsWorld ecs)
    {
        // Each file asked once, however many copies of it are placed.
        List<int>? changed = null;
        var written = new Dictionary<string, DateTime?>();
        foreach (var (entity, spawned) in ecs.Query<SceneRefSpawned>())
        {
            if (spawned.File is not { } file) continue;
            if (!written.TryGetValue(file, out var now))
                written[file] = now = System.IO.File.Exists(file) ? System.IO.File.GetLastWriteTimeUtc(file) : null;
            if (now is { } time && time != spawned.Written) (changed ??= []).Add(entity);
        }
        if (changed is null) return;

        foreach (var entity in changed)
        {
            var path = ecs.GetReadOnly<SceneRef>(entity).Path;
            foreach (var child in ecs.ChildrenOf(entity).ToArray())
                if (ecs.TryGet<SceneInstance>(child, out var instance) && instance.SourcePath == path)
                    ecs.DespawnRecursive(child);
            ecs.Remove<SceneRefSpawned>(entity);
            Logger.Info($"SceneRef: '{path}' was written since it was spawned, so it is spawned again.");
        }
    }

    private static void Spawn(World world, EcsWorld ecs, int entity, string path)
    {
        ecs.Add(entity, new SceneRefSpawned());
        if (path.Length == 0) return;
        if (Depth(ecs, entity) >= MaxDepth)
        {
            Logger.Warn($"SceneRef: '{path}' is {MaxDepth} references deep, which a file naming itself reaches, so it is not spawned.");
            return;
        }
        if (Resolve(path) is not { } file)
        {
            Logger.Warn($"SceneRef: '{path}' was not found beside the program, in its source folder or in the working directory.");
            return;
        }

        // Copies of one version of a file share it parsed and its meshes, so a prefab placed many
        // times is read and parsed once and is one upload drawn as instances, rather than a parse,
        // an upload and a draw for each copy.
        var written = File.GetLastWriteTimeUtc(file);
        List<int> spawned;
        try
        {
            ecs.GetRef<SceneRefSpawned>(entity) = new SceneRefSpawned { File = file, Written = written };
            if (world.TryGetResource<AssetRelease>(out var release)) release.HoldFile(ecs.Handle(entity), file);
            if (!Versions.TryGetValue(file, out var version) || version.Written != written)
            {
                var parsed = JsonDocument.Parse(File.ReadAllText(file));
                version?.Document.Dispose();
                Versions[file] = version = new Version(written, parsed);
            }
            var shared = version.Meshes;
            spawned = SceneFile.Read(world, version.Document.RootElement, (index, component) => component == "Mesh" && index < shared.Count && shared[index] is not null);
            for (int i = 0; i < spawned.Count; i++)
            {
                if (i < shared.Count && shared[i] is { } mesh) ecs.Add(spawned[i], mesh);
                else if (ecs.TryGet<Mesh>(spawned[i], out var read))
                {
                    while (shared.Count <= i) shared.Add(null);
                    shared[i] = read;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        {
            Logger.Warn($"SceneRef: '{path}' cannot be read: {ex.Message}");
            return;
        }

        foreach (var child in spawned)
        {
            ecs.Remove<SceneId>(child);
            ecs.Add(child, new SceneInstance { SourcePath = path });
            if (ecs.ParentOf(child) == 0) ecs.SetParent(child, entity);
        }
    }

    // The version of each file last spawned, parsed, and the meshes its first copy read, by the
    // place of their entity in the file.
    private sealed record Version(DateTime Written, JsonDocument Document)
    {
        public List<Mesh?> Meshes { get; } = [];
    }

    private static readonly Dictionary<string, Version> Versions = [];

    // Whether a file's parsed copy is kept, for the tests.
    internal static bool IsParsed(string file) => Versions.ContainsKey(file);

    /// <summary>Drops a file's parsed copy and the meshes its copies shared, once no entity spawned from it is left.</summary>
    /// <returns>Whether the file was held.</returns>
    internal static bool Forget(string file)
    {
        if (!Versions.Remove(file, out var version)) return false;
        version.Document.Dispose();
        return true;
    }

    // How many of the entity and its ancestors were spawned by a reference of their own.
    private static int Depth(EcsWorld ecs, int entity)
    {
        var depth = 0;
        for (var at = entity; at != 0; at = ecs.ParentOf(at))
            if (ecs.Has<SceneInstance>(at) && ecs.Has<SceneRef>(at)) depth++;
        return depth;
    }

    internal static string? Resolve(string path)
    {
        if (File.Exists(path)) return path;
        foreach (var root in new[] { AppContext.BaseDirectory, System.IO.Path.Combine(AppContext.BaseDirectory, "source") })
        {
            var candidate = System.IO.Path.Combine(root, path);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
