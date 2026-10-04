using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Per-frame upload of <see cref="RenderLights"/> into a transient uniform buffer
/// allocated through <see cref="DynamicBufferAllocator"/>. Stores the resulting
/// <see cref="UniformBufferBinding"/> as <see cref="FrameLightingBinding"/> on the
/// render world so downstream nodes can write it into the lighting descriptor set
/// of the mesh pipeline.
/// </summary>
public sealed class LightingUboPrepare : IPrepareSystem
{
    private static readonly ILogger Logger = Log.Category("Engine.Lighting");

    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        var lights = renderWorld.TryGet<RenderLights>();
        var allocator = renderContext.DynamicAllocator;
        if (allocator is null)
            return;

        // Always upload (even with zero lights) so the shader can rely on the binding
        // existing, since the count is what the shader iterates against.
        var ubo = LightingUboPacker.Pack(lights?.All ?? (IReadOnlyList<RenderLight>)System.Array.Empty<RenderLight>());
        var environment = renderWorld.TryGet<EnvironmentMap>();
        if (environment is not null)
        {
            ubo.Environment = new System.Numerics.Vector4(environment.Intensity, environment.MipLevels - 1, 1, 0);
            for (int i = 0; i < 9; i++) ubo.EnvironmentIrradiance[i] = new System.Numerics.Vector4(environment.Irradiance[i], 0);
        }

        var draws = renderWorld.TryGet<ModelDrawList>();
        var first = draws?.WindowViewProjection ?? (draws?.Targets() is [var t, ..] ? draws.ViewProjectionOf(t) : null);
        var eye = first is { } camera0 ? EyeOf(camera0) : null;
        BindProbes(renderWorld, ref ubo, eye);

        // The lights without their shadows, which each view's buffer starts from.
        var unshadowed = ubo;
        var casters = Casters(renderWorld, lights, ubo.LightCount, eye);
        var shadow = casters is not null && draws?.WindowViewProjection is { } window ? casters.For(window) : null;
        if (shadow is null) renderWorld.Remove<FrameShadow>();
        else
        {
            renderWorld.Set(shadow);
            Apply(ref ubo, shadow);
        }
        // With bloom on the window's view is drawn into the HDR frame and leaves its light linear.
        var windowUbo = ubo;
        if (BloomRenderer.IsOn(renderWorld)) windowUbo.Output.X = 1;
        var binding = Upload(allocator, in windowUbo);
        renderWorld.Set(new FrameLightingBinding(binding, ubo.LightCount, environment is not null, windowUbo.Output.X > 0));

        // The window's light at the exposure a reflection probe's faces are drawn at.
        var capture = ubo;
        capture.Environment.W = ReflectionProbes.CaptureExposure;
        renderWorld.TryGet<BoundProbes>()!.CaptureBinding = Upload(allocator, in capture);

        // A render target drawing meshes through a camera of its own has its cascades fitted to
        // that camera, in a buffer of its own, since the window's would leave whatever it looks
        // at past them unshadowed, or all of it when nothing is drawn into the window.
        var targets = renderWorld.TryGet<TargetShadows>();
        targets?.ByTarget.Clear();
        if (draws is not null)
            foreach (var target in draws.Targets())
            {
                if (targets is null) renderWorld.Set(targets = new TargetShadows());
                var own = casters is not null && draws.ViewProjectionOf(target) is { } camera ? casters.For(camera) : null;
                var targetUbo = unshadowed;
                if (own is not null) Apply(ref targetUbo, own);
                targets.ByTarget[target] = (own, Upload(allocator, in targetUbo));
            }

