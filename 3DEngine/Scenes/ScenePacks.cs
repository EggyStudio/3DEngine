using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine;

/// <summary>A well-known scene kept as a pack fetched on demand, as its manifest names it.</summary>
/// <remarks>
/// A scene such as Intel's Sponza is hundreds of megabytes with its textures made small, too much
/// for a repository to hold, so a manifest of JSON holding these fields is kept in the repository's
/// <c>scenes</c> folder in its place, written as BevyCSharp's are. Its fields are settable, since a
/// source-generated reader gives a field a manifest leaves out the value it starts with.
/// </remarks>
internal sealed record ScenePack
{
    /// <summary>What it is called, which its manifest's file and its folder are named by.</summary>
    public string Name { get; set; } = "";

    /// <summary>What a person is shown it as.</summary>
    public string Title { get; set; } = "";

    /// <summary>Where the scene was published by its makers.</summary>
    public string Source { get; set; } = "";

    /// <summary>The license it is published under.</summary>
    public string License { get; set; } = "";

    /// <summary>The words its license asks to be shown with it.</summary>
    public string Attribution { get; set; } = "";

    /// <summary>Where the pack is fetched from, over HTTPS or as a file on this machine.</summary>
    public string Url { get; set; } = "";

    /// <summary>How many bytes the pack is.</summary>
    public long Size { get; set; }

    /// <summary>The pack's SHA-256 in lowercase hexadecimal, which a fetched copy must have.</summary>
    public string Sha256 { get; set; } = "";

    /// <summary>The model to load from the pack's files, by its path among them, such as <c>sponza.gltf</c>.</summary>
    public string Model { get; set; } = "";
}

/// <summary>
/// The scene packs, read from their manifests, fetched into the user's cache folder, checked and
/// unpacked into a folder of their files that a program loads the model from.
/// </summary>
/// <remarks>
/// <para>
/// A pack is an index followed by the files' bytes, as BevyCSharp writes one and
/// build/make-scene-pack.py writes the engine's, the eight bytes BCSPACK and a zero, the format's
/// version and the number of files as little-endian 32-bit integers, then for each file its path,
/// a 16-bit length and UTF-8, and where its bytes start and how many there are as little-endian
/// 64-bit integers counted from the start of the pack.
/// </para>
/// <para>
/// <see cref="FetchAsync"/> downloads a pack beside its folder, hashing the bytes as they come, and
/// unpacks it only where its size and SHA-256 are the manifest's, into a folder of its own moved into
/// place whole, so a fetch stopped half way or a pack that is not the manifest's leaves the folder
/// fetched before, if any, and nothing new. The pack is let go once unpacked. Nothing is thrown for
/// a manifest, a download or a pack that is wrong; each answers with the reason, naming the file.
/// </para>
/// </remarks>
internal static class ScenePacks
{
    private static readonly HttpClient Http = new();

    // The file in a pack's folder holding the SHA-256 it was unpacked from, which says the folder is
    // whole and is the manifest's.
    private const string Stamp = ".sha256";

    private static string? _folder;

    /// <summary>
    /// Where fetched packs are unpacked, each in a folder of its name, the user's cache folder under
    /// <c>3DEngine/scenes</c> unless <c>E3D_SCENES</c> names another or this is set.
    /// </summary>
    /// <remarks>
    /// The cache folder is <c>XDG_CACHE_HOME</c> or <c>~/.cache</c> on Linux, <c>~/Library/Caches</c>
    /// on macOS and the local application data on Windows (<see cref="UserCache"/>). Set to
    /// <see langword="null"/> to go back to the default.
    /// </remarks>
    public static string Folder
    {
        get => _folder
            ?? (Environment.GetEnvironmentVariable("E3D_SCENES") is { Length: > 0 } named ? named : null)
            ?? Path.Combine(UserCache.Folder, "scenes");
        set => _folder = value;
    }

    /// <summary>Reads every manifest in a folder, in the order of their names.</summary>
    /// <param name="folder">A folder of manifests, each a <c>.json</c> file.</param>
    /// <param name="problems">Why each manifest that did not read did not, naming its file.</param>
    public static IReadOnlyList<ScenePack> List(string folder, out IReadOnlyList<string> problems)
    {
        var found = new List<ScenePack>();
        var wrong = new List<string>();
        problems = wrong;
        if (!Directory.Exists(folder)) return found;
        foreach (var file in Directory.GetFiles(folder, "*.json").Order(StringComparer.Ordinal))
        {
            if (TryRead(file, out var pack, out var problem)) found.Add(pack);
            else wrong.Add(problem);
        }
        return found;
    }

