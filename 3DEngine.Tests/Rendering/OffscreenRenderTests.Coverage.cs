using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Masked_Cutouts_Edge_Shares_Its_Pixels_Between_Samples_Where_The_Frame_Has_Several()
    {
        // A disc cut from a blue quad by its texture's alpha, before a red wall. With one sample a
        // pixel shows the disc or the wall. With four, a pixel the cutout's edge crosses shows both
        // in the share of its samples the disc covers, as a pixel a triangle's edge crosses does.
        var disc = new byte[32 * 32 * 4];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            var inside = (x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f) < 12 * 12;
            (disc[(y * 32 + x) * 4], disc[(y * 32 + x) * 4 + 1], disc[(y * 32 + x) * 4 + 2], disc[(y * 32 + x) * 4 + 3]) =
                (255, 255, 255, inside ? (byte)255 : (byte)0);
        }

        var single = Mixed(MaskedBeforeAWall(1, new Image(disc, 32, 32), "disc-1"));
        var four = Mixed(MaskedBeforeAWall(4, new Image(disc, 32, 32), "disc-4"));

        single.Should().Be(0, "with one sample a pixel is in the disc or not");
        four.Should().BeGreaterThan(60, $"with four a pixel the cutout's edge crosses is partly covered, {four} of them");
    }

    [NeedsVulkanFact]
    public void A_Masked_Edge_Shared_Between_Samples_Stays_A_Pixel_Wide_Where_Its_Texture_Is_Magnified()
    {
        // A texture of two texels, clear and solid, filtered across the quad, so its alpha climbs
        // from 0 to 1 over half the quad's width. Taken as the samples covered as it is, the cut
        // would fade over that half. Sharpened by how fast the alpha changes across the screen, it
        // is cut where the alpha crosses the cutoff, within a pixel or two.
        var image = MaskedBeforeAWall(4, new Image([255, 255, 255, 0, 255, 255, 255, 255], 2, 1), "ramp-4");

        var row = Mixed(image, 48);
        row.Should().BeLessThanOrEqualTo(2, $"the cut is sharp where the texture is magnified, {row} pixels of the row mixing the quad and the wall");
        Count(image, 0, 48, 96, 49, c => c.B > 40 && c.R < 10).Should().BeGreaterThan(10, "the solid half of the quad is drawn");
    }

    // A quad of the texture given, masked and blue, before a red wall, drawn at the samples given.
    private Image MaskedBeforeAWall(int samples, Image pattern, string name)
    {
        Open(96, 96, samples);
        var texture = LoadTextureFromImage(pattern);
        SetTextureWrap(texture, TextureWrap.Clamp);
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var wall = LoadModelFromMesh(GenMeshCube(8, 8, 0.1f));
        var front = LoadModelFromMesh(GenMeshPlane(2.4f, 2.4f, 1, 1));
        front.Materials[0] = new ModelMaterial(new Color(0, 0, 255), texture) { AlphaMode = MaterialAlphaMode.Mask };
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(front, new Vector3(0, 0, 0.5f), Vector3.UnitX, 90, Vector3.One, Color.White);
            DrawModel(wall, new Vector3(0, 0, -1), 1, new Color(255, 0, 0));
            EndMode3D();
        }, name);
        UnloadModel(front);
        UnloadModel(wall);
        UnloadTexture(texture);
        CloseWindow();
        UseApp(null);
        return image;
    }

    // How many pixels of the image, or of its row given, show both the blue quad and the red wall.
    private static int Mixed(Image image, int row = -1) =>
        Count(image, 0, row < 0 ? 0 : row, image.Width, row < 0 ? image.Height : row + 1, c => c.R > 30 && c.B > 30);
}
