using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// Scene packs, their manifests read or refused, a pack fetched from a file, checked and unpacked,
/// and kept only where its hash is the manifest's.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ScenePackTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-scene-packs-");

    public ScenePackTests() => ScenePacks.Folder = Path.Combine(_folder.Path, "cache");

    public void Dispose()
    {
        ScenePacks.Folder = null!;
        _folder.Dispose();
    }

    // A pack of these files, as build/make-scene-pack.py writes one.
    private static byte[] Pack(params (string Path, byte[] Bytes)[] files)
    {
        var index = 16 + files.Sum(f => 2 + Encoding.UTF8.GetByteCount(f.Path) + 16);
        using var pack = new MemoryStream();
        pack.Write("BCSPACK\0"u8);
        Span<byte> number = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(number, 1);
        BinaryPrimitives.WriteInt32LittleEndian(number[4..], files.Length);
        pack.Write(number);
        long offset = index;
        foreach (var (path, bytes) in files)
        {
            var name = Encoding.UTF8.GetBytes(path);
            BinaryPrimitives.WriteUInt16LittleEndian(number, (ushort)name.Length);
            pack.Write(number[..2]);
            pack.Write(name);
            BinaryPrimitives.WriteInt64LittleEndian(number, offset);
            pack.Write(number);
            BinaryPrimitives.WriteInt64LittleEndian(number, bytes.Length);
            pack.Write(number);
            offset += bytes.Length;
        }
        foreach (var (_, bytes) in files) pack.Write(bytes);
        return pack.ToArray();
    }

    // A pack written to a file and a manifest naming it.
    private (string File, ScenePack Manifest) Written(string name, byte[] pack)
    {
        var file = Path.Combine(_folder.Path, name + ".pack");
        File.WriteAllBytes(file, pack);
        return (file, new ScenePack
        {
            Name = name, Title = name, Url = file, Size = pack.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(pack)), Model = "hall.gltf",
        });
    }

    [Fact]
    public void A_Manifest_Is_Read_By_Its_File_Name_And_One_Missing_A_Field_Is_Said_Not_Thrown()
    {
        var manifests = Path.Combine(_folder.Path, "scenes");
        Directory.CreateDirectory(manifests);
        File.WriteAllText(Path.Combine(manifests, "hall.json"), """
            {
              // A comment, as a person writing one by hand may leave.
              "title": "A hall",
              "license": "CC BY 4.0",
              "url": "https://example.com/hall.pack",
              "size": 10,
              "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
              "model": "hall.gltf"
            }
            """);
        File.WriteAllText(Path.Combine(manifests, "no-hash.json"), """{ "url": "https://example.com/a.pack", "size": 10, "model": "a.gltf" }""");
        File.WriteAllText(Path.Combine(manifests, "broken.json"), "{ not json");

        var packs = ScenePacks.List(manifests, out var problems);

        var hall = packs.Should().ContainSingle().Subject;
        (hall.Name, hall.Title, hall.Model).Should().Be(("hall", "A hall", "hall.gltf"));
        problems.Should().HaveCount(2);
        problems.Should().Contain(p => p.Contains("broken.json"));
        problems.Should().Contain(p => p.Contains("no-hash.json") && p.Contains("SHA-256"));
        ScenePacks.List(Path.Combine(_folder.Path, "nowhere"), out _).Should().BeEmpty();
    }

    [Fact]
    public async Task A_Pack_Is_Fetched_From_A_File_Checked_And_Unpacked_And_One_Of_Another_Hash_Keeps_Nothing()
    {
        byte[] model = [1, 2, 3], texture = [4, 5, 6, 7];
        var (_, manifest) = Written("hall", Pack(("hall.gltf", model), ("textures/stone.ktx2", texture)));
        var reported = new List<double>();

        ScenePacks.IsFetched(manifest).Should().BeFalse();
        (await ScenePacks.FetchAsync(manifest, new Progress(reported))).Should().BeNull();
        ScenePacks.IsFetched(manifest).Should().BeTrue();
        File.ReadAllBytes(ScenePacks.ModelOf(manifest)).Should().Equal(model);
        File.ReadAllBytes(Path.Combine(ScenePacks.FolderOf(manifest), "textures", "stone.ktx2")).Should().Equal(texture);
        reported[^1].Should().Be(1.0);

        // A manifest whose hash is another's unpacks nothing, and the folder fetched before stays.
        var wrong = manifest with { Sha256 = new string('a', 64) };
        (await ScenePacks.FetchAsync(wrong)).Should().Contain("SHA-256");
        ScenePacks.IsFetched(manifest).Should().BeTrue();
        Directory.GetFileSystemEntries(ScenePacks.Folder).Select(Path.GetFileName).Should().Equal(["hall"], "nothing is left beside the folder");

        // An address with nothing at it is answered, naming it.
        var missing = manifest with { Name = "missing", Url = Path.Combine(_folder.Path, "missing.pack") };
        (await ScenePacks.FetchAsync(missing)).Should().Contain("missing.pack");
        ScenePacks.IsFetched(missing).Should().BeFalse();
    }

    [Fact]
    public async Task A_Pack_Naming_A_File_Outside_Its_Folder_Or_Lacking_Its_Model_Is_Refused_And_Writes_Nothing()
    {
        var (_, climbing) = Written("climbing", Pack(("hall.gltf", [1]), ("../outside.txt", [2])));
        (await ScenePacks.FetchAsync(climbing)).Should().Contain("outside the folder");
        File.Exists(Path.Combine(ScenePacks.Folder, "outside.txt")).Should().BeFalse();

        var (_, empty) = Written("empty", Pack(("other.gltf", [1])));
        (await ScenePacks.FetchAsync(empty)).Should().Contain("holds no hall.gltf");
        Directory.GetFileSystemEntries(ScenePacks.Folder).Should().BeEmpty("a refused pack leaves nothing");
    }

    // Reports as they are made, where Progress<T> would post them to the thread pool after the fetch.
    private sealed class Progress(List<double> into) : IProgress<double>
    {
        public void Report(double value) => into.Add(value);
    }
}
