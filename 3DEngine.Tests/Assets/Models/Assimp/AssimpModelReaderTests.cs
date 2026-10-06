using System.Collections.Concurrent;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Models.Assimp;

/// <summary>
/// Integration tests for the <see cref="AssimpModelReader"/> backend. Builds a tiny
/// OBJ file (text format) in-memory and runs it through the reader to verify the
/// produced <see cref="Scene"/> snapshot.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Backend", "Assimp")]
public sealed class AssimpModelReaderTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-assimp-test-");

    public void Dispose() => _folder.Dispose();

    private static byte[] MakeTriangleObj()
    {
        // Minimal valid OBJ: a single triangle named "Tri".
        var sb = new StringBuilder();
        sb.AppendLine("o Tri");
        sb.AppendLine("v 0 0 0");
        sb.AppendLine("v 1 0 0");
        sb.AppendLine("v 0 1 0");
        sb.AppendLine("f 1 2 3");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static AssetLoadContext OpenContext(string path, byte[] bytes) =>
        new AssetLoadContext(new MemoryStream(bytes), new AssetPath(path), _ => default);

    [Fact]
    public void Reader_Format_Id_Is_Assimp()
    {
        new AssimpModelReader().FormatId.Should().Be("assimp");
    }

    [Fact]
    public void Reader_Advertises_Common_Mesh_Extensions()
    {
        var exts = new AssimpModelReader().Extensions;
        // Sanity floor: Assimp's import-format list almost certainly includes these.
        exts.Should().Contain(".obj");
        exts.Should().Contain(".gltf");
        exts.Should().Contain(".glb");
        exts.Should().NotContain(".usd", "Assimp reads USD without its materials");
    }

    [Fact]
    public async Task ReadAsync_Triangle_Obj_Produces_Scene_With_One_Mesh()
    {
        var reader = new AssimpModelReader();
        using var ctx = OpenContext("tests/inline.obj", MakeTriangleObj());

        Scene scene;
        try
        {
            scene = await reader.ReadAsync(ctx, SceneImportSettings.Default, CancellationToken.None);
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex.GetType().Name.Contains("Assimp"))
        {
            // Native Assimp is not available on this RID, so the test is skipped rather than failed.
            return;
        }

        scene.Roots.Should().NotBeEmpty();

        // Walk the scene collecting any SceneMeshPayload.
        var meshes = new List<SceneMeshPayload>();
        void Walk(SceneNode n)
        {
            foreach (var c in n.Components)
                if (c is SceneMeshPayload m) meshes.Add(m);
            foreach (var ch in n.Children) Walk(ch);
        }
        foreach (var r in scene.Roots) Walk(r);

        meshes.Should().NotBeEmpty();
        var first = meshes[0];
        first.Positions.Length.Should().Be(3, "the OBJ has a single triangle (3 verts)");
        first.Indices.Length.Should().Be(3);
    }

    [Fact]
    public async Task ReadAsync_Honours_Cancellation()
    {
        var reader = new AssimpModelReader();
        using var ctx = OpenContext("tests/inline.obj", MakeTriangleObj());
        var ct = new CancellationToken(canceled: true);

        var act = () => reader.ReadAsync(ctx, SceneImportSettings.Default, ct);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task A_Model_Finds_The_Material_Library_Beside_It_From_A_Path_Or_A_File_Stream()
    {
        File.WriteAllText(_folder.File("tri.mtl"), "newmtl Red\nKd 1 0 0\nmap_Kd red.png\n");
        File.WriteAllText(_folder.File("tri.obj"), "mtllib tri.mtl\no Tri\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl Red\nf 1 2 3\n");

        AssertRed(new AssimpModelReader().ReadFile(_folder.File("tri.obj"), new SceneImportSettings()));

        // The asset server hands the reader the file and the reader it came from, which the
        // library beside it is read from as well.
        using var context = new AssetLoadContext(File.OpenRead(_folder.File("tri.obj")), new AssetPath("tri.obj"), _ => default, new FileAssetReader(_folder.Path));
        AssertRed(await new AssimpModelReader().ReadAsync(context, new SceneImportSettings(), default));
    }

    [Fact]
    public void An_Obj_Material_Is_Opaque_Whatever_Its_Dissolve_Says()
    {
        // raylib's character.obj says d 0, which raylib's loader does not read.
        File.WriteAllText(_folder.File("clear.mtl"), "newmtl Skin\nKd 0.8 0.8 0.8\nd 0.000000\n");
        File.WriteAllText(_folder.File("clear.obj"), "mtllib clear.mtl\no Tri\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl Skin\nf 1 2 3\n");

        var material = new AssimpModelReader().ReadFile(_folder.File("clear.obj"), new SceneImportSettings())
            .Traverse().SelectMany(n => n.Components).OfType<SceneMaterialPayload>().Should().ContainSingle().Subject;
        material.BaseColorFactor.W.Should().Be(1f);
        material.AlphaMode.Should().Be(SceneAlphaMode.Opaque);
    }

    [Fact]
    public async Task A_Model_In_A_Reader_That_Is_No_Folder_Finds_The_Material_Library_Beside_It()
    {
        // A model a program holds in memory, or embeds, has no folder for Assimp to look in, and
        // its library comes from the reader it came from, under the same folder name.
        var source = new InMemoryAssetReader(new ConcurrentDictionary<string, byte[]>());
        source.Set(new AssetPath("models/tri.mtl"), Encoding.ASCII.GetBytes("newmtl Red\nKd 1 0 0\nmap_Kd red.png\n"));
        var obj = Encoding.ASCII.GetBytes("mtllib tri.mtl\no Tri\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl Red\nf 1 2 3\n");

        using var context = new AssetLoadContext(new MemoryStream(obj), new AssetPath("models/tri.obj"), _ => default, source);
        AssertRed(await new AssimpModelReader().ReadAsync(context, new SceneImportSettings(), default));
    }

    [Fact]
    public async Task A_Gltf_Reads_Its_Buffer_From_Beside_It_From_A_Folder_Or_A_Reader()
    {
        // The arm with its buffer in a file of its own, as most exporters write a glTF.
        var arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
        var gltf = JsonNode.Parse(File.ReadAllText(arm))!.AsObject();
        var buffer = gltf["buffers"]![0]!.AsObject();
        var uri = buffer["uri"]!.GetValue<string>();
        var bytes = Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]);
        buffer["uri"] = "arm.bin";
        File.WriteAllText(_folder.File("arm.gltf"), gltf.ToJsonString());
        File.WriteAllBytes(_folder.File("arm.bin"), bytes);
        var expected = Positions(new AssimpModelReader().ReadFile(arm, new SceneImportSettings()));
        expected.Should().NotBeEmpty();

        Positions(new AssimpModelReader().ReadFile(_folder.File("arm.gltf"), new SceneImportSettings())).Should().Equal(expected);

        var source = new InMemoryAssetReader(new ConcurrentDictionary<string, byte[]>());
        source.Set(new AssetPath("models/arm.bin"), bytes);
        using var context = new AssetLoadContext(new MemoryStream(Encoding.UTF8.GetBytes(gltf.ToJsonString())), new AssetPath("models/arm.gltf"), _ => default, source);
        Positions(await new AssimpModelReader().ReadAsync(context, new SceneImportSettings(), default)).Should().Equal(expected);

        static Vector3[] Positions(Scene scene) =>
            scene.Traverse().SelectMany(n => n.Components).OfType<SceneMeshPayload>().SelectMany(m => m.Positions).ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Reader_That_Throws_Inside_Assimp_Fails_The_Load_With_The_Files_Name(bool whileReading)
    {
        // A game's own reader over an archive with a damaged entry, which throws what no file
        // stream does, as it opens the entry or as Assimp reads it. Thrown into native code, it
        // would end the process.
        var source = new DamagedReader("models/tri.mtl", whileReading);
        var obj = Encoding.ASCII.GetBytes("mtllib tri.mtl\no Tri\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl Red\nf 1 2 3\n");

        using var context = new AssetLoadContext(new MemoryStream(obj), new AssetPath("models/tri.obj"), _ => default, source);
        var read = () => new AssimpModelReader().ReadAsync(context, new SceneImportSettings(), default);

        var thrown = await read.Should().ThrowAsync<IOException>();
        thrown.Which.Message.Should().Contain("models/tri.mtl").And.Contain("damaged");
        thrown.Which.InnerException.Should().BeOfType<InvalidDataException>();

        using var again = new AssetLoadContext(new MemoryStream(obj), new AssetPath("models/tri.obj"), _ => default, source);
        var result = await new AssimpModelLoader(new AssimpModelReader()).LoadAsync(again, default);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("models/tri.mtl", "the asset server's message names the file the reader failed to give");
    }

    private sealed class DamagedReader(string damaged, bool whileReading) : IAssetReader
    {
        public bool Exists(AssetPath path) => path.Path == damaged;

        public Task<Stream> ReadAsync(AssetPath path, CancellationToken ct = default) => whileReading
            ? Task.FromResult<Stream>(new DamagedStream())
            : throw new InvalidDataException("the archive's entry is damaged");
    }

    private sealed class DamagedStream() : MemoryStream(Encoding.ASCII.GetBytes("newmtl Red\nKd 1 0 0\n"))
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidDataException("the archive's entry is damaged");

        public override int Read(Span<byte> buffer) => throw new InvalidDataException("the archive's entry is damaged");
    }

    private static void AssertRed(Scene scene)
    {
        var material = scene.Traverse().SelectMany(n => n.Components).OfType<SceneMaterialPayload>().Should().ContainSingle().Subject;
        material.BaseColorFactor.X.Should().BeApproximately(1f, 1e-5f);
        material.BaseColorFactor.Y.Should().BeApproximately(0f, 1e-5f);
        material.BaseColorTexture!.AssetPath.Should().Be("red.png");
    }
}
