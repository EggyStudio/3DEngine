using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>
/// Marks a component struct as one a scene file saves and loads. Its public fields of the types
/// <see cref="SceneFile"/> knows are written by name, and the rest are left out.
/// </summary>
/// <remarks>
/// The generator writes the code that saves and loads it, so no reflection runs when a scene is
/// read. A <c>[Behavior]</c> struct is saved the same way without the attribute, since its fields
/// are its state. A field's value when it is missing from a file is the component's static
/// <c>Default</c> or <c>Identity</c> when it has one, and zero otherwise.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class SceneComponentAttribute : Attribute;

/// <summary>
/// An entity's id in a scene file, which stays the same when the entity is renamed, so other
/// entities and other files can refer to it.
/// </summary>
/// <remarks>Given to an entity the first time it is saved, and read back with it.</remarks>
public struct SceneId
{
    /// <summary>The id, unique within a scene.</summary>
    public string Value;
}

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
public static class ModelRefSystem
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

            // A file with clips plays its first through an AnimatedModel on a child, where its
            // meshes spawned as entities would stand at rest. The child carries a SceneInstance,
            // so a level saved with the reference is saved without it, as spawned meshes are.
            if (Engine3D.Holds(world) && Engine3D.HasAnimations(path))
            {
                var child = ecs.Spawn();
                ecs.Add(child, new AnimatedModel(path));
                ecs.Add(child, new Transform(Vector3.Zero));
                ecs.Add(child, new SceneInstance { SourcePath = path });
                ecs.SetParent(child, entity);
                continue;
            }

            if (server is null) continue;
            try
            {
                ecs.Add(entity, new SpawnSceneRequest { Handle = server.Load<SceneAsset>(path) });
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
public struct ModelRefSpawned;

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
public struct SceneRefSpawned
{
    /// <summary>The file spawned, as it was found, or null when none was.</summary>
    public string? File;

    /// <summary>When the file was last written as it was spawned, which a later write differs from.</summary>
    public DateTime Written;
}

/// <summary>Spawns the scene file of every <see cref="SceneRef"/> that has not been, under its entity.</summary>
public static class SceneRefSystem
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
        List<int>? changed = null;
        foreach (var (entity, spawned) in ecs.Query<SceneRefSpawned>())
            if (spawned.File is { } file && System.IO.File.Exists(file) && System.IO.File.GetLastWriteTimeUtc(file) != spawned.Written)
                (changed ??= []).Add(entity);
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

        List<int> spawned;
        try
        {
            ecs.GetRef<SceneRefSpawned>(entity) = new SceneRefSpawned { File = file, Written = File.GetLastWriteTimeUtc(file) };
            spawned = SceneFile.Read(world, File.ReadAllText(file));
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or IOException)
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

/// <summary>Saves a component of one type into a scene file's entry and loads it back.</summary>
public interface ISceneCodec
{
    /// <summary>The key the component is written under, its type's name.</summary>
    string Name { get; }

    /// <summary>The component type.</summary>
    Type Type { get; }

    /// <summary>Whether the entity has the component.</summary>
    bool Has(EcsWorld ecs, int entity);

    /// <summary>Writes the entity's component's fields into the object the caller has opened for it.</summary>
    void Write(Utf8JsonWriter writer, EcsWorld ecs, int entity, SceneWriteContext context);

    /// <summary>Reads a component from an object of its fields and adds it to the entity, replacing one it has.</summary>
    void Read(JsonElement element, EcsWorld ecs, int entity, SceneReadContext context);
}

/// <summary>A codec made of the two functions the generator writes for a component type.</summary>
public sealed class SceneCodec<T>(string name, SceneCodec<T>.Writer write, SceneCodec<T>.Reader read) : ISceneCodec
{
    /// <summary>Writes a component's fields into the open object.</summary>
    public delegate void Writer(Utf8JsonWriter writer, in T value, SceneWriteContext context);

    /// <summary>Reads a component from an object of its fields.</summary>
    public delegate T Reader(JsonElement element, SceneReadContext context);

    /// <inheritdoc />
    public string Name { get; } = name;

    /// <inheritdoc />
    public Type Type => typeof(T);

    /// <inheritdoc />
    public bool Has(EcsWorld ecs, int entity) => ecs.Has<T>(entity);

    /// <inheritdoc />
    public void Write(Utf8JsonWriter writer, EcsWorld ecs, int entity, SceneWriteContext context)
    {
        if (ecs.TryGet<T>(entity, out var value)) write(writer, value!, context);
    }

    /// <inheritdoc />
    public void Read(JsonElement element, EcsWorld ecs, int entity, SceneReadContext context)
    {
        var value = read(element, context);
        if (ecs.Has<T>(entity)) ecs.Update(entity, value);
        else ecs.Add(entity, value);
    }
}

/// <summary>The component types a scene file can hold, registered by the generated code of each assembly.</summary>
/// <remarks>
/// A component is written under its type's name, and under its full name when another registered
/// type has the same name, so neither is read back as the other whichever registered first.
/// </remarks>
public static class SceneComponents
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, ISceneCodec> ByFullName = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<ISceneCodec>> ByName = new(StringComparer.Ordinal);

    /// <summary>Registers a component type's codec. Registering the same type again replaces it.</summary>
    public static void Add(ISceneCodec codec)
    {
        lock (Gate)
        {
            var fullName = FullName(codec);
            if (ByFullName.Remove(fullName, out var replaced)) ByName[replaced.Name].Remove(replaced);
            ByFullName[fullName] = codec;
            if (!ByName.TryGetValue(codec.Name, out var named)) ByName[codec.Name] = named = [];
            named.Add(codec);
        }
    }

    /// <summary>Every registered codec.</summary>
    public static IReadOnlyList<ISceneCodec> All
    {
        get { lock (Gate) return ByFullName.Values.ToArray(); }
    }

    /// <summary>
    /// The codec written under <paramref name="key"/>, which is a type's full name, or a name that
    /// one of the engine's own types has or that no other registered type shares. Null for none, or
    /// for a name more than one of a program's types has.
    /// </summary>
    public static ISceneCodec? Find(string key)
    {
        lock (Gate)
        {
            if (ByFullName.TryGetValue(key, out var exact)) return exact;
            if (!ByName.TryGetValue(key, out var named)) return null;
            return EngineOwner(named) ?? (named.Count == 1 ? named[0] : null);
        }
    }

    /// <summary>
    /// The key a codec's components are written under. That is its name, unless another type shares
    /// the name and the name is not this codec's as one of the engine's own types, then its full name.
    /// </summary>
    /// <remarks>
    /// An engine type keeps its name whatever a program registers, so a level saved with the
    /// engine's <c>Light</c> under <c>Light</c> still loads its lights once the game adds a
    /// <c>Light</c> of its own, which is written by its full name instead.
    /// </remarks>
    public static string KeyOf(ISceneCodec codec)
    {
        lock (Gate)
        {
            if (!ByName.TryGetValue(codec.Name, out var named) || named.Count == 1) return codec.Name;
            return ReferenceEquals(EngineOwner(named), codec) ? codec.Name : FullName(codec);
        }
    }

    // The one engine type among the types with a name, or null when there is none or more than one.
    private static ISceneCodec? EngineOwner(List<ISceneCodec> named)
    {
        ISceneCodec? owner = null;
        foreach (var codec in named)
        {
            if (codec.Type.Assembly != typeof(SceneComponents).Assembly) continue;
            if (owner is not null) return null;
            owner = codec;
        }
        return owner;
    }

    private static string FullName(ISceneCodec codec) => codec.Type.FullName ?? codec.Name;
}

