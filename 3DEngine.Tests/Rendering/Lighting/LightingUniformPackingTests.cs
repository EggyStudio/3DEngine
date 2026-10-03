using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Lighting;

/// <summary>
/// Pure-managed tests for <see cref="LightingUboPacker"/>: feeds it
/// <see cref="RenderLight"/> snapshots and asserts the packed bytes match the
/// std140 layout the mesh shader's lighting block declares.
/// </summary>
[Trait("Category", "Unit")]
public class LightingUniformPackingTests
{
    [Fact]
    public void Pack_Empty_Sets_Count_To_Zero()
    {
        var ubo = LightingUboPacker.Pack(System.Array.Empty<RenderLight>());
        ubo.LightCount.Should().Be(0);
    }

    [Fact]
    public void Pack_Clamps_To_MaxLights()
    {
        var lights = new RenderLight[LightingUboPacker.MaxLights + 5];
        for (int i = 0; i < lights.Length; i++) lights[i] = new RenderLight { Type = LightType.Sphere };

        var ubo = LightingUboPacker.Pack(lights);

        ubo.LightCount.Should().Be(LightingUboPacker.MaxLights);
    }

    [Fact]
    public void Pack_Encodes_Position_Direction_Color_Into_Vec4_Tuples()
    {
        var light = new RenderLight
        {
            Type = LightType.Rect,
            Position = new Vector3(1, 2, 3),
            Direction = new Vector3(0, -1, 0),
            EmittedColor = new Vector3(10, 5, 1),
            Radius = 0.5f,
            Width = 4f,
            Height = 7f,
            Length = 2.5f,
            CastsShadows = true,
            HasCone = true,
            ConeAngle = 35f,
            ConeSoftness = 0.25f,
        };

        var ubo = LightingUboPacker.Pack(new[] { light });
        ubo.LightCount.Should().Be(1);

        ref var e = ref ubo.Lights[0];
        e.PositionAndType.Should().Be(new Vector4(1, 2, 3, (int)LightType.Rect));
        e.DirectionAndRadius.Should().Be(new Vector4(0, -1, 0, 0.5f));
        e.EmittedColorAndShadow.Should().Be(new Vector4(10, 5, 1, 1f));
        e.ShapeParams.Should().Be(new Vector4(4f, 7f, 2.5f, 35f));
        e.ShapingParams.X.Should().BeApproximately(0.25f, 1e-6f);
    }

    [Fact]
    public void Pack_Without_Cone_Zeros_Cone_Angle_And_Softness()
    {
        var light = new RenderLight { HasCone = false, ConeAngle = 99f, ConeSoftness = 0.7f };

        var ubo = LightingUboPacker.Pack(new[] { light });

        ubo.Lights[0].ShapeParams.W.Should().Be(0f);
        ubo.Lights[0].ShapingParams.X.Should().Be(0f);
    }

    [Fact]
    public void Pack_CastShadowsFalse_Is_Encoded_As_Zero()
    {
        var light = new RenderLight { CastsShadows = false };
        var ubo = LightingUboPacker.Pack(new[] { light });
        ubo.Lights[0].EmittedColorAndShadow.W.Should().Be(0f);
    }

    [Fact]
    public void SizeBytes_Matches_Marshal_SizeOf()
    {
        LightingUboPacker.SizeBytes.Should().Be(System.Runtime.InteropServices.Marshal.SizeOf<LightingUbo>());
    }
}

