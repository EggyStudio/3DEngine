using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;

namespace Engine.Tests.Rendering.Models;

[Trait("Category", "Unit")]
public class ModelInstanceTests
{
    [Fact]
    public void An_Instance_Is_Laid_Out_As_The_Shader_Reads_It()
    {
        Marshal.SizeOf<ModelRenderer.Instance>().Should().Be(ModelRenderer.Instance.Size);
        var world = Matrix4x4.CreateScale(2) * Matrix4x4.CreateTranslation(5, 6, 7);
        var instance = ModelRenderer.Instance.Of(new ModelDraw(1, world, Matrix4x4.Identity, Color.White, 0,
            Metallic: 0.25f, Roughness: 0.5f, NormalMap: 3, NormalScale: 0.75f, OcclusionStrength: 1), Matrix4x4.Identity);
        var floats = MemoryMarshal.Cast<ModelRenderer.Instance, float>(new[] { instance });

        // The transform by its rows, then the world matrix's columns, which carry the translation last.
        floats[..4].ToArray().Should().Equal(2, 0, 0, 0);
        floats[12..16].ToArray().Should().Equal(5, 6, 7, 1);
        floats[16..20].ToArray().Should().Equal(2, 0, 0, 5);
        floats[24..28].ToArray().Should().Equal(0, 0, 2, 7);
        floats[36..40].ToArray().Should().Equal(0.25f, 0.5f, 0.75f, 1);
    }

    [Fact]
    public void A_Draws_Color_Is_Decoded_To_Linear_And_A_Missing_Normal_Map_Bends_Nothing()
    {
        var instance = ModelRenderer.Instance.Of(new ModelDraw(1, Matrix4x4.Identity, Matrix4x4.Identity, new Color(255, 188, 0, 128), 0,
            NormalScale: 2, Emission: new Vector3(3, 0, 0)), Matrix4x4.Identity);

        instance.Color.X.Should().Be(1);
        instance.Color.Y.Should().BeApproximately(0.5f, 0.005f, "sRGB 188 is half the light");
        instance.Color.W.Should().BeApproximately(0.5f, 0.005f, "alpha is linear as it is");
        instance.Emission.X.Should().Be(3, "emission is linear and may pass 1");
        instance.Factors.Z.Should().Be(0, "the draw has no normal map");
    }

    [Fact]
    public void The_Transform_Carries_The_Camera_And_The_World_Rows_Do_Not()
    {
        var world = Matrix4x4.CreateTranslation(1, 2, 3);
        var camera = Matrix4x4.CreateScale(10);
        var instance = ModelRenderer.Instance.Of(new ModelDraw(1, world, camera, Color.White, 0), camera);

        instance.Transform.Should().Be(world * camera);
        instance.WorldX.W.Should().Be(1, "the world rows hold the world translation alone");
    }
}