/// <summary>What a component's writer asks of the scene being saved.</summary>
public sealed class SceneWriteContext
{
    private readonly EcsWorld _ecs;

    internal SceneWriteContext(EcsWorld ecs) => _ecs = ecs;

    /// <summary>The scene id of the entity a field refers to, or null for none or one that is gone.</summary>
    public string? IdOf(Entity entity) =>
        _ecs.TryResolve(entity, out var id) && _ecs.TryGet<SceneId>(id, out var sceneId) ? sceneId.Value : null;

    /// <summary>The path of the asset a handle names, or null for none.</summary>
    public string? PathOf<TAsset>(Handle<TAsset> handle) => handle.IsValid ? handle.Path.ToString() : null;
}

/// <summary>What a component's reader asks of the scene being loaded.</summary>
public sealed class SceneReadContext
{
    private readonly EcsWorld _ecs;
    private readonly Dictionary<string, int> _entities;
    private readonly AssetServer? _assets;

    internal SceneReadContext(EcsWorld ecs, Dictionary<string, int> entities, AssetServer? assets)
    {
        _ecs = ecs;
        _entities = entities;
        _assets = assets;
    }

    /// <summary>The entity a scene id names, or none when the scene has no such entity.</summary>
    public Entity Resolve(string? id) =>
        id is not null && _entities.TryGetValue(id, out var entity) ? _ecs.Handle(entity) : default;

    /// <summary>A handle to the asset at a path, loading it, or none without a path or an asset server.</summary>
    public Handle<TAsset> Load<TAsset>(string? path) =>
        string.IsNullOrEmpty(path) || _assets is null ? default : _assets.Load<TAsset>(path);
}

