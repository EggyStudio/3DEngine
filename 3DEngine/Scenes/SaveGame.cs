using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>
/// Saves a game as what play changed over the scene files it started from, and reads it back by
/// loading those files again and laying the save over them.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Start"/> loads the scenes a game starts from and keeps each of their entities'
/// components as loaded. <see cref="Save"/> then writes the scenes' paths and, for each of their
/// entities, only the components play changed, put on or took off, keyed by the scene and the id
/// the entity has in it, which stays the same from one run to the next. An entity of theirs play
/// despawned is written as removed, and one the game spawned with <see cref="Saved"/> is written
/// whole. Entities a <see cref="SceneRef"/> or a <see cref="ModelRef"/> placed are left out, as a
/// scene file leaves them, since what placed them places them again.
/// </para>
/// <para>
/// <see cref="Load"/> ends the game in progress, loads the scenes again and lays the save over
/// them, so what the save does not touch is as the files have it now, and a level fixed after the
/// save was written shows the fix wherever play had not changed the same component. Each component
/// of each entity is then told as a <see cref="Loaded{T}"/>, and the save as one
/// <see cref="SceneLoaded"/> naming it, once over the whole. The game in progress is a resource of
/// the world, so each world keeps its own.
/// </para>
/// <para>
/// An entity is named by its id alone where a component refers to it, so the scenes of one game
/// keep their ids apart, as files <see cref="SceneFile.Save"/> wrote do with ids it made.
/// </para>
/// </remarks>
public static class SaveGame
{
    /// <summary>The value of a save file's <c>format</c> key.</summary>
    public const string Format = "3dengine-save";

    /// <summary>The version this code writes.</summary>
    public const int Version = 1;

    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    // The game in progress: the scenes it started from, each of their entities by its scene and id,
    // and the components each held as loaded, written as a save writes them.
    private sealed class Game
    {
        public readonly List<string> Scenes = [];
        public readonly Dictionary<(int Scene, string Id), Entity> FromScenes = [];
        public readonly Dictionary<(int Scene, string Id), Dictionary<string, string>> AsLoaded = [];
    }

    /// <summary>The scenes the world's game in progress started from, or none where none has.</summary>
    public static IReadOnlyList<string> ScenesOf(World world) =>
        world.TryGetResource<Game>(out var game) ? [.. game.Scenes] : [];

    /// <summary>Starts a game from scene files, loading each in order and keeping what their entities hold.</summary>
    /// <returns>Every entity the files spawned, in their order.</returns>
    public static List<int> Start(World world, params string[] scenes)
    {
        ArgumentNullException.ThrowIfNull(world);
        var spawned = new List<int>();
        var game = new Game();
        for (int i = 0; i < scenes.Length; i++)
        {
            var loaded = SceneFile.Load(world, scenes[i]);
            Keep(world, game, i, loaded);
            spawned.AddRange(loaded);
        }
        game.Scenes.AddRange(scenes);
        world.InsertResource(game);
        return spawned;
    }

