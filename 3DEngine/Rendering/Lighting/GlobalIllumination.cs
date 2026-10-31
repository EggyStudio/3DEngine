using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>How much global illumination is worked out, from none to the full set of cascades.</summary>
/// <seealso cref="E3D.SetGlobalIllumination"/>
public enum GlobalIllumination
{
    /// <summary>No global illumination, as raylib draws a scene.</summary>
    Off,

    /// <summary>Two cascades of probes, 64 directions each, and no probes on the view's surfaces, every surface reading the cascades' light alone, for a slow GPU.</summary>
    Low,

    /// <summary>Three cascades, 64, 64 and 256 directions, and a probe on the view's surfaces every 12 pixels.</summary>
    Medium,

    /// <summary>Four cascades, 64 directions in the first and 256 in each after, and a probe on the view's surfaces every 8 pixels.</summary>
    High,
}

/// <summary>How much global illumination the program set, a world resource the renderer reads each frame.</summary>
internal sealed class GlobalIlluminationSettings
{
    /// <summary>The quality, <see cref="GlobalIllumination.Off"/> for none.</summary>
    public GlobalIllumination Quality { get; set; }

    /// <summary>Whether High leaves the device's ray tracing alone, as <c>gi.rays off</c> sets it, so what it costs can be measured.</summary>
    public bool RaysOff { get; set; }

    /// <summary>Whether the screen's probes take nothing of the frame before's, as <c>gi.toggle history off</c> sets it.</summary>
    public bool HistoryOff { get; set; }

    /// <summary>Whether each screen probe keeps its own light, blended with none of its neighbors', as <c>gi.toggle filter off</c> sets it.</summary>
    public bool FilterOff { get; set; }

    /// <summary>
    /// Whether the screen's probes are left out whole at any quality, for the window and the render
    /// targets, neither made nor traced, every surface reading the cascades' light alone and each
    /// pixel marching to the glow lights itself, as <c>gi.toggle screen off</c> and the global
    /// illumination window's debug toggle set it, so what they cost and give can be read.
    /// </summary>
    /// <remarks>
    /// A debugging switch alone, which no program's call reaches. A program leaves the screen's
    /// probes out by <see cref="GlobalIllumination.Low"/> (<see cref="GlobalIlluminationRenderer.ScreenProbesAt"/>).
    /// Each pixel's march costs the model pass in step with the glow lights in view, 13.3 ms in a
    /// voxel game's room of forty glowstones at 1280 by 720 at High, where 2.2 with the probes read.
    /// </remarks>
    public bool ScreenOff { get; set; }

    /// <summary>The window's screen probe whose rays are written for <c>gi.screenprobe</c> to read, by its index, or -1 for none.</summary>
    public int LoggedScreenProbe { get; set; } = -1;

    /// <summary>
    /// Whether the logged screen probe (<see cref="LoggedScreenProbe"/>) writes a value that is not a
    /// number as its light, which a test feeds the probes' history and the window's blend with.
    /// </summary>
    public bool LoggedScreenProbeNotANumber { get; set; }

    /// <summary>Whether each cascade keeps its own rays' light, taking nothing from the cascade above, as <c>gi.toggle merge off</c> sets it.</summary>
    public bool MergeOff { get; set; }

    /// <summary>Whether the surfaces the rays meet take none of the indirect light that reached them the frame before, so light makes one bounce, as <c>gi.toggle again off</c> sets it.</summary>
    public bool AgainOff { get; set; }

    /// <summary>
    /// Whether the world's probes take the whole of the indirect light of the frame before however
    /// their own light falls, so the indirect light from a light that went out fades over
    /// the frames each bounce takes, as <c>gi.toggle follow off</c> sets it.
    /// </summary>
    public bool FollowOff { get; set; }

    /// <summary>
    /// Whether the small emissive surfaces global illumination carries as lights give none, their
    /// light lost to the rays all the same, as <c>gi.toggle glow off</c> sets it, so what the glow
    /// lights give and cost can be read (<see cref="GlowLights"/>).
    /// </summary>
    public bool GlowOff { get; set; }

    /// <summary>The one cascade whose rays' light alone reaches the pixels, or -1 for every cascade, as <c>gi.toggle cascade</c> sets it.</summary>
    public int Alone { get; set; } = -1;

    /// <summary>
    /// Whether the rays of the world's probes and the screen's stop at a surface that lets light
    /// through as at a solid one, the merge of the cascades still seeing past it, as
    /// <c>gi.toggle through off</c> sets it, so what crossing a canopy's leaves costs can be read.
    /// </summary>
    public bool ThroughOff { get; set; }

