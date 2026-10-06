using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Materials;

[Trait("Category", "Unit")]
public class MaterialDescriptionTests
{
    [Fact]
    public void Defaults_Match_GltfPbr_Spec()
    {
        var d = new MaterialDescription();

        d.Name.Should().Be("Material");
        d.SourcePath.Should().BeNull();
        d.BaseColorFactor.Should().Be(Vector4.One);
        d.MetallicFactor.Should().Be(0f);
        d.RoughnessFactor.Should().Be(1f);
        d.NormalScale.Should().Be(1f);
        d.EmissiveFactor.Should().Be(Vector3.Zero);
        d.OcclusionStrength.Should().Be(1f);
        d.AlphaMode.Should().Be(MaterialAlphaMode.Opaque);
        d.AlphaCutoff.Should().Be(0.5f);
        d.DoubleSided.Should().BeFalse();
    }

    [Fact]
    public void Clone_Returns_Independent_Copy_With_Same_Field_Values()
    {
        var d = new MaterialDescription
        {
            Name = "Brass",
            SourcePath = "/Looks/Brass",
            BaseColorFactor = new Vector4(0.8f, 0.6f, 0.2f, 1f),
            MetallicFactor = 1f,
            RoughnessFactor = 0.3f,
            EmissiveFactor = new Vector3(0.1f, 0.05f, 0f),
            AlphaMode = MaterialAlphaMode.Mask,
            AlphaCutoff = 0.25f,
            DoubleSided = true,
            BaseColorTexture = new MaterialTextureRef("textures/brass.png"),
        };

        var c = d.Clone();

        c.Should().NotBeSameAs(d);
        c.Name.Should().Be("Brass");
        c.SourcePath.Should().Be("/Looks/Brass");
        c.BaseColorFactor.Should().Be(d.BaseColorFactor);
        c.MetallicFactor.Should().Be(1f);
        c.RoughnessFactor.Should().Be(0.3f);
        c.EmissiveFactor.Should().Be(d.EmissiveFactor);
        c.AlphaMode.Should().Be(MaterialAlphaMode.Mask);
        c.AlphaCutoff.Should().Be(0.25f);
        c.DoubleSided.Should().BeTrue();
        // texture ref is a record, so it compares by value rather than by reference
        c.BaseColorTexture.Should().Be(d.BaseColorTexture);

        // mutating the clone must not affect the original
        c.MetallicFactor = 0f;
        d.MetallicFactor.Should().Be(1f);
    }

    [Fact]
    public void MaterialTextureRef_Defaults_Are_UvSet0_RepeatRepeat()
    {
        var t = new MaterialTextureRef("a.png");

        t.UvSet.Should().Be(0);
        t.WrapS.Should().Be(TextureWrapMode.Repeat);
        t.WrapT.Should().Be(TextureWrapMode.Repeat);
    }
}