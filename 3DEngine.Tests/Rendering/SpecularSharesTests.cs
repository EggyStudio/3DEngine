using FluentAssertions;

namespace Engine.Tests.Rendering;

/// <summary>
/// The split sum's table of the specular lobe's shares of the light from all around
/// (<see cref="SpecularShares"/>), held to what GGX's lobe gives at known points and to the table's
/// own texels.
/// </summary>
public sealed class SpecularSharesTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void A_Metal_Of_Half_Its_F0_Reflects_What_GGXs_Lobe_Gives()
    {
        // F0 of 0.502, the emissive metal box's, its share 0.414 at roughness 0.6 and 0.154 at 1
        // seen square on, and 0.446 and 0.329 at a cosine of 0.2, by GGX's lobe sampled 4,096 times
        // with Smith's height-correlated shadowing apart from this code.
        float Share(float nv, float roughness)
        {
            var (scale, bias) = SpecularShares.At(nv, roughness);
            return 0.502f * scale + bias;
        }
        Share(1, 0.6f).Should().BeApproximately(0.414f, 0.004f);
        Share(1, 1).Should().BeApproximately(0.154f, 0.004f);
        Share(0.2f, 0.6f).Should().BeApproximately(0.446f, 0.006f);
        Share(0.2f, 1).Should().BeApproximately(0.329f, 0.006f);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Smooth_Face_Seen_Square_On_Reflects_Its_F0_And_The_Table_Holds_Each_Texels_Middle()
    {
        var (scale, bias) = SpecularShares.At(1, 0.02f);
        scale.Should().BeApproximately(1, 0.01f, "a mirror reflects its F0 square on");
        bias.Should().BeApproximately(0, 0.01f);

        var table = SpecularShares.Table();
        table.Should().HaveCount(SpecularShares.Size * SpecularShares.Size * 4);
        var (row, column) = (19, 7);
        var (s, b) = SpecularShares.At((column + 0.5f) / SpecularShares.Size, (row + 0.5f) / SpecularShares.Size);
        var at = (row * SpecularShares.Size + column) * 4;
        ((float)table[at]).Should().BeApproximately(s, 1e-3f);
        ((float)table[at + 1]).Should().BeApproximately(b, 1e-3f);
    }
}