    /// <summary>Reads one manifest, or says why it cannot, naming the file.</summary>
    /// <remarks>The pack's name is the file's, whatever the file says.</remarks>
    public static bool TryRead(string file, [NotNullWhen(true)] out ScenePack? pack, [NotNullWhen(false)] out string? problem)
    {
        pack = null;
        try
        {
            ScenePack? read;
            using (var stream = File.OpenRead(file)) read = JsonSerializer.Deserialize(stream, ScenePackJson.Default.ScenePack);
            if (read is null)
            {
                problem = $"{file} holds no scene pack.";
                return false;
            }
            var name = Path.GetFileNameWithoutExtension(file);
            read = read with { Name = name, Title = read.Title.Length > 0 ? read.Title : name };
            if (Missing(read) is { } field)
            {
                problem = $"{file} gives no {field}, so its pack cannot be fetched and checked.";
                return false;
            }
            pack = read;
            problem = null;
            return true;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            problem = $"{file} could not be read as a scene pack. {error.Message}";
            return false;
        }
    }

    /// <summary>The folder a pack's files are unpacked into.</summary>
    public static string FolderOf(ScenePack pack) => Path.Combine(Folder, pack.Name);

    /// <summary>Where a fetched pack's model is, among its unpacked files.</summary>
    public static string ModelOf(ScenePack pack) => Path.Combine(FolderOf(pack), pack.Model);

