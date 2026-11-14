using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering;

/// <summary>The lamps global illumination writes into its lights, read on the CPU with no device.</summary>
[Trait("Category", "Unit")]
public sealed class GlobalIlluminationLampsTests
{
    private static RenderLight Lamp(int i, LightKind kind = LightKind.Point) => new()
    {
        Kind = kind, Position = new Vector3(i, 2, -i), Direction = -Vector3.UnitY, EmittedColor = new Vector3(1, 0.8f, 0.6f), Range = 6,
    };

    [Fact]
    public void Twenty_Lamps_Leave_The_Bytes_After_The_Lamps_As_Sixteen_Do()
    {
        var sixteen = new byte[GpuIllumination.LightsBytes];
        var twenty = new byte[GpuIllumination.LightsBytes];
        GlobalIlluminationRenderer.WriteLamps(sixteen, [.. Enumerable.Range(0, 16).Select(i => Lamp(i))]).Should().Be(16);
        GlobalIlluminationRenderer.WriteLamps(twenty, [.. Enumerable.Range(0, 20).Select(i => Lamp(i))])
            .Should().Be(16, "the kernels' array holds sixteen lamps");
        // The frame before's corners start at byte 1,088, past the sixteen entries, and the glow
        // lights after them, which a lamp past the sixteenth was written over.
        twenty.AsSpan(1088).ToArray().Should().Equal(sixteen.AsSpan(1088).ToArray(), "no lamp is written past the sixteen entries");
        twenty.Should().Equal(sixteen, "the lamps kept are the first sixteen");
    }

    [Fact]
    public void The_Lamps_Kept_Are_Those_Among_The_Sixteen_Lights_The_Model_Pass_Lights()
    {
        var bytes = new byte[GpuIllumination.LightsBytes];
        RenderLight[] lights = [Lamp(0, LightKind.Directional), Lamp(1, LightKind.Ambient), .. Enumerable.Range(2, 18).Select(i => Lamp(i, i % 3 == 0 ? LightKind.Spot : LightKind.Point))];
        // The sun and the sky take two of the model pass's sixteen, so fourteen lamps are lit and
        // bounce, the eighteen's first fourteen, and the four past them neither.
        GlobalIlluminationRenderer.WriteLamps(bytes, lights).Should().Be(14);
        var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
        Enumerable.Range(0, 14).Select(e => floats[16 + e * 16]).Should().Equal(Enumerable.Range(2, 14).Select(i => (float)i),
            "the lamps are kept in the order of the lights");
    }
}