    /// <summary>Writes what play changed over the scenes the world's game started from.</summary>
    /// <returns>How many entities the save holds.</returns>
    /// <exception cref="InvalidOperationException">No game was started in the world.</exception>
    public static int Save(World world, string path)
    {
        var game = world.TryGetResource<Game>(out var started) ? started
            : throw new InvalidOperationException("SaveGame.Save: no game was started in this world. SaveGame.Start starts one.");
        var ecs = world.Resource<EcsWorld>();
        var context = new SceneWriteContext(ecs);
        var codecs = Codecs();
        var fromScenes = new HashSet<int>();

        // Ids for what the game spawned, given before anything is written so a field names one
        // written after it.
        foreach (var (entity, _) in ecs.Query<Saved>())
            if (!ecs.Has<SceneId>(entity)) ecs.Add(entity, new SceneId { Value = Guid.NewGuid().ToString("N")[..12] });

        var buffer = new ArrayBufferWriter<byte>();
        var written = 0;
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteString("format", Format);
            json.WriteNumber("version", Version);
            json.WriteStartArray("scenes");
            foreach (var scene in game.Scenes) json.WriteStringValue(scene);
            json.WriteEndArray();
            json.WriteStartArray("entities");

            foreach (var ((scene, id), handle) in game.FromScenes)
            {
                if (!ecs.TryResolve(handle, out var entity))
                {
                    Begin(json, scene, id);
                    json.WriteBoolean("removed", true);
                    json.WriteEndObject();
                    written++;
                    continue;
                }
                fromScenes.Add(entity);
                var loaded = game.AsLoaded[(scene, id)];
                var began = false;
                foreach (var codec in codecs)
                {
                    var key = SceneComponents.KeyOf(codec);
                    var now = codec.Has(ecs, entity) ? Written(codec, ecs, entity, context) : null;
                    var was = loaded.GetValueOrDefault(key);
                    if (now == was) continue;
                    if (!began)
                    {
                        Begin(json, scene, id);
                        json.WriteStartObject("components");
                        began = true;
                    }
                    json.WritePropertyName(key);
                    if (now is null) json.WriteNullValue();
                    else json.WriteRawValue(now);
                }
                if (!began) continue;
                json.WriteEndObject();
                json.WriteEndObject();
                written++;
            }

            foreach (var (entity, _) in ecs.Query<Saved>())
            {
                if (fromScenes.Contains(entity) || ecs.Has<SceneInstance>(entity)) continue;
                json.WriteStartObject();
                json.WriteString("id", ecs.GetReadOnly<SceneId>(entity).Value);
                if (ecs.TryGet<Name>(entity, out var name)) json.WriteString("name", name.Value);
                var parent = ecs.ParentOf(entity);
                if (parent != 0 && ecs.TryGet<SceneId>(parent, out var above)) json.WriteString("parent", above.Value);
                json.WriteStartObject("components");
                foreach (var codec in codecs)
                {
                    if (!codec.Has(ecs, entity)) continue;
                    json.WritePropertyName(SceneComponents.KeyOf(codec));
                    json.WriteRawValue(Written(codec, ecs, entity, context));
                }
                json.WriteEndObject();
                json.WriteEndObject();
                written++;
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        // Written beside the old save and moved over it, so a save cut short leaves the one before.
        var full = Path.GetFullPath(path);
        if (Path.GetDirectoryName(full) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var temporary = full + ".writing";
        File.WriteAllBytes(temporary, buffer.WrittenSpan.ToArray());
        File.Move(temporary, full, overwrite: true);
        return written;
    }

    /// <summary>
    /// Ends the game in progress, loads the scenes a save names again and lays the save over them.
    /// </summary>
    /// <returns>Every entity the scenes and the save spawned that is still there, in their order.</returns>
    /// <exception cref="InvalidDataException">The file is not a save this code reads, which leaves the game in progress as it was.</exception>
    public static List<int> Load(World world, string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("format", out var format) || format.GetString() != Format)
            throw new InvalidDataException($"Not a saved game: its \"format\" is not \"{Format}\".");
        if (root.TryGetProperty("version", out var version) && version.GetInt32() > Version)
            throw new InvalidDataException($"The saved game is version {version.GetInt32()}, and this engine reads up to {Version}.");

        var ecs = world.Resource<EcsWorld>();
        End(world, ecs);

        var scenes = root.TryGetProperty("scenes", out var named) ? named.EnumerateArray().Select(s => s.GetString() ?? "").ToArray() : [];
        var game = new Game();
        game.Scenes.AddRange(scenes);
        var spawned = new List<int>();
        for (int i = 0; i < scenes.Length; i++)
        {
            var loaded = SceneFile.Load(world, scenes[i], tell: false);
            Keep(world, game, i, loaded);
            spawned.AddRange(loaded);
        }
        world.InsertResource(game);

        // An entity named by its id alone where a component refers to it, the scenes' first and
        // then what the game spawned.
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ((_, id), handle) in game.FromScenes)
            if (ecs.TryResolve(handle, out var entity)) byId.TryAdd(id, entity);

        var entries = root.TryGetProperty("entities", out var listed) ? listed.EnumerateArray().ToArray() : [];
        var targets = new int[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var id = entry.TryGetProperty("id", out var text) ? text.GetString() ?? "" : "";
            if (entry.TryGetProperty("scene", out var scene))
            {
                targets[i] = game.FromScenes.TryGetValue((scene.GetInt32(), id), out var handle) && ecs.TryResolve(handle, out var entity) ? entity : 0;
                continue;
            }
            var made = ecs.Spawn();
            ecs.Add(made, new SceneId { Value = id });
            ecs.Add(made, default(Saved));
            if (entry.TryGetProperty("name", out var name) && name.GetString() is { } called) ecs.SetName(made, called);
            byId[id] = made;
            targets[i] = made;
            spawned.Add(made);
        }

        world.TryGetResource<AssetServer>(out var assets);
        var context = new SceneReadContext(ecs, byId, assets);
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var entity = targets[i];
            if (entity == 0) continue;
            if (entry.TryGetProperty("removed", out var removed) && removed.ValueKind == JsonValueKind.True)
            {
                ecs.DespawnRecursive(entity);
                continue;
            }
            if (entry.TryGetProperty("parent", out var parent) && parent.GetString() is { } parentId && byId.TryGetValue(parentId, out var above))
                ecs.SetParent(entity, above);
            if (!entry.TryGetProperty("components", out var components)) continue;
            foreach (var component in components.EnumerateObject())
            {
                if (SceneComponents.Find(component.Name) is not { } codec)
                {
                    Logger.Warn($"Saved game: no single component is called '{component.Name}', so it is skipped.");
                    continue;
                }
                if (component.Value.ValueKind == JsonValueKind.Null) ecs.RemoveBoxed(entity, codec.Type);
                else codec.Read(component.Value, ecs, entity, context);
            }
        }

