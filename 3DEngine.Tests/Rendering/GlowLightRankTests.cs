using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;

namespace Engine.Tests.Rendering;

/// <summary>
/// Which of more carried emitters than the cap are lit, those sending the most about the eye, each
/// one's light over its squared distance from the eye.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GlowLightRankTests
{
    [Fact]
    public void A_Bright_Lamp_Farther_Than_Seventy_Faint_Ones_Is_Lit_And_The_Faintest_Are_Not()
    {
        // Seventy faint blocks a tenth of a meter wide along x, from a fifth of a meter to fourteen
        // meters from the eye, and one bright block a meter wide twenty meters off along z, as a
        // street lantern beyond a row of small lit signs. The lantern sends the eye's surroundings
        // more than any sign, so it is lit, and the seven signs that send the least, the farthest,
        // are not. Taken nearest first, the lantern would be the farthest of all and dropped.
        var vertices = new ModelVertex[] { new() { Position = new Vector3(-0.5f) }, new() { Position = new Vector3(0.5f) } };
        var faint = Enumerable.Range(1, 70).Select(i => (new SceneFieldPlan.Instance(1, vertices, Matrix4x4.CreateScale(0.1f) * Matrix4x4.CreateTranslation(i * 0.2f, 0.5f, 0),
            false, Vector3.One, new Vector3(0.04f)), false));
        var lantern = (new SceneFieldPlan.Instance(2, vertices, Matrix4x4.CreateTranslation(0, 0.5f, 20), false, Vector3.One, new Vector3(40, 30, 20)), false);
        var bytes = new byte[GlowLights.Size];
        GlowLights.Write(bytes, [.. faint, lantern], Vector3.Zero, 0.25f, SceneFieldPlan.Box.Of, _ => 1).Should().Be(71);

        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
        floats[0].Should().Be(GlowLights.Most);
        var middles = Enumerable.Range(0, GlowLights.Most)
            .Select(n => new Vector3(floats.AsSpan(GlowLights.Head / 4 + n * GlowLights.Bytes / 4, 3))).ToList();
        middles.Should().Contain(new Vector3(0, 0.5f, 20), "the lantern sends more about the eye than any sign");
        middles.Where(m => m.Z == 0).Select(m => m.X).Max().Should().BeApproximately(63 * 0.2f, 1e-4f, "the seven farthest signs send the least and are dropped");
    }
}