    /// <summary>What <see cref="BounceViewRenderer"/> draws over the window, as <c>gi.show</c> sets it.</summary>
    public BounceView Shown { get; set; }

    /// <summary>The cascade a view of one shows.</summary>
    public int ShownCascade { get; set; }

    /// <summary>
    /// A reference's linear light, red, green and blue a pixel with rows from the top, its width and
    /// height, and how many times one has been given, which the difference view is drawn against.
    /// </summary>
    public (float[] Light, int Width, int Height, int Version)? Reference { get; set; }
}

/// <summary>
/// The probes the model pass reads the window's indirect light from this frame, over the field they
/// were traced through, and the screen's probes, which it reads first where one stands near, with
/// what a glossy surface traces its reflection through, the window's depth, its scene the frame
/// before where one was kept, its meshes for the device's ray tracing where it traces rays, and how
/// many glow lights the frame's lights hold, without which the model pass draws with its build that
/// leaves them out.
/// </summary>
internal sealed record IlluminationBinding(GpuIllumination Probes, GpuSceneField Field, GpuScreenProbes? Screen,
    WindowDepth? Depth = null, GpuReflectionHistory? History = null, GpuRayScene? Rays = null, int GlowLights = 0);

/// <summary>
/// What the model pass reads each render target's indirect light from this frame, by the target's
/// texture id, the world's probes with the target's own screen probes, stood on its own depth,
/// where it draws meshes through a camera of its own.
/// </summary>
internal sealed class TargetIllumination
{
    /// <summary>Each target's binding, set as the target's probes are traced ahead of its pass.</summary>
    public Dictionary<int, IlluminationBinding> ByTarget { get; } = [];
}