        Logger.FrameTrace($"LightingUboPrepare: uploaded {ubo.LightCount} light(s) into a {LightingUboPacker.SizeBytes}-byte UBO.");
    }

    // The probes with a capture to reflect, the four whose boxes come nearest the eye, written into
    // the buffer by slot and named for the model pass, which binds each one's cube at its slot.
    private static void BindProbes(RenderWorld renderWorld, ref LightingUbo ubo, System.Numerics.Vector3? eye)
    {
        var bound = renderWorld.TryGet<BoundProbes>() ?? new BoundProbes();
        renderWorld.Set(bound);
        bound.Slots.Clear();
        if (renderWorld.TryGet<ReflectionProbes>() is not { } probes) return;

        float Away(ReflectionProbes.Probe p) => eye is { } at
            ? System.Numerics.Vector3.Distance(System.Numerics.Vector3.Clamp(at, p.Position - p.HalfSize, p.Position + p.HalfSize), at)
            : 0;
        bound.Slots.AddRange(probes.ByEntity.Values.Where(p => p.Map is not null).OrderBy(Away).Take(LightingUboPacker.MaxProbes));
        ubo.ProbeCount = new System.Numerics.Vector4(bound.Slots.Count, 0, 0, 0);
        for (int i = 0; i < bound.Slots.Count; i++)
        {
            var probe = bound.Slots[i];
            var map = probe.Map!;
            ref var entry = ref ubo.Probes[i];
            entry.CenterAndIntensity = new System.Numerics.Vector4(probe.Captured?.Position ?? probe.Position, probe.Intensity);
            entry.HalfSizeAndMip = new System.Numerics.Vector4(probe.HalfSize, map.MipLevels - 1);
            for (int c = 0; c < 9; c++) entry.Irradiance[c] = new System.Numerics.Vector4(map.Irradiance[c], 0);
        }
    }

    private static UniformBufferBinding Upload(DynamicBufferAllocator allocator, in LightingUbo ubo)
    {
        var sizeBytes = (ulong)LightingUboPacker.SizeBytes;
        var alloc = allocator.Allocate(sizeBytes, BufferUsage.Uniform);
        var span = allocator.Map(alloc);
        MemoryMarshal.Write(span, in ubo);
        allocator.Unmap(alloc);
        return new UniformBufferBinding(alloc.Buffer, Binding: 0, alloc.Offset, sizeBytes);
    }

    // Writes a view's shadow into its lighting buffer.
    private static void Apply(ref LightingUbo ubo, FrameShadow shadow)
    {
        ubo.ShadowLight = shadow.Light;
        ubo.CascadeCount = shadow.Cascades.Count;
        Span<float> texels = stackalloc float[LightingUboPacker.MaxCascades];
        for (int i = 0; i < shadow.Cascades.Count; i++)
        {
            ubo.ShadowCascades[i] = shadow.Cascades[i].ViewProjection;
            texels[i] = shadow.Cascades[i].Texel;
        }
        ubo.ShadowTexels = new System.Numerics.Vector4(texels[0], texels[1], texels[2], 0);

        // A shadowed spot light names its slot, counted from one, in its cone's third
        // component, as a point light does.
        var spots = shadow.SpotLights ?? [];
        ubo.SpotShadowCount = spots.Count;
        Span<float> spotTexels = stackalloc float[ShadowFit.MaxSpotLights];
        for (int s = 0; s < spots.Count; s++)
        {
            ubo.Lights[spots[s].Light].Cone.Z = s + 1;
            ubo.SpotShadows[s] = spots[s].ViewProjection;
            spotTexels[s] = spots[s].TexelPerUnit;
        }
        ubo.SpotShadowTexels = new System.Numerics.Vector4(spotTexels[0], spotTexels[1], spotTexels[2], spotTexels[3]);

        // Each shadowed point light names its slot in the face array, counted from one in its
        // cone's third component, which a light with no shadow leaves at zero.
        var points = shadow.PointLights ?? [];
        for (int p = 0; p < points.Count; p++)
        {
            ubo.Lights[points[p].Light].Cone.Z = p + 1;
            for (int f = 0; f < 6; f++) ubo.PointShadowFaces[p * 6 + f] = points[p].Faces[f];
        }
        ubo.PointShadow = new System.Numerics.Vector4(2 * ShadowFit.PointFaceSlack / shadow.PointFaceSize, points.Count, 0, 0);
    }

    // The lights that cast shadows this frame: the first directional one, and the first spot and
    // point lights, the spots fitted already, since only the cascades depend on a camera.
    private sealed record ShadowCasters(System.Numerics.Vector3? Sun, int SunIndex, float Distance, int TileSize,
        List<(int, System.Numerics.Matrix4x4, float)> Spots, List<(int, System.Numerics.Matrix4x4[])> Points)
    {
        // The frame's shadow as a view through camera sees it, or null when nothing casts one.
        public FrameShadow? For(System.Numerics.Matrix4x4 camera)
        {
            (System.Numerics.Matrix4x4, float)[] cascades = Sun is { } direction ? ShadowFit.FitCascades(camera, direction, Distance, TileSize) : [];
            var sun = cascades.Length == 0 ? -1 : SunIndex;
            return sun < 0 && Spots.Count == 0 && Points.Count == 0 ? null : new FrameShadow(sun, cascades, Spots, Points, TileSize, TileSize / 4);
        }
    }

    // Where a camera is, near enough, the middle of its near plane.
    private static System.Numerics.Vector3? EyeOf(System.Numerics.Matrix4x4 viewProjection)
    {
        if (!System.Numerics.Matrix4x4.Invert(viewProjection, out var inverse)) return null;
        var near = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(0, 0, 0, 1), inverse);
        return near.W == 0 ? null : new System.Numerics.Vector3(near.X, near.Y, near.Z) / near.W;
    }

    // The lights that cast shadows, or null when none does or no mesh is drawn. Past the four
    // spot and four point lights there is room for, the ones whose reach comes nearest the eye
    // are taken, so a level of many torches shadows those around the camera.
    private static ShadowCasters? Casters(RenderWorld renderWorld, RenderLights? lights, int count, System.Numerics.Vector3? eye)
    {
        if (lights is null || renderWorld.TryGet<ModelDrawList>() is not { IsEmpty: false }) return null;

        var settings = renderWorld.TryGet<ShadowSettings>();
        var distance = settings?.Distance is > 0 and var d ? d : ShadowFit.Distance;
        var tileSize = settings?.TileSize is >= 64 and var t ? t : ShadowFit.TileSize;
        int sun = -1;
        var spotCandidates = new List<int>();
        var pointCandidates = new List<int>();
        for (int i = 0; i < count; i++)
        {
            var light = lights.All[i];
            if (!light.CastsShadows) continue;
            if (sun < 0 && light.Kind == LightKind.Directional) sun = i;
            if (light.Kind == LightKind.Spot) spotCandidates.Add(i);
            if (light.Kind == LightKind.Point) pointCandidates.Add(i);
        }

        // How far a light's reach is from the eye, 0 inside it, and the distance itself for a light
        // with no range. A stable sort keeps the order lights were made in among equals.
        float Reach(int i)
        {
            var light = lights.All[i];
            if (eye is not { } at) return 0;
            var away = System.Numerics.Vector3.Distance(light.Position, at);
            return light.Range > 0 ? MathF.Max(0, away - light.Range) : away;
        }
        var spotLights = spotCandidates.OrderBy(Reach).Take(ShadowFit.MaxSpotLights).ToList();
        var points = pointCandidates.OrderBy(Reach).Take(ShadowFit.MaxPointLights)
            .Select(i => (i, ShadowFit.FitPoint(lights.All[i].Position, lights.All[i].Range, distance))).ToList();
        if (sun < 0 && spotLights.Count == 0 && points.Count == 0) return null;

        // The spot lights share the spot tile, so each one's texels are as wide as its share of it.
        var spots = new List<(int, System.Numerics.Matrix4x4, float)>();
        var size = ShadowFit.SpotTileArea(0, spotLights.Count, tileSize).Size;
        foreach (var i in spotLights)
        {
            var light = lights.All[i];
            if (ShadowFit.TryFitSpot(light.Position, light.Direction, light.CosOuter, light.Range, out var viewProjection, out var texel, distance, size))
                spots.Add((i, viewProjection, texel));
        }
        return new ShadowCasters(sun >= 0 ? lights.All[sun].Direction : null, sun, distance, tileSize, spots, points);
    }
}

