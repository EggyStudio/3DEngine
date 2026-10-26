using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Components;

/// <summary>
/// Tests for the PBR <see cref="Material"/> component, its factor defaults, texture-handle
/// slot defaults, and the back-compat "albedo only" constructor.
/// </summary>
[Trait("Category", "Unit")]
public class MaterialTests
{
    [Fact]
    public void Default_Material_Has_Invalid_Texture_Handles()
    {
        var m = default(Material);

        m.BaseColorTexture.IsValid.Should().BeFalse();
        m.MetallicRoughnessTexture.IsValid.Should().BeFalse();
        m.NormalTexture.IsValid.Should().BeFalse();
        m.EmissiveTexture.IsValid.Should().BeFalse();
        m.OcclusionTexture.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Albedo_Constructor_Sets_Reasonable_PBR_Defaults()
    {
        var m = new Material(new Vector4(0.2f, 0.4f, 0.6f, 1f));

        m.Albedo.Should().Be(new Vector4(0.2f, 0.4f, 0.6f, 1f));
        m.MetallicFactor.Should().Be(0f);
        m.RoughnessFactor.Should().Be(0.5f);
        m.EmissiveFactor.Should().Be(Vector3.Zero);
        m.NormalScale.Should().Be(1f);
        m.OcclusionStrength.Should().Be(1f);
        m.CastsShadows.Should().BeTrue();
        m.BouncesLight.Should().BeTrue();

        m.BaseColorTexture.IsValid.Should().BeFalse();
        m.MetallicRoughnessTexture.IsValid.Should().BeFalse();
        m.NormalTexture.IsValid.Should().BeFalse();
        m.EmissiveTexture.IsValid.Should().BeFalse();
        m.OcclusionTexture.IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_Mesh_Entitys_Material_Defaults_As_A_Models_Material_Does_In_Every_Field_They_Share()
    {
        // A mesh drawn through DrawMesh and the same mesh as an entity, each of a material made
        // from the color alone, are drawn alike.
        var color = new Color(200, 120, 40, 255);
        var entity = new Material(color);
        var model = new ModelMaterial(color);
        entity.MetallicFactor.Should().Be(model.Metallic);
        entity.RoughnessFactor.Should().Be(model.Roughness);
        entity.NormalScale.Should().Be(model.NormalScale);
        entity.OcclusionStrength.Should().Be(model.OcclusionStrength);
        entity.EmissiveFactor.Should().Be(new Vector3(model.Emissive.R, model.Emissive.G, model.Emissive.B) / 255 * model.EmissiveIntensity);
        entity.AlphaMode.Should().Be(model.AlphaMode);
        entity.AlphaCutoff.Should().Be(model.AlphaCutoff);
        entity.DoubleSided.Should().Be(model.DoubleSided);
        entity.CastsShadows.Should().Be(model.CastsShadows);
        entity.BouncesLight.Should().Be(model.BouncesLight);
        entity.SubsurfaceColor.Should().Be(new Vector3(model.SubsurfaceColor.R, model.SubsurfaceColor.G, model.SubsurfaceColor.B) / 255);
        entity.SubsurfaceRadius.Should().Be(model.SubsurfaceRadius);
        entity.SubsurfaceThickness.Should().Be(model.SubsurfaceThickness);
        entity.LightThrough.Should().Be(model.LightThrough);
        entity.Shader.Should().Be(model.Shader);
        Material.Default.DoubleSided.Should().Be(model.DoubleSided, "a scene file's entry missing the field reads it from the default");
    }

    [Fact]
    public void Texture_Slots_Are_Independently_Assignable()
    {
        var a = default(Handle<TextureAsset>);
        var m = new Material(Vector4.One)
        {
            BaseColorTexture = a,
            EmissiveTexture = a,
        };

        // Assigning Invalid slots leaves IsValid false, though the field is settable.
        m.BaseColorTexture.IsValid.Should().BeFalse();
        m.EmissiveTexture.IsValid.Should().BeFalse();
    }
}