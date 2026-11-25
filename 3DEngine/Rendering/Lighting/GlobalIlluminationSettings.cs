using System.Globalization;
using System.Numerics;

namespace Engine;

/// <summary>How much global illumination the program set, a world resource the renderer reads each frame.</summary>
internal sealed class GlobalIlluminationSettings
{
    private static readonly ILogger Logger = Log.Category("Engine.GlobalIllumination");
    private GlobalIllumination _quality;

    /// <summary>The quality, <see cref="GlobalIllumination.Off"/> for none.</summary>
    /// <remarks>
    /// A value the enum names none of is refused with an error naming it and the quality stays as it
    /// was, since the renderer has no cascades for it and would fail every frame on it.
    /// </remarks>
    public GlobalIllumination Quality
    {
        get => _quality;
        set
        {
            if (!Enum.IsDefined(value))
            {
                var names = Enum.GetNames<GlobalIllumination>();
                Logger.Error($"Global illumination's quality {(int)value} names none of {string.Join(", ", names[..^1])} and {names[^1]}, so it stays {_quality}.");
                return;
            }
            _quality = value;
        }
    }

    /// <summary>Whether High leaves the device's ray tracing alone, as <c>gi.rays off</c> sets it, so what it costs can be measured.</summary>
    public bool RaysOff { get; set; }

    /// <summary>
    /// Why High left the device's ray tracing off itself, the structures of the window's meshes not
    /// fitting the device's memory, or null where it did not, which <c>gi.rays on</c> clears.
    /// </summary>
    public string? RaysLeftOff { get; set; }

    /// <summary>
    /// How many corners of the meshes the device's rays meet the ray scene makes room for ahead,
    /// 0 unless <c>E3D.ReserveRayCorners</c> set it.
    /// </summary>
    public int ReservedRayCorners { get; set; }

    /// <summary>
    /// Whether High traces the world's probes' rays through the device's ray tracing in place of the
    /// scene's distance field, crossing a surface that lets light through by the share it lets
    /// through, as <c>gi.meshes on</c> sets it.
    /// </summary>
    /// <remarks>
    /// Off unless set, since in some views the field's trace lights the scene nearer a path-traced
    /// reference than this one does.
    /// </remarks>
    public bool MeshesTraced { get; set; }

    /// <summary>
    /// Whether the screen's probes take nothing of the frame before's, as <c>gi.history off</c>
    /// sets it.
    /// </summary>
    public bool HistoryOff { get; set; }

    /// <summary>
    /// Whether each screen probe keeps its own light, blended with none of its neighbors', as
    /// <c>gi.filter off</c> sets it.
    /// </summary>
    public bool FilterOff { get; set; }

    /// <summary>
    /// Whether the screen's probes are left out whole at any quality, for the window and the render
    /// targets, neither made nor traced, every surface reading the cascades' light alone and each
    /// pixel marching to the glow lights itself, as <c>gi.screen off</c> and the global
    /// illumination window's debug toggle set it, so what they cost and give can be read.
    /// </summary>
    /// <remarks>
    /// A debugging switch alone, which no program's call reaches. A program leaves the screen's
    /// probes out by <see cref="GlobalIllumination.Low"/> (<see cref="Illumination.ScreenProbesAt"/>).
    /// Each pixel's march costs the model pass in step with the glow lights in view.
    /// </remarks>
    public bool ScreenOff { get; set; }

    /// <summary>
    /// How the light reaches the picture where the screen's probes stand, by those probes or by the
    /// gather by pixel, as <c>gi.gather</c> sets it.
    /// </summary>
    public ScreenGather Gather { get; set; }

    /// <summary>The size of the gather by pixel's grid, as <c>gi.gather_size</c> sets it.</summary>
    public GatherSize GatherSize { get; set; } = GatherSize.Half;

    /// <summary>
    /// How far the gather by pixel's rays are traced through the scene's distance field before the
    /// cascades' light along their way is read, in units, or 0 for as far as the field reaches, as
    /// <c>gi.gather_reach</c> sets it.
    /// </summary>
    public float GatherReach { get; set; }

