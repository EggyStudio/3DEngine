using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>
/// An entity's id in a scene file, which stays the same when the entity is renamed, so other
/// entities and other files can refer to it.
/// </summary>
/// <remarks>Given to an entity the first time it is saved, and read back with it.</remarks>
internal struct SceneId
{
    /// <summary>The id, unique within a scene.</summary>
    public string Value;
}

/// <summary>
/// Saves entities to a JSON scene file and loads them back: each with its id, name, parent,
/// model and the components registered with <see cref="SceneComponents"/>.
/// </summary>
/// <remarks>
/// <para>
/// A level, as a game keeps one. Entities spawned from a model file or another scene file (those
/// with a <see cref="SceneInstance"/>) are not saved, since the <see cref="ModelRef"/> or
/// <see cref="SceneRef"/> that spawned them brings them back, and neither are components no codec is registered for. An entity is given a
/// <see cref="SceneId"/> when it is first saved, which other entities' fields and parents refer to
/// it by, and which survives a rename.
/// </para>
/// <para>
/// Loading spawns every entity first and reads components after, so a field may refer to an
/// entity later in the file. A component the running program has no codec for is skipped with a
/// warning, so a file outlives a component type being removed.
/// </para>
/// </remarks>
public static partial class SceneFile
{
    /// <summary>The value of the file's <c>format</c> key.</summary>
    public const string Format = "3dengine-scene";

    /// <summary>The version this code writes.</summary>
    public const int Version = 1;

    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    /// <summary>Writes a scene file of <paramref name="entities"/>, or of every entity not spawned from a model.</summary>
    public static void Save(EcsWorld ecs, string path, IEnumerable<int>? entities = null) =>
        File.WriteAllText(path, Write(ecs, entities));

    /// <summary>The JSON of a scene file of <paramref name="entities"/>, or of every entity not spawned from a model.</summary>
    public static string Write(EcsWorld ecs, IEnumerable<int>? entities = null)
    {
        var saved = (entities ?? ecs.AllEntities().Where(e => !ecs.Has<SceneInstance>(e))).Distinct().ToArray();

        // Ids first, so a field or a parent can name an entity written after it.
        foreach (var entity in saved)
            if (!ecs.Has<SceneId>(entity))
                ecs.Add(entity, new SceneId { Value = Guid.NewGuid().ToString("N")[..12] });

        var context = new SceneWriteContext(ecs);
        var codecs = SceneComponents.All
            .Where(c => c.Type != typeof(SceneId) && c.Type != typeof(Name) && c.Type != typeof(Parent))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToArray();

        using var buffer = new MemoryStream();
        // The relaxed encoder writes characters such as the + of a nested type's name as they are
        // rather than as \u escapes, which a file a person reads is better without. It still
        // escapes what JSON needs escaped.
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
               {
                   Indented = true,
                   Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
               }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("version", Version);
            writer.WriteStartArray("entities");
            foreach (var entity in saved)
            {
                writer.WriteStartObject();
                writer.WriteString("id", ecs.GetReadOnly<SceneId>(entity).Value);
                if (ecs.TryGet<Name>(entity, out var name)) writer.WriteString("name", name.Value);
                var parent = ecs.ParentOf(entity);
                if (parent != 0 && ecs.TryGet<SceneId>(parent, out var parentId)) writer.WriteString("parent", parentId.Value);

                writer.WriteStartObject("components");
                foreach (var codec in codecs)
                {
                    if (!codec.Has(ecs, entity)) continue;
                    writer.WriteStartObject(SceneComponents.KeyOf(codec));
                    codec.Write(writer, ecs, entity, context);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        // An indented writer puts every number of a vector on a line of its own, so arrays of
        // numbers are folded back onto one line, which is how a person reads a position.
        return NumberArray().Replace(Encoding.UTF8.GetString(buffer.ToArray()),
            m => "[" + string.Join(", ", m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries)) + "]");
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\[\s*([-0-9.eE+]+(?:\s*,\s*[-0-9.eE+]+)*)\s*\]")]
    private static partial System.Text.RegularExpressions.Regex NumberArray();

    /// <summary>Loads a scene file into the world's ECS and returns the entities it spawned.</summary>
    /// <exception cref="InvalidDataException">The file is not a scene file this code reads.</exception>
    public static List<int> Load(World world, string path) => Read(world, File.ReadAllText(path));

    /// <summary>Spawns the entities of a scene file's JSON into the world's ECS and returns them.</summary>
    /// <exception cref="InvalidDataException">The text is not a scene file this code reads.</exception>
    public static List<int> Read(World world, string json) => Read(world, json, null);

    // The same, leaving out each component for which skip answers true by the index of its
    // entity in the file and the component's name, which a copy of a prefab whose mesh is shared
    // with the copies before it uses.
    internal static List<int> Read(World world, string json, Func<int, string, bool>? skip)
    {
        using var document = JsonDocument.Parse(json);
        return Read(world, document.RootElement, skip);
    }

    // The same from a file already parsed, which a prefab placed many times is parsed into once.
    internal static List<int> Read(World world, JsonElement root, Func<int, string, bool>? skip)
    {
        var ecs = world.Resource<EcsWorld>();
        world.TryGetResource<AssetServer>(out var assets);
        if (!root.TryGetProperty("format", out var format) || format.GetString() != Format)
            throw new InvalidDataException($"Not a scene file: its \"format\" is not \"{Format}\".");
        if (root.TryGetProperty("version", out var version) && version.GetInt32() > Version)
            throw new InvalidDataException($"The scene file is version {version.GetInt32()}, and this engine reads up to {Version}.");

        var entries = root.GetProperty("entities").EnumerateArray().ToArray();
        var byId = new Dictionary<string, int>(StringComparer.Ordinal);
        var spawned = new List<int>(entries.Length);
        foreach (var entry in entries)
        {
            var entity = ecs.Spawn();
            spawned.Add(entity);
            if (entry.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } value)
            {
                byId[value] = entity;
                ecs.Add(entity, new SceneId { Value = value });
            }
            if (entry.TryGetProperty("name", out var name) && name.GetString() is { } text) ecs.SetName(entity, text);
        }

        var context = new SceneReadContext(ecs, byId, assets);
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.TryGetProperty("parent", out var parent) && parent.GetString() is { } parentId && byId.TryGetValue(parentId, out var parentEntity))
                ecs.SetParent(spawned[i], parentEntity);

            if (!entry.TryGetProperty("components", out var components)) continue;
            foreach (var component in components.EnumerateObject())
            {
                if (skip?.Invoke(i, component.Name) == true) continue;
                if (SceneComponents.Find(component.Name) is not { } codec)
                {
                    Logger.Warn($"Scene file: no single component is called '{component.Name}', so it is skipped. "
                                + "A name two component types share is written as a full name.");
                    continue;
                }
                codec.Read(component.Value, ecs, spawned[i], context);
            }
        }

        Logger.Debug($"Scene file: spawned {spawned.Count} entit{(spawned.Count == 1 ? "y" : "ies")}.");
        return spawned;
    }
}
