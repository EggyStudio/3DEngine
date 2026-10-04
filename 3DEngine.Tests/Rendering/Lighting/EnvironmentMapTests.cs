using System.Numerics;
using FluentAssertions;
using Xunit;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering.Lighting;

[Trait("Category", "Unit")]
public class EnvironmentMapTests
{
    // An equirectangular image, red above the horizon and blue below.
    private static Image Sky(int width = 64, int height = 32)
    {
        var data = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var at = (y * width + x) * 4;
            data[at] = (byte)(y < height / 2 ? 255 : 0);
            data[at + 2] = (byte)(y < height / 2 ? 0 : 255);
            data[at + 3] = 255;
        }
        return new Image(data, width, height);
    }

    // The texel at the middle of a face of a mip.
    private static Vector3 Middle(EnvironmentMap map, int mip, int face)
    {
        int offset = 0;
        for (int m = 0; m < mip; m++) offset += Math.Max(1, map.Size >> m) * Math.Max(1, map.Size >> m) * 6 * 4;
        int s = Math.Max(1, map.Size >> mip);
        int at = offset + ((face * s + s / 2) * s + s / 2) * 4;
        return new Vector3((float)map.Texels[at], (float)map.Texels[at + 1], (float)map.Texels[at + 2]);
    }

    [Fact]
    public void A_Face_Looks_Along_Its_Axis_As_Vulkan_Has_It()
    {
        EnvironmentMap.Direction(0, 0, 0).Should().Be(Vector3.UnitX);
        EnvironmentMap.Direction(2, 0, 0).Should().Be(Vector3.UnitY);
        EnvironmentMap.Direction(5, 0, 0).Should().Be(-Vector3.UnitZ);
        // The +X face's top left texel looks up and toward +Z.
        var corner = EnvironmentMap.Direction(0, -1, -1);
        (corner.Y > 0 && corner.Z > 0).Should().BeTrue();
    }

    [Fact]
    public void A_Uniform_Sky_Is_Its_Linear_Color_At_Every_Mip()
    {
        var map = EnvironmentMap.FromEquirectangular(GenImageColor(16, 8, new Color(0, 188, 0)), faceSize: 16);

        map.MipLevels.Should().Be(5);
        for (int mip = 0; mip < map.MipLevels; mip++)
            Middle(map, mip, 4).Y.Should().BeApproximately(0.5f, 0.01f, "sRGB 188 is half the light, however rough");
    }

    [Fact]
    public void A_Uniform_Sky_Lights_A_Diffuse_Surface_Its_Own_Color_Whichever_Way_It_Faces()
    {
        var map = EnvironmentMap.FromEquirectangular(GenImageColor(64, 32, new Color(0, 188, 0)), faceSize: 16);

        foreach (var normal in new[] { Vector3.UnitY, -Vector3.UnitY, Vector3.UnitX, Vector3.Normalize(new Vector3(1, 1, -1)) })
            map.IrradianceAt(normal).Y.Should().BeApproximately(0.5f, 0.01f, "the whole sky's light, weighted by the cosine and divided by pi, is the sky's own");
    }

    [Fact]
    public void A_Sky_Lit_From_Above_Lights_What_Faces_Up_And_Half_Lights_What_Faces_Sideways()
    {
        // White above the horizon and black below, so a surface facing up sees all of the light,
        // one facing sideways half of it and one facing down none, which three bands come close to.
        var data = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 16; i++) { data[i * 4] = data[i * 4 + 1] = data[i * 4 + 2] = 255; }
        for (int i = 0; i < 64 * 32; i++) data[i * 4 + 3] = 255;
        var map = EnvironmentMap.FromEquirectangular(new Image(data, 64, 32), faceSize: 16);

        map.IrradianceAt(Vector3.UnitY).X.Should().BeApproximately(1f, 0.1f);
        map.IrradianceAt(Vector3.UnitZ).X.Should().BeApproximately(0.5f, 0.05f);
        map.IrradianceAt(-Vector3.UnitY).X.Should().BeApproximately(0f, 0.1f);
    }

    [Fact]
    public void The_Top_Of_The_Image_Is_Up_And_Roughness_Blurs_Toward_The_Horizon()
    {
        var map = EnvironmentMap.FromEquirectangular(Sky(), faceSize: 32);

        Middle(map, 0, 2).X.Should().BeApproximately(1, 0.01f, "straight up is the top row, red");
        Middle(map, 0, 3).Z.Should().BeApproximately(1, 0.01f, "straight down is blue");
        var sideways = Middle(map, 0, 4);
        var roughSideways = Middle(map, map.MipLevels - 1, 4);
        (roughSideways.X > 0.2f && roughSideways.Z > 0.2f).Should().BeTrue("a rough surface facing the horizon sees both halves");
        sideways.Should().NotBe(roughSideways);
    }

    // A Radiance file of flat RGBE pixels, which stb reads when a scanline does not start as a run.
    private static byte[] Hdr(int width, int height, Func<int, Vector3> row)
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
    public void An_HDR_File_Keeps_Light_Past_White()
    {
        // A sun 20 times white above the horizon, and dim ground below.
        var file = Hdr(16, 8, y => y < 4 ? new Vector3(20, 18, 16) : new Vector3(0.05f));
        var map = EnvironmentMap.FromHdrFile(file, faceSize: 16);

        Middle(map, 0, 2).X.Should().BeApproximately(20, 0.5f, "straight up is the sun, at its own brightness");
        Middle(map, 0, 3).X.Should().BeApproximately(0.05f, 0.01f, "straight down is the ground");
        Middle(map, map.MipLevels - 1, 2).X.Should().BeGreaterThan(5, "a rough surface facing up still gathers mostly sun");
    }

    [Fact]
    public void Bytes_That_Are_Not_A_Radiance_Image_Are_Refused()
    {
        var act = () => EnvironmentMap.FromHdrFile([1, 2, 3, 4]);
        act.Should().Throw<ArgumentException>();
        SetEnvironmentMap("no-such-sky.hdr").Should().BeFalse("a missing file is reported rather than thrown");
    }
}
