using FluentAssertions;

namespace Engine.Tests.Rendering;

// Each room at each quality held within its measured difference from its reference, the readings
// that set each bound beside it.
public sealed partial class BounceRoomsTests
{
    // Each view at each quality, bound 15% over what it read on the development machine's GPU,
    // where the views read 0.12 to 0.14 of their light apart from their references in the Cornell
    // box, 0.09 in the thin room, 0.20 to 0.24 in the corridor, 0.27 to 0.30 in the window's, 0.02
    // among the red walls, 0.22 in the strip's, 0.11 to 0.12 on the grazing floor, 0.11 to 0.12 in
    // the carried lamp's and 0.05 to 0.06 around the emissive blocks, the screen's probes' rays
    // reaching twice as far as the world's probes are apart, which took the corridor from 0.26, the
    // strip's from 0.27 to 0.29 and the carried lamp's from 0.10 to 0.12, and since the small
    // emitters are lights of their own (GlowLights), where the strip's read 0.45 to 0.47. The
    // strip's and the blocks' read 0.25 to 0.27 and 0.05 with each pixel's glow light its own;
    // taken from texels of half the window's size (gi_screen_glow.slang), which at 160 by 90 are
    // two pixels wide beside a strip six hundredths of a block thick, they read further, where at
    // 1280 by 720 the strip's reads 0.163 against 0.162 and the blocks' 0.020 against 0.018. Since
    // the second cascade of Low and Medium, of 8 texels a side, sends four rays a texel as the
    // first does (RaysAt), the rooms whose views it moved read as High reads them, the window's
    // 0.356 at Low and 0.340 at Medium where 0.315 and 0.300, High's 0.324, its walls 21 and 16%
    // over at Low where High's are 22 and 19%; the strip's 0.266 and 0.255 where 0.254 and 0.243,
    // High's 0.250; and the grazing floor's 0.105 at Medium where 0.080, High's 0.099; and since
    // the last cascade's rays go on to the field's edge (GlobalIlluminationRenderer), Low's reads
    // 0.104, where 0.092 with one ray a texel and 0.074 with four and the last cascade's rays
    // ending at four times its spacing, its room 7% over its reference where 4% under, High's 8%
    // over. Read with no lean off the surface (Lean), the window's view reads 0.301, 0.287 and
    // 0.270 at Low, Medium and High where 0.356, 0.340 and 0.324, the strip's 0.216, 0.213 and
    // 0.209 where 0.266, 0.255 and 0.250, and the grazing floor's 0.075, 0.075 and 0.055 where
    // 0.104, 0.105 and 0.099. With a hit given none of a glow light whose box its ray passed
    // through within a cell before it (glowLightPassed), the strip's view reads 0.234, 0.245 and
    // 0.244 where 0.222, 0.216 and 0.212, its room 12 to 13% under its reference where level, since
    // the light the strip throws on the wall it is mounted on lies within a fifth of a block of the
    // strip, a band the probes' rays, through the same points of each texel at every probe of a
    // row, pass over, and the probes' rays through the strip to the wall had made up for it. With
    // each tile's rays placed at one of sixteen places in their texels, the thin walls' view reads
    // 0.085, 0.090 and 0.094 where 0.072, 0.071 and 0.072, its world probes 9 to 12% over the
    // reference by their own rays, the gain past the first bounce, and the grazing floor's at
    // High 0.086 where 0.058, its walls 8% over where 8% under, the merge's parallax, each read
    // under by the fixed ways before, which also had the strip's read 0.234, 0.245 and 0.244 where
    // 0.216, 0.240 and 0.218. With the probes above read toward what the rays would meet (the
    // merge's parallax), the thin walls' view reads 0.090, 0.094 and 0.098, their walls thinner
    // than a cell, and the window's, the grazing floor's and the strip's narrow. With the reach's
    // slack an eighth of the probes' spacing where half (probeSees), the probes beyond a wall lend
    // the screen's probes' hits beside it none of their light, and the thin walls' view reads
    // 0.078, 0.079 and 0.084, the lamp outside lighting their walls' foot and edges none. The
    // corridor's reads 0.234, 0.244 and 0.232 where 0.219, 0.232 and 0.215, its walls 78 to 79%
    // under their reference at 800 by 450 where 67 to 70%, the sunlit probes beyond them having
    // covered part of their own reading under, and the grazing floor's at High 0.082 where 0.078,
    // its wall facing -x 12% over where 9%, the dark probes behind it having covered part of the
    // gain past the first bounce. With the screen's probes' rays past their reach marched on
    // through the field where their world probes give nothing (gi_screen.slang), the corridor's
    // view reads 0.213, 0.222 and 0.215, its first cascade's probes holding nothing along it since
    // no probe of a coarser cascade stands inside it. With a probe's hit within a cell of a glow
    // light taking the light's mean over a cell's square about it (glowLightsAt), the strip's view
    // reads 0.207, 0.217 and 0.201 where 0.207, 0.233 and 0.213. With Low leaving the screen's
    // probes out, the cascades read alone, the rooms at Low read 0.099, 0.117, 0.270, 0.227, 0.019,
    // 0.202, 0.051, 0.062 and 0.058 where with the probes 0.115, 0.078, 0.226, 0.308, 0.020, 0.207,
    // 0.069, 0.113 and 0.058, the thin walls' and the corridor's further from their references
    // with nothing under the probes' spacing, and the Cornell box's, the window's, the grazing
    // floor's and the carried lamp's nearer. With a pixel by a wall thinner than a cell reading the
    // probes through the field (byThinWall), the Cornell box's reads 0.103 at Low and the thin walls'
    // 0.107, their floor along each foot no longer lit by the lamp outside.
    // Against references traced with the first face's specular share (gi.reference's specular), the
    // reflection of the indirect light the model pass gives a surface by the environment, the
    // probes' share of its sky and the trace, the views read 0.108, 0.132 and 0.121 at Low, Medium
    // and High in the Cornell box, 0.058, 0.049 and 0.044 in the thin room, 0.298, 0.289 and 0.273
    // in the corridor, 0.206, 0.236 and 0.229 in the window's, 0.021, 0.022 and 0.021 among the red
    // walls, 0.219, 0.227 and 0.220 in the strip's, 0.116, 0.093 and 0.101 on the grazing floor,
    // 0.114, 0.122 and 0.125 in the carried lamp's and 0.090, 0.087 and 0.082 around the emissive
    // blocks. A rough surface reflecting the light its probes hold along its mirror way
    // (reflectedAt), the views read 0.087, 0.096 and 0.083 in the Cornell box, 0.107, 0.092 and
    // 0.090 in the thin room, 0.277, 0.267 and 0.254 in the corridor, 0.228, 0.253 and 0.245 in the
    // window's, 0.021, 0.024 and 0.023 among the red walls, 0.206, 0.208 and 0.201 in the strip's,
    // 0.041, 0.044 and 0.047 on the grazing floor, 0.071, 0.091 and 0.081 in the carried lamp's and
    // 0.086, 0.083 and 0.078 around the emissive blocks, each bound 15% over. The thin room reads 8
    // to 10% over its reference on every surface alike, the gain past the first bounce its probes
    // hold, which its rough surfaces' reflection of no indirect light hid. With the split sum's
    // table in place of Karis's fit (SpecularShares), each view reads within 0.004 of these, the
    // thin room's 0.109, 0.094 and 0.092 and the grazing floor's 0.043, 0.042 and 0.043. With the
    // probes' hits taking the sun through the window's shadow map (SunShadowMap), the thin room
    // reads 0.102, 0.088 and 0.086, the corridor 0.281, 0.271 and 0.257, and the window's 0.214,
    // 0.241 and 0.232, the other rooms as before. With each probe's reach kept at sixteen by sixteen
    // directions, the window's reads 0.247 at High and 0.255 at Medium, its surfaces brighter, the
    // other rooms within 0.001. With a probe that sees the probes above it for under a quarter of
    // their weight carried on past its interval, the strip's reads 0.241, 0.243 and 0.236 at Low,
    // Medium and High where 0.230, 0.234 and 0.226, a carried ray's one traced sample standing where
    // the merge averaged the probes above, which moves a closed room by a hundredth and a walled
    // passage by its whole light, and its bound is 0.25. With each hit reading its probes' faces
    // gathered at its surface (gi_ambient.slang), which takes the closed white rooms' light past the
    // first bounce from 1.11 to 1.26 of their references' to 0.99 to 1.11, the strip's reads 0.264,
    // 0.268 and 0.260 where 0.241, 0.243 and 0.236. Its room reads its first two bounces 9 to 12%
    // under its reference as before, and the light past them, 44% of the reference's, 24 to 25%
    // under where 17 to 19%, the shortfall each bounce through the probes takes on, which the gain
    // past the first bounce had made up for. With the probes' rays lighting what they meet by its
    // gloss and its diffuse share of what the gloss leaves (reflectedShare in probelight.slang),
    // against references traced with every face's gloss, each face after the first sending on its
    // diffuse share of what its gloss leaves as the probes' hits do, the views read at Low, Medium
    // and High the Cornell box 0.084, 0.095 and 0.082, the thin room 0.055, 0.037 and 0.021, the
    // corridor 0.287, 0.277 and 0.153, the window's 0.098, 0.115 and 0.105, the red walls 0.014,
    // 0.019 and 0.018, the strip's 0.266, 0.254 and 0.261, the grazing floor 0.046, 0.042 and 0.052,
    // the carried lamp's 0.052, 0.086 and 0.083 and the emissive blocks 0.083, 0.081 and 0.075, and
    // the bounds are 15% over these.
    [NeedsRayQueryTheory]
    [InlineData(1, GlobalIllumination.Low, 0.10)]
    [InlineData(1, GlobalIllumination.Medium, 0.11)]
    [InlineData(1, GlobalIllumination.High, 0.10)]
    [InlineData(2, GlobalIllumination.Low, 0.07)]
    [InlineData(2, GlobalIllumination.Medium, 0.05)]
    [InlineData(2, GlobalIllumination.High, 0.03)]
    [InlineData(3, GlobalIllumination.Low, 0.34)]
    [InlineData(3, GlobalIllumination.Medium, 0.32)]
    [InlineData(3, GlobalIllumination.High, 0.18)]
    [InlineData(4, GlobalIllumination.Low, 0.12)]
    [InlineData(4, GlobalIllumination.Medium, 0.14)]
    [InlineData(4, GlobalIllumination.High, 0.13)]
    [InlineData(5, GlobalIllumination.Low, 0.02)]
    [InlineData(5, GlobalIllumination.Medium, 0.03)]
    [InlineData(5, GlobalIllumination.High, 0.03)]
    [InlineData(6, GlobalIllumination.Low, 0.31)]
    [InlineData(6, GlobalIllumination.Medium, 0.30)]
    [InlineData(6, GlobalIllumination.High, 0.31)]
    [InlineData(7, GlobalIllumination.Low, 0.06)]
    [InlineData(7, GlobalIllumination.Medium, 0.05)]
    [InlineData(7, GlobalIllumination.High, 0.06)]
    [InlineData(8, GlobalIllumination.Low, 0.06)]
    [InlineData(8, GlobalIllumination.Medium, 0.10)]
    [InlineData(8, GlobalIllumination.High, 0.10)]
    [InlineData(9, GlobalIllumination.Low, 0.10)]
    [InlineData(9, GlobalIllumination.Medium, 0.10)]
    [InlineData(9, GlobalIllumination.High, 0.09)]
    public void Each_Room_Reads_Within_Its_Measured_Difference_From_Its_Reference(int view, GlobalIllumination quality, double bound)
    {
        var share = Differs(view, quality);
        share.Should().BeLessThan(bound, $"view {view} at {quality} differs from its reference by {share:0.000} of its light");
    }
}
