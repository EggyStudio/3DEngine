using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Models;

[Trait("Category", "Unit")]
public class MaterialPackingTests
{
    // E5B9G9R9 read back the way modelpass.slang reads it.
    private static Vector3 Unpack(uint e)
    {
        var scale = MathF.Pow(2, (int)(e >> 27) - 24);
        return new Vector3(e & 0x1FF, (e >> 9) & 0x1FF, (e >> 18) & 0x1FF) * scale;
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(1f, 0f, 0f)]
    [InlineData(0.5f, 0.25f, 0.125f)]
    [InlineData(4f, 2f, 1f)]
    [InlineData(0.01f, 0.02f, 0.03f)]
    public void Emission_Packs_Into_32_Bits_And_Back_Within_A_Mantissa_Step(float r, float g, float b)
    {
        var color = new Vector3(r, g, b);
        var back = Unpack(ModelRenderer.Rgb9E5(color));

        var step = MathF.Max(r, MathF.Max(g, b)) / 256;
        Vector3.Distance(back, color).Should().BeLessThanOrEqualTo(step * 1.8f, "each channel is within half a step of a 9-bit mantissa");
    }

    [Fact]
    public void Emission_Past_The_Format_Is_Clamped_And_Below_Zero_Is_Black()
    {
        Unpack(ModelRenderer.Rgb9E5(new Vector3(1e9f, -1, 0))).X.Should().BeApproximately(65408, 1);
        Unpack(ModelRenderer.Rgb9E5(new Vector3(-1))).Should().Be(Vector3.Zero);
    }
}
