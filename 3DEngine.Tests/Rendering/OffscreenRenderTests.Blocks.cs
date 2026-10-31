using System.Numerics;
using Engine.Tests.Assets.Textures;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void KTX2_Files_Of_BC1_BC5_And_BC7_Blocks_Draw_Their_Colors_At_Every_Level_And_Know_Their_Average()
    {
        Open(32, 8, samples: 1);
        string File(string name, byte[] bytes)
        {
            var path = Path.Combine(_folder.Path, name);
            System.IO.File.WriteAllBytes(path, bytes);
            return path;
        }
        var color = LoadTexture(File("color.ktx2", Ktx2Files.Solid(146, 16, Ktx2Files.Bc7(1, 201, 1, 255))));
        var rough = LoadTexture(File("rough.ktx2", Ktx2Files.Solid(131, 8, Ktx2Files.Bc1(255, 0, 0))));
        var normal = LoadTexture(File("normal.ktx2", Ktx2Files.Solid(141, 8, Ktx2Files.Bc5(200, 100))));
        var glass = LoadTexture(File("glass.ktx2", Ktx2Files.Solid(146, 4, Ktx2Files.Bc7(255, 255, 255, 129))));
        var wide = LoadTexture(File("wide.ktx2", Ktx2Files.Solid(131, 128, Ktx2Files.Bc1(255, 0, 0))));
        var pixels = LoadTextureFromImage(GenImageColor(8, 8, new Color(1, 201, 1)));
        (color.IsValid, rough.IsValid, normal.IsValid, glass.IsValid).Should().Be((true, true, true, true));
        (color.Width, color.Height, color.Mipmaps).Should().Be((16, 16, 5));
        var single = LoadTexture(File("single.ktx2", Ktx2Files.Solid(131, 8, Ktx2Files.Bc1(255, 0, 0), count: 1)));
        var kept = single;
        GenTextureMipmaps(ref kept);
        kept.Should().Be(single, "a texture of blocks has the levels its file holds and the GPU makes none");

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTexture(color, 0, 0, Color.White);
            DrawTexture(rough, 8, 0, Color.White);
            DrawTexture(normal, 16, 0, Color.White);
            // The 16 texels a side drawn on 4 pixels, which samples the levels past the first.
            DrawTexturePro(color, new Rectangle(0, 0, 16, 16), new Rectangle(24, 0, 4, 4), Vector2.Zero, 0, Color.White);
        }, "blocks");

        GetImageColor(image, 4, 4).Should().Be(new Color(1, 201, 1), "a BC7 block of one color decodes to it exactly");
        GetImageColor(image, 12, 4).Should().Be(new Color(255, 0, 0), "BC1's red in 5 bits is 255");
        GetImageColor(image, 20, 4).Should().Be(new Color(200, 100, 0), "BC5 holds red and green, blue reading 0 and alpha 1");
        GetImageColor(image, 26, 2).Should().Be(new Color(1, 201, 1), "every level the file holds is uploaded");

        var store = GetApp().World.Resource<TextureStore>();
        store.AverageColor(color.Id).Should().NotBeNull("the first level is unpacked and read back the frame it is uploaded in");
        Vector3.Distance(store.AverageColor(color.Id)!.Value, store.AverageColor(pixels.Id)!.Value).Should().BeLessThan(1e-5f,
            "the average is the one a texture of the same pixels has");
        Vector3.Distance(store.AverageColor(wide.Id)!.Value, Vector3.UnitX).Should().BeLessThan(1e-5f, "a BC1 texture's average is read from a level of 64 texels");
        (store.IsTranslucent(color.Id), store.IsTranslucent(glass.Id), store.IsTranslucent(wide.Id)).Should().Be((false, true, false));
        GetImageColor(LoadImageFromTexture(color), 15, 15).Should().Be(new Color(1, 201, 1), "a texture of blocks is read back as its pixels");
        UnloadTexture(color);
        UnloadTexture(rough);
        UnloadTexture(normal);
        UnloadTexture(glass);
        UnloadTexture(pixels);
        UnloadTexture(single);
        UnloadTexture(wide);
    }

    [NeedsVulkanFact]
    public void A_BC5_Normal_Map_Of_A_Flat_Surface_Lights_It_As_No_Map_Does()
    {
        Open(32, 32, samples: 1);
        var path = Path.Combine(_folder.Path, "flat.ktx2");
        File.WriteAllBytes(path, Ktx2Files.Solid(141, 8, Ktx2Files.Bc5(128, 128)));
        var flat = LoadTexture(path);
        var plain = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        var mapped = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        mapped.Materials[0].NormalMap = flat;
        CreateDirectionalLight(-Vector3.UnitY, Color.White, 1);
        var camera = new Camera3D(new Vector3(0, 3, 0.01f), Vector3.Zero, Vector3.UnitY, 45);
        Color Floor(Model model) => GetImageColor(Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(model, Vector3.Zero, 1, Color.Gray);
            EndMode3D();
        }, "floor"), 16, 16);

        var (without, with) = (Floor(plain), Floor(mapped));
        without.R.Should().BeGreaterThan(60, "the light falls straight on the floor");
        Math.Abs(with.R - without.R).Should().BeLessThanOrEqualTo(2, "a map of x and y at their middles is the surface's own normal, its z worked out from them");
        UnloadModel(plain);
        UnloadModel(mapped);
        UnloadTexture(flat);
    }
}
