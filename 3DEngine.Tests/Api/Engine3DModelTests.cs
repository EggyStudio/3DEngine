using System.Numerics;
using System.Text;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// The flat API's model loading, against an app whose stores queue uploads without a GPU. The flat
/// API holds one app in a static field, so these tests do not run beside other tests that set it.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class Engine3DModelTests : IDisposable
{
    private readonly App _app = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-model-api-").FullName;

    public Engine3DModelTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<MeshStore>();
        UseApp(_app);
    }

    public void Dispose()
    {
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    // A triangle whose material's base color texture is a 4 by 2 PNG stored in the file's binary
    // chunk, which is how a .glb carries its images.
    private string WriteGlbWithEmbeddedPng(byte[] rgba)
    {
        var pngPath = Path.Combine(_directory, "pixels.png");
        PngWriter.Write(pngPath, rgba, 4, 2);
        var png = File.ReadAllBytes(pngPath);

        var bin = new MemoryStream();
        var w = new BinaryWriter(bin);
        foreach (var f in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }) w.Write(f);
        foreach (var f in new float[] { 0, 0, 1, 0, 0, 1 }) w.Write(f);
        w.Write(png);
        while (bin.Length % 4 != 0) w.Write((byte)0);

        var json = """
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],
             "meshes":[{"primitives":[{"attributes":{"POSITION":0,"TEXCOORD_0":1},"material":0}]}],
             "materials":[{"pbrMetallicRoughness":{"baseColorTexture":{"index":0}}}],
             "textures":[{"source":0}],
             "images":[{"bufferView":2,"mimeType":"image/png"}],
             "accessors":[
               {"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]},
               {"bufferView":1,"componentType":5126,"count":3,"type":"VEC2"}],
             "bufferViews":[
               {"buffer":0,"byteOffset":0,"byteLength":36},
               {"buffer":0,"byteOffset":36,"byteLength":24},
               {"buffer":0,"byteOffset":60,"byteLength":PNG_LENGTH}],
             "buffers":[{"byteLength":BIN_LENGTH}]}
            """.Replace("PNG_LENGTH", png.Length.ToString()).Replace("BIN_LENGTH", bin.Length.ToString());
        var jsonBytes = Encoding.UTF8.GetBytes(json).ToList();
        while (jsonBytes.Count % 4 != 0) jsonBytes.Add((byte)' ');

        var path = Path.Combine(_directory, "triangle.glb");
        using var file = new BinaryWriter(File.Create(path));
        file.Write(0x46546C67u);
        file.Write(2u);
        file.Write((uint)(12 + 8 + jsonBytes.Count + 8 + bin.Length));
        file.Write((uint)jsonBytes.Count);
        file.Write(0x4E4F534Au);
        file.Write(jsonBytes.ToArray());
        file.Write((uint)bin.Length);
        file.Write(0x004E4942u);
        file.Write(bin.ToArray());
        return path;
    }

    [Fact]
    public void A_Texture_Embedded_In_A_Glb_Is_Decoded_And_Owned_By_The_Model()
    {
        var rgba = new byte[4 * 2 * 4];
        for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = ((byte)(i * 7), 200, 30, 255);

        var model = LoadModel(WriteGlbWithEmbeddedPng(rgba));

        model.Meshes.Should().ContainSingle();
        var texture = model.Materials[model.MeshMaterial[0]].Texture;
        texture.IsValid.Should().BeTrue();
        (texture.Width, texture.Height).Should().Be((4, 2));
        model.OwnedTextures.Should().Equal(texture);

        var upload = _app.World.Resource<TextureStore>().Take().Uploads.Should().ContainSingle().Subject;
        upload.Id.Should().Be(texture.Id);
        upload.Rgba.Should().Equal(rgba);
    }

    [Fact]
    public void An_Embedded_Texture_Is_Found_By_Index_Or_By_Its_Original_File_Name()
    {
        var scene = new Scene();
        scene.EmbeddedTextures.Add(new SceneEmbeddedTexture("textures/wood.png", [1], "png", 0, 0, null));
        scene.EmbeddedTextures.Add(new SceneEmbeddedTexture(null, [2], "jpg", 0, 0, null));

        scene.FindEmbeddedTexture("*1")!.FormatHint.Should().Be("jpg");
        scene.FindEmbeddedTexture("C:\\art\\Wood.png")!.FormatHint.Should().Be("png");
        scene.FindEmbeddedTexture("*2").Should().BeNull();
        scene.FindEmbeddedTexture("stone.png").Should().BeNull();
    }

    public static TheoryData<string, Vector3, Vector3> Generators => new()
    {
        { "poly", new(-1, 0, -1), new(1, 0, 1) },
        { "cylinder", new(-1, 0, -1), new(1, 2, 1) },
        { "cone", new(-1, 0, -1), new(1, 2, 1) },
        { "hemisphere", new(-1, 0, -1), new(1, 1, 1) },
        { "torus", new(-1.25f, -0.25f, -1.25f), new(1.25f, 0.25f, 1.25f) },
    };

    private static ModelMesh Generate(string shape) => shape switch
    {
        "poly" => GenMeshPoly(64, 1),
        "cylinder" => GenMeshCylinder(1, 2, 64),
        "cone" => GenMeshCone(1, 2, 64),
        "hemisphere" => GenMeshHemiSphere(1, 16, 64),
        "torus" => GenMeshTorus(1, 0.25f, 64, 32),
        _ => GenMeshKnot(3, 0.3f, 128, 16),
    };

    [Theory]
    [MemberData(nameof(Generators))]
    public void A_Generated_Mesh_Faces_Outward_With_Unit_Normals_Inside_Its_Bounds(string shape, Vector3 min, Vector3 max)
    {
        var mesh = Generate(shape);

        _app.World.Resource<MeshStore>().TryGetData(mesh.Id, out var vertices, out var indices).Should().BeTrue();
        vertices.Should().OnlyContain(v => MathF.Abs(v.Normal.Length() - 1) < 1e-4f);
        for (int i = 0; i < indices.Length; i += 3)
        {
            var (a, b, c) = (vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]);
            var face = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            if (face.LengthSquared() < 1e-12f) continue; // the cone's tip
            Vector3.Dot(face, a.Normal + b.Normal + c.Normal).Should().BePositive($"triangle {i / 3} of the {shape} winds counterclockwise from outside");
        }
        Vector3.Distance(mesh.Bounds.Min, min).Should().BeLessThan(0.01f);
        Vector3.Distance(mesh.Bounds.Max, max).Should().BeLessThan(0.01f);
    }

    [Fact]
    public void A_Knot_Faces_Outward_Without_A_Seam()
    {
        var mesh = Generate("knot");

        _app.World.Resource<MeshStore>().TryGetData(mesh.Id, out var vertices, out var indices).Should().BeTrue();
        for (int i = 0; i < indices.Length; i += 3)
        {
            var (a, b, c) = (vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]]);
            Vector3.Dot(Vector3.Cross(b.Position - a.Position, c.Position - a.Position), a.Normal).Should().BePositive();
        }
        // The last ring of the tube lies on the first.
        for (int j = 0; j <= 16; j++)
            Vector3.Distance(vertices[j].Position, vertices[128 * 17 + j].Position).Should().BeLessThan(1e-3f);
    }

    [Fact]
    public void DrawModelWires_Draws_Each_Shared_Edge_Once()
    {
        _app.World.InitResource<DrawList>();
        var model = LoadModelFromMesh(GenMeshPoly(4, 1));

        DrawModelWires(model, Vector3.Zero, 1, Color.Red);

        // A square fan of four triangles has four rim edges and four spokes.
        _app.World.Resource<DrawList>().Vertices.Length.Should().Be(8 * 2);
    }
}
