using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Lighting;

[Trait("Category", "Unit")]
public class LightingUniformPackingTests
{
    [Fact]
    public void An_Empty_Frame_Packs_A_Count_Of_Zero()
    {
        LightingUboPacker.Pack([]).LightCount.Should().Be(0);
    }

    [Fact]
    public void Lights_Past_The_Most_The_Buffer_Holds_Are_Dropped()
    {
        var lights = Enumerable.Range(0, LightingUboPacker.MaxLights + 5).Select(_ => new RenderLight()).ToList();
        LightingUboPacker.Pack(lights).LightCount.Should().Be(LightingUboPacker.MaxLights);
    }

    [Fact]
    public void A_Light_Packs_Into_Four_Rows_As_The_Shader_Reads_Them()
    {
        var light = new RenderLight
        {
            Kind = LightKind.Spot,
            Position = new Vector3(1, 2, 3),
            Direction = new Vector3(0, -1, 0),
            EmittedColor = new Vector3(4, 5, 6),
            Range = 7,
            CosInner = 0.9f,
            CosOuter = 0.8f,
            CastsShadows = true,
        };

        var entry = LightingUboPacker.Pack([light]).Lights[0];

        entry.PositionAndKind.Should().Be(new Vector4(1, 2, 3, (int)LightKind.Spot));
        entry.DirectionAndRange.Should().Be(new Vector4(0, -1, 0, 7));
        entry.ColorAndShadow.Should().Be(new Vector4(4, 5, 6, 1));
        entry.Cone.Should().Be(new Vector4(0.9f, 0.8f, 0, 0));
    }

    [Fact]
    public void The_Buffer_Is_A_304_Byte_Header_And_16_Entries_Of_64_Bytes()
    {
        Marshal.SizeOf<LightUboEntry>().Should().Be(64);
        Marshal.OffsetOf<LightingUbo>(nameof(LightingUbo.ShadowTexels)).Should().Be(16, "a float4 starts on a 16-byte boundary");
        Marshal.OffsetOf<LightingUbo>(nameof(LightingUbo.ShadowCascades)).Should().Be(32);
        Marshal.OffsetOf<LightingUbo>(nameof(LightingUbo.Environment)).Should().Be(32 + LightingUboPacker.MaxCascades * 64);
        Marshal.OffsetOf<LightingUbo>(nameof(LightingUbo.Lights)).Should().Be(304);
        LightingUboPacker.SizeBytes.Should().Be(304 + LightingUboPacker.MaxLights * 64);
    }

    [Fact]
    public void Packing_Marks_No_Light_As_Shadowed()
    {
        LightingUboPacker.Pack([new RenderLight { Kind = LightKind.Directional, CastsShadows = true }]).ShadowLight
            .Should().Be(-1, "the prepare step names the shadowed light once it has fitted a map for it");
        LightingUboPacker.Pack([new RenderLight { Kind = LightKind.Spot, CastsShadows = true }]).SpotShadowLight
            .Should().Be(-1, "and the shadowed spot light the same way");
    }
}
