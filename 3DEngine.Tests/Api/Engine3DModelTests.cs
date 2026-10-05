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
    private readonly TestFolder _folder = new("engine-model-api-");

    public Engine3DModelTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<MeshStore>();
        UseApp(_app);
    }

    public void Dispose()
    {
        UseApp(null);
        _folder.Dispose();
    }

    // A triangle whose material's base color texture is a 4 by 2 PNG stored in the file's binary
    // chunk, which is how a .glb carries its images.
    private string WriteGlbWithEmbeddedPng(byte[] rgba)
    {
        var pngPath = Path.Combine(_folder.Path, "pixels.png");
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

        var path = Path.Combine(_folder.Path, "triangle.glb");
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
        { "heightmap", new(0, 0, 0), new(4, 2, 4) },
        { "cubicmap", new(-0.5f, 0, -0.5f), new(2.5f, 1, 2.5f) },
        { "sphere", new(-1, -1, -1), new(1, 1, 1) },
        { "cube", new(-1, -1, -1), new(1, 1, 1) },
        { "plane", new(-1, 0, -1), new(1, 0, 1) },
    };

    private static ModelMesh Generate(string shape) => shape switch
    {
        "poly" => GenMeshPoly(64, 1),
        "cylinder" => GenMeshCylinder(1, 2, 64),
        "cone" => GenMeshCone(1, 2, 64),
        "hemisphere" => GenMeshHemiSphere(1, 16, 64),
        "torus" => GenMeshTorus(1, 0.25f, 64, 32),
        "heightmap" => GenMeshHeightmap(GenImageGradientLinear(8, 8, 90, Color.Black, Color.White), new Vector3(4, 2, 4)),
        "cubicmap" => GenMeshCubicmap(Maze(), Vector3.One),
        "sphere" => GenMeshSphere(1, 16, 32),
        "cube" => GenMeshCube(2, 2, 2),
        "plane" => GenMeshPlane(2, 2, 4, 4),
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

    // A 3 by 3 maze with one wall in the middle.
    private static Image Maze()
    {
        var image = GenImageColor(3, 3, Color.Black);
        ImageDrawPixel(ref image, 1, 1, Color.White);
        return image;
    }

    [Fact]
    public void A_Cubicmap_Makes_The_Faces_Raylibs_Makes_Each_Cell_Centered_On_Its_Pixel()
    {
        var mesh = GenMeshCubicmap(Maze(), Vector3.One);

        // Eight open cells with a floor and a roof, and the wall's top, bottom and four sides.
        mesh.TriangleCount.Should().Be((8 * 2 + 6) * 2);
    }

    [Fact]
    public void A_Node_Moved_In_The_File_Moves_Its_Mesh()
    {
        // One triangle at the origin, under a node translated 5 units along X and turned a
        // quarter about Y. Assimp's matrices read untransposed lost every node's translation.
        var bytes = new List<byte>();
        foreach (var f in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }) bytes.AddRange(BitConverter.GetBytes(f));
        var json = $$$"""
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],
             "nodes":[{"mesh":0,"translation":[5,0,0],"rotation":[0,0.70710677,0,0.70710677]}],
             "meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],
             "buffers":[{"byteLength":36,"uri":"data:application/octet-stream;base64,{{{Convert.ToBase64String(bytes.ToArray())}}}"}],
             "bufferViews":[{"buffer":0,"byteLength":36}],
             "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]}]}
            """;
        var path = Path.Combine(_folder.Path, "moved.gltf");
        File.WriteAllText(path, json);

        var bounds = GetModelBoundingBox(LoadModel(path));
        bounds.Min.X.Should().BeApproximately(5, 1e-4f, "the node is 5 units along X");
        bounds.Min.Z.Should().BeApproximately(-1, 1e-4f, "a quarter turn about Y takes the corner at +X to -Z");
    }

    // One triangle with a material of the given glTF alpha fields.
    private string WriteTriangleWithMaterial(string material)
    {
        var bytes = new List<byte>();
        foreach (var f in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }) bytes.AddRange(BitConverter.GetBytes(f));
        var json = $$$"""
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],
             "materials":[{{{material}}}],
             "meshes":[{"primitives":[{"attributes":{"POSITION":0},"material":0}]}],
             "buffers":[{"byteLength":36,"uri":"data:application/octet-stream;base64,{{{Convert.ToBase64String(bytes.ToArray())}}}"}],
             "bufferViews":[{"buffer":0,"byteLength":36}],
             "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]}]}
            """;
        var path = Path.Combine(_folder.Path, $"alpha-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void A_Files_Alpha_Mode_And_Cutoff_Reach_The_Material()
    {
        LoadModel(WriteTriangleWithMaterial("""{"alphaMode":"MASK","alphaCutoff":0.3}""")).Materials[0]
            .Should().Match<ModelMaterial>(m => m.AlphaMode == MaterialAlphaMode.Mask && Math.Abs(m.AlphaCutoff - 0.3f) < 1e-6f);
        LoadModel(WriteTriangleWithMaterial("""{"alphaMode":"BLEND"}""")).Materials[0].AlphaMode.Should().Be(MaterialAlphaMode.Blend);
        LoadModel(WriteTriangleWithMaterial("""{"pbrMetallicRoughness":{"baseColorFactor":[1,1,1,0.5]}}""")).Materials[0].AlphaMode
            .Should().Be(MaterialAlphaMode.Opaque, "glTF's default ignores alpha, though the color has some");
    }

    [Fact]
    public void A_Texture_With_Alpha_Between_Clear_And_Solid_Is_Translucent_And_A_Cut_Out_One_Is_Not()
    {
        var textures = _app.World.Resource<TextureStore>();
        var soft = textures.Add([255, 255, 255, 128], 1, 1);
        var cut = textures.Add([255, 255, 255, 0, 255, 255, 255, 255], 2, 1);

        textures.IsTranslucent(soft).Should().BeTrue();
        textures.IsTranslucent(cut).Should().BeFalse();
    }

    [Fact]
    public void Materials_Are_Set_By_Raylibs_Map_Names_And_Meshes_Pick_Theirs()
    {
        var material = LoadMaterialDefault();
        material.Color.Should().Be(Color.White);
        IsMaterialValid(material).Should().BeTrue("a material with no maps is drawn with");

        var texture = LoadTextureFromImage(GenImageColor(2, 2, Color.Red));
        SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);
        SetMaterialTexture(ref material, MaterialMapIndex.Normal, texture);
        SetMaterialTexture(ref material, MaterialMapIndex.Roughness, texture);
        (material.Texture, material.NormalMap, material.MetallicRoughnessMap).Should().Be((texture, texture, texture));
        IsMaterialValid(material).Should().BeTrue();
        UnloadTexture(texture);
        IsMaterialValid(material).Should().BeFalse("its maps are gone");

        var model = new Model { Meshes = [default, default], Materials = [LoadMaterialDefault(), material], MeshMaterial = [0, 0] };
        SetModelMeshMaterial(model, 1, 1);
        SetModelMeshMaterial(model, 0, 5);
        model.MeshMaterial.Should().Equal(0, 1);
    }

    [Fact]
    public void A_Models_Points_Are_Its_Vertices()
    {
        _app.World.InitResource<DrawList>();
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var drawList = _app.World.Resource<DrawList>();

        DrawModelPoints(cube, Vector3.Zero, 1, Color.Red);

        drawList.Vertices.Length.Should().BeGreaterThan(0);
        drawList.Vertices.ToArray().Select(v => v.Position).Should().OnlyContain(p => MathF.Abs(p.X) <= 0.52f && MathF.Abs(p.Y) <= 0.52f && MathF.Abs(p.Z) <= 0.52f,
            "every point is at a vertex of the unit cube");
    }

    [Fact]
    public void An_Exported_Mesh_Loads_Back_With_Its_Positions_And_Texture_Coordinates()
    {
        var cube = GenMeshCube(1, 2, 3);
        var path = Path.Combine(_folder.Path, "cube.obj");

        ExportMesh(cube, path).Should().BeTrue();
        var loaded = LoadModel(path);

        var store = _app.World.Resource<MeshStore>();
        store.TryGetData(cube.Id, out var made, out var madeIndices).Should().BeTrue();
        loaded.Meshes.Should().ContainSingle();
        store.TryGetData(loaded.Meshes[0].Id, out var read, out var readIndices).Should().BeTrue();
        readIndices.Length.Should().Be(madeIndices.Length, "every triangle comes back");
        // The corners as the triangles name them, each with where it samples the texture.
        static string[] Corners(ModelVertex[] v, uint[] i) =>
            [.. i.Select(n => $"{v[n].Position.X:0.###} {v[n].Position.Y:0.###} {v[n].Position.Z:0.###} {v[n].Uv.X:0.###} {v[n].Uv.Y:0.###}").Order()];
        Corners(read, readIndices).Should().Equal(Corners(made, madeIndices));
    }

    [Fact]
    public void An_Image_From_Text_Is_Its_Bytes_As_Gray_Then_Black()
    {
        var image = GenImageText(4, 2, "AB");

        GetImageColor(image, 0, 0).Should().Be(new Color(65, 65, 65));
        GetImageColor(image, 1, 0).Should().Be(new Color(66, 66, 66));
        GetImageColor(image, 2, 0).Should().Be(new Color(0, 0, 0));
        GetImageColor(image, 3, 1).Should().Be(new Color(0, 0, 0));
    }

    [Fact]
    public void A_Gltf_Keeps_Its_Texture_Coordinates_Counted_From_The_Images_Top()
    {
        // glTF counts V down from the image's top row, as the engine samples, so the coordinates
        // come back as the file has them, where Assimp's own convention turns them over.
        var model = LoadModel(WriteGlbWithEmbeddedPng(new byte[32]));

        _app.World.Resource<MeshStore>().TryGetData(model.Meshes[0].Id, out var vertices, out _).Should().BeTrue();
        vertices.Single(v => v.Position == Vector3.Zero).Uv.Should().Be(new Vector2(0, 0));
        vertices.Single(v => v.Position == Vector3.UnitY).Uv.Should().Be(new Vector2(0, 1));
    }

    [Fact]
    public void A_Model_Files_Materials_Load_Without_Its_Meshes()
    {
        var meshesBefore = _app.World.Resource<MeshStore>().Count;

        var materials = LoadMaterials(WriteGlbWithEmbeddedPng(new byte[32]));

        materials.Should().NotBeEmpty();
        materials.Should().Contain(m => m.Texture.IsValid, "the texture the material names is loaded with it");
        _app.World.Resource<MeshStore>().Count.Should().Be(meshesBefore, "the meshes are let go");
    }
}
