using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The light probes of global illumination on the GPU, for each cascade an image of what each
/// probe's rays brought back and one of that merged with the cascades above, a tile of octahedron
/// texels a probe, and for every cascade the six faces of the light each probe receives, which the
/// model pass reads, with a uniform buffer of the lights the rays' surfaces are lit by.
/// </summary>
/// <remarks>
/// The images of rays, merges and distances stay in the general layout, written and read by the
/// probes' work alone. The faces and the reach wait to be sampled between frames, which the next
/// frame's rays read as indirect light and how far each probe sees, and go to the general
/// layout while they are gathered again.
/// </remarks>
internal sealed class GpuIllumination : IDisposable
{
    private readonly Action _dispose;

    internal GpuIllumination(int probes, int[] texels, (VkImage Image, VkImageView View)[] radiance, (VkImage Image, VkImageView View)[] merged,
        VkImage cubes, IImageView cubesView, ISampler sampler, IBuffer lights, Action dispose,
        (VkImage Image, VkImageView View)[] distances, VkImage reach, IImageView reachView,
        VkImageView partial, VkImageView held, (VkImage Image, VkImageView View)[] mergedSky, VkImage skies, IImageView skiesView,
        VkImage ways, IImageView waysView)
    {
        Ways = ways;
        WaysView = waysView;
        MergedSky = mergedSky;
        Skies = skies;
        SkiesView = skiesView;
        Partial = partial;
        Held = held;
        Distances = distances;
        Reach = reach;
        ReachView = reachView;
        Probes = probes;
        Texels = texels;
        Radiance = radiance;
        Merged = merged;
        Cubes = cubes;
        CubesView = cubesView;
        Sampler = sampler;
        Lights = lights;
        _dispose = dispose;
    }

    /// <summary>The probes along each side of a cascade.</summary>
    public int Probes { get; }

    /// <summary>Each cascade's octahedron's texels along each side, so its probes' rays.</summary>
    public IReadOnlyList<int> Texels { get; }

    /// <summary>How many cascades.</summary>
    public int Cascades => Texels.Count;

    /// <summary>Whether the last cascade is the far cascade's probes, which the screen's probes do not stand in.</summary>
    public bool Far { get; init; }

    /// <summary>The rays each texel of cascade <paramref name="c"/>'s octahedron sends, as <c>gi.slang</c>'s <c>RaysAt</c> has them.</summary>
    public int RaysAt(int c) => Texels[c] <= 8 ? 4 : 1;

    /// <summary>The six faces of the light each probe receives, every cascade, as <c>gi_ambient.slang</c> writes them.</summary>
    public IImageView CubesView { get; }

    /// <summary>A sampler that blends between probes, clamped at the edges.</summary>
    public ISampler Sampler { get; }

    /// <summary>The lights, as <c>gi.slang</c>'s <c>GiLights</c>.</summary>
    public IBuffer Lights { get; }

    /// <summary>Each glow light place's faces as last decided, as <c>glow_faces.slang</c> keeps them.</summary>
    public IBuffer? GlowFaces { get; init; }

    /// <summary>
    /// The bytes of <see cref="Lights"/>: the sun, the sky and the counts, sixteen lights, four
    /// cascades' corners of the frame before, the glow lights (<see cref="GlowLights"/>), and the
    /// sun's shadow map's cascades the frame before (<see cref="SunMapOffset"/>).
    /// </summary>
    public const int LightsBytes = SunMapOffset + SunMapBytes;

    /// <summary>
    /// Where in <see cref="Lights"/> the sun's shadow map's cascades lie, four view-projections, each
    /// cascade's texel in world units, and how many cascades with the map's texel in uv.
    /// </summary>
    public const int SunMapOffset = GlowLights.Offset + GlowLights.Size;

    /// <summary>The bytes of the sun's shadow map's cascades in <see cref="Lights"/>.</summary>
    public const int SunMapBytes = 4 * 64 + 2 * 16;

    /// <summary>
    /// How far each probe's rays reached along sixteen by sixteen directions, every cascade, before a
    /// surface stopped them, as <c>gi_ambient.slang</c> lays it out, which weighs how much of each
    /// probe a surface takes.
    /// </summary>
    public IImageView ReachView { get; }

    /// <summary>The bytes the reach and each cascade's distances take, each with the share its rays
    /// kept before their interval.</summary>
    public long ReachBytes => (long)(ReachTexels * Probes) * (ReachTexels * Probes) * Probes * Cascades * 2
        + Texels.Sum(n => (long)(Probes * n) * (Probes * n) * Probes * 4);

    /// <summary>
    /// The share of the sky each probe's faces see, every cascade, laid out as <see cref="CubesView"/>
    /// is, as <c>gi_ambient.slang</c> gathers it from the rays that met nothing, which the model pass
    /// weighs the environment's reflection by.
    /// </summary>
    public IImageView SkiesView { get; }

    /// <summary>The bytes the sky's shares take, the faces' and each cascade's merged rays'.</summary>
    public long SkyBytes => (long)(6 * Probes) * Probes * Probes * Cascades * 2
        + Texels.Sum(n => (long)(Probes * n) * (Probes * n) * Probes * 2);

    /// <summary>
    /// The light each probe receives along eight by eight directions of its octahedron, every
    /// cascade, each the mean of the merged directions inside it, in a block of ten by ten texels a
    /// probe whose border holds the directions across each edge of the octahedron, as
    /// <c>gi_ambient.slang</c> writes it, which a rough surface reflects.
    /// </summary>
    public IImageView WaysView { get; }

