using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Engine;

/// <summary>
/// A value kept in a file beside the program between runs, as a game's settings or its records,
/// read over a default so a field a later version adds keeps the default's value.
/// </summary>
/// <typeparam name="T">
/// What is kept, a type marked <see cref="SceneComponentAttribute"/> or
/// <see cref="DataAssetAttribute"/>, whose fields the scene codec writes and reads as a scene file
/// holds a component.
/// </typeparam>
/// <remarks>
/// <para>
/// The file is the object of the value's fields by name, as a scene file holds one component. It
/// is read laid over the default's own fields, so a field the file does not name, as one a later
/// version of the game added, keeps the default's value rather than the type's, and a type's
/// <see cref="DataVersionAttribute"/> brings an older file up as a scene's would. A file that is
/// missing gives the default, and one that cannot be read gives it too, with the reason in
/// <see cref="Problem"/> and the log, so a settings file broken by hand starts the game rather
/// than stopping it, and is not written over until the value is saved.
/// </para>
/// <para>
/// <see cref="Save"/> writes the value on demand, and one made with <c>saveOnChange</c> writes it at
/// each <see cref="Set"/> too. A write goes to a file beside the old one, moved over it, so one cut
/// short leaves the value before.
/// </para>
/// <para>
/// A data file (<see cref="DataAssets"/>) holds a type the same way, but it is the game's own data,
/// which components refer to by the id the file carries, where a persistent value is the player's,
/// read by its file's name and written as it changes, so its file is the object of the fields alone
/// and has no id.
/// </para>
/// </remarks>
public sealed class Persistent<T>
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    // The world a value's entity fields are read against and written from, which has none, since a
    // value kept apart from a scene refers to no entity of one.
    private static readonly EcsWorld Detached = new();

    private readonly SceneCodec<T> _codec;
    private readonly T _fallback;
    private readonly bool _saveOnChange;

    /// <summary>Reads the value from its file over <paramref name="fallback"/>, or starts from it where the file is missing.</summary>
    /// <param name="fileName">The file, a relative name beside the program.</param>
    /// <param name="fallback">The value where the file has none, and the value its missing fields keep.</param>
    /// <param name="saveOnChange">Whether each <see cref="Set"/> writes the file, as well as <see cref="Save"/>.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is marked neither <see cref="SceneComponentAttribute"/> nor <see cref="DataAssetAttribute"/>, so no codec writes it.</exception>
    public Persistent(string fileName, T fallback, bool saveOnChange = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        _codec = SceneComponents.All.OfType<SceneCodec<T>>().FirstOrDefault() ?? DataAssets.CodecOf<T>()
                 ?? throw new InvalidOperationException($"Persistent: {typeof(T).Name} has no scene codec. Mark it [SceneComponent] or [DataAsset] so its fields can be written.");
        _fallback = fallback;
        _saveOnChange = saveOnChange;
        Path = System.IO.Path.IsPathRooted(fileName) ? fileName : System.IO.Path.Combine(AppContext.BaseDirectory, fileName);
        Value = Read();
    }

    /// <summary>The file it is kept in.</summary>
    public string Path { get; }

    /// <summary>The value as it is now, which may hold changes not yet saved.</summary>
    public T Value { get; private set; }

    /// <summary>Why the file could not be read the last time it was, or null where it was or was not there.</summary>
    public string? Problem { get; private set; }

    /// <summary>Whether the value changed since it was last read or saved.</summary>
    public bool Changed { get; private set; }

    /// <summary>Replaces the value, writing it where it is saved on change.</summary>
    public void Set(T value)
    {
        Value = value;
        Changed = true;
        if (_saveOnChange) Save();
    }

    /// <summary>Changes the value through a function of the one before, as <see cref="Set"/> does.</summary>
    public void Update(Func<T, T> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Set(change(Value));
    }

    /// <summary>Writes the value to its file.</summary>
    public void Save()
    {
        var full = System.IO.Path.GetFullPath(Path);
        if (System.IO.Path.GetDirectoryName(full) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var temporary = full + ".writing";
        File.WriteAllBytes(temporary, Written(Value, indented: true));
        File.Move(temporary, full, overwrite: true);
        Changed = false;
        Problem = null;
    }

    /// <summary>Throws away changes not yet saved by reading the file again.</summary>
    public void Revert() => Value = Read();

    /// <summary>Puts the default back and saves it, as a settings screen's reset does.</summary>
    public void Reset()
    {
        Value = _fallback;
        Save();
    }

    // The value the file holds laid over the default, or the default with the reason it is not.
    private T Read()
    {
        Changed = false;
        Problem = null;
        if (!File.Exists(Path)) return _fallback;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(Path)) is not JsonObject given)
                throw new InvalidDataException("it holds no object of fields");
            var over = JsonNode.Parse(Written(_fallback, indented: false))!.AsObject();
            Lay(over, given);
            using var document = JsonDocument.Parse(over.ToJsonString());
            return _codec.ReadValue(document.RootElement, new SceneReadContext(Detached, [], null));
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            Problem = $"{Path} could not be read, so the default stands. {ex.Message}";
            Logger.Warn($"Persistent: {Problem}");
            return _fallback;
        }
    }

    // Each field of over laid on under, an object in both laid field by field the same way, so a
    // nested value the file names part of keeps the rest of the default's.
    private static void Lay(JsonObject under, JsonObject over)
    {
        foreach (var (name, value) in over)
        {
            if (value is JsonObject inner && under[name] is JsonObject beneath)
            {
                Lay(beneath, inner);
                continue;
            }
            under[name] = value?.DeepClone();
        }
    }

    private byte[] Written(T value, bool indented)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions
               {
                   Indented = indented,
                   Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
               }))
        {
            json.WriteStartObject();
            _codec.WriteValue(json, value, new SceneWriteContext(Detached));
            json.WriteEndObject();
        }
        return buffer.WrittenSpan.ToArray();
    }
}