    /// <summary>Whether a pack is fetched, by its folder's stamp holding the manifest's SHA-256.</summary>
    public static bool IsFetched(ScenePack pack)
    {
        try
        {
            var stamp = Path.Combine(FolderOf(pack), Stamp);
            return File.Exists(stamp) && File.ReadAllText(stamp).Trim().Equals(pack.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Fetches a pack, telling how far it has come, and unpacks it into <see cref="FolderOf"/> where
    /// its size and SHA-256 are the manifest's.
    /// </summary>
    /// <param name="pack">The manifest.</param>
    /// <param name="progress">Told the fraction fetched, from nothing to one, as the bytes come.</param>
    /// <param name="cancel">Stops the fetch, which is then answered as stopped.</param>
    /// <returns>Nothing where the pack was fetched, checked and unpacked, or why it was not.</returns>
    public static async Task<string?> FetchAsync(ScenePack pack, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var folder = FolderOf(pack);
        var part = folder + ".pack.part";
        var unpacking = folder + ".unpacking";
        try
        {
            Directory.CreateDirectory(Folder);
            long fetched = 0;
            string hash;
            await using (var into = File.Create(part))
            {
                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using var from = await Open(pack.Url, cancel).ConfigureAwait(false);
                var buffer = new byte[1 << 20];
                int read;
                while ((read = await from.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0)
                {
                    hasher.AppendData(buffer, 0, read);
                    await into.WriteAsync(buffer.AsMemory(0, read), cancel).ConfigureAwait(false);
                    fetched += read;
                    progress?.Report(Math.Min(1.0, (double)fetched / pack.Size));
                }
                hash = Convert.ToHexStringLower(hasher.GetHashAndReset());
            }
            if (fetched != pack.Size || !hash.Equals(pack.Sha256, StringComparison.OrdinalIgnoreCase))
                return $"What {pack.Url} gave is not the pack {pack.Title}'s manifest names, {fetched} bytes with SHA-256 {hash} where {pack.Size} with {pack.Sha256.ToLowerInvariant()} were expected.";

            if (Directory.Exists(unpacking)) Directory.Delete(unpacking, recursive: true);
            if (Unpack(part, unpacking) is { } problem) return problem;
            if (!File.Exists(Path.Combine(unpacking, pack.Model)))
                return $"{pack.Url} holds no {pack.Model}, the model its manifest names.";
            File.WriteAllText(Path.Combine(unpacking, Stamp), hash + "\n");
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            Directory.Move(unpacking, folder);
            progress?.Report(1.0);
            return null;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or UriFormatException)
        {
            return error is OperationCanceledException
                ? $"Fetching {pack.Title} was stopped."
                : $"{pack.Title} could not be fetched from {pack.Url}. {error.Message}";
        }
        finally
        {
            Remove(part);
            if (Directory.Exists(unpacking))
            {
                try
                {
                    Directory.Delete(unpacking, recursive: true);
                }
                catch (IOException)
                {
                    // Left for the next fetch, which removes it first.
                }
            }
        }
    }

    /// <summary>Writes a pack's files into a folder, or says why the pack cannot be read.</summary>
    /// <remarks>
    /// A path that is rooted or climbs out of the folder is refused, so a pack can write nowhere but
    /// under the folder it is unpacked into.
    /// </remarks>
    internal static string? Unpack(string pack, string folder)
    {
        using var file = File.OpenRead(pack);
        var head = new byte[16];
        if (file.ReadAtLeast(head, 16, throwOnEndOfStream: false) < 16 || !head.AsSpan(0, 8).SequenceEqual("BCSPACK\0"u8))
            return $"{pack} is not a scene pack, its first bytes not BCSPACK.";
        var version = BinaryPrimitives.ReadInt32LittleEndian(head.AsSpan(8));
        var count = BinaryPrimitives.ReadInt32LittleEndian(head.AsSpan(12));
        if (version != 1) return $"{pack} is a scene pack of version {version}, where this reads version 1.";

        var entries = new List<(string Path, long Offset, long Length)>();
        var fixedPart = new byte[16];
        for (int i = 0; i < count; i++)
        {
            if (file.ReadAtLeast(head.AsSpan(0, 2), 2, throwOnEndOfStream: false) < 2) return $"{pack}'s index ends at its file {i}.";
            var name = new byte[BinaryPrimitives.ReadUInt16LittleEndian(head)];
            if (file.ReadAtLeast(name, name.Length, throwOnEndOfStream: false) < name.Length
                || file.ReadAtLeast(fixedPart, 16, throwOnEndOfStream: false) < 16)
                return $"{pack}'s index ends at its file {i}.";
            var (offset, length) = (BinaryPrimitives.ReadInt64LittleEndian(fixedPart), BinaryPrimitives.ReadInt64LittleEndian(fixedPart.AsSpan(8)));
            if (offset < 0 || length < 0 || offset + length > file.Length)
                return $"{pack}'s file {i} lies past its end.";
            entries.Add((Encoding.UTF8.GetString(name), offset, length));
        }

        var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        foreach (var (name, offset, length) in entries)
        {
            var target = Path.GetFullPath(Path.Combine(root, name));
            if (Path.IsPathRooted(name) || !target.StartsWith(root, StringComparison.Ordinal))
                return $"{pack} names a file {name} outside the folder it is unpacked into.";
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var into = File.Create(target);
            file.Position = offset;
            CopyBytes(file, into, length);
        }
        return null;
    }

    private static void CopyBytes(Stream from, Stream into, long length)
    {
        var buffer = new byte[1 << 20];
        while (length > 0)
        {
            var read = from.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
            if (read == 0) throw new EndOfStreamException();
            into.Write(buffer, 0, read);
            length -= read;
        }
    }

    private static void Remove(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // Left for the next fetch, which writes over it.
        }
    }

    /// <summary>The first field a manifest leaves out that a fetch needs, or nothing.</summary>
    private static string? Missing(ScenePack pack) =>
        pack.Url.Length == 0 ? "address"
        : pack.Size <= 0 ? "size"
        : pack.Sha256.Length != 64 || !pack.Sha256.All(Uri.IsHexDigit) ? "SHA-256"
        : pack.Model.Length == 0 ? "model"
        : null;

    /// <summary>The bytes at an address, over HTTP or from a file on this machine.</summary>
    private static async Task<Stream> Open(string url, CancellationToken cancel)
    {
        if (Path.IsPathRooted(url)) return File.OpenRead(url);
        var address = new Uri(url, UriKind.Absolute);
        if (address.IsFile) return File.OpenRead(address.LocalPath);
        var response = await Http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
    }
}

/// <summary>How a manifest is read, generated so nothing reflects.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(ScenePack))]
internal sealed partial class ScenePackJson : JsonSerializerContext;
