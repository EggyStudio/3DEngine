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

    /// <summary>The bytes the reach, each ray's own where a texel has four, and each cascade's distances take, each with the share its rays kept before their interval.</summary>
    public long ReachBytes => (long)(ReachTexels * Probes) * (ReachTexels * Probes) * Probes * (Cascades + Math.Max(FourRayCascades(Texels), 1)) * 2
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

    // Each cascade's carried probes' cycle of the sixteen places their rays move through
    // (gi_merge.slang), the mean gathered so far as the frame before left it, which this frame's
    // merge reads at each probe's place then, the mean gathered so far and the last whole cycle's as
    // this frame's merge writes them, and the whole one as the frame before left it.
    internal (VkImage Image, VkImageView View)[] CyclePartialBefore { get; init; } = [];
    internal (VkImage Image, VkImageView View)[] CyclePartial { get; init; } = [];
    internal (VkImage Image, VkImageView View)[] CycleFull { get; init; } = [];
    internal (VkImage Image, VkImageView View)[] CycleFullBefore { get; init; } = [];

    // The same cycle's share of the sky and mark of what the rays met, the merged light's alpha,
    // the mean gathered so far and the last whole cycle's of each, as the frame before left them
    // and as this frame's merge writes them, since taken from each frame's places the share and the
    // mark a carried probe passed down and to its faces moved with its rays, and every probe whose
    // hits read them flickered with them, as along the edges of Sponza's balcony.
    internal (VkImage Image, VkImageView View)[] CycleSkyMarkBefore { get; init; } = [];
    internal (VkImage Image, VkImageView View)[] CycleSkyMark { get; init; } = [];

    // The whole cycle before the last, its light and share of the sky, which the light a carried
    // probe shows eases from to the last whole cycle's across a cycle (gi_merge.slang), as the frame
    // before left it and as this frame's merge writes it.
    internal (VkImage Image, VkImageView View)[] CycleEasedBefore { get; init; } = [];
    internal (VkImage Image, VkImageView View)[] CycleEased { get; init; } = [];

    // Whether the cycle's images are made whole, at High on a device that traces rays, where a
    // carried probe's rays move (gi_trace.slang), or stand-ins of a texel, since a mean of one place
    // is that place.
    internal bool CycleOn { get; init; }

    // How many cascades, from the first, can carry a probe's rays past their interval and keep its
    // cycle, every one but the last counted and the far, whose rays go on to the field's edge
    // already. The probes the view carries lie in the first cascades, and the last counted and the
    // far hold the most texels of all, so the cycle's images are made whole in the cascades before
    // them alone.
    internal int CycleCascades { get; init; }

    /// <summary>The bytes the carried probes' cycle takes, none where its images are stand-ins.</summary>
    public long CycleBytes => CycleOn ? Texels.Take(CycleCascades).Sum(n => 8L * 8 * (Probes * n) * (Probes * n) * Probes) : 0;
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

    // The image behind Held, which a command reads the carried probes from.
    internal VkImage HeldImage { get; init; }

    internal VkImage Cubes { get; }

    // Each face's light at the surface its probe's ray along the face's opposite way met, laid out
    // as the faces are, with that ray's distance in alpha, which a hit reads (gi_ambient.slang).
    internal VkImage CubesAt { get; init; }
    internal VkImageView CubesAtView { get; init; }

    // The way out from the surfaces each cascade's texels' rays met, as the trace keeps them for the
    // gather.
    internal (VkImage Image, VkImageView View)[] HitNormals { get; init; } = [];

    // How far each ray of the cascades sending four rays a texel went, which the gather lays out as
    // the reach in place of their mean.
    internal VkImageView RayReach { get; init; }

    // The cascades sending four rays a texel, the first ones, those of 8 a side.
    internal static int FourRayCascades(IEnumerable<int> texels) => texels.TakeWhile(n => n <= 8).Count();

    internal VkImage Reach { get; }
    internal VkImage Ways { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}
