using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;

namespace Engine.Tests.Rendering.Models;

[Trait("Category", "Unit")]
public class MaterialFactorsTests
{
    [Fact]
    public void The_Block_Is_Laid_Out_As_The_Shader_Reads_It()
    {
        Marshal.SizeOf<ModelRenderer.MaterialFactors>().Should().Be(ModelRenderer.MaterialFactors.Size);
        var bytes = new byte[ModelRenderer.MaterialFactors.Size];
        var factors = new ModelRenderer.MaterialFactors(new Vector4(1, 2, 3, 4), new Vector4(5, 6, 7, 0), 8, 9, 10, 11);
        MemoryMarshal.Write(bytes, in factors);

        MemoryMarshal.Cast<byte, float>(bytes).ToArray().Should().Equal(1, 2, 3, 4, 5, 6, 7, 0, 8, 9, 10, 11);
    }

    [Fact]
    public void A_Draws_Color_Is_Decoded_To_Linear_And_A_Missing_Normal_Map_Bends_Nothing()
    {
        var factors = ModelRenderer.MaterialFactors.Of(new ModelDraw(1, Matrix4x4.Identity, Matrix4x4.Identity, new Color(255, 188, 0, 128), 0,
            NormalScale: 2, Emission: new Vector3(3, 0, 0)));

        factors.Color.X.Should().Be(1);
        factors.Color.Y.Should().BeApproximately(0.5f, 0.005f, "sRGB 188 is half the light");
        factors.Color.W.Should().BeApproximately(0.5f, 0.005f, "alpha is linear as it is");
        factors.Emission.X.Should().Be(3, "emission is linear and may pass 1");
        factors.NormalScale.Should().Be(0, "the draw has no normal map");
    }
}