/// <summary>
/// Global illumination, as cascades of light probes over the scene's distance field (Radiance
/// Cascades), traced, merged and gathered on the GPU each frame
/// (<see cref="GraphicsDevice.RecordGlobalIllumination"/>), which the model pass adds to the
/// window's light from all around.
/// </summary>
/// <remarks>
/// <para>
/// A cascade's probes lie every <see cref="ProbeSpacing"/> cells of the field's cascade of the same
/// number, eight along each side, so each cascade's probes are twice as far apart as the one
/// before's and cover eight times the room. Each probe's rays cover an interval from as far as the
/// probes are apart to four times that, the first cascade's from the probe itself, so it reaches as
/// far again as the next cascade's interval begins, and each cascade has
/// four times the directions of the one before up to 256, so the far light, which changes slowly
/// across space and quickly across directions, is sampled as it changes.
/// </para>
/// <para>
/// A ray that meets a surface brings back the light the surface sends along it, its color, which
/// the field paints, times the sun's light where the field lets the sun through to it, the point
/// and spot lights', each that casts shadows where the field lets it through too, and the indirect
/// light that reached it the frame before, so each frame adds a bounce, with its emission. A ray of
/// the last cascade that meets nothing brings back the environment map's light, or the ambient
/// lights' where there is none.
/// </para>
/// </remarks>
internal sealed class GlobalIlluminationRenderer : IDisposable
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

    private GpuIllumination? _gi;
    private GpuScreenProbes? _screen;
    // Whether the window's screen probes stand for the glow lights alone.
    private bool _screenGlowOnly;

    // The window's camera the screen's probes were last placed through, which their blend finds a
    // probe's place in the frame before by, or null where the probes were laid out again since.
    private (Matrix4x4 ViewProjection, Vector3 Eye)? _lastScreen;
    // The places the glow lights were written in, kept from frame to frame, and how many there were.
    private readonly GlowPlaces _glowPlaces = new();
    private int _glowCount;
    // Whether some surface drawn this frame lets light through (ModelMaterial.LightThrough), where a
    // frame of none traces the probes' rays with the build that leaves the crossing out.
    private bool _lightThrough;

    /// <summary>How many emitters are carried as glow lights this frame, and how many of them, the nearest the eye, are lit.</summary>
    internal (int Carried, int Lit) Glow => (_glowFound, _glowCount);

    private int _glowFound;
    // The field's cascades built this frame as meshes came or went within them or for the first
    // time, a bit a cascade, and the box around what came or went, where the glow lights' young
    // blends start again.
    private uint _fieldRebuilt;
    private SceneFieldPlan.Box _fieldChanged;
    // How far the screen's probes trace, the first cascade's probes' spacing, this frame.
    private float _screenReach;
    // The frame's settings, whose switches the screen's blend reads.
    private GlobalIlluminationSettings? _switches;

    // A render target's screen probes, its camera the frame before and the frame it was last drawn in.
    private sealed class TargetScreen(GpuScreenProbes screen) : IDisposable
    {
        public GpuScreenProbes Screen { get; } = screen;
        public (Matrix4x4 ViewProjection, Vector3 Eye)? Last { get; set; }
        public long Drawn { get; set; }
        public bool GlowOnly { get; init; }

        public void Dispose() => Screen.Dispose();
    }

    private readonly Dictionary<int, TargetScreen> _targets = [];
    private GpuReflectionHistory? _history;
    private GpuRayScene? _rays;
    private (GpuSceneField Field, GlobalIllumination Quality, int Cascades) _made;
    private CubeMap? _black;
    private long _frame;
    // Where each cascade of the field lay the frame before, which says whether the probes' own light
    // of the frame before is the same probes'.
    private Vector3?[] _origins = [];
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];

    /// <summary>The probes being traced, or null where light does not bounce.</summary>
    internal GpuIllumination? Probes => _gi;

    /// <summary>The screen's probes being placed, or null where none are.</summary>
    internal GpuScreenProbes? Screen => _screen;

    /// <summary>
    /// The window's camera the frame <see cref="KeepFrame"/> last kept the scene of, which a
    /// reflection looks a surface up in that picture by, or null where none is kept.
    /// </summary>
    internal Matrix4x4? HistoryViewProjection { get; private set; }

    /// <summary>
    /// The window's meshes for the device's ray tracing, built each frame at the top quality where
    /// the device and the model pass trace rays, or null where none are.
    /// </summary>
    internal GpuRayScene? Rays => _rays;

    /// <summary>Each cascade's octahedron's texels along each side at a quality, before the field's cascades cut them short.</summary>
    internal static int[] TexelsAt(GlobalIllumination quality) => quality switch
    {
        GlobalIllumination.Low => [8, 8],
        GlobalIllumination.Medium => [8, 8, 16],
        GlobalIllumination.High => [8, 16, 16, 16],
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
    /// At the window's half-sized depth a tile of eight pixels holds four by four of its texels, which
    /// a probe's point falls on, so a round marches from each texel once. The Halton points of bases
    /// 2 and 3 fell on one row of those texels five times a round and on another three, and the floor
    /// along a lamp's penumbra moved with turns of one to eight pixels by 0.64 to 1.27% of its light,
    /// where by 0.45 to 0.94% with these.
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
    /// Whether the screen's probes stand for the glow lights alone this frame, which they do at
    /// <see cref="GlobalIllumination.Low"/>, which leaves them out, while some glow light is lit.
    /// </summary>
    /// <remarks>
    /// Without them each pixel marches to every glow light in view itself, which took the model pass
    /// from 0.31 to 1.01 ms at Low on the Cornell box's four small emitters at 800 by 450 on the
    /// development machine's GPU, and the voxel game's from 1.2 to 13.3 ms with forty glowstones in
    /// view at 1280 by 720, so they are marched to once a tile, as the screen's probes march to
    /// them, and a pixel reads the texels around it, its cost the same however many lights there
    /// are. Left out for debugging (<see cref="GlobalIlluminationSettings.ScreenOff"/>), they stand
    /// for nothing, and each pixel marches.
    /// </remarks>
    internal bool GlowOnly(RenderWorld renderWorld) =>
        _glowCount > 0 && renderWorld.TryGet<GlobalIlluminationSettings>() is { ScreenOff: false, Quality: GlobalIllumination.Low };

    /// <summary>
    /// The pixels along each side of the tile a screen probe stands in, at a quality, for a window
    /// <paramref name="height"/> pixels high, the same up to 1080 and growing with the height past
    /// it, so a probe stands for about the same share of the picture on any screen.
    /// </summary>
    /// <remarks>
    /// The probes' cost grows with how many there are. Every 8 pixels at High whatever the screen,
    /// the voxel game's room of forty glowstones took 8.3 ms of global illumination at 1280 by 720
    /// and 35.9 at 3840 by 2160 on the development machine's GPU, the world's probes about 3 of it,
    /// and the Cornell box 3.3 and 7.2. A render texture's probes take the window's tile, so a view
    /// of a split screen reads the window's spacing.
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
    private static bool FarIn(RenderWorld renderWorld) => renderWorld.TryGet<SceneFieldRenderer>()?.Plan is { HasFar: true };

    /// <summary>
    /// Each of <paramref name="cascades"/> cascades' octahedron's texels at a quality, the far
    /// cascade's, the last where <paramref name="far"/>, as many as the cascade before it.
    /// </summary>
    internal static int[] TexelsFor(GlobalIllumination quality, int cascades, bool far)
    {
        var texels = TexelsAt(quality);
        return far ? [.. texels[..(cascades - 1)], texels[cascades - 2 < 0 ? 0 : cascades - 2]] : texels[..cascades];
    }

    /// <summary>Traces, merges and gathers the frame's probes, binding them for the model pass, or lets them go where light does not bounce.</summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld)
    {
        _frame++;
        for (int i = _retired.Count - 1; i >= 0; i--)
            if (_frame - _retired[i].Frame >= GpuTextures.RetireFrames)
            {
                _retired[i].Disposable.Dispose();
                _retired.RemoveAt(i);
            }
        renderWorld.TryGet<TargetIllumination>()?.ByTarget.Clear();
        _switches = renderWorld.TryGet<GlobalIlluminationSettings>();
        // A target's probes not drawn the frame before are let go, its history no longer the frame before's.
        foreach (var (id, state) in _targets.Where(entry => entry.Value.Drawn < _frame - 1).ToList())
        {
            _retired.Add((_frame, state));
            _targets.Remove(id);
        }
        var cascades = CascadesIn(renderWorld);
        if (cascades == 0 || renderContext.Device is not GraphicsDevice { CanBounceLight: true } device
            || renderWorld.TryGet<SceneFieldBinding>() is not { On: true } field || renderWorld.TryGet<SceneFieldSettings>() is not { } fieldSettings)
        {
            Release();
            renderWorld.Remove<IlluminationBinding>();
            return;
        }

        var wanted = renderWorld.TryGet<GlobalIlluminationSettings>()!.Quality;
        var plan = renderWorld.TryGet<SceneFieldRenderer>()?.Plan;
        var far = FarIn(renderWorld);
        if (_gi is null || !ReferenceEquals(_made.Field, field.Field) || _made.Quality != wanted || _made.Cascades != cascades)
        {
            Release();
            _gi = device.CreateGlobalIllumination(field.Field.Resolution / ProbeSpacing, TexelsFor(wanted, cascades, far));
            _made = (field.Field, wanted, cascades);
        }

        // Each cascade's interval, from its probes' spacing to four times that, the first's from the
        // probe, so a ray reaches as far again as the next cascade's begin. Ending at twice the
        // spacing, where the next cascade's begin, a ray met nothing short of a surface that the
        // probes of the next cascade nearer it met before their own interval began, which the merge
        // takes as dark, and the sun's patch on a room's floor lit the room 62% under a path-traced
        // reference at Low, where it reads 50% under.
        //
        // The last cascade's interval ends at the field's edge, the diagonal of its coarsest
        // cascade, since past it no cascade takes over. Ending at four times its spacing, a ray
        // across a room wider than that ended in mid-air and brought nothing, and at Low, whose
        // last cascade reached 9.6 units, the probe at the far wall of a room 12 wide read its face
        // toward the room 35% under a path-traced reference where High's, with a third cascade past
        // it, read 21%. Its rays go on until they meet a surface or leave the field, and read the
        // sky there as a ray leaving the field does.
        //
        // The far cascade's probes stand in the field's far cascade, their spacing that of its cells,
        // and its interval begins half way along the one before's, as each cascade's does. The
        // cascade before it traces as the last does, on to the far cascade's edge, meeting the far
        // cascade's cells only once its ray has left the counted ones, and bringing the sky where it
        // meets nothing, so it takes no light from the far cascade's probes. Those stand eight of its
        // cells apart, in cells that hold the ground near them as a slab, and merged into the
        // cascade before at four times its spacing, the voxel game's glass frame read 197, 192, 144
        // where 197, 195, 194 without the far cascade, and a red wall's blue 57 against 98. The far
        // cascade's probes give their light to the pixels past the counted cascades alone.
        var fieldEdge = SceneFieldPlan.Resolution * (plan?.CellOf(plan.Cascades - 1)
            ?? fieldSettings.CellSize * (1 << (Math.Clamp(fieldSettings.Cascades, 1, 8) - 1))) * MathF.Sqrt(3);
        var intervals = new (float Start, float End)[cascades];
        for (int c = 0; c < cascades; c++)
        {
            var spacing = ProbeSpacing * fieldSettings.CellSize * (1 << c);
            var start = c == 0 ? 0 : far && c == cascades - 1 ? 2 * ProbeSpacing * fieldSettings.CellSize * (1 << (c - 1)) : spacing;
            intervals[c] = (start, c == cascades - 1 || far && c == cascades - 2 ? fieldEdge : 4 * spacing);
        }
        // The screen's probes' rays reach twice as far as the world's probes are apart before they
        // take the light from beyond from them, where they end in the finest cascade
        // (gi_screen.slang), so a wall two units before a pillar sees the pillar and the floor lit
        // around it itself where those probes, standing at the pillar's dark face, saw the face
        // alone. Reaching as far as the probes are apart, the wall behind a pillar beside a lamp
        // read 0.68 of a path-traced reference's bounce where it read 1.05 without the pillar, and
        // reaching twice as far 0.79 and 1.01; three and four times as far read the wall past the
        // pillar up to 1.75.
        _screenReach = 2 * ProbeSpacing * fieldSettings.CellSize;

        var environment = renderWorld.TryGet<EnvironmentMap>() is not null ? renderWorld.TryGet<ModelRenderer>()?.Environment : null;
        _black ??= device.CreateCubeMap(1, 1, new Half[6 * 4]);
        var (view, sampler) = environment is not null ? (environment.View, environment.Sampler) : (_black.View, _black.Sampler);
        var lights = Lights(renderWorld, environment is not null);
        var switches = renderWorld.TryGet<GlobalIlluminationSettings>()!;

        // Each cascade lying where it lay the frame before, its probes the same, so a probe's own
        // light of the frame before is its own.
        _fieldRebuilt = 0;
        foreach (var build in plan?.Builds ?? [])
            if (build.Changed is { } changed)
            {
                _fieldChanged = _fieldRebuilt == 0 ? changed : _fieldChanged.With(changed);
                _fieldRebuilt |= 1u << build.Cascade;
            }
        var origins = new Vector3?[cascades];
        var lying = new bool[cascades];
        // And where each lay the frame before, which the rays read the faces of the light that
        // bounced at, since they were gathered there.
        var corners = MemoryMarshal.Cast<byte, Vector4>(lights.AsSpan(64 + 16 * 64, MostCascades * 16));
        for (int c = 0; c < cascades; c++)
        {
            origins[c] = plan?.BuiltOrigin(plan.CascadeOfProbes(c, cascades));
            lying[c] = origins[c] is not null && c < _origins.Length && _origins[c] == origins[c];
            if (c < corners.Length && ((c < _origins.Length ? _origins[c] : null) ?? origins[c]) is { } then)
                corners[c] = new Vector4(then, 0);
        }
        _origins = origins;
        _lightThrough = false;
        foreach (var (instance, _) in renderWorld.TryGet<SceneFieldRenderer>()?.Drawn ?? [])
            _lightThrough |= instance.LightThrough > 0 && !switches.ThroughOff;
        _retired.Add((_frame, device.RecordGlobalIllumination(renderContext.CommandBuffer, _gi, field.Field, view, sampler, lights, intervals, ProbeSpacing,
            switches.MergeOff, switches.Alone, switches.Shown is BounceView.Rays or BounceView.Merged, _frame, switches.FollowOff ? null : lying,
            far ? cascades - 2 : -1, _lightThrough)));

        // The screen's probes, where the window has a depth to stand them on, the first interval
        // theirs. At Low, which leaves them out, they stand for the glow lights alone while some
        // are lit, each marching to the lights once a tile and tracing no ray (GlowOnly).
        var quality = renderWorld.TryGet<GlobalIlluminationSettings>()!.Quality;
        var glowOnly = GlowOnly(renderWorld);
        if ((ScreenProbesOn(renderWorld) || glowOnly) && renderWorld.TryGet<WindowDepth>() is { } depth
            && renderWorld.TryGet<WindowView>() is { } window && renderWorld.TryGet<SwapchainTarget>() is { } swapchain
            && Matrix4x4.Invert(window.ViewProjection, out var inverse))
        {
            var (width, height, tile) = (swapchain.Extent.Width, swapchain.Extent.Height, TileAt(quality, swapchain.Extent.Height));
            if (_screen is null || _screen.Tile != tile || _screen.Across != (width + tile - 1) / tile || _screen.Down != (height + tile - 1) / tile
                || _screenGlowOnly != glowOnly)
            {
                if (_screen is not null) _retired.Add((_frame, _screen));
                _screen = device.CreateScreenProbes(width, height, tile);
                _screenGlowOnly = glowOnly;
                _lastScreen = null;
            }
            var jitter = JitterAt(_frame, tile);
            var bytes = ViewBytes(window.ViewProjection, inverse, window.Eye, _screen, depth.Extent, swapchain.Extent, jitter, _lastScreen);
            _retired.Add((_frame, device.RecordScreenProbes(renderContext.CommandBuffer, _gi, _screen, field.Field, depth.View, depth.Sampler, bytes, _glowCount > 0,
                renderWorld.TryGet<GlobalIlluminationSettings>()?.LoggedScreenProbe ?? -1, glowOnly,
                renderWorld.TryGet<GlobalIlluminationSettings>()?.LoggedScreenProbeNotANumber ?? false, _lightThrough)));
            _lastScreen = (window.ViewProjection, window.Eye);
        }
        else if (_screen is not null)
        {
            _retired.Add((_frame, _screen));
            _screen = null;
            _lastScreen = null;
        }

        // The window's meshes for the device's ray tracing, which a reflection the field misses is
        // traced through, at the top quality where the device and the model pass trace rays, a
        // skinned mesh, whose posed shape no vertices on the CPU hold, left out, and each marked
        // where global illumination carries it as a glow light, for a probe's reference.
        if (quality == GlobalIllumination.High && device.CanQueryRays && renderWorld.TryGet<ModelRenderer>() is { TracesRays: true }
            && !renderWorld.TryGet<GlobalIlluminationSettings>()!.RaysOff
            && renderWorld.TryGet<SceneFieldRenderer>() is { } fields)
        {
            _rays ??= device.CreateRayScene();
            var copies = new List<RayInstance>(fields.Drawn.Count);
            // A mesh over shader buffers, whose triangles the CPU does not hold, is passed over.
            foreach (var (instance, skinned) in fields.Drawn)
                if (!skinned && fields.HoldsTriangles(instance.Vertices)) copies.Add(new RayInstance(instance.Vertices, instance.World, instance.Color, instance.Emission, instance.Roughness, instance.Metallic,
                    plan is not null && fields.Carried(instance, plan.CellSize), instance.LightThrough));
            _retired.Add((_frame, device.RecordRayScene(renderContext.CommandBuffer, _rays, copies, mesh => fields.CornersOf((ModelVertex[])mesh),
                mesh => fields.CornerColorsOf((ModelVertex[])mesh), fields.Recolored)));
        }
        else if (_rays is not null)
        {
            _retired.Add((_frame, _rays));
            _rays = null;
        }
        renderWorld.Set(new IlluminationBinding(_gi, field.Field, _screen, renderWorld.TryGet<WindowDepth>(), _history, _rays, _glowCount));
    }

    /// <summary>
    /// Traces, blends and holds the screen's probes of render target <paramref name="id"/>, of
    /// <paramref name="size"/>, as the window's are, stood on the depth at half its size of the
    /// meshes it draws through <paramref name="camera"/> that its occlusion was worked out from
    /// (<see cref="AmbientOcclusionRenderer.DrawTarget"/>), after this frame's world probes and
    /// ahead of its pass, which reads them first as the window's model pass reads its own.
    /// </summary>
    /// <remarks>
    /// Each target has probes and a history of its own, let go the frame after one it is not drawn
    /// in. A reflection probe's faces read the world's probes alone.
    /// </remarks>
    public void DrawTarget(RenderContext renderContext, RenderWorld renderWorld, int id, Extent2D size, Matrix4x4 camera)
    {
        if (_gi is null || renderContext.Device is not GraphicsDevice device || renderWorld.TryGet<SceneFieldBinding>() is not { On: true } field
            || renderWorld.TryGet<GlobalIlluminationSettings>() is not { } settings
            || renderWorld.TryGet<TargetOcclusion>()?.Depths.GetValueOrDefault(id) is not { } depth
            || !Matrix4x4.Invert(camera, out var inverse)) return;
        var targets = renderWorld.TryGet<TargetIllumination>();
        if (targets is null) renderWorld.Set(targets = new TargetIllumination());
        // Where the screen's probes are left out the target reads the world's probes alone, and at
        // Low its glow lights from probes standing for them alone while some are lit, as the window.
        var glowOnly = GlowOnly(renderWorld);
        if (!ScreenProbesOn(renderWorld) && !glowOnly)
        {
            if (_targets.Remove(id, out var left)) _retired.Add((_frame, left));
            targets.ByTarget[id] = new IlluminationBinding(_gi, field.Field, null, depth, GlowLights: _glowCount);
            return;
        }
        var tile = TileAt(settings.Quality, renderWorld.TryGet<SwapchainTarget>()?.Extent.Height ?? size.Height);
        if (!_targets.TryGetValue(id, out var state) || state.Screen.Tile != tile || state.GlowOnly != glowOnly
            || state.Screen.Across != (size.Width + tile - 1) / tile || state.Screen.Down != (size.Height + tile - 1) / tile)
        {
            if (state is not null) _retired.Add((_frame, state));
            state = _targets[id] = new TargetScreen(device.CreateScreenProbes(size.Width, size.Height, tile)) { GlowOnly = glowOnly };
        }
        state.Drawn = _frame;
        var eye = Vector4.Transform(new Vector4(0, 0, 0, 1), inverse);
        var at = new Vector3(eye.X, eye.Y, eye.Z) / eye.W;
        var jitter = JitterAt(_frame, tile);
        var bytes = ViewBytes(camera, inverse, at, state.Screen, depth.Extent, size, jitter, state.Last);
        _retired.Add((_frame, device.RecordScreenProbes(renderContext.CommandBuffer, _gi, state.Screen, field.Field, depth.View, depth.Sampler, bytes, _glowCount > 0,
            glowOnly: glowOnly, lightThrough: _lightThrough)));
        state.Last = (camera, at);
        targets.ByTarget[id] = new IlluminationBinding(_gi, field.Field, state.Screen, depth, GlowLights: _glowCount);
    }

    // A view's buffer for the screen's probes, as gi_screen.slang reads it, with its camera both
    // ways and its eye, the probes' tile and how many lie across and down, the first interval's
    // half, the world's probes, their spacing and cascades, the depth's size and the view's, its
    // camera and eye the frame before where the probes were placed then, how far from their tiles'
    // middles the probes march to the glow lights from, how many glow lights there are with the
    // places whose light the frame before had there, and the field's cascades built this frame as
    // meshes came or went within them with the box around those meshes. The matrices lie as they do
    // in memory, which the shaders, reading them column-major, multiply a point by as the CPU does.
    private byte[] ViewBytes(Matrix4x4 viewProjection, Matrix4x4 inverse, Vector3 eye, GpuScreenProbes screen, Extent2D depth, Extent2D size,
        Vector2 jitter, (Matrix4x4 ViewProjection, Vector3 Eye)? last)
    {
        var bytes = new byte[GpuScreenProbes.ViewBytes];
        MemoryMarshal.Write(bytes, viewProjection);
        MemoryMarshal.Write(bytes.AsSpan(64), inverse);
        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
        (floats[32], floats[33], floats[34]) = (eye.X, eye.Y, eye.Z);
        (floats[36], floats[37], floats[38]) = (screen.Tile, screen.Across, screen.Down);
        // What of the screen's blend is left out, the frame before's light and the neighbors'.
        floats[39] = _switches is { } off ? (off.HistoryOff ? 1 : 0) + (off.FilterOff ? 2 : 0) : 0;
        (floats[40], floats[41], floats[42], floats[43]) = (_screenReach, _gi!.Probes, ProbeSpacing, _made.Cascades);
        (floats[44], floats[45], floats[46], floats[47]) = (depth.Width, depth.Height, size.Width, size.Height);
        (floats[68], floats[69], floats[70]) = (jitter.X, jitter.Y, _frame % 16);
        if (last is { } then)
        {
            MemoryMarshal.Write(bytes.AsSpan(192), then.ViewProjection);
            (floats[64], floats[65], floats[66], floats[67]) = (then.Eye.X, then.Eye.Y, then.Eye.Z, 1);
        }
        floats[72] = _glowCount;
        var words = MemoryMarshal.Cast<byte, uint>(bytes.AsSpan());
        (words[73], words[74], words[75]) = (_glowPlaces.Kept.Low, _glowPlaces.Kept.High, _fieldRebuilt);
        (floats[76], floats[77], floats[78]) = (_fieldChanged.Min.X, _fieldChanged.Min.Y, _fieldChanged.Min.Z);
        (floats[80], floats[81], floats[82]) = (_fieldChanged.Max.X, _fieldChanged.Max.Y, _fieldChanged.Max.Z);
        return bytes;
    }

    /// <summary>
    /// Keeps <paramref name="scene"/>, the window's scene the HDR frame drew this frame at
    /// <paramref name="extent"/>, for the reflections of the frame after, once the model pass has
    /// read the frame before's, with the window's depth beside it, made again where the window's size
    /// changed.
    /// </summary>
    public void KeepFrame(RenderContext renderContext, RenderWorld renderWorld, IImageView scene, Extent2D extent)
    {
        if (_gi is null || renderContext.Device is not GraphicsDevice device || renderWorld.TryGet<WindowView>() is not { } window
            || renderWorld.TryGet<WindowDepth>() is not { } depth) return;
        if (_history is null || _history.Window != extent)
        {
            if (_history is not null) _retired.Add((_frame, _history));
            _history = device.CreateReflectionHistory(extent.Width, extent.Height);
        }
        device.RecordKeepFrame(renderContext.CommandBuffer, scene, depth.View, _history);
        HistoryViewProjection = window.ViewProjection;
    }

    // The lights as gi.slang's GiLights, the sun, the first directional light, the shadowed one
    // where it is one; the light from all around a ray meets where it meets nothing; and the point
    // and spot lights, sixteen at most.
    private byte[] Lights(RenderWorld renderWorld, bool environment)
    {
        var bytes = new byte[GpuIllumination.LightsBytes];
        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
        var all = renderWorld.TryGet<RenderLights>()?.All ?? [];
        var shadowed = renderWorld.TryGet<FrameShadow>()?.Light ?? -1;
        var sun = shadowed >= 0 && shadowed < all.Count && all[shadowed].Kind == LightKind.Directional
            ? shadowed : all.FindIndex(light => light.Kind == LightKind.Directional);
        if (sun >= 0)
        {
            var towardSun = -all[sun].Direction;
            (floats[0], floats[1], floats[2], floats[3]) = (towardSun.X, towardSun.Y, towardSun.Z, 1);
            (floats[4], floats[5], floats[6]) = (all[sun].EmittedColor.X, all[sun].EmittedColor.Y, all[sun].EmittedColor.Z);
        }
        var sky = Vector3.Zero;
        if (environment && renderWorld.TryGet<EnvironmentMap>() is { } map)
        {
            // The map's light on the scene, at the share and tint the program set for it.
            sky = map.Intensity * EnvironmentLight.In(renderWorld);
            floats[11] = 1;
        }
        else
            foreach (var light in all.Where(light => light.Kind == LightKind.Ambient)) sky += light.EmittedColor;
        (floats[8], floats[9], floats[10]) = (sky.X, sky.Y, sky.Z);

        var entries = MemoryMarshal.Cast<byte, LightUboEntry>(bytes.AsSpan(64));
        var count = 0;
        foreach (var light in all)
        {
            if (light.Kind is not (LightKind.Point or LightKind.Spot) || count == entries.Length) continue;
            entries[count++] = new LightUboEntry
            {
                PositionAndKind = new Vector4(light.Position, (int)light.Kind),
                DirectionAndRange = new Vector4(light.Direction, light.Range),
                // w: 1 for a light that casts shadows, which the probes' rays march toward.
                ColorAndShadow = new Vector4(light.EmittedColor, light.CastsShadows ? 1 : 0),
                Cone = new Vector4(light.CosInner, light.CosOuter, 0, 0),
            };
        }
        (floats[12], floats[13], floats[14]) = (count, _frame, renderWorld.TryGet<GlobalIlluminationSettings>() is { AgainOff: true } ? 0 : 1);
        // The small emitters carried as lights, the nearest the eye the field lies around, which
        // where the window draws no mesh is the first render target's camera's.
        if (renderWorld.TryGet<GlobalIlluminationSettings>() is not { GlowOff: true }
            && renderWorld.TryGet<SceneFieldRenderer>() is { Plan: { } plan, Eye: { } eye } field)
        {
            _glowFound = GlowLights.Write(bytes.AsSpan(GlowLights.Offset, GlowLights.Size), field.Drawn, eye, plan.CellSize, field.BoundsOf, field.ShareOf,
                _glowPlaces, field.GlowPartsOf);
            _glowCount = (int)MemoryMarshal.Cast<byte, float>(bytes.AsSpan(GlowLights.Offset, 4))[0];
        }
        else
        {
            _glowPlaces.Forget();
            (_glowCount, _glowFound) = (0, 0);
        }
        return bytes;
    }

    // Lets the probes go, once no frame in flight reads them.
    private void Release()
    {
        if (_gi is not null) _retired.Add((_frame, _gi));
        if (_screen is not null) _retired.Add((_frame, _screen));
        if (_history is not null) _retired.Add((_frame, _history));
        if (_rays is not null) _retired.Add((_frame, _rays));
        foreach (var state in _targets.Values) _retired.Add((_frame, state));
        _targets.Clear();
        (_gi, _screen, _history, _rays, _lastScreen) = (null, null, null, null, null);
        _origins = [];
        HistoryViewProjection = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        _gi?.Dispose();
        _screen?.Dispose();
        _history?.Dispose();
        _rays?.Dispose();
        foreach (var state in _targets.Values) state.Dispose();
        _black?.Dispose();
    }
}

/// <summary>Render graph node that traces global illumination, after the scene's distance field is built and before the passes that light the window.</summary>
internal sealed class GlobalIlluminationNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<GlobalIlluminationRenderer>()?.Draw(renderContext, renderWorld);
}
