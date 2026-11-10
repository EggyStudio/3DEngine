using FluentAssertions;

namespace Engine.Tests.Rendering;

// Each room at each quality held within its measured difference from its reference.
public sealed partial class BounceRoomsTests
{
    // Each view at each quality against its reference traced with every face's gloss, each face
    // after the first sending on its diffuse share of what its gloss leaves, as the probes' rays
    // light what they meet by its gloss and its diffuse share of what the gloss leaves
    // (reflectedShare in probelight.slang). Each bound is 15% over the difference the view read.
    // The strip's room and the corridor stand furthest from their references. The strip and the
    // emissive blocks are glow lights (GlowLights), taken from texels of half the window's size
    // (gi_screen_glow.slang), which at 160 by 90 are two pixels wide beside a strip six hundredths
    // of a block thick, so their views read further from their references than with each pixel's
    // glow light its own, where at 1280 by 720 the two read alike. The light the strip throws on
    // the wall it is mounted on lies within a fifth of a block of the strip, a band the probes'
    // rays, through the same points of each texel at every probe of a row, pass over, and its room
    // reads the light past its first two bounces further under its reference than the first two,
    // the shortfall each bounce through the probes takes on. The corridor's walls read under their
    // reference, and no probe of a coarser cascade stands inside it, so the screen's probes' rays
    // past their reach are marched on through the field (gi_screen.slang). Low leaves the screen's
    // probes out and reads the cascades alone, which takes the thin walls' and the corridor's views
    // further from their references, with nothing under the probes' spacing.
    [NeedsRayQueryTheory]
    [InlineData(1, GlobalIllumination.Low, 0.10)]
    [InlineData(1, GlobalIllumination.Medium, 0.098)]
    [InlineData(1, GlobalIllumination.High, 0.10)]
    [InlineData(2, GlobalIllumination.Low, 0.07)]
    [InlineData(2, GlobalIllumination.Medium, 0.05)]
    [InlineData(2, GlobalIllumination.High, 0.03)]
    [InlineData(3, GlobalIllumination.Low, 0.34)]
    [InlineData(3, GlobalIllumination.Medium, 0.303)]
    [InlineData(3, GlobalIllumination.High, 0.164)]
    [InlineData(4, GlobalIllumination.Low, 0.12)]
    [InlineData(4, GlobalIllumination.Medium, 0.119)]
    [InlineData(4, GlobalIllumination.High, 0.106)]
    [InlineData(5, GlobalIllumination.Low, 0.02)]
    [InlineData(5, GlobalIllumination.Medium, 0.03)]
    [InlineData(5, GlobalIllumination.High, 0.03)]
    [InlineData(6, GlobalIllumination.Low, 0.31)]
    [InlineData(6, GlobalIllumination.Medium, 0.30)]
    [InlineData(6, GlobalIllumination.High, 0.31)]
    [InlineData(7, GlobalIllumination.Low, 0.06)]
    [InlineData(7, GlobalIllumination.Medium, 0.04)]
    [InlineData(7, GlobalIllumination.High, 0.036)]
    [InlineData(8, GlobalIllumination.Low, 0.06)]
    [InlineData(8, GlobalIllumination.Medium, 0.084)]
    [InlineData(8, GlobalIllumination.High, 0.078)]
    [InlineData(9, GlobalIllumination.Low, 0.10)]
    [InlineData(9, GlobalIllumination.Medium, 0.10)]
    [InlineData(9, GlobalIllumination.High, 0.09)]
    public void Each_Room_Reads_Within_Its_Measured_Difference_From_Its_Reference(int view, GlobalIllumination quality, double bound)
    {
        var share = Differs(view, quality);
        share.Should().BeLessThan(bound, $"view {view} at {quality} differs from its reference by {share:0.000} of its light");
    }
}
