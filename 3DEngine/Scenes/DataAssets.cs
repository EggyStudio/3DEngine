using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine;

/// <summary>
/// Reads, shares and writes data files, which hold <see cref="DataAssetAttribute"/> types and
/// <see cref="AnimationStateMachine"/>s, and keeps the table of the ids they were given and where
/// the files are.
/// </summary>
/// <remarks>
/// <para>
/// A data file is JSON with a <c>format</c>, a <c>version</c>, its <c>id</c>, the full name of the
/// type it holds and the object of the type's fields, as a scene file holds a component. A
/// machine's file has its own format, with its id beside it.
/// </para>
/// <code>
/// { "format": "3dengine-data", "version": 1, "id": "3f9c0a1b2d4e", "type": "Game.Weapon",
///   "fields": { "Damage": 12, "Title": "Sword" } }
/// </code>
/// <para>
/// The id is given at the file's first save, as an entity's id in a scene file is, or the first
/// time a file written by hand is read by its name, and every later save keeps it, so it goes with
/// the file through a rename or a move. The table is filled by each file read or written, and an id
/// not in it, or whose file is gone or holds another id, is looked for through the folder beside
/// the program, <c>source/</c> under it included, the folder of the scene or data file naming it,
/// and each folder a data file was read from or written to, the start of each JSON file there read
/// for its id. A reference to a file renamed by
/// hand finds it so, and a look that reads more than <see cref="ManyFiles"/> files says how many.
/// </para>
/// <para>
/// A file is read once and its value shared by every reference to it. One that is missing, cannot
/// be read or holds another type is said once in the log, naming its id, and its references read as
/// none. A scene with no reference asks nothing of this class.
/// </para>
/// </remarks>
public static class DataAssets
{
    /// <summary>The value of a data file's <c>format</c> key.</summary>
    public const string Format = "3dengine-data";

    /// <summary>The version this code writes.</summary>
    public const int Version = 1;

    /// <summary>The number of files past which a look for an id says how many it read.</summary>
    internal const int ManyFiles = 500;

    private static readonly ILogger Logger = Log.Category("Engine.Scenes");
    private static readonly object Gate = new();

    // The world a file's entity fields are read against and written from, which has none, since a
    // file apart from a scene refers to no entity of one.
    private static readonly EcsWorld Detached = new();

    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly EnumerationOptions Everywhere = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    // The kinds by type, by the name a data file records, by a name a type had, and by the format
    // of a kind whose files have one of their own.
    private static readonly Dictionary<Type, Kind> ByType = [];
    private static readonly Dictionary<string, Kind> ByName = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Kind> ByFormerName = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Kind> ByFormat = new(StringComparer.Ordinal);

    // The table, each id's file by its full path and each file's id, the folders files were read
    // from or written to, and each id's value as each type it was asked for as, read once.
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> Ids = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Folders = new(StringComparer.Ordinal);
    private static readonly Dictionary<(string Id, Type Type), object> Values = [];

    /// <summary>
    /// How many times this thread asked for a file or a value, or looked through folders, which the
    /// test holding a scene with no reference to none reads.
    /// </summary>
    [ThreadStatic] internal static int Asked;

    static DataAssets()
    {
        var machine = new MachineKind();
        ByType[typeof(AnimationStateMachine)] = machine;
        ByFormat[AnimationStateMachine.Format] = machine;
    }

    /// <summary>
    /// Registers a data asset type's codec, which the generated code of each assembly does for each
    /// <see cref="DataAssetAttribute"/> type. Registering the same type again replaces it.
    /// </summary>
    public static void Add<T>(SceneCodec<T> codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        var kind = new CodecKind<T>(codec);
        lock (Gate)
        {
            ByType[typeof(T)] = kind;
            ByName[kind.Name] = kind;
            foreach (var former in codec.FormerNames) ByFormerName[former] = kind;
        }
    }

