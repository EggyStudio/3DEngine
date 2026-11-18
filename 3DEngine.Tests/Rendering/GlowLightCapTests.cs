using System.Text.RegularExpressions;
using FluentAssertions;

namespace Engine.Tests.Rendering;

/// <summary>
/// The cap on the glow lights held the same in the shaders as in the engine, and the sizes that
/// follow from it worked out from it on both sides, so a cap raised on one side alone fails here
/// rather than leaving a screen probe to write past its own uints into the next probe's.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GlowLightCapTests
{
    private static string Glow => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "source", "shaders", "glow.slang"));

    [Fact]
    public void The_Shaders_Read_As_Many_Glow_Lights_As_The_Engine_Writes()
    {
        var most = Regex.Match(Glow, @"public static const int MostGlowLights = (\d+);");
        most.Success.Should().BeTrue("glow.slang states the cap as a number");
        int.Parse(most.Groups[1].Value).Should().Be(GlowLights.Most, "the engine writes as many lights as the shaders read");
    }

    [Fact]
    public void The_Sizes_That_Follow_The_Cap_Are_Worked_Out_From_It()
    {
        Glow.Should().Contain("GlowSeenLevels = MostGlowLights / 4;", "a probe keeps a byte a light, four to a uint")
            .And.Contain("GlowSeenUints = GlowSeenLevels + 4;", "the fainter lights' light follows the levels")
            .And.Contain("GlowKeptVectors = (MostGlowLights + 127) / 128;", "a bit a place says whether it holds the frame before's light")
            .And.Contain("GlowLightsHead = 32 + GlowKeptVectors * 16;", "those bits lie ahead of the lights");
        GpuScreenProbes.GlowSeenBytes.Should().Be((GlowLights.Most / 4 + 4) * 4, "the engine makes each probe's uints as the shaders lay them out");
        GlowLights.Head.Should().Be(32 + (GlowLights.Most + 127) / 128 * 16, "the engine writes the lights where the shaders read them");
    }
}
