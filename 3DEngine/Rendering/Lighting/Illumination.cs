using System.Numerics;

namespace Engine;

/// <summary>
/// What global illumination works out from the quality and the field alone, which the passes that
/// read its binding, the model pass, the lighting buffer, the occlusion, the bloom and the fog, take
/// with no part of the illumination's renderer: the probes' spacing, each cascade's texels, the
/// screen's tile and whether its probes are on, the reflection's steps and the cascades the field
/// holds this frame.
/// </summary>
internal static class Illumination
{
    /// <summary>How many cells of their field cascade the probes lie apart.</summary>
    public const int ProbeSpacing = 8;

    /// <summary>The most cascades of probes, the four High traces and the far cascade's.</summary>
    public const int MostCascades = 5;

    /// <summary>
    /// The roughness from which a surface traces no reflection, the probes and the environment
    /// reflecting alone, its traced reflection fading out from half of it.
    /// </summary>
    public const float GlossyRoughness = 0.5f;

    /// <summary>Each cascade's octahedron's texels along each side at a quality, before the field's cascades cut them short.</summary>
    internal static int[] TexelsAt(GlobalIllumination quality) => quality switch
    {
        // Low's third cascade of 8 a side, so a surface past its second, 16 units across in
        // Sponza's field of 0.125, takes probes rather than the sky over a ground from beyond them,
        // which lit the atrium's far half at noon as though it stood outdoors.
        GlobalIllumination.Low => [8, 8, 8],
        GlobalIllumination.Medium => [8, 8, 16],
        // High's second cascade of 8 a side as Low's and Medium's, four rays a texel, where of 16 a
        // side, one ray from each texel's middle, its probes under a slab read the sky past its
        // edge along the level ways brighter than a path-traced reference takes it, the floor over
        // both Low's and the reference's. Four rays a texel of 16 a side read it right, at four
        // times the rays.
        GlobalIllumination.High => [8, 8, 16, 16],
        _ => [],
    };

    /// <summary>The steps a reflection is traced through the window's depth in, and how far, at a quality.</summary>
    internal static (int Steps, float Reach) ReflectionStepsAt(GlobalIllumination quality) => quality switch
    {
        GlobalIllumination.Low => (12, 8),
        GlobalIllumination.Medium => (16, 12),
        _ => (24, 16),
    };

    /// <summary>
    /// How far from its tile's middle each screen probe marches to the glow lights from in frame
    /// <paramref name="frame"/>, in pixels, the same for every probe, the middles of the tile's four
    /// by four cells in the order of Bayer's matrix, sixteen frames a round, so a round visits each
    /// cell once and each frame's cell lies far from the last.
    /// </summary>
    /// <remarks>
    /// At the window's half-sized depth a tile of eight pixels holds four by four of its texels,
    /// which a probe's point falls on, so a round marches from each texel once. The Halton points
    /// of bases 2 and 3 fell on one row of those texels five times a round and on another three, so
    /// the floor along a lamp's penumbra moved more with turns of one to eight pixels than it does
    /// with these.
    /// </remarks>
    internal static Vector2 JitterAt(long frame, int tile)
    {
        ReadOnlySpan<byte> cells = [0, 10, 2, 8, 5, 15, 7, 13, 1, 11, 3, 9, 4, 14, 6, 12];
        var cell = cells[(int)(frame % 16)];
        return new Vector2(((cell & 3) + 0.5f) / 4 - 0.5f, ((cell >> 2) + 0.5f) / 4 - 0.5f) * tile;
    }

    /// <summary>
    /// Whether a quality stands probes on the view's surfaces every few pixels, beside the cascades
    /// of probes through the scene, which <see cref="GlobalIllumination.Medium"/> and
    /// <see cref="GlobalIllumination.High"/> do and <see cref="GlobalIllumination.Low"/> leaves
    /// out, every surface there reading the cascades' light alone, so a weak GPU has the cheaper
    /// path with no switch of its own.
    /// </summary>
    internal static bool ScreenProbesAt(GlobalIllumination quality) => quality is GlobalIllumination.Medium or GlobalIllumination.High;

    /// <summary>Whether the views have screen probes this frame, at a quality that has them and not left out for debugging (<see cref="GlobalIlluminationSettings.ScreenOff"/>).</summary>
    internal static bool ScreenProbesOn(RenderWorld renderWorld) =>
        renderWorld.TryGet<GlobalIlluminationSettings>() is { ScreenOff: false } settings && ScreenProbesAt(settings.Quality);

    /// <summary>
    /// The pixels along each side of the tile a screen probe stands in, at a quality, for a window
    /// <paramref name="height"/> pixels high, the same up to 1080 and growing with the height past
    /// it, so a probe stands for about the same share of the picture on any screen.
    /// </summary>
    /// <remarks>
    /// The probes' cost grows with how many there are, which at the same spacing on every screen
    /// grows with the window's pixels, so the spacing grows with the height past 1080. A render
    /// texture's probes take the window's tile, so a view of a split screen reads the window's
    /// spacing.
    /// </remarks>
    internal static int TileAt(GlobalIllumination quality, uint height = 1080)
    {
        var at1080 = quality switch
        {
            GlobalIllumination.Low => 16,
            GlobalIllumination.Medium => 12,
            _ => 8,
        };
        return Math.Max(at1080, (int)MathF.Round(at1080 * height / 1080f));
    }

    /// <summary>
    /// How many cascades of probes global illumination traces for the window this frame, which the
    /// window's lighting buffer tells the model pass, 0 where global illumination is off.
    /// </summary>
    /// <remarks>
    /// The field's far cascade, where it has one, has a cascade of probes of its own after those the
    /// quality traces, which stand in the field's first cascades.
    /// </remarks>
    internal static int CascadesIn(RenderWorld renderWorld) =>
        renderWorld.TryGet<GlobalIlluminationSettings>() is { Quality: not GlobalIllumination.Off } settings
        && renderWorld.TryGet<SceneFieldSettings>() is { On: true } field
            ? Math.Min(TexelsAt(settings.Quality).Length, Math.Clamp(field.Cascades, 1, 8)) + (FarIn(renderWorld) ? 1 : 0)
            : 0;

    // Whether the field the probes stand in has a far cascade this frame.
    internal static bool FarIn(RenderWorld renderWorld) => renderWorld.TryGet<SceneFieldRenderer>()?.Plan is { HasFar: true };

    /// <summary>
    /// Each of <paramref name="cascades"/> cascades' octahedron's texels at a quality, the far
    /// cascade's, the last where <paramref name="far"/>, as many as the cascade before it.
    /// </summary>
    internal static int[] TexelsFor(GlobalIllumination quality, int cascades, bool far)
    {
        var texels = TexelsAt(quality);
        return far ? [.. texels[..(cascades - 1)], texels[cascades - 2 < 0 ? 0 : cascades - 2]] : texels[..cascades];
    }
}