    /// <summary>
    /// Writes a value to a data file, keeping the id the file has or giving it one at its first
    /// save, and shares it with every reference to the file.
    /// </summary>
    /// <param name="fileName">The file, a relative name beside the program, where <see cref="Load{T}"/> finds it.</param>
    /// <param name="value">What the file holds.</param>
    /// <returns>A reference to the file.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a data asset.</exception>
    public static DataRef<T> Save<T>(string fileName, T value)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        var kind = KindOf<T>();
        var path = Path.GetFullPath(Path.IsPathRooted(fileName) ? fileName : Path.Combine(AppContext.BaseDirectory, fileName));
        lock (Gate)
        {
            Asked++;
            return new DataRef<T>(Write(kind, path, value, File.Exists(path) ? HeadId(path) : null));
        }
    }

    /// <summary>
    /// Writes a value over the file a reference names, found by its id wherever it was moved, and
    /// shares it with every reference to the file.
    /// </summary>
    /// <exception cref="ArgumentException">The reference names no file.</exception>
    /// <exception cref="FileNotFoundException">No data file holds the reference's id.</exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a data asset.</exception>
    public static void Save<T>(DataRef<T> reference, T value)
    {
        if (reference.Id is not { } id) throw new ArgumentException("The reference names no data file.", nameof(reference));
        var kind = KindOf<T>();
        lock (Gate)
        {
            Asked++;
            if (!Paths.TryGetValue(id, out var path) || HeadId(path) != id)
            {
                Look();
                if (!Paths.TryGetValue(id, out path)) throw new FileNotFoundException($"No data file holds the id {id}.");
            }
            Write(kind, path, value, id);
        }
    }

    /// <summary>
    /// A reference to a data file by its name, found in the working directory, beside the program
    /// or under <c>source/</c> beside it, as a model is, and read once.
    /// </summary>
    /// <remarks>
    /// A file with no id, written by hand, is given one here, written into it after its format and
    /// version. A file that is missing or cannot be read gives a reference that reads as none, said
    /// in the log naming the file.
    /// </remarks>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a data asset.</exception>
    public static DataRef<T> Load<T>(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        var kind = KindOf<T>();
        lock (Gate)
        {
            Asked++;
            var found = E3D.ResolveFile(fileName) is { } file ? Path.GetFullPath(file) : null;
            if (found is not null && Ids.TryGetValue(found, out var known) && Values.TryGetValue((known, typeof(T)), out var shared))
                return new DataRef<T>((Held<T>)shared);
            if (found is null || !TryRead(kind, found, null, null, out var held))
                return new DataRef<T>(Failed(kind, null, $"DataAssets: '{fileName}' was not found in the working directory, beside the program or under source/ beside it, so the reference to it reads as none."));
            if (held.Problem is null && held.Id is null && GiveId(found) is { } given)
                held = new Held<T>(given, found, held.Value, null);
            if (held.Id is not { } id) return new DataRef<T>(held);
            Remember(id, found);
            if (Values.TryGetValue((id, typeof(T)), out var before)) return new DataRef<T>((Held<T>)before);
            Values[(id, typeof(T))] = held;
            return new DataRef<T>(held);
        }
    }

    /// <summary>
    /// A reference to the data file with an id, wherever it was moved, read once, or one that
    /// reads as none where no file has the id, said once in the log naming it.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is not a data asset.</exception>
    public static DataRef<T> Find<T>(string id) => Find<T>(id, null);

    // The same, reading a file's asset paths through the server of the scene that asks, so a model
    // a data file names loads, and looking in the folder of the file that names the id as well,
    // where a level keeps its data beside it. A null or empty id is a reference to nothing, asking
    // nothing.
    internal static DataRef<T> Find<T>(string? id, AssetServer? assets, string? near = null)
    {
        if (string.IsNullOrEmpty(id)) return default;
        var kind = KindOf<T>();
        lock (Gate)
        {
            Asked++;
            if (Values.TryGetValue((id, typeof(T)), out var shared)) return new DataRef<T>((Held<T>)shared);
            if (!string.IsNullOrEmpty(near) && Path.GetDirectoryName(Path.GetFullPath(near)) is { Length: > 0 } beside) Folders.Add(beside);
            Held<T>? held = null;
            if (!(Paths.TryGetValue(id, out var path) && TryRead(kind, path, id, assets, out held)))
            {
                Look();
                if (!(Paths.TryGetValue(id, out path) && TryRead(kind, path, id, assets, out held)))
                {
                    held = Failed(kind, id, $"DataAssets: no data file holds the id {id}, so a reference to it reads as none.");
                    path = null;
                }
            }
            if (path is not null) Remember(id, path);
            Values[(id, typeof(T))] = held!;
            return new DataRef<T>(held!);
        }
    }

    /// <summary>The value a reference that reads as none gives, its type's start, or the default of a type that is no data asset.</summary>
    internal static T Fallback<T>()
    {
        lock (Gate) return ByType.TryGetValue(typeof(T), out var kind) && kind is Kind<T> known ? known.Fallback : default!;
    }

    /// <summary>The codec of a data asset type, or null for a type that is none, which a persistent value reads its file with.</summary>
    internal static SceneCodec<T>? CodecOf<T>()
    {
        lock (Gate) return ByType.TryGetValue(typeof(T), out var kind) && kind is CodecKind<T> codec ? codec.Codec : null;
    }

    /// <summary>Forgets where an id's file is and its value, as a program begun again has, for the test of a file renamed while it is not running.</summary>
    internal static void Forget(string id)
    {
        lock (Gate)
        {
            if (Paths.Remove(id, out var path)) Ids.Remove(path);
            foreach (var key in Values.Keys.Where(key => key.Id == id).ToArray()) Values.Remove(key);
        }
    }

    /// <summary>A value shared by every reference to one file, with the file's id and place, or the reason it reads as none.</summary>
    internal sealed class Held<T>(string? id, string? path, T value, string? problem)
    {
        public readonly string? Id = id;
        public string? Path = path;
        public T Value = value;
        public string? Problem = problem;
    }

    private static Kind<T> KindOf<T>()
    {
        lock (Gate)
            return ByType.TryGetValue(typeof(T), out var kind) && kind is Kind<T> known
                ? known
                : throw new InvalidOperationException($"DataAssets: {typeof(T).Name} is not a data asset. Mark it [DataAsset] so the generator writes its fields.");
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..12];

    private static string Subject(string? id) => id is null ? "the reference to it" : $"a reference to {id}";

    private static Held<T> Failed<T>(Kind<T> kind, string? id, string problem)
    {
        Logger.Warn(problem);
        return new Held<T>(id, null, kind.Fallback, problem);
    }

    private static void Remember(string id, string path)
    {
        if (Paths.Remove(id, out var before)) Ids.Remove(before);
        Paths[id] = path;
        Ids[path] = id;
        if (Path.GetDirectoryName(path) is { Length: > 0 } folder) Folders.Add(folder);
    }

    // The file's value, or the reason it reads as none. False where the file is gone or holds
    // another id than the one wanted, which a look may mend.
    private static bool TryRead<T>(Kind<T> kind, string path, string? wanted, AssetServer? assets, out Held<T> held)
    {
        held = null!;
        string? id = wanted;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            id = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var named) && named.ValueKind == JsonValueKind.String
                ? named.GetString()
                : null;
            if (wanted is not null && id != wanted) return false;
            var found = KindIn(root);
            if (found is not Kind<T> right) throw new InvalidDataException($"It holds {found.Name} rather than {kind.Name}.");
            held = new Held<T>(id, path, right.Read(root, new SceneReadContext(Detached, [], assets, path), path), null);
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or FormatException)
        {
            held = Failed(kind, id, $"DataAssets: {path} could not be read as {kind.Name}, so {Subject(id)} reads as none. {ex.Message}");
            return true;
        }
    }

    // The kind a file's root holds, by its format and the type it names.
    private static Kind KindIn(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format", out var format) || format.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("It has no \"format\".");
        var name = format.GetString()!;
        if (name != Format)
            return ByFormat.TryGetValue(name, out var own) ? own : throw new InvalidDataException($"Its \"format\" is \"{name}\", which is no data this engine reads.");
        if (root.TryGetProperty("version", out var version) && version.GetInt32() > Version)
            throw new InvalidDataException($"It is version {version.GetInt32()}, and this engine reads data files up to {Version}.");
        if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("It names no \"type\".");
        var typeName = type.GetString()!;
        return ByName.GetValueOrDefault(typeName) ?? ByFormerName.GetValueOrDefault(typeName)
               ?? throw new InvalidDataException($"It holds {typeName}, which no data asset of this build is called.");
    }

    private static Held<T> Write<T>(Kind<T> kind, string path, T value, string? id)
    {
        id ??= NewId();
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer, Options)) kind.Write(json, value, id, path);
        WriteText(path, SceneFile.Folded(Encoding.UTF8.GetString(buffer.WrittenSpan)));
        Remember(id, path);
        if (Values.TryGetValue((id, typeof(T)), out var known))
        {
            var held = (Held<T>)known;
            held.Value = value;
            held.Path = path;
            held.Problem = null;
            return held;
        }
        var made = new Held<T>(id, path, value, null);
        Values[(id, typeof(T))] = made;
        return made;
    }

    // Writes to a file beside the old one moved over it, so a write cut short leaves the file before.
    private static void WriteText(string path, string text)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var temporary = path + ".writing";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }

    // Writes an id into a file written by hand with none, after its format and version, the rest
    // kept as it is, or null where the file cannot be written, which is said.
    private static string? GiveId(string path)
    {
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var id = NewId();
            root.Insert(root.ContainsKey("version") ? root.IndexOf("version") + 1 : root.ContainsKey("format") ? root.IndexOf("format") + 1 : 0, "id", id);
            WriteText(path, SceneFile.Folded(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = Options.Encoder })));
            return id;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.Warn($"DataAssets: {path} has no id and could not be given one, so a reference to it cannot be saved. {ex.Message}");
            return null;
        }
    }

    // Fills the table again from every data file in the folder beside the program and in each
    // folder a data file was read from or written to, a folder inside another looked through once.
    private static void Look()
    {
        Asked++;
        var roots = new List<string>();
        foreach (var folder in Folders.Append(Path.GetFullPath(AppContext.BaseDirectory)).Select(f => Path.TrimEndingDirectorySeparator(f)).Distinct().OrderBy(f => f.Length))
            if (!roots.Exists(root => folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                roots.Add(folder);

        var files = roots.Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*.json", Everywhere))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Paths.Clear();
        Ids.Clear();
        foreach (var file in files)
        {
            if (HeadId(file) is not { } id) continue;
            if (Paths.TryGetValue(id, out var first))
            {
                Logger.Warn($"DataAssets: {first} and {file} both hold the id {id}, as a copy made by hand does, so a reference to it finds the first. "
                            + "A copy whose id is taken out is given one of its own the next time it is read by its name.");
                continue;
            }
            Paths[id] = file;
            Ids[file] = id;
        }
        if (files.Length > ManyFiles)
            Logger.Warn($"DataAssets: looking for a data file's id read the start of {files.Length:N0} files under {string.Join(", ", roots)}, "
                        + "which each id not found costs again.");
    }

    // The id at the start of a data file, read from its first bytes, or null for a file that is not
    // one or has none there.
    private static string? HeadId(string file)
    {
        Span<byte> start = stackalloc byte[1024];
        int read;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1);
            read = stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        var head = start[..read];
        if (head.StartsWith("﻿"u8)) head = head[3..];
        var reader = new Utf8JsonReader(head, isFinalBlock: false, default);
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return null;
            var known = false;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString();
                if (!reader.Read()) return null;
                switch (name)
                {
                    case "format":
                        known = reader.TokenType == JsonTokenType.String && reader.GetString() is { } format && (format == Format || ByFormat.ContainsKey(format));
                        if (!known) return null;
                        break;
                    case "version":
                        break;
                    case "id":
                        return known && reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
                    default:
                        return null;
                }
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    // How one type's files are told apart, read and written.
    private abstract class Kind
    {
        // The name a data file records for it, or a name for messages where its files have a format of their own.
        public abstract string Name { get; }
    }

    private abstract class Kind<T> : Kind
    {
        public abstract T Fallback { get; }
        public abstract T Read(JsonElement root, SceneReadContext context, string path);
        public abstract void Write(Utf8JsonWriter json, T value, string id, string path);
    }

    // A data asset type, its fields written and read by its generated codec under "fields".
    private sealed class CodecKind<T>(SceneCodec<T> codec) : Kind<T>
    {
        public readonly SceneCodec<T> Codec = codec;
        private T _start = default!;
        private bool _started;

        public override string Name => typeof(T).FullName ?? typeof(T).Name;

        // As the codec starts a value, from the type's Default or Identity where it has one, read
        // from an empty object the first time it is asked for.
        public override T Fallback
        {
            get
            {
                lock (Gate)
                {
                    if (_started) return _start;
                    using var empty = JsonDocument.Parse("{}");
                    _start = Codec.ReadValue(empty.RootElement, new SceneReadContext(Detached, [], null));
                    _started = true;
                    return _start;
                }
            }
        }

        public override T Read(JsonElement root, SceneReadContext context, string path)
        {
            if (!root.TryGetProperty("fields", out var fields)) return Fallback;
            if (fields.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Its \"fields\" is not an object.");
            if (SceneJson.VersionOf(fields, Name) is var held && held > Codec.Version)
                Logger.Warn($"DataAssets: {path} holds {Name} at version {held}, and this build reads up to {Codec.Version}, "
                            + "so it is read as far as this build knows it and a save keeps the rest.");
            return Codec.ReadValue(fields, context);
        }

        public override void Write(Utf8JsonWriter json, T value, string id, string path)
        {
            json.WriteStartObject();
            json.WriteString("format", Format);
            json.WriteNumber("version", Version);
            json.WriteString("id", id);
            json.WriteString("type", Name);
            json.WritePropertyName("fields");
            var context = new SceneWriteContext(Detached);
            if (Later(path) is not { } kept)
            {
                json.WriteStartObject();
                Codec.WriteValue(json, value, context);
                json.WriteEndObject();
            }
            else
            {
                // The file's fields at the later version with each field this build writes set, so
                // an older build saving a file a newer one wrote loses nothing the newer one wrote.
                var written = new ArrayBufferWriter<byte>();
                using (var fields = new Utf8JsonWriter(written))
                {
                    fields.WriteStartObject();
                    Codec.WriteValue(fields, value, context);
                    fields.WriteEndObject();
                }
                foreach (var (name, field) in JsonNode.Parse(written.WrittenSpan)!.AsObject())
                    if (name != SceneJson.VersionName) kept[name] = field?.DeepClone();
                kept.WriteTo(json);
            }
            json.WriteEndObject();
        }

        // The fields of the file already at the path where they are at a later version than this
        // build writes, or null.
        private JsonObject? Later(string path)
        {
            try
            {
                if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root || root["fields"] is not JsonObject fields) return null;
                return fields[SceneJson.VersionName] is JsonValue version && version.TryGetValue<int>(out var held) && held > Codec.Version ? fields : null;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    // A machine, whose file has its own format with the id beside it.
    private sealed class MachineKind : Kind<AnimationStateMachine>
    {
        public override string Name => nameof(AnimationStateMachine);

        // No states, which runs nothing and says so once.
        public override AnimationStateMachine Fallback { get; } = new();

        public override AnimationStateMachine Read(JsonElement root, SceneReadContext context, string path) => AnimationStateMachine.Read(root);

        public override void Write(Utf8JsonWriter json, AnimationStateMachine value, string id, string path) => value.Write(json, id);
    }
}
