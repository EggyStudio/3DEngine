using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>The flat API's color functions, checked against what raylib's give, and its random values.</summary>
/// <remarks>
/// In the flat API's collection, since its random values come from one generator every program
/// drawing them shares, and a program run beside a seed and the values read after it moved them.
/// </remarks>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public class ColorRandomTests
{
    [Fact]
    public void Hsv_Goes_Round_The_Hue_Circle_As_Raylib_Does()
    {
        ColorFromHSV(0, 1, 1).Should().Be(new Color(255, 0, 0));
        ColorFromHSV(120, 1, 1).Should().Be(new Color(0, 255, 0));
        ColorFromHSV(240, 1, 0.5f).Should().Be(new Color(0, 0, 127));
        ColorFromHSV(60, 0, 1).Should().Be(new Color(255, 255, 255));

        ColorToHSV(new Color(0, 0, 255)).Should().Be(new Vector3(240, 1, 1));
        ColorToHSV(new Color(255, 255, 0)).Should().Be(new Vector3(60, 1, 1));
        ColorToHSV(new Color(130, 130, 130)).Should().Be(new Vector3(0, 0, 130 / 255f));
        ColorToHSV(new Color(0, 0, 0)).Should().Be(Vector3.Zero);
    }

    [Fact]
    public void A_Color_Packs_Into_A_Number_And_Back()
    {
        ColorToInt(new Color(0x12, 0x34, 0x56, 0x78)).Should().Be(0x12345678);
        GetColor(0x12345678).Should().Be(new Color(0x12, 0x34, 0x56, 0x78));
        GetColor(unchecked((uint)ColorToInt(Color.Gold))).Should().Be(Color.Gold);
        ColorFromNormalized(ColorNormalize(Color.SkyBlue)).Should().Be(Color.SkyBlue);
    }

    [Fact]
    public void Tint_Brightness_Contrast_And_Alpha_Work_Channel_By_Channel()
    {
        ColorTint(new Color(200, 100, 50), new Color(128, 255, 0)).Should().Be(new Color(100, 100, 0));
        ColorBrightness(new Color(100, 100, 100, 7), -0.5f).Should().Be(new Color(50, 50, 50, 7));
        ColorBrightness(new Color(100, 100, 100), 0.5f).Should().Be(new Color(177, 177, 177));
        ColorContrast(new Color(255, 0, 128, 9), -1).Should().Be(new Color(127, 127, 127, 9));
        Fade(Color.Red, 0.5f).Should().Be(Color.Red with { A = 127 });
        ColorAlpha(Color.Red, 2).A.Should().Be(255);
    }

    [Fact]
    public void Alpha_Blend_Lays_The_Source_Over_By_Its_Alpha()
    {
        var white = new Color(255, 255, 255);
        ColorAlphaBlend(white, new Color(0, 0, 0, 128), white).Should().Be(new Color(126, 126, 126, 255));
        ColorAlphaBlend(white, new Color(10, 20, 30, 255), white).Should().Be(new Color(10, 20, 30, 255));
        ColorAlphaBlend(white, new Color(10, 20, 30, 0), white).Should().Be(white);
    }

    [Fact]
    public void Lerp_Goes_From_One_Color_To_The_Other()
    {
        ColorLerp(Color.Black, Color.White, 0.5f).Should().Be(new Color(127, 127, 127));
        ColorLerp(Color.Black, Color.White, 2).Should().Be(Color.White);
        ColorIsEqual(Color.Red, new Color(230, 41, 55)).Should().BeTrue();
    }

    [Fact]
    public void A_Seed_Repeats_The_Values_And_Bounds_Are_Included()
    {
        SetRandomSeed(42);
        var first = Enumerable.Range(0, 20).Select(_ => GetRandomValue(0, 1000)).ToArray();
        SetRandomSeed(42);
        Enumerable.Range(0, 20).Select(_ => GetRandomValue(0, 1000)).Should().Equal(first);

        // Bounds in either order, each reached.
        var seen = Enumerable.Range(0, 1000).Select(_ => GetRandomValue(3, 1)).ToHashSet();
        seen.Should().BeEquivalentTo([1, 2, 3]);
        GetRandomValue(5, 5).Should().Be(5);
        var wide = GetRandomValue(int.MinValue, int.MaxValue);
        wide.Should().BeInRange(int.MinValue, int.MaxValue);
    }

    // The values raylib's rprand.h gives for these seeds, printed by a C program that includes it
    // from the checkout build/raylib-bench/run.sh pins.
    [Fact]
    public void A_Seed_Gives_The_Values_Raylib_Gives_For_It()
    {
        SetRandomSeed(42);
        Enumerable.Range(0, 5).Select(_ => GetRandomValue(0, 1000)).Should().Equal(797, 798, 285, 181, 433);
        SetRandomSeed(42);
        Enumerable.Range(0, 5).Select(_ => GetRandomValue(-50, 50)).Should().Equal(-22, -17, -3, -46, 38);
        SetRandomSeed(7);
        LoadRandomSequence(6, 1, 10).Should().Equal(5, 4, 3, 9, 1, 7);
    }

    [Fact]
    public void A_Seed_Gives_Raylibs_Noise_Images()
    {
        SetRandomSeed(42);
        var noise = GenImageWhiteNoise(4, 2, 0.5f);
        Enumerable.Range(0, 8).Select(i => GetImageColor(noise, i % 4, i / 4) == Color.White ? 1 : 0)
            .Should().Equal(1, 1, 1, 1, 1, 1, 0, 0);

        // Each cell's point is darkest, so the four points raylib places are where the image is black.
        SetRandomSeed(3);
        var cells = GenImageCellular(16, 16, 8);
        foreach (var (x, y) in new[] { (4, 0), (15, 5), (6, 8), (15, 10) })
            GetImageColor(cells, x, y).R.Should().Be(0, $"raylib places a point at {x}, {y}");
    }

    [Fact]
    public void A_Sequence_Holds_Different_Values_In_The_Range()
    {
        LoadRandomSequence(10, 1, 10).Should().BeEquivalentTo(Enumerable.Range(1, 10));
        LoadRandomSequence(11, 1, 10).Should().BeEmpty();
        LoadRandomSequence(0, 1, 10).Should().BeEmpty();

        var sparse = LoadRandomSequence(500, 0, 1_000_000);
        sparse.Should().HaveCount(500).And.OnlyHaveUniqueItems();
        sparse.Should().OnlyContain(v => v >= 0 && v <= 1_000_000);
    }
}