    /// <summary>The texels along each side of a probe's block in <see cref="WaysView"/>, eight with a border.</summary>
    public const int WaysTexels = 10;

    /// <summary>The directions along each side of the octahedron each probe's reach is kept along, as <c>gi.slang</c>'s <c>ReachTexels</c>.</summary>
    public const uint ReachTexels = 16;

    /// <summary>The bytes <see cref="WaysView"/> takes.</summary>
    public long WaysBytes => (long)(WaysTexels * Probes) * (WaysTexels * Probes) * Probes * Cascades * 8;

    internal (VkImage Image, VkImageView View)[] Radiance { get; }
    internal (VkImage Image, VkImageView View)[] Merged { get; }

    // The light each texel's ray brought at its traced side, and the share it carries on.
    internal (VkImage Image, VkImageView View)[] RayLights { get; init; } = [];

    // How far each of those rays went and how it ended, as gi_trace.slang writes them.
    internal (VkImage Image, VkImageView View)[] RayEnds { get; init; } = [];

    // Each cascade merged at the side it is traced at, which the cascade below's merge reads.
    internal (VkImage Image, VkImageView View)[] MergedTraced { get; init; } = [];

    // The share of the sky each of those texels merged, as gi_merge.slang writes it.
    internal (VkImage Image, VkImageView View)[] MergedSkyTraced { get; init; } = [];

    /// <summary>How many times the side a cascade is merged at its rays are traced at, 1 or 2 (<see
    /// cref="GlobalIlluminationSettings.TraceSpread"/>).</summary>
    public int Spread { get; init; } = 1;

    /// <summary>The partial sums each probe's trace leaves, one a workgroup of its 64 rays, as many
    /// as fill a probe traced at a spread of 2, as <c>gi.slang</c>'s
    /// <c>PartialsAProbe</c>.</summary>
    public const int PartialsAProbe = 16;

    /// <summary>
    /// The texels along each side cascade <paramref name="c"/> is merged at, which the cascade
    /// below reads, twice its octahedron's where each texel sends four rays.
    /// </summary>
    public int MergeSide(int c) => RaysAt(c) == 4 ? 2 * Texels[c] : Texels[c];

    /// <summary>The texels along each side cascade <paramref name="c"/>'s rays are traced at, <see
    /// cref="Spread"/> times its merge's.</summary>
    public int TracedSide(int c) => Spread * MergeSide(c);

    /// <summary>The rays each texel of cascade <paramref name="c"/>'s octahedron is traced
    /// with.</summary>
    public int TracedRays(int c) => RaysAt(c) * Spread * Spread;

    /// <summary>
    /// The bytes each cascade's rays take, their light and ends at the side they are traced at and
    /// their merged light and share of the sky at the side they are merged at.
    /// </summary>
    public long TracedBytes => Enumerable.Range(0, Cascades).Sum(c =>
        (long)(Probes * TracedSide(c)) * (Probes * TracedSide(c)) * Probes * (8 + 4) + (long)(Probes * MergeSide(c)) * (Probes * MergeSide(c)) * Probes * (8 + 2));

    internal (VkImage Image, VkImageView View)[] Distances { get; }

    // Each cascade's merged rays' share of the sky, a texel a ray, as gi_merge.slang writes it.
    internal (VkImage Image, VkImageView View)[] MergedSky { get; }

    internal VkImage Skies { get; }

    // What each workgroup of a probe's rays brought back straight from the sun, the lights and
    // emissive surfaces, and the luminance of all they brought back, four a probe, every cascade.
    internal VkImageView Partial { get; }

    // Each probe's own light and the luminance of all its rays brought, two frames side by side,
    // the share of the indirect light each probe's next rays take, and where the trace moved
    // each probe to, every cascade.
    internal VkImageView Held { get; }

    // The image behind Held, which ReadIlluminationHeld copies from.
    internal VkImage HeldImage { get; init; }

    internal VkImage Cubes { get; }

    // Each face's light at the surface its probe's ray along the face's opposite way met, laid out
    // as the faces are, with that ray's distance in alpha, which a hit reads (gi_ambient.slang).
    internal VkImage CubesAt { get; init; }
    internal VkImageView CubesAtView { get; init; }

    // Each probe's faces' light times 1 where the probe holds light and 0 where it does not, with
    // that in alpha, which the merge's marches light what they meet by (gi_ambient.slang).
    internal VkImage Kept { get; init; }
    internal VkImageView KeptView { get; init; }

    // The light of each cell of the field within a cell of a surface that no finer cascade holds,
    // shaded each frame as the trace lights a hit, which the merge's marches read at the cells they
    // meet a surface in, each side's own light and its light past the first bounce side by side
    // along x, a sheet's two sides and a solid's one, the field's cascades one after another along
    // z (gi_hold.slang).
    internal VkImage HeldCells { get; init; }
    internal VkImageView HeldCellsView { get; init; }
    internal long HeldCellsBytes { get; init; }

    // The cells of the field the holding pass shades as it last listed them, the workgroups of its
    // dispatch, their count and their indices (gi_hold.slang).
    internal IBuffer? HeldList { get; init; }

    // The cells of the field the merge's marches meet this frame, a bit a cell, which the holding
    // pass alone lists (gi_merge.slang built with MARK_HELD).
    internal IBuffer? HeldMarks { get; init; }

    // The way out from the surfaces each cascade's texels' rays met, as the trace keeps them for the
    // gather.
    internal (VkImage Image, VkImageView View)[] HitNormals { get; init; } = [];

    internal VkImage Reach { get; }
    internal VkImage Ways { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}