/// <summary>The reflection probes bound this frame, by slot, which the model pass binds the cubes of.</summary>
internal sealed class BoundProbes
{
    /// <summary>Each bound probe, by the slot its cube is bound at.</summary>
    public readonly List<ReflectionProbes.Probe> Slots = [];

    /// <summary>The window's lighting buffer at <see cref="ReflectionProbes.CaptureExposure"/>, which a probe's faces are drawn with.</summary>
    public UniformBufferBinding? CaptureBinding;
}

/// <summary>
/// The shadow of each render target that draws meshes this frame, published by
/// <see cref="LightingUboPrepare"/>: its cascades fitted to the target's own camera, or null when
/// nothing casts one, and the lighting buffer it is drawn with.
/// </summary>
/// <remarks>
/// The spot and point lights are the same for every view. The window's shadow is
/// <see cref="FrameShadow"/>, and its buffer <see cref="FrameLightingBinding"/>.
/// </remarks>
internal sealed class TargetShadows
{
    /// <summary>Each target's shadow and lighting buffer, by its texture id.</summary>
    public readonly Dictionary<int, (FrameShadow? Shadow, UniformBufferBinding Binding)> ByTarget = [];
}

/// <summary>
/// Render-world resource published by <see cref="LightingUboPrepare"/>: the
/// <see cref="UniformBufferBinding"/> for this frame's lighting UBO and the count
/// of valid entries inside it. Renamed each frame; consumers should read it
/// transiently rather than caching.
/// </summary>
/// <param name="Binding">Buffer binding suitable for <see cref="IGraphicsDevice.UpdateDescriptorSet(IDescriptorSet, in UniformBufferBinding?, in CombinedImageSamplerBinding?)"/>.</param>
/// <param name="LightCount">Number of valid <see cref="LightUboEntry"/> entries in the buffer.</param>
/// <param name="HasEnvironment">Whether an <see cref="EnvironmentMap"/> lights the frame.</param>
/// <param name="Linear">Whether the window's view is drawn into the HDR frame, which a frame with no light still binds the buffer for, to read its output flag.</param>
public sealed record FrameLightingBinding(UniformBufferBinding Binding, int LightCount, bool HasEnvironment = false, bool Linear = false);