/// <summary>The JSON forms of the values scene files hold, which the generated code calls.</summary>
public static class SceneJson
{
    /// <summary>
    /// Writes a vector as an array of its components, as an element of the open array when
    /// <paramref name="name"/> is null.
    /// </summary>
    public static void Write(Utf8JsonWriter writer, string? name, Vector2 v) => Floats(writer, name, v.X, v.Y);

    /// <inheritdoc cref="Write(Utf8JsonWriter, string, Vector2)"/>
    public static void Write(Utf8JsonWriter writer, string? name, Vector3 v) => Floats(writer, name, v.X, v.Y, v.Z);

    /// <inheritdoc cref="Write(Utf8JsonWriter, string, Vector2)"/>
    public static void Write(Utf8JsonWriter writer, string? name, Vector4 v) => Floats(writer, name, v.X, v.Y, v.Z, v.W);

    /// <summary>Writes a rotation as [x, y, z, w].</summary>
    public static void Write(Utf8JsonWriter writer, string? name, Quaternion q) => Floats(writer, name, q.X, q.Y, q.Z, q.W);

    /// <summary>Writes a matrix as its sixteen numbers, row by row.</summary>
    public static void Write(Utf8JsonWriter writer, string? name, Matrix4x4 m) =>
        Floats(writer, name, m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

    /// <summary>Writes a color as [r, g, b, a], each from 0 to 255.</summary>
    public static void Write(Utf8JsonWriter writer, string? name, Color c)
    {
        Start(writer, name);
        writer.WriteNumberValue(c.R);
        writer.WriteNumberValue(c.G);
        writer.WriteNumberValue(c.B);
        writer.WriteNumberValue(c.A);
        writer.WriteEndArray();
    }

    /// <summary>Reads a vector written by <see cref="Write(Utf8JsonWriter, string, Vector2)"/>.</summary>
    public static Vector2 ReadVector2(JsonElement e) => new(At(e, 0), At(e, 1));

    /// <summary>Reads a vector written by <see cref="Write(Utf8JsonWriter, string, Vector3)"/>.</summary>
    public static Vector3 ReadVector3(JsonElement e) => new(At(e, 0), At(e, 1), At(e, 2));

    /// <summary>Reads a vector written by <see cref="Write(Utf8JsonWriter, string, Vector4)"/>.</summary>
    public static Vector4 ReadVector4(JsonElement e) => new(At(e, 0), At(e, 1), At(e, 2), At(e, 3));

    /// <summary>Reads a rotation written by <see cref="Write(Utf8JsonWriter, string, Quaternion)"/>.</summary>
    public static Quaternion ReadQuaternion(JsonElement e) => new(At(e, 0), At(e, 1), At(e, 2), At(e, 3));

    /// <summary>Reads a matrix written by <see cref="Write(Utf8JsonWriter, string, Matrix4x4)"/>.</summary>
    public static Matrix4x4 ReadMatrix4x4(JsonElement e) => new(
        At(e, 0), At(e, 1), At(e, 2), At(e, 3), At(e, 4), At(e, 5), At(e, 6), At(e, 7),
        At(e, 8), At(e, 9), At(e, 10), At(e, 11), At(e, 12), At(e, 13), At(e, 14), At(e, 15));

    /// <summary>Reads a color written by <see cref="Write(Utf8JsonWriter, string, Color)"/>.</summary>
    public static Color ReadColor(JsonElement e) =>
        new((byte)At(e, 0), (byte)At(e, 1), (byte)At(e, 2), e.GetArrayLength() > 3 ? (byte)At(e, 3) : (byte)255);

    private static void Floats(Utf8JsonWriter writer, string? name, params ReadOnlySpan<float> values)
    {
        Start(writer, name);
        foreach (var v in values) writer.WriteNumberValue(v);
        writer.WriteEndArray();
    }

    private static void Start(Utf8JsonWriter writer, string? name)
    {
        if (name is null) writer.WriteStartArray();
        else writer.WriteStartArray(name);
    }

    private static float At(JsonElement e, int index) => index < e.GetArrayLength() ? e[index].GetSingle() : 0f;
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
    public static List<int> Read(World world, string json)
    {
        var ecs = world.Resource<EcsWorld>();
        world.TryGetResource<AssetServer>(out var assets);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
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
                if (SceneComponents.Find(component.Name) is not { } codec)
                {
                    Logger.Warn($"Scene file: no single component is called '{component.Name}', so it is skipped. "
                                + "A name two component types share is written as a full name.");
                    continue;
                }
                codec.Read(component.Value, ecs, spawned[i], context);
            }
        }

        Logger.Info($"Scene file: spawned {spawned.Count} entit{(spawned.Count == 1 ? "y" : "ies")}.");
        return spawned;
    }
}
