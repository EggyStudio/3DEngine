using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>How much of the light that bounces between surfaces is worked out, from none to the full set of cascades.</summary>
/// <seealso cref="Engine3D.SetGlobalIllumination"/>
public enum GlobalIllumination
{
    /// <summary>No light bounces, as raylib draws a scene.</summary>
    Off,

    /// <summary>Two cascades of probes, 16 and 64 directions each, for a slow GPU.</summary>
    Low,

    /// <summary>Three cascades, 16, 64 and 256 directions.</summary>
    Medium,

    /// <summary>Four cascades, 64 directions in the first and 256 in each after.</summary>
    High,
}

/// <summary>How much light bounces as the program set it, a world resource the renderer reads each frame.</summary>
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

    /// <summary>Whether the model pass reads the world's probes alone, the screen's left unread, as <c>gi.toggle screen off</c> sets it.</summary>
    public bool ScreenOff { get; set; }

    /// <summary>Whether each cascade keeps its own rays' light, taking nothing from the cascade above, as <c>gi.toggle merge off</c> sets it.</summary>
    public bool MergeOff { get; set; }

    /// <summary>Whether the surfaces the rays meet take none of the light that bounced to them the frame before, so light bounces once, as <c>gi.toggle again off</c> sets it.</summary>
    public bool AgainOff { get; set; }

    /// <summary>The one cascade whose rays' light alone reaches the pixels, or -1 for every cascade, as <c>gi.toggle cascade</c> sets it.</summary>
    public int Alone { get; set; } = -1;

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
/// The probes the model pass reads the window's bounced light from this frame, over the field they
/// were traced through, and the screen's probes, which it reads first where one stands near, with
/// what a glossy surface traces its reflection through: the window's depth, its scene the frame
/// before where one was kept, and its meshes for the device's ray tracing where it traces rays.
/// </summary>
internal sealed record IlluminationBinding(GpuIllumination Probes, GpuSceneField Field, GpuScreenProbes? Screen,
    WindowDepth? Depth = null, GpuReflectionHistory? History = null, GpuRayScene? Rays = null);

/// <summary>
/// What the model pass reads each render target's bounced light from this frame, by the target's
/// texture id: the world's probes with the target's own screen probes, stood on its own depth,
/// where it draws meshes through a camera of its own.
/// </summary>
internal sealed class TargetIllumination
{
    /// <summary>Each target's binding, set as the target's probes are traced ahead of its pass.</summary>
    public Dictionary<int, IlluminationBinding> ByTarget { get; } = [];
}

/// <summary>
/// The light that bounces between surfaces, as cascades of light probes over the scene's distance
/// field (Radiance Cascades), traced, merged and gathered on the GPU each frame
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
/// A ray that meets a surface brings back the light the surface sends along it: its color, which
/// the field paints, times the sun's light where the field lets the sun through to it, the point
/// and spot lights', each that casts shadows where the field lets it through too, and the light
/// that bounced to it the frame before, so light
/// bounces again each frame, with the light it gives off. A ray of the last cascade that meets
/// nothing brings back the environment map's light, or the ambient lights' where there is none.
/// </para>
/// </remarks>
internal sealed class GlobalIlluminationRenderer : IDisposable
{
    /// <summary>How many cells of their field cascade the probes lie apart.</summary>
    public const int ProbeSpacing = 8;

    /// <summary>
    /// The roughness from which a surface traces no reflection, the probes and the environment
    /// reflecting alone, its traced reflection fading out from half of it.
    /// </summary>
    public const float GlossyRoughness = 0.5f;

    private GpuIllumination? _gi;
    private GpuScreenProbes? _screen;

    // The window's camera the screen's probes were last placed through, which their blend finds a
    // probe's place in the frame before by, or null where the probes were laid out again since.
    private (Matrix4x4 ViewProjection, Vector3 Eye)? _lastScreen;
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

