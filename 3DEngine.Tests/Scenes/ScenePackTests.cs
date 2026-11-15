using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// Scene packs, their manifests read or refused, a pack fetched from a file or a server on this
/// machine, checked and unpacked, told stage by stage, and kept only where its hash is the
/// manifest's and the fetch was not stopped.
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
        var reported = new List<ScenePackProgress>();

        ScenePacks.IsFetched(manifest).Should().BeFalse();
        (await ScenePacks.FetchAsync(manifest, new Progress(reported))).Should().BeNull();
        ScenePacks.IsFetched(manifest).Should().BeTrue();
        File.ReadAllBytes(ScenePacks.ModelOf(manifest)).Should().Equal(model);
        File.ReadAllBytes(Path.Combine(ScenePacks.FolderOf(manifest), "textures", "stone.ktx2")).Should().Equal(texture);
        // Each stage told in its order with its bytes, the download's of the pack's and the
        // unpacking's of its files', ending done.
        reported.Select(r => r.Stage).Distinct().Should().Equal(ScenePackStage.Downloading, ScenePackStage.Checking, ScenePackStage.Unpacking, ScenePackStage.Done);
        reported.Should().Contain(new ScenePackProgress(ScenePackStage.Downloading, manifest.Size, manifest.Size));
        reported.Should().Contain(new ScenePackProgress(ScenePackStage.Unpacking, 7, 7), "the files hold 7 bytes");
        reported[^1].Fraction.Should().Be(1.0);

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

    [Fact]
    public async Task A_Pack_Fetched_Already_Is_Answered_At_Once_Without_Its_Address()
    {
        var (_, manifest) = Written("hall", Pack(("hall.gltf", [1, 2, 3])));
        (await ScenePacks.FetchAsync(manifest)).Should().BeNull();
        var reported = new List<ScenePackProgress>();
        var elsewhere = manifest with { Url = Path.Combine(_folder.Path, "nowhere.pack") };
        (await ScenePacks.FetchAsync(elsewhere, new Progress(reported))).Should().BeNull("the folder holds the pack the manifest names");
        reported.Should().Equal(new ScenePackProgress(ScenePackStage.Done, manifest.Size, manifest.Size));
    }

    [Fact]
    public async Task A_Fetch_Stopped_While_Downloading_Or_Unpacking_Leaves_Nothing_New()
    {
        // Stopped once its first bytes have come, a fetch leaves no part of the pack beside the
        // folder and no folder.
        var (_, first) = Written("hall", Pack(("hall.gltf", [1, 2, 3])));
        using (var stop = new CancellationTokenSource())
        {
            var said = await ScenePacks.FetchAsync(first, new Progress([], r => { if (r is { Stage: ScenePackStage.Downloading, Bytes: > 0 }) stop.Cancel(); }), stop.Token);
            said.Should().Be("Fetching hall was stopped.");
        }
        Directory.GetFileSystemEntries(ScenePacks.Folder).Should().BeEmpty("nothing is left of the download");

        // Stopped as it unpacks a pack of other files, a fetch leaves the folder fetched before.
        (await ScenePacks.FetchAsync(first)).Should().BeNull();
        var (_, next) = Written("hall-next", Pack(("hall.gltf", [9, 9]), ("textures/new.ktx2", new byte[3 << 20])));
        next = next with { Name = "hall", Title = "hall" };
        using (var stop = new CancellationTokenSource())
        {
            var said = await ScenePacks.FetchAsync(next, new Progress([], r => { if (r is { Stage: ScenePackStage.Unpacking, Bytes: > 0 }) stop.Cancel(); }), stop.Token);
            said.Should().Be("Fetching hall was stopped.");
        }
        ScenePacks.IsFetched(first).Should().BeTrue("the folder fetched before stays whole");
        File.ReadAllBytes(ScenePacks.ModelOf(first)).Should().Equal([1, 2, 3]);
        Directory.GetFileSystemEntries(ScenePacks.Folder).Select(Path.GetFileName).Should().Equal(["hall"], "nothing is left beside the folder");
    }

    [Fact]
    public async Task A_Pack_Is_Fetched_From_A_Server_And_An_Address_The_Server_Has_Nothing_At_Is_Said()
    {
        var pack = Pack(("hall.gltf", [1, 2, 3]), ("textures/stone.ktx2", new byte[(1 << 20) + 5]));
        using var server = new Serving(pack);
        var (_, written) = Written("hall", pack);
        var manifest = written with { Url = server.Address + "hall.pack" };
        var reported = new List<ScenePackProgress>();
        (await ScenePacks.FetchAsync(manifest, new Progress(reported))).Should().BeNull();
        ScenePacks.IsFetched(manifest).Should().BeTrue();
        reported.Where(r => r.Stage == ScenePackStage.Downloading).Select(r => r.Bytes).Should().BeInAscendingOrder().And.Contain(pack.Length);

        var missing = manifest with { Name = "missing", Url = server.Address + "missing.pack" };
        (await ScenePacks.FetchAsync(missing)).Should().Contain("missing.pack").And.Contain("404");
        ScenePacks.IsFetched(missing).Should().BeFalse();
    }

    // A server on this machine answering one path with a pack and every other with 404, as a
    // release's host answers, so a fetch over HTTP is read without the network.
    private sealed class Serving : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _serving;

        public Serving(byte[] pack)
        {
            _listener.Start();
            Address = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
            _serving = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    await using var stream = client.GetStream();
                    var request = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                    var line = await request.ReadLineAsync(_stop.Token) ?? "";
                    while (await request.ReadLineAsync(_stop.Token) is { Length: > 0 }) { }
                    var found = line.StartsWith("GET /hall.pack ", StringComparison.Ordinal);
                    var body = found ? pack : "not here"u8.ToArray();
                    var head = $"HTTP/1.1 {(found ? "200 OK" : "404 Not Found")}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(head), _stop.Token);
                    await stream.WriteAsync(body, _stop.Token);
                }
            });
        }

        public string Address { get; }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            try
            {
                _serving.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // The accept the stop ended.
            }
            _stop.Dispose();
        }
    }

    // Reports as they are made, where Progress<T> would post them to the thread pool after the
    // fetch, each handed on to what is given as well.
    private sealed class Progress(List<ScenePackProgress> into, Action<ScenePackProgress>? then = null) : IProgress<ScenePackProgress>
    {
        public void Report(ScenePackProgress value)
        {
            into.Add(value);
            then?.Invoke(value);
        }
    }
}
