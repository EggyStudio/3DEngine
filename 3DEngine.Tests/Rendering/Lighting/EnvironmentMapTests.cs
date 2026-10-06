using System.Numerics;
using FluentAssertions;
using Xunit;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering.Lighting;

/// <summary>An environment's image decoded on the CPU, which the renderer filters on the GPU (<see cref="EnvironmentFilterTests"/>).</summary>
[Trait("Category", "Unit")]
public class EnvironmentMapTests
{
    // The light of the image's pixel at (x, y).
    private static Vector3 Pixel(EnvironmentMap map, int x, int y)
    {
        var at = (y * map.Width + x) * 4;
        return new Vector3((float)map.Pixels[at], (float)map.Pixels[at + 1], (float)map.Pixels[at + 2]);
    }

    // A Radiance file of flat RGBE pixels, which stb reads when a scanline does not start as a run.
    internal static byte[] Hdr(int width, int height, Func<int, Vector3> row)
    {
        var bytes = new List<byte>(System.Text.Encoding.ASCII.GetBytes($"#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y {height} +X {width}\n"));
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var c = row(y);
            var peak = MathF.Max(c.X, MathF.Max(c.Y, c.Z));
            if (peak < 1e-32f)
            {
                bytes.AddRange([0, 0, 0, 0]);
                continue;
            }
            // As frexp has it, the peak is a mantissa from a half to one times 2 to the exponent.
            int exponent = (int)MathF.Floor(MathF.Log2(peak)) + 1;
            var scale = 256f / MathF.Pow(2, exponent);
            bytes.AddRange([(byte)(c.X * scale), (byte)(c.Y * scale), (byte)(c.Z * scale), (byte)(exponent + 128)]);
        }
        return [.. bytes];
    }

    [Fact]
    public void An_Image_Is_Decoded_From_Srgb_To_Linear_Light()
    {
        var map = EnvironmentMap.FromEquirectangular(GenImageColor(16, 8, new Color(0, 188, 0)), faceSize: 16);

        Pixel(map, 3, 5).Y.Should().BeApproximately(0.5f, 0.01f, "sRGB 188 is half the light");
        (map.Size, map.MipLevels).Should().Be((16, 5));
        map.SkySize.Should().Be(16, "the sky is a quarter of the image's width, no smaller than the filtered cube");
    }

    [Fact]
    public void An_HDR_File_Keeps_Light_Past_White()
    {
        // A sun 20 times white above the horizon, and dim ground below.
        var map = EnvironmentMap.FromHdrFile(Hdr(16, 8, y => y < 4 ? new Vector3(20, 18, 16) : new Vector3(0.05f)), faceSize: 16);

        Pixel(map, 0, 0).X.Should().BeApproximately(20, 0.1f, "the sky keeps its own brightness");
        Pixel(map, 0, 7).X.Should().BeApproximately(0.05f, 0.01f);
    }

    [Fact]
    public void An_Image_Wider_Than_A_Device_Makes_Is_Halved_And_A_Broken_Pixel_Is_Dark()
    {
        var pixels = Enumerable.Repeat(new Vector3(2), 8192 * 2).ToArray();
        pixels[0] = new Vector3(float.NaN);
        var map = EnvironmentMap.FromLinear(pixels, 8192, 2);

        (map.Width, map.Height).Should().Be((4096, 1));
        Pixel(map, 0, 0).Should().Be(Vector3.Zero, "a pixel that is not a number spreads through no average");
        Pixel(map, 1, 0).Should().Be(new Vector3(2));
        map.SkySize.Should().Be(512, "a quarter of the width, at most 512");
    }

    [Fact]
    public void Bytes_That_Are_Not_A_Radiance_Image_Are_Refused()
    {
        var act = () => EnvironmentMap.FromHdrFile([1, 2, 3, 4]);
        act.Should().Throw<ArgumentException>();
        SetEnvironmentMap("no-such-sky.hdr").Should().BeFalse("a missing file is reported rather than thrown");
    }
}
