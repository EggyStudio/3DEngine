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
}