    /// <summary>
    /// Where the gather by pixel draws its rays' ways from, as <c>gi.gather_guide</c> sets it.
    /// </summary>
    public GatherGuide GatherGuide { get; set; }

    /// <summary>
    /// Whether the gather by pixel's rays meet the window's meshes through the device's ray tracing
    /// in place of the window's depth and the scene's distance field, and the faces they meet take
    /// the sun through a ray of their own, as <c>gi.gather_rays on</c> sets it, where High builds
    /// the meshes for the device's rays.
    /// </summary>
    /// <remarks>
    /// Off unless set. Every face such a ray meets stops it, as a solid one does, a canopy's leaves
    /// among them.
    /// </remarks>
    public bool GatherRays { get; set; }

    /// <summary>The window's screen probe whose rays are written for <c>gi.screenprobe</c> to read, by its index, or -1 for none.</summary>
    public int LoggedScreenProbe { get; set; } = -1;

    /// <summary>
    /// Whether the logged screen probe (<see cref="LoggedScreenProbe"/>) writes a value that is not a
    /// number as its light, which a test feeds the probes' history and the window's blend with.
    /// </summary>
    public bool LoggedScreenProbeNotANumber { get; set; }

    /// <summary>
    /// Whether each cascade keeps its own rays' light, taking nothing from the cascade above, as
    /// <c>gi.merge off</c> sets it.
    /// </summary>
    public bool MergeOff { get; set; }

    /// <summary>
    /// Whether the surfaces the rays meet take none of the indirect light that reached them the
    /// frame before, so light makes one bounce, as <c>gi.again off</c> sets it.
    /// </summary>
    public bool AgainOff { get; set; }

    /// <summary>What the indirect light reaching the surfaces the rays meet is taken times, 1 unless <c>E3D.SetBounceGain</c> set it.</summary>
    public float BounceGain { get; set; } = 1;

    /// <summary>
    /// Whether the world's probes take the whole of the indirect light of the frame before however
    /// their own light falls, so the indirect light from a light that went out fades over
    /// the frames each bounce takes, as <c>gi.follow off</c> sets it.
    /// </summary>
    public bool FollowOff { get; set; }

    /// <summary>
    /// Whether the small emissive surfaces global illumination carries as lights give none, their
    /// light lost to the rays all the same, as <c>gi.glow off</c> sets it, so what the glow
    /// lights give and cost can be read (<see cref="GlowLights"/>).
    /// </summary>
    public bool GlowOff { get; set; }

    /// <summary>
    /// Whether the window draws every surface white, so the frame shows the light that reaches each
    /// surface alone and not its color, as <c>gi.albedo off</c> sets it, which a reading of how
    /// the light changes as the camera moves takes apart from the textures the camera moves over.
    /// </summary>
    public bool AlbedoOff { get; set; }

    /// <summary>
    /// Whether the window leaves out the surfaces' reflection of the indirect light, the probes'
    /// along the mirror way, the environment's, a reflection probe's, the ambient lights' and what
    /// the trace through the depth meets, as <c>gi.specular off</c> sets it, so a reading
    /// takes the diffuse light apart from it against a reference traced without the first face's
    /// specular.
    /// </summary>
    public bool SpecularOff { get; set; }

    /// <summary>
    /// Whether the window lights every face by its own normal and leaves the materials' normal maps
    /// out, as <c>gi.normals off</c> sets it, with global illumination on or off, so a reading
    /// takes the faces' light apart from their maps against a reference that takes the faces' own
    /// normals.
    /// </summary>
    public bool NormalMapsOff { get; set; }

    /// <summary>
    /// Whether the profile times global illumination's passes apart, the ray scene, each cascade's
    /// trace, the merges, the gather and the screen's probes, as <c>gi.passes on</c> sets it, so
    /// where its time goes can be read, the rest staying <c>gpu.global_illumination</c>.
    /// </summary>
    public bool PassesTimed { get; set; }

    /// <summary>
    /// Whether every cascade's probes trace their rays every frame, as <c>gi.spread off</c>
    /// sets it, where the cascades past the first otherwise trace half their probes a frame while
    /// they lie still, so the light of the whole trace can be read beside the spread one.
    /// </summary>
    public bool SpreadOff { get; set; }