        public void Dispose() => Screen.Dispose();
    }

    private readonly Dictionary<int, TargetScreen> _targets = [];
    private GpuReflectionHistory? _history;
    private GpuRayScene? _rays;
    private (GpuSceneField Field, GlobalIllumination Quality, int Cascades) _made;
    private CubeMap? _black;
    private long _frame;
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
        GlobalIllumination.Low => [4, 8],
        GlobalIllumination.Medium => [4, 8, 16],
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

    /// <summary>The pixels along each side of the tile a screen probe stands in, at a quality.</summary>
    internal static int TileAt(GlobalIllumination quality) => quality switch
    {
        GlobalIllumination.Low => 16,
        GlobalIllumination.Medium => 12,
        _ => 8,
    };

    /// <summary>
    /// How many cascades of probes the window's light bounces through this frame, which the window's
    /// lighting buffer tells the model pass, 0 where light does not bounce.
    /// </summary>
    internal static int CascadesIn(RenderWorld renderWorld) =>
        renderWorld.TryGet<GlobalIlluminationSettings>() is { Quality: not GlobalIllumination.Off } settings
        && renderWorld.TryGet<SceneFieldSettings>() is { On: true } field
            ? Math.Min(TexelsAt(settings.Quality).Length, Math.Clamp(field.Cascades, 1, 8))
            : 0;

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
        if (_gi is null || !ReferenceEquals(_made.Field, field.Field) || _made.Quality != wanted || _made.Cascades != cascades)
        {
            Release();
            _gi = device.CreateGlobalIllumination(field.Field.Resolution / ProbeSpacing, TexelsAt(wanted)[..cascades]);
            _made = (field.Field, wanted, cascades);
        }

        // Each cascade's interval, from its probes' spacing to four times that, the first's from the
        // probe, so a ray reaches as far again as the next cascade's begin. Ending at twice the
        // spacing, where the next cascade's begin, a ray met nothing short of a surface that the
        // probes of the next cascade nearer it met before their own interval began, which the merge
        // takes as dark, and the sun's patch on a room's floor lit the room 62% under a path-traced
        // reference at Low, where it reads 50% under.
        var intervals = new (float Start, float End)[cascades];
        for (int c = 0; c < cascades; c++)
        {
            var spacing = ProbeSpacing * fieldSettings.CellSize * (1 << c);
            intervals[c] = (c == 0 ? 0 : spacing, 4 * spacing);
        }
        _screenReach = ProbeSpacing * fieldSettings.CellSize;

        var environment = renderWorld.TryGet<EnvironmentMap>() is not null ? renderWorld.TryGet<ModelRenderer>()?.Environment : null;
        _black ??= device.CreateCubeMap(1, 1, new Half[6 * 4]);
        var (view, sampler) = environment is not null ? (environment.View, environment.Sampler) : (_black.View, _black.Sampler);
        var lights = Lights(renderWorld, environment is not null);
        var switches = renderWorld.TryGet<GlobalIlluminationSettings>()!;
        _retired.Add((_frame, device.RecordGlobalIllumination(renderContext.CommandBuffer, _gi, field.Field, view, sampler, lights, intervals, ProbeSpacing,
            switches.MergeOff, switches.Alone, switches.Shown is BounceView.Rays or BounceView.Merged)));

        // The screen's probes, where the window has a depth to stand them on, the first interval theirs.
        var quality = renderWorld.TryGet<GlobalIlluminationSettings>()!.Quality;
        if (renderWorld.TryGet<WindowDepth>() is { } depth && renderWorld.TryGet<WindowView>() is { } window
            && renderWorld.TryGet<SwapchainTarget>() is { } swapchain && Matrix4x4.Invert(window.ViewProjection, out var inverse))
        {
            var (width, height, tile) = (swapchain.Extent.Width, swapchain.Extent.Height, TileAt(quality));
            if (_screen is null || _screen.Tile != tile || _screen.Across != (width + tile - 1) / tile || _screen.Down != (height + tile - 1) / tile)
            {
                if (_screen is not null) _retired.Add((_frame, _screen));
                _screen = device.CreateScreenProbes(width, height, tile);
                _lastScreen = null;
            }
            var bytes = ViewBytes(window.ViewProjection, inverse, window.Eye, _screen, depth.Extent, swapchain.Extent, _lastScreen);
            _retired.Add((_frame, device.RecordScreenProbes(renderContext.CommandBuffer, _gi, _screen, field.Field, depth.View, depth.Sampler, bytes)));
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
        // skinned mesh, whose posed shape no vertices on the CPU hold, left out.
        if (quality == GlobalIllumination.High && device.CanQueryRays && renderWorld.TryGet<ModelRenderer>() is { TracesRays: true }
            && !renderWorld.TryGet<GlobalIlluminationSettings>()!.RaysOff
            && renderWorld.TryGet<SceneFieldRenderer>() is { } fields)
        {
            _rays ??= device.CreateRayScene();
            var copies = new List<RayInstance>(fields.Drawn.Count);
            foreach (var (instance, skinned) in fields.Drawn)
                if (!skinned) copies.Add(new RayInstance(instance.Vertices, instance.World, instance.Color, instance.Emission, instance.Roughness, instance.Metallic));
            _retired.Add((_frame, device.RecordRayScene(renderContext.CommandBuffer, _rays, copies, mesh => fields.CornersOf((ModelVertex[])mesh))));
        }
        else if (_rays is not null)
        {
            _retired.Add((_frame, _rays));
            _rays = null;
        }
        renderWorld.Set(new IlluminationBinding(_gi, field.Field, _screen, renderWorld.TryGet<WindowDepth>(), _history, _rays));
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
        var tile = TileAt(settings.Quality);
        if (!_targets.TryGetValue(id, out var state) || state.Screen.Tile != tile
            || state.Screen.Across != (size.Width + tile - 1) / tile || state.Screen.Down != (size.Height + tile - 1) / tile)
        {
            if (state is not null) _retired.Add((_frame, state));
            state = _targets[id] = new TargetScreen(device.CreateScreenProbes(size.Width, size.Height, tile));
        }
        state.Drawn = _frame;
        var eye = Vector4.Transform(new Vector4(0, 0, 0, 1), inverse);
        var at = new Vector3(eye.X, eye.Y, eye.Z) / eye.W;
        var bytes = ViewBytes(camera, inverse, at, state.Screen, depth.Extent, size, state.Last);
        _retired.Add((_frame, device.RecordScreenProbes(renderContext.CommandBuffer, _gi, state.Screen, field.Field, depth.View, depth.Sampler, bytes)));
        state.Last = (camera, at);
        var targets = renderWorld.TryGet<TargetIllumination>();
        if (targets is null) renderWorld.Set(targets = new TargetIllumination());
        targets.ByTarget[id] = new IlluminationBinding(_gi, field.Field, state.Screen, depth);
    }

    // A view's buffer for the screen's probes, as gi_screen.slang reads it: its camera both ways
    // and its eye, the probes' tile and how many lie across and down, the first interval's half, the
    // world's probes, their spacing and cascades, the depth's size and the view's, and its camera
    // and eye the frame before where the probes were placed then. The matrices lie as they do in
    // memory, which the shaders, reading them column-major, multiply a point by as the CPU does.
    private byte[] ViewBytes(Matrix4x4 viewProjection, Matrix4x4 inverse, Vector3 eye, GpuScreenProbes screen, Extent2D depth, Extent2D size,
        (Matrix4x4 ViewProjection, Vector3 Eye)? last)
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
        if (last is { } then)
        {
            MemoryMarshal.Write(bytes.AsSpan(192), then.ViewProjection);
            (floats[64], floats[65], floats[66], floats[67]) = (then.Eye.X, then.Eye.Y, then.Eye.Z, 1);
        }
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

    // The lights as gi.slang's GiLights: the sun, the first directional light, the shadowed one
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
            sky = new Vector3(map.Intensity);
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

/// <summary>Render graph node that traces the light that bounces, after the scene's distance field is built and before the passes that light the window.</summary>
internal sealed class GlobalIlluminationNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<GlobalIlluminationRenderer>()?.Draw(renderContext, renderWorld);
}
