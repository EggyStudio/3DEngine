using System.Numerics;
using System.Text.Json;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// An edit mesh written as glTF and read back by the engine's own model reader and into an edit
/// mesh, the file held to the fields the glTF schema requires, and a known glTF read into an edit
/// mesh and counted.
/// </summary>
/// <remarks>
/// <c>two-pieces.gltf</c> is written by <c>build/make-modeling-gltf.py</c>, a cube of a unit in the
/// material "Stone Wall" and a square in "Grass" on a node moved and scaled, so its counts are known.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class ExchangeTests : IDisposable
{
    private const float Close = 1e-4f;
    private readonly TestFolder _folder = new("engine-exchange-");
    private readonly List<EditMesh> _made = [];

    public ExchangeTests()
    {
        var config = Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private EditMesh New()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        return mesh;
    }

    private static (int, int, int) Counts(EditMesh mesh) =>
        (GetEditMeshInfo(mesh).VertexCount, GetEditMeshInfo(mesh).EdgeCount, GetEditMeshInfo(mesh).FaceCount);

    private static string TwoPieces => Path.Combine(Api.CheatsheetTests.RepoRoot(), "3DEngine.Tests", "Modeling", "two-pieces.gltf");

    [Fact]
    public void A_Box_Written_As_GlTF_Reads_Back_With_The_Same_Counts()
    {
        var box = New();
        EditMeshBox(box, new Vector3(2, 3, 4));
        var path = _folder.File("box.gltf");
        ExportEditMesh(box, path).Should().BeTrue();

        File.Exists(_folder.File("box.bin")).Should().BeTrue();
        Directory.GetFiles(_folder.Path, "*.part").Should().BeEmpty();

        // The engine's model reader finds the box's twenty-four corners and twelve triangles.
        var model = LoadModel(path);
        model.Meshes.Should().ContainSingle();
        (model.Meshes[0].VertexCount, model.Meshes[0].TriangleCount).Should().Be((24, 12));
        GetModelBoundingBox(model).Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 3, 4)));
        UnloadModel(model);

        // Read into an edit mesh, it is the box's eight vertices and six faces again.
        var again = New();
        EditMeshImport(again, path);
        Counts(again).Should().Be((8, 12, 6));
        GetEditMeshInfo(again).IsClosed.Should().BeTrue();
        GetEditMeshInfo(again).Volume.Should().BeApproximately(24, Close);
    }

    [Fact]
    public void A_Written_File_Has_Every_Field_The_GlTF_Schema_Requires()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMaterial(mesh, "roof");
        EditMeshMaterial(mesh, "unused");
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMaterial(mesh, "roof");
        var path = _folder.File("house.gltf");
        ExportEditMesh(mesh, path).Should().BeTrue();

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var gltf = document.RootElement;
        gltf.GetProperty("asset").GetProperty("version").GetString().Should().Be("2.0");

        var buffers = gltf.GetProperty("buffers").EnumerateArray().ToArray();
        var bytes = File.ReadAllBytes(_folder.File(buffers.Single().GetProperty("uri").GetString()!));
        buffers.Single().GetProperty("byteLength").GetInt32().Should().Be(bytes.Length);

        var views = gltf.GetProperty("bufferViews").EnumerateArray().ToArray();
        views.Should().OnlyContain(view => view.GetProperty("buffer").GetInt32() == 0
            && view.GetProperty("byteOffset").GetInt32() % 4 == 0
            && view.GetProperty("byteOffset").GetInt32() + view.GetProperty("byteLength").GetInt32() <= bytes.Length);

        var accessors = gltf.GetProperty("accessors").EnumerateArray().ToArray();
        foreach (var accessor in accessors)
        {
            accessor.GetProperty("bufferView").GetInt32().Should().BeLessThan(views.Length);
            accessor.GetProperty("componentType").GetInt32().Should().BeOneOf(5125, 5126);
            accessor.GetProperty("count").GetInt32().Should().BePositive();
            accessor.GetProperty("type").GetString().Should().BeOneOf("SCALAR", "VEC2", "VEC3");
        }

        // A slot with no faces has no primitive, so the house has two, each with its material.
        var materials = gltf.GetProperty("materials").EnumerateArray().Select(material => material.GetProperty("name").GetString()).ToArray();
        materials.Should().Equal("default", "roof");
        var primitives = gltf.GetProperty("meshes").EnumerateArray().Single().GetProperty("primitives").EnumerateArray().ToArray();
        primitives.Should().HaveCount(2);
        foreach (var primitive in primitives)
        {
            var position = accessors[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
            position.GetProperty("min").GetArrayLength().Should().Be(3, "a position accessor carries its bounds");
            position.GetProperty("max").GetArrayLength().Should().Be(3);
            accessors[primitive.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32().Should().BePositive().And.Match(count => count % 3 == 0);
            primitive.GetProperty("material").GetInt32().Should().BeLessThan(materials.Length);
        }
        gltf.GetProperty("scenes")[0].GetProperty("nodes")[0].GetInt32().Should().Be(0);
        gltf.GetProperty("nodes")[0].GetProperty("mesh").GetInt32().Should().Be(0);
    }

    [Fact]
    public void A_Buffer_Embedded_Leaves_No_File_Beside_And_Reads_Back()
    {
        var mesh = New();
        EditMeshCylinder(mesh, 1, 2, 8);
        var path = _folder.File("pillar.gltf");
        ExportEditMesh(mesh, path, embedBuffer: true).Should().BeTrue();

        File.Exists(_folder.File("pillar.bin")).Should().BeFalse();
        File.ReadAllText(path).Should().Contain("data:application/octet-stream;base64,");
        var again = New();
        EditMeshImport(again, path);
        Counts(again).Should().Be((16, 24, 10));
    }

    [Fact]
    public void What_Is_Written_Is_The_Mesh_With_Its_Modifiers()
    {
        var half = New();
        EditMeshBox(half, new Vector3(1, 2, 2));
        EditMeshSelectFacing(half, -Vector3.UnitX);
        EditMeshDelete(half);
        EditMeshModifierMirror(half, "x");
        var path = _folder.File("whole.gltf");
        ExportEditMesh(half, path);

        var whole = New();
        EditMeshImport(whole, path);
        // The two halves' faces in one plane are joined, so the box is six faces again, its
        // vertices on the plane between them left on the outlines of the faces they cross.
        GetEditMeshInfo(whole).FaceCount.Should().Be(6);
        GetEditMeshInfo(whole).IsClosed.Should().BeTrue();
        GetEditMeshInfo(whole).Volume.Should().BeApproximately(8, Close);
    }

    [Fact]
    public void A_Known_GlTF_Is_Read_Into_Faces_With_Its_Materials_As_Slots()
    {
        var mesh = New();
        EditMeshImport(mesh, TwoPieces);

        // The cube's eight vertices and six faces, and the square's four and one, its edges open.
        Counts(mesh).Should().Be((12, 16, 7));
        GetEditMeshInfo(mesh).OpenEdges.Should().Be(4);
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(1, Close);
        GetEditMeshInfo(mesh).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(5, 1, 2)));
        GetEditMeshSlots(mesh).Should().Equal("default", "Stone-Wall", "Grass");
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(7);

        // Its line runs again from the file.
        GetEditMeshRecipe(mesh).Should().Be($"model 1\nimport {TwoPieces}\n");
        var again = LoadEditMeshFromMemory(GetEditMeshRecipe(mesh));
        _made.Add(again);
        Counts(again).Should().Be((12, 16, 7));
    }

    [Fact]
    public void A_File_That_Is_Missing_Or_Not_A_Model_Is_Answered_False_And_Leaves_The_Mesh()
    {
        var mesh = New();
        EditMeshImport(mesh, _folder.File("none.gltf")).Should().BeFalse();

        var noise = _folder.File("noise.gltf");
        var bytes = new byte[512];
        new Random(7).NextBytes(bytes);
        File.WriteAllBytes(noise, bytes);
        EditMeshImport(mesh, noise).Should().BeFalse();
        Counts(mesh).Should().Be((0, 0, 0));

        // A recipe's line is refused with the line named, as any line that fails is.
        var read = () => LoadEditMeshFromMemory($"import {noise}");
        read.Should().Throw<FormatException>().Which.Message.Should().StartWith("Line 1 of the recipe");
    }
}