    /// <summary>
    /// Whether a probe's ray's hit takes every glow light by its own form, as <c>gi.points
    /// off</c> sets it, where a light giving the hit under a 32nd of the largest before it is
    /// otherwise taken as one point, so the light of the full forms can be read beside it.
    /// </summary>
    public bool PointsOff { get; set; }

    /// <summary>
    /// Whether a still probe's ray reads the sun at its hit through the shadow map at the hit
    /// alone, as <c>gi.footprint off</c> sets it, where it is otherwise read over the patch of the
    /// face the ray's cone covers there, so the light of the read at the hit can be read beside it.
    /// </summary>
    /// <remarks>
    /// Read at the hit alone, the second cascade's probe at 7, 7, -5 in a sunlit court finds no sun
    /// at its hits where a path-traced reference finds 0.00079, and the light of the first
    /// cascade's at 7.5, 7.5, -5.5 moves by up to 10.9% as the view turns 3 degrees with the eye
    /// held, the shadow map's texels moving under its hits. Read over the patch the one finds 1.22
    /// of the reference's sun and the other moves by up to 2.2%.
    /// </remarks>
    public bool FootprintOff { get; set; }

    /// <summary>
    /// Whether a probe below every surface, most of its downward face's rays meeting nothing, keeps
    /// a twentieth of its weight at a surface that does not see it, as every probe it does not see
    /// keeps, as <c>gi.under off</c> sets it, where it otherwise lends that surface nothing.
    /// </summary>
    /// <remarks>
    /// Kept so, the probes in the open under the floor of a sunlit court lit the face of a doorway
    /// beside the floor at 5.29 times a path-traced reference's light with the world's probes
    /// alone, and at 28.3 times with 8 probes a side in every cascade. Left out, it reads 1.81 and
    /// 6.88 times. With the screen probes it reads 5.80 times where 5.90.
    /// </remarks>
    public bool UnderOff { get; set; }

    /// <summary>
    /// Whether the model pass's pixels read each of their probes' faces moved toward its light at
    /// the surface the face's ray met, as far as the pixel lies behind the probe toward it, as a
    /// probe's hit reads them, as <c>gi.toward on</c> sets it, where they otherwise read the faces
    /// as they stand.
    /// </summary>
    /// <remarks>
    /// Off unless set, since a face moved to the surface its ray met is not tested from the pixel's
    /// point, so light reaches a floor behind a thin wall and a closed room's inside, and at Low a
    /// floor under a slab or leaves that let light through takes less of the sky than they let by.
    /// </remarks>
    public bool FacesMovedToward { get; set; }

    /// <summary>
    /// Whether the model pass takes the world's probes' light at each of the window's pixels as the
    /// RadianceCascades module gathers a surface point (<c>gi_surface.slang</c>), the point and its
    /// normal read from the window's depth, from the finest level whose probes stand about it on
    /// every side or within half their spacing past its edge probes, as <c>gi.surface on</c> sets
    /// it, where the pixel otherwise reads its probes' faces.
    /// </summary>
    /// <remarks>
    /// Off unless set, since once the bounce has settled it reads the top left, the top middle,
    /// the bottom left and the bottom middle of a wall beside a door further from a path-traced
    /// reference than the faces do, each over it, where with the bounce at zero it reads every
    /// part nearer, and its pass costs the GPU more than the model pass saves by it.
    /// </remarks>
    public bool SurfaceGather { get; set; }

    /// <summary>
    /// Whether the screen's probes stand in cascades of their own traced by the device's rays at
    /// <see cref="GlobalIllumination.High"/> (<c>gi_screen_cascades.slang</c>), as
    /// <c>gi.screen_cascades on</c> sets it, each level's probes twice as far apart as the level
    /// before's with twice as many ways along each side, each level tracing a stretch twice as long
    /// against the meshes and the last on to the scene's end, so no light from beyond is read from
    /// the world's probes, which give the light at a ray's hit alone. The probes are blended within
    /// the frame and keep nothing of the frames before.
    /// </summary>
    /// <remarks>
    /// Off unless set. It reads a shaded court, a far doorway and a wall beside a door nearer a
    /// path-traced reference than the screen probes' rays do, and a gallery's near wall further,
    /// but with nothing kept from the frames before a turn moves the light of many more pixels,
    /// the frame takes longer in every scene timed, and five of the nine bounce rooms at High read
    /// further from their references than their measured bounds.
    /// </remarks>
    public bool ScreenCascades { get; set; }

