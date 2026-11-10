using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static Engine.Illumination;

namespace Engine;

/// <summary>
/// Global illumination, as cascades of light probes over the scene's distance field (Radiance
/// Cascades), traced, merged and gathered on the GPU each frame
/// (<see cref="GlobalIlluminationStages.RecordGlobalIllumination"/>), which the model pass adds to
/// the window's light from all around.
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
internal sealed partial class GlobalIlluminationRenderer : IIlluminationPasses, IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.GlobalIllumination");

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
    // The window's shadow as this frame's shadow pass draws it after the probes' rays, which the
    // frame after's rays read the sun through.
    private FrameShadow? _sunBefore;

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
    public Matrix4x4? HistoryViewProjection { get; private set; }

    /// <summary>
    /// The window's meshes for the device's ray tracing, built each frame at the top quality where
    /// the device and the model pass trace rays, or null where none are.
    /// </summary>
    internal GpuRayScene? Rays => _rays;

    /// <inheritdoc />
    public bool TracesRays => _rays is not null;

    /// <summary>
    /// Whether the screen's probes stand for the glow lights alone this frame, which they do at
    /// <see cref="GlobalIllumination.Low"/>, which leaves them out, while some glow light is lit.
    /// </summary>
    /// <remarks>
    /// Without them each pixel marches to every glow light in view itself, the model pass's cost
    /// growing with the lights in view, so they are marched to once a tile, as the screen's probes
    /// march to them, and a pixel reads the texels around it, its cost the same however many lights
    /// there are. Left out for debugging (<see cref="GlobalIlluminationSettings.ScreenOff"/>), they
    /// stand for nothing, and each pixel marches.
    /// </remarks>
    public bool GlowOnly(RenderWorld renderWorld) =>
        _glowCount > 0 && renderWorld.TryGet<GlobalIlluminationSettings>() is { ScreenOff: false, Quality: GlobalIllumination.Low };

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
        if (cascades == 0 || renderContext.Device is not GraphicsDevice device || !GlobalIlluminationStages.Of(device).CanBounceLight
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
            var making = Stopwatch.GetTimestamp();
            _gi = GlobalIlluminationStages.Of(device).CreateGlobalIllumination(field.Field.Resolution / ProbeSpacing, TexelsFor(wanted, cascades, far), far,
                cycle: wanted == GlobalIllumination.High && device.CanQueryRays);
            _made = (field.Field, wanted, cascades);
            Logger.Info($"Global illumination's probes made at {wanted}, {cascades} cascades of {_gi.Probes} a side, "
                + $"in {Stopwatch.GetElapsedTime(making).TotalMilliseconds:0} ms.");
        }

        // Each cascade's interval, from its probes' spacing to four times that, the first's from
        // the probe, so a ray reaches as far again as the next cascade's begin. Ending at twice the
        // spacing, where the next cascade's begin, a ray met nothing short of a surface that the
        // probes of the next cascade nearer it met before their own interval began, which the merge
        // takes as dark, and the room lost part of the light the sun's patch on its floor gave it.
        //
        // The last cascade's interval ends at the field's edge, the diagonal of its coarsest
        // cascade, since past it no cascade takes over. Ending at four times its spacing, a ray
        // across a room wider than that ended in mid-air and brought nothing, and at Low, whose
        // last cascade reached 9.6 units, the probe at the far wall of a room 12 wide read its face
        // toward the room dimmer than High's, with a third cascade past it, did. Its rays go on
        // until they meet a surface or leave the field, and read the sky there as a ray leaving the
        // field does.
        //
        // The far cascade's probes stand in the field's far cascade, their spacing that of its
        // cells, and its interval begins half way along the one before's, as each cascade's does.
        // The cascade before it traces as the last does, on to the far cascade's edge, meeting the
        // far cascade's cells only once its ray has left the counted ones, and bringing the sky
        // where it meets nothing, so it takes no light from the far cascade's probes. Those stand
        // eight of its cells apart, in cells that hold the ground near them as a slab, and merged
        // into the cascade before at four times its spacing they drew the blue out of the voxel
        // game's glass frame and of a red wall. The far cascade's probes give their light to the
        // pixels past the counted cascades alone.
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
        // read darker than a path-traced reference, and three and four times as far read the wall
        // past the pillar brighter than it.
        _screenReach = 2 * ProbeSpacing * fieldSettings.CellSize;

        var environment = renderWorld.TryGet<EnvironmentMap>() is not null ? renderWorld.TryGet<ModelRenderer>()?.Environment : null;
        _black ??= device.CreateCubeMap(1, 1, new Half[6 * 4]);
        var (view, sampler) = environment is not null ? (environment.View, environment.Sampler) : (_black.View, _black.Sampler);
        var (sunMap, sunSampler, sunShadow) = SunMap(renderWorld, device);
        var lights = Lights(renderWorld, environment is not null, sunShadow);
        // Whether the sun, the sky or a light differs from the frame before's by more than a
        // hundredth, which with a build of the field begins every screen probe's cycle of places
        // again (gi_screen_filter.slang).
        NoteLights(lights);
        _sunBefore = renderWorld.TryGet<FrameShadow>();
        var switches = renderWorld.TryGet<GlobalIlluminationSettings>()!;
        NoteSwitches(switches);

        // Each cascade lying where it lay the frame before, its probes the same, so a probe's own
        // light of the frame before is its own.
        _fieldRebuilt = 0;
        foreach (var build in plan?.Builds ?? [])
            if (build.Changed is { } changed)
            {
                _fieldChanged = _fieldRebuilt == 0 ? changed : _fieldChanged.With(changed);
                _fieldRebuilt |= 1u << build.Cascade;
            }
        NoteChanged();
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
        var shifts = Shifts(origins, plan, cascades);
        _origins = origins;
        // The far cascade's probes trace a part of them a frame, and all on a frame it moved, whose
        // probes' light of the frames before lay elsewhere (RecordGlobalIllumination).
        var farPart = far && lying[cascades - 1] ? (int)(_frame % GlobalIlluminationStages.FarParts) : -1;
        _lightThrough = false;
        foreach (var (instance, _) in renderWorld.TryGet<SceneFieldRenderer>()?.Drawn ?? [])
            _lightThrough |= instance.LightThrough > 0 && !switches.ThroughOff;
        GlobalIlluminationStages.Of(device).GlobalIlluminationPassesTimed = switches.PassesTimed;
        RecordRays(renderContext, renderWorld, device, plan);
        // A carried probe's rays move within their texels and meet the meshes through the device's
        // rays at High where it traces them, the field's finest cells missing an arch's edge thinner
        // than they are, which the moving rays passed beside, and elsewhere keep their middles.
        var moving = _gi.CycleOn && _rays is not null && !_lightThrough;
        _retired.Add((_frame, GlobalIlluminationStages.Of(device).RecordGlobalIllumination(renderContext.CommandBuffer, _gi, field.Field, view, sampler, sunMap, sunSampler, lights, intervals, ProbeSpacing,
            switches.MergeOff, switches.Alone, switches.Shown is BounceView.Rays or BounceView.Merged, _frame, switches.FollowOff ? null : lying,
            far ? cascades - 2 : -1, _lightThrough, plan is { Whole: true }, farPart, switches.MeshesTraced || moving ? _rays : null, shifts, CarriedShare, CarriedSince, CarriedPhase,
            (moving ? 1u : 0) | (switches.MeshesTraced ? 2u : 0))));

        // The screen's probes, where the window has a depth to stand them on, the first interval
        // theirs. At Low, which leaves them out, they stand for the glow lights alone while some
        // are lit, each marching to the lights once a tile and tracing no ray (GlowOnly).
        var quality = renderWorld.TryGet<GlobalIlluminationSettings>()!.Quality;
        var glowOnly = GlowOnly(renderWorld);
        if ((ScreenProbesOn(renderWorld) || glowOnly) && renderWorld.TryGet<WindowDepth>() is { } depth
            && renderWorld.TryGet<WindowView>() is { } window && renderWorld.TryGet<SwapchainTarget>() is { } swapchain
            && Matrix4x4.Invert(window.Unjittered, out var inverse))
        {
            var (width, height, tile) = (swapchain.Extent.Width, swapchain.Extent.Height, TileAt(quality, swapchain.Extent.Height));
            var glowTexel = GpuScreenProbes.GlowTexelAt(height);
            if (_screen is null || _screen.Tile != tile || _screen.Across != (width + tile - 1) / tile || _screen.Down != (height + tile - 1) / tile
                || _screenGlowOnly != glowOnly || _screen.GlowTexel != glowTexel)
            {
                if (_screen is not null) _retired.Add((_frame, _screen));
                _screen = GpuScreenProbes.Create(device, width, height, tile, glowTexel);
                _screenGlowOnly = glowOnly;
                _lastScreen = null;
            }
            var jitter = JitterAt(_frame, tile);
            var bytes = ViewBytes(window.Unjittered, inverse, window.Eye, _screen, depth.Extent, swapchain.Extent, jitter, _lastScreen);
            _retired.Add((_frame, GlobalIlluminationStages.Of(device).RecordScreenProbes(renderContext.CommandBuffer, _gi, _screen, field.Field, depth.View, depth.Sampler, bytes, _glowCount > 0,
                renderWorld.TryGet<GlobalIlluminationSettings>()?.LoggedScreenProbe ?? -1, glowOnly,
                renderWorld.TryGet<GlobalIlluminationSettings>()?.LoggedScreenProbeNotANumber ?? false, _lightThrough)));
            _lastScreen = (window.Unjittered, window.Eye);
        }
        else if (_screen is not null)
        {
            _retired.Add((_frame, _screen));
            _screen = null;
            _lastScreen = null;
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
        // A target's glow texels the window's, as its tile is, since the model pass reads one ratio.
        var glowTexel = GpuScreenProbes.GlowTexelAt(renderWorld.TryGet<SwapchainTarget>()?.Extent.Height ?? size.Height);
        if (!_targets.TryGetValue(id, out var state) || state.Screen.Tile != tile || state.GlowOnly != glowOnly || state.Screen.GlowTexel != glowTexel
            || state.Screen.Across != (size.Width + tile - 1) / tile || state.Screen.Down != (size.Height + tile - 1) / tile)
        {
            if (state is not null) _retired.Add((_frame, state));
            state = _targets[id] = new TargetScreen(GpuScreenProbes.Create(device, size.Width, size.Height, tile, glowTexel)) { GlowOnly = glowOnly };
        }
        state.Drawn = _frame;
        var eye = Vector4.Transform(new Vector4(0, 0, 0, 1), inverse);
        var at = new Vector3(eye.X, eye.Y, eye.Z) / eye.W;
        var jitter = JitterAt(_frame, tile);
        var bytes = ViewBytes(camera, inverse, at, state.Screen, depth.Extent, size, jitter, state.Last);
        _retired.Add((_frame, GlobalIlluminationStages.Of(device).RecordScreenProbes(renderContext.CommandBuffer, _gi, state.Screen, field.Field, depth.View, depth.Sampler, bytes, _glowCount > 0,
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
        (floats[68], floats[69], floats[70], floats[71]) = (jitter.X, jitter.Y, _frame % 16, Settling ? 1 : 0);
        if (last is { } then)
        {
            MemoryMarshal.Write(bytes.AsSpan(192), then.ViewProjection);
            // 2 where the view is the frame before's, which the filter reads each probe where it
            // stands by, as the frame before's matrices put it back off its place far from the origin.
            var still = then.ViewProjection == viewProjection && then.Eye == eye;
            (floats[64], floats[65], floats[66], floats[67]) = (then.Eye.X, then.Eye.Y, then.Eye.Z, still ? 2 : 1);
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
        HistoryViewProjection = window.Unjittered;
    }

    // The window's shadow map as the frame before's shadow pass drew it and the shadow it drew it
    // for, where that shadow was the sun's, a directional light's, and the map is of its size, or a
    // white texture and none.
    private (IImageView Map, ISampler Sampler, FrameShadow? Shadow) SunMap(RenderWorld renderWorld, GraphicsDevice device)
    {
        var lights = renderWorld.TryGet<RenderLights>()?.All ?? [];
        if (_sunBefore is { } before && before.Light >= 0 && before.Light < lights.Count && lights[before.Light].Kind == LightKind.Directional
            && renderWorld.TryGet<ModelRenderer>()?.DrawnShadowMap is { } map && map.Extent.Width == 2 * before.TileSize)
            return (map.DepthView, map.Sampler, before);
        var (white, whiteSampler) = renderWorld.TryGet<GpuTextures>()!.ViewFor(device, 0);
        return (white, whiteSampler, null);
    }

    /// <summary>
    /// The index among the render world's lights of the sun the probes are lit by, the directional
    /// light the frame's shadow is drawn for or the first directional light, or -1 where there is none.
    /// </summary>
    internal static int SunOf(RenderWorld renderWorld)
    {
        var all = renderWorld.TryGet<RenderLights>()?.All ?? [];
        var shadowed = renderWorld.TryGet<FrameShadow>()?.Light ?? -1;
        return shadowed >= 0 && shadowed < all.Count && all[shadowed].Kind == LightKind.Directional
            ? shadowed : all.FindIndex(light => light.Kind == LightKind.Directional);
    }

    // The lights as gi.slang's GiLights, the sun, the first directional light, the shadowed one
    // where it is one; the light from all around a ray meets where it meets nothing; the point
    // and spot lights, sixteen at most; and the counted cascades of the sun's shadow the frame
    // before drew its map for, where there is one.
    private byte[] Lights(RenderWorld renderWorld, bool environment, FrameShadow? sunShadow)
    {
        var bytes = new byte[GpuIllumination.LightsBytes];
        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
        var all = renderWorld.TryGet<RenderLights>()?.All ?? [];
        var sun = SunOf(renderWorld);
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
        var settings = renderWorld.TryGet<GlobalIlluminationSettings>();
        (floats[12], floats[13], floats[14]) = (count, _frame, settings is null ? 1 : settings.AgainOff ? 0 : settings.BounceGain);
        // The sun's map is read for its own light alone, so a shadow drawn for another light is not.
        if (sunShadow is { } drawn && drawn.Light == sun)
        {
            var mapped = MemoryMarshal.Cast<byte, float>(bytes.AsSpan(GpuIllumination.SunMapOffset, GpuIllumination.SunMapBytes));
            var counted = Math.Min(drawn.Cascades.Count, 4);
            for (int c = 0; c < counted; c++)
            {
                MemoryMarshal.Write(bytes.AsSpan(GpuIllumination.SunMapOffset + c * 64), drawn.Cascades[c].ViewProjection);
                mapped[64 + c] = drawn.Cascades[c].Texel;
            }
            (mapped[68], mapped[69]) = (counted, 1f / (2 * drawn.TileSize));
        }
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