        spawned.RemoveAll(entity => !ecs.IsAlive(entity));
        SceneFile.TellLoaded(ecs, spawned, path);
        return spawned;
    }

    // Despawns the game in progress, what its scenes spawned and what the game spawned to be saved,
    // with what is below each.
    private static void End(World world, EcsWorld ecs)
    {
        var going = new List<int>();
        if (world.TryGetResource<Game>(out var game))
            foreach (var handle in game.FromScenes.Values)
                if (ecs.TryResolve(handle, out var entity)) going.Add(entity);
        foreach (var (entity, _) in ecs.Query<Saved>()) going.Add(entity);
        foreach (var entity in going)
            if (ecs.IsAlive(entity)) ecs.DespawnRecursive(entity);
    }

    // Keeps each entity a scene spawned by its scene and id, with its components as loaded.
    private static void Keep(World world, Game game, int scene, List<int> loaded)
    {
        var ecs = world.Resource<EcsWorld>();
        var context = new SceneWriteContext(ecs);
        var codecs = Codecs();
        foreach (var entity in loaded)
        {
            if (!ecs.TryGet<SceneId>(entity, out var id) || ecs.Has<SceneInstance>(entity)) continue;
            if (!game.FromScenes.TryAdd((scene, id.Value), ecs.Handle(entity))) continue;
            var components = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var codec in codecs)
                if (codec.Has(ecs, entity)) components[SceneComponents.KeyOf(codec)] = Written(codec, ecs, entity, context);
            game.AsLoaded[(scene, id.Value)] = components;
        }
    }

    // The codecs a save writes, every one a scene file writes but the id, the name and the parent,
    // which it writes apart, and the mark of what the game spawned.
    private static ISceneCodec[] Codecs() =>
        [.. SceneComponents.All
            .Where(c => c.Type != typeof(SceneId) && c.Type != typeof(Name) && c.Type != typeof(Parent) && c.Type != typeof(Saved))
            .OrderBy(c => c.Name, StringComparer.Ordinal)];

    // A component as the object a save holds it in, compact so two compare as text.
    private static string Written(ISceneCodec codec, EcsWorld ecs, int entity, SceneWriteContext context)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            codec.Write(json, ecs, entity, context);
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void Begin(Utf8JsonWriter json, int scene, string id)
    {
        json.WriteStartObject();
        json.WriteNumber("scene", scene);
        json.WriteString("id", id);
    }
}