    /// <summary>
    /// How many probes a side each cascade of the world's probes holds, from the first on, the last
    /// count holding for the cascades past it, as <c>gi.side</c> sets it, and unless set
    /// <see cref="DefaultSides"/>, or <see cref="CpuSides"/> on a device that draws on its CPU. A
    /// cascade keeps its probes' spacing, so more of them reach further, and its probes' box lies
    /// around the middle of the field's cascade it stands in.
    /// </summary>
    /// <remarks>
    /// The probes' images are made at the most probes any cascade holds, a cascade of fewer reading
    /// and tracing the middle of its grid alone, so the memory goes by the most and the time by
    /// each cascade's own, as <c>docs/global-illumination.md</c> gives them. The probes of the
    /// field's last cascade past its box see none of the scene, so the light past the probes takes
    /// their place from the field's side in, and more than 8 a side there adds time and no reach.
    /// </remarks>
    public IReadOnlyList<int> ProbesSides
    {
        get => _sides ?? (OnCpu ? CpuSides : DefaultSides);
        set => _sides = value;
    }

    private IReadOnlyList<int>? _sides;

    /// <summary>
    /// The probes a side the cascades hold unless set, 8 for the first two and 12 past them.
    /// </summary>
    public static readonly int[] DefaultSides = [8, 8, 12];

    /// <summary>
    /// The probes a side every cascade holds unless set on a device that draws on its CPU, as
    /// lavapipe does, 8.
    /// </summary>
    /// <remarks>
    /// Such a device traces the probes on its processor, where the 12 a side past the second
    /// cascade trace 3.4 times the probes of 8 and the frames of global illumination's tests took
    /// so long that the suite on lavapipe passed its bound.
    /// </remarks>
    public static readonly int[] CpuSides = [8];

    /// <summary>
    /// Whether the device draws on its CPU, given each frame by the settings' extract from what the
    /// renderer's part found on starting.
    /// </summary>
    internal bool OnCpu { get; set; }

    /// <summary>
    /// The fewest probes a side a cascade holds, the field's cells a side over the probes' spacing.
    /// </summary>
    public const int LeastSide = SceneFieldPlan.Resolution / Illumination.ProbeSpacing;

    /// <summary>The most probes a side a cascade holds.</summary>
    public const int MostSide = 24;

    /// <summary>The probes a side cascade <paramref name="c"/> holds.</summary>
    public int SideOf(int c) => ProbesSides[Math.Min(c, ProbesSides.Count - 1)];

    /// <summary>
    /// The probes a side the images are made at for <paramref name="cascades"/> cascades, the most
    /// any of them holds.
    /// </summary>
    public int GridSide(int cascades) => Enumerable.Range(0, Math.Max(cascades, 1)).Max(SideOf);

    /// <summary>
    /// The corner of a grid of <paramref name="grid"/> probes a side around the middle of the field
    /// cascade whose box starts at <paramref name="origin"/> in cells <paramref name="cell"/> wide,
    /// and the first and last probe a side of it cascade <paramref name="c"/> holds, its middle, as
    /// <c>scenefield.slang</c>'s <c>cascadeAt</c> and <c>probeSpan</c> place them.
    /// </summary>
    public (Vector3 Corner, int First, int Last) GridOf(int c, int grid, Vector3 origin, float cell) =>
        (origin - new Vector3(0.5f * (grid * Illumination.ProbeSpacing - SceneFieldPlan.Resolution) * cell),
            (grid - SideOf(c)) / 2, (grid + SideOf(c)) / 2 - 1);

