using System.Numerics;
using FluentAssertions;
using Vortice.Vulkan;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A table loaded from a file, the look of AgX - Powerful.exr the Sponza example grades by, which
// build/unity-lut.py writes from the strip of squares HDRP reads.
public sealed partial class TonemapTests
{
    private static string PowerfulFile => Path.Combine(Api.CheatsheetTests.RepoRoot(), "3DEngine.Examples", "resources", "agx_powerful.ktx2");

    [NeedsVulkanFact]
    public void The_Ramp_Through_A_Table_Loaded_From_A_File_Is_HDRPs_Lookup_Of_It_After_The_Gamma()
    {
        // HDRP's gamma of w 0.225 raises the light to 1 / 1.225 before it is looked up as LogC.
        var frameImage = DrawRamp("lookup", () =>
        {
            SetTonemapLookup(PowerfulFile);
            SetLiftGammaGain(new Vector4(1, 1, 1, 0), new Vector4(1, 1, 1, 0.225f), new Vector4(1, 1, 1, 0));
        });
        GetTonemap().Should().Be(Tonemap.Lookup, "loading a table chooses it");
        var model = new BevyModel(Tonemap.Lookup, TonemapTables.Read(File.ReadAllBytes(PowerfulFile)));
        var worst = (Apart: 0, X: 0, Y: 0, Got: Color.Black, Expected: Color.Black);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var light = Ramp(x, y);
                var expected = model.Shown(new Vector3(MathF.Pow(light.X, 1 / 1.225f), MathF.Pow(light.Y, 1 / 1.225f), MathF.Pow(light.Z, 1 / 1.225f)));
                var got = GetImageColor(frameImage, x, y);
                var apart = Math.Max(Math.Max(Math.Abs(got.R - expected.R), Math.Abs(got.G - expected.G)), Math.Abs(got.B - expected.B));
                if (apart > worst.Apart) worst = (apart, x, y, got, expected);
            }
        worst.Apart.Should().BeLessThanOrEqualTo(4,
            $"the ramp is the table's lookup of its light as HDRP gives it, at worst {worst.Got} where {worst.Expected} at {worst.X}, {worst.Y}");
    }

    [Fact]
    public void A_Table_Written_From_HDRPs_Strip_Holds_Its_Corners_Where_HDRP_Reads_Them()
    {
        // The strip's corners as ImageMagick reads them from AgX - Powerful.exr, the first square's
        // bottom left black and its top left the green corner, the last square's bottom left blue.
        var table = TonemapTables.Read(File.ReadAllBytes(PowerfulFile));
        table.Should().Match<TonemapTables.Table>(t => t.Size == 32 && t.Format == VkFormat.R16G16B16A16Sfloat);
        Vector3 At(int r, int g, int b)
        {
            var i = (r + 32 * (g + 32 * b)) * 8;
            return new Vector3((float)BitConverter.ToHalf(table.Texels, i), (float)BitConverter.ToHalf(table.Texels, i + 2),
                (float)BitConverter.ToHalf(table.Texels, i + 4));
        }
        At(0, 0, 0).Should().Be(Vector3.Zero);
        At(31, 0, 0).Should().Be(new Vector3(1.30859375f, 1.0234375f, 0.96240234375f));
        At(0, 31, 0).Should().Be(new Vector3(1.1728515625f, 1.283203125f, 1.0947265625f));
        At(0, 0, 31).Should().Be(new Vector3(0.95556640625f, 1.072265625f, 1.30859375f));
        At(31, 31, 31).Should().Be(new Vector3(1.2705078125f));
    }
}
