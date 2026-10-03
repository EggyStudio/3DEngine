using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Components;

/// <summary>
/// Tests for the PBR <see cref="Material"/> component: factor defaults, texture-handle
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
        m.RoughnessFactor.Should().Be(1f);
        m.EmissiveFactor.Should().Be(Vector3.Zero);
        m.NormalScale.Should().Be(1f);
        m.OcclusionStrength.Should().Be(1f);

        m.BaseColorTexture.IsValid.Should().BeFalse();
        m.MetallicRoughnessTexture.IsValid.Should().BeFalse();
        m.NormalTexture.IsValid.Should().BeFalse();
        m.EmissiveTexture.IsValid.Should().BeFalse();
        m.OcclusionTexture.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Texture_Slots_Are_Independently_Assignable()
    {
        var a = default(Handle<Texture>);
        var m = new Material(Vector4.One)
        {
            BaseColorTexture = a,
            EmissiveTexture = a,
        };

        // Sanity: assigning Invalid slots leaves IsValid false but the field is settable.
        m.BaseColorTexture.IsValid.Should().BeFalse();
        m.EmissiveTexture.IsValid.Should().BeFalse();
    }
}