    /// <summary>
    /// The counts <c>gi.side</c> reads, even counts from <see cref="LeastSide"/> to
    /// <see cref="MostSide"/> joined by commas, or null where the text holds another. Each is even
    /// so a cascade's probes lie in the middle of the grid of the most whole probes in.
    /// </summary>
    internal static int[]? ParseSides(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        var sides = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out sides[i]) || sides[i] is < LeastSide or > MostSide || sides[i] % 2 != 0)
                return null;
        return sides;
    }

    /// <summary>The counts as <c>gi.side</c> shows them, joined by commas.</summary>
    internal static string SidesText(IEnumerable<int> sides) => string.Join(",", sides);

    /// <summary>
    /// The turns the probes past a cascade's inner eight a side trace in, a part a frame and whole
    /// on a frame the cascade moved, or 0 for every frame, as <c>gi.shell_parts</c> sets it.
    /// </summary>
    /// <remarks>
    /// Off unless set. In quarters the cascades' passes at 8, 8, 12 take about a tenth less time
    /// than traced whole, as <c>docs/global-illumination.md</c> gives it. A probe carried with its
    /// rays moving traces every frame, since traced on its turn alone its cycle of sixteen places
    /// met four of them in quarters and two in eighths, and a doorway's face in a court at 15:00
    /// read 0.940 and 0.922 of its light traced whole, 1.000 with it.
    /// </remarks>
    public int ShellParts { get; set; }

    /// <summary>
    /// Whether each cascade's probes march the field from the cascade they stand in outward, as
    /// <c>gi.own_field on</c> sets it, so a coarser cascade's light does not move when a finer
    /// cascade's box moves with the eye, where they otherwise march it from its finest cascade that
    /// holds each point.
    /// </summary>
    /// <remarks>
    /// Off unless set, since the coarser cells light some walls well past a path-traced reference,
    /// a wall beside a doorway at three and a half times its light, and let the light through a
    /// wall a tenth of a unit thick.
    /// </remarks>
    public bool OwnFieldMarched { get; set; }

    /// <summary>
    /// Whether each cascade's probes are placed by the field from its finest cascade that holds
    /// each spot, as <c>gi.own_place off</c> sets it, where they are otherwise placed by the field
    /// from the cascade they stand in outward, so a probe stands where it stood when a finer
    /// cascade's box moves with the eye. A probe above the first cascade beside a wall then stays
    /// on its side of it.
    /// </summary>
    /// <remarks>
    /// Placed from the cascade they stand in, a probe of the first cascade deep in a passage a unit
    /// wide reads the sunlit court through the passage's opening at 0.62 of a path-traced
    /// reference, the probes of the cascade above, moved out of the passage's walls into it, giving
    /// it less of the court's light than its own rays carried on bring. The passage's test stands
    /// skipped as that open fault.
    /// </remarks>
    public bool OwnPlaceOff { get; set; }

    /// <summary>
    /// The one term of the model pass's light drawn, numbered as modelpass.slang's termTaken lists
    /// them, or 0 for all, which light.terms sets in turn to read a pixel's light by its terms.
    /// </summary>
    public int Term { get; set; }

    /// <summary>
    /// The one cascade whose rays' light alone reaches the pixels, or -1 for every cascade, as
    /// <c>gi.cascade</c> sets it.
    /// </summary>
    public int Alone { get; set; } = -1;

    /// <summary>
    /// Whether the rays of the world's probes and the screen's stop at a surface that lets light
    /// through as at a solid one, the merge of the cascades still seeing past it, as
    /// <c>gi.through off</c> sets it, so what crossing a canopy's leaves costs can be read.
    /// </summary>
    public bool ThroughOff { get; set; }

    /// <summary>What <c>BounceViewRenderer</c> draws over the window, as <c>gi.show</c> sets it.</summary>
    public BounceView Shown { get; set; }

    /// <summary>The cascade a view of one shows.</summary>
    public int ShownCascade { get; set; }

    /// <summary>
    /// A reference's linear light, red, green and blue a pixel with rows from the top, its width and
    /// height, and how many times one has been given, which the difference view is drawn against.
    /// </summary>
    public (float[] Light, int Width, int Height, int Version)? Reference { get; set; }
}
