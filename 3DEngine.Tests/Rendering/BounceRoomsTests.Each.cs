using FluentAssertions;

namespace Engine.Tests.Rendering;

// Each room at each quality held within its measured difference from its reference.
public sealed partial class BounceRoomsTests
{
    // Each view at each quality against its reference traced with every face's gloss, each face
    // after the first sending on its diffuse share of what its gloss leaves, as the probes' rays
    // light what they meet by its gloss and its diffuse share of what the gloss leaves
    // (reflectedShare in probelight.slang). Each view is read with no screen layer, the world's
    // probes lighting every pixel and each pixel marching to the glow lights itself, so Low and
    // Medium, whose cascades differ in the third alone, read alike. Each bound is 15% over the
    // difference the view read. The strip's room and the corridor stand furthest from their
    // references. The light the strip throws on the wall it is mounted on lies within a fifth of
    // a block of the strip, a band the probes' rays, through the same points of each texel at
    // every probe of a row, pass over, and its room reads the light past its first two bounces
    // further under its reference than the first two, the shortfall each bounce through the
    // probes takes on. The corridor's walls read under their reference, and no probe of a coarser
    // cascade stands inside it.
    [NeedsRayQueryTheory]
    [InlineData(1, GlobalIllumination.Low, 0.093)]
    [InlineData(1, GlobalIllumination.Medium, 0.093)]
    [InlineData(1, GlobalIllumination.High, 0.092)]
    [InlineData(2, GlobalIllumination.Low, 0.082)]
    [InlineData(2, GlobalIllumination.Medium, 0.082)]
    [InlineData(2, GlobalIllumination.High, 0.064)]
    [InlineData(3, GlobalIllumination.Low, 0.295)]
    [InlineData(3, GlobalIllumination.Medium, 0.295)]
    [InlineData(3, GlobalIllumination.High, 0.164)]
    [InlineData(4, GlobalIllumination.Low, 0.100)]
    [InlineData(4, GlobalIllumination.Medium, 0.101)]
    [InlineData(4, GlobalIllumination.High, 0.091)]
    [InlineData(5, GlobalIllumination.Low, 0.018)]
    [InlineData(5, GlobalIllumination.Medium, 0.018)]
    [InlineData(5, GlobalIllumination.High, 0.018)]
    [InlineData(6, GlobalIllumination.Low, 0.225)]
    [InlineData(6, GlobalIllumination.Medium, 0.225)]
    [InlineData(6, GlobalIllumination.High, 0.215)]
    [InlineData(7, GlobalIllumination.Low, 0.052)]
    [InlineData(7, GlobalIllumination.Medium, 0.055)]
    [InlineData(7, GlobalIllumination.High, 0.051)]
    [InlineData(8, GlobalIllumination.Low, 0.067)]
    [InlineData(8, GlobalIllumination.Medium, 0.067)]
    [InlineData(8, GlobalIllumination.High, 0.060)]
    [InlineData(9, GlobalIllumination.Low, 0.086)]
    [InlineData(9, GlobalIllumination.Medium, 0.086)]
    [InlineData(9, GlobalIllumination.High, 0.086)]
    public void Each_Room_Reads_Within_Its_Measured_Difference_From_Its_Reference(int view, GlobalIllumination quality, double bound)
    {
        var share = Differs(view, quality, layout: GridLayout.ForTheGridsOwnReading);
        share.Should().BeLessThan(bound, $"view {view} at {quality} differs from its reference by {share:0.000} of its light");
    }
}
