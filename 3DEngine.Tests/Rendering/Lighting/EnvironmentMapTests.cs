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
}
