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
/// probes are apart to twice that, the first cascade's from the probe itself, and each cascade has
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

        // Each cascade's interval, from its probes' spacing to twice that, the first's from the probe.
        var intervals = new (float Start, float End)[cascades];
        for (int c = 0; c < cascades; c++)
        {
            var spacing = ProbeSpacing * fieldSettings.CellSize * (1 << c);
            intervals[c] = (c == 0 ? 0 : spacing, 2 * spacing);
        }

        var environment = renderWorld.TryGet<EnvironmentMap>() is not null ? renderWorld.TryGet<ModelRenderer>()?.Environment : null;
        _black ??= device.CreateCubeMap(1, 1, new Half[6 * 4]);
        var (view, sampler) = environment is not null ? (environment.View, environment.Sampler) : (_black.View, _black.Sampler);
        var lights = Lights(renderWorld, environment is not null);
        _retired.Add((_frame, device.RecordGlobalIllumination(renderContext.CommandBuffer, _gi, field.Field, view, sampler, lights, intervals, ProbeSpacing)));

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
            var bytes = new byte[GpuScreenProbes.ViewBytes];
            // The matrices as they lie in memory, which the shaders, reading them column-major,
            // multiply a point by as the CPU does.
            MemoryMarshal.Write(bytes, window.ViewProjection);
            MemoryMarshal.Write(bytes.AsSpan(64), inverse);
            var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
            (floats[32], floats[33], floats[34]) = (window.Eye.X, window.Eye.Y, window.Eye.Z);
            (floats[36], floats[37], floats[38]) = (tile, _screen.Across, _screen.Down);
            (floats[40], floats[41], floats[42], floats[43]) = (intervals[0].End / 2, _gi.Probes, ProbeSpacing, cascades);
            (floats[44], floats[45], floats[46], floats[47]) = (depth.Extent.Width, depth.Extent.Height, width, height);
            if (_lastScreen is { } last)
            {
                MemoryMarshal.Write(bytes.AsSpan(192), last.ViewProjection);
                (floats[64], floats[65], floats[66], floats[67]) = (last.Eye.X, last.Eye.Y, last.Eye.Z, 1);
            }
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
                if (!skinned) copies.Add(new RayInstance(instance.Vertices, instance.World, instance.Color, instance.Emission));
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
        (floats[12], floats[13], floats[14]) = (count, _frame, 1);
        return bytes;
    }

    // Lets the probes go, once no frame in flight reads them.
    private void Release()
    {
        if (_gi is not null) _retired.Add((_frame, _gi));
        if (_screen is not null) _retired.Add((_frame, _screen));
        if (_history is not null) _retired.Add((_frame, _history));
        if (_rays is not null) _retired.Add((_frame, _rays));
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
