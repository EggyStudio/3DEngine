using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Per-frame upload of <see cref="RenderLights"/> into a transient uniform buffer
/// allocated through <see cref="DynamicBufferAllocator"/>. Stores the resulting
/// <see cref="UniformBufferBinding"/> as <see cref="FrameLightingBinding"/> on the
/// render world so downstream nodes can write it into the lighting descriptor set
/// of the mesh pipeline.
/// </summary>
internal sealed class LightingUboPrepare : IPrepareSystem
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
        // existing, since the shader iterates against the count.
        var ubo = LightingUboPacker.Pack(lights?.All ?? (IReadOnlyList<RenderLight>)System.Array.Empty<RenderLight>());
        var environment = renderWorld.TryGet<EnvironmentMap>();
        if (environment is not null)
            ubo.Environment = new System.Numerics.Vector4(environment.Intensity, environment.MipLevels - 1, 1, 0);

        var draws = renderWorld.TryGet<ModelDrawList>();
        var first = draws?.WindowViewProjection ?? (draws?.Targets() is [var t, ..] ? draws.ViewProjectionOf(t) : null);
        var eye = first is { } camera0 ? EyeOf(camera0) : null;
        BindProbes(renderWorld, ref ubo, eye);

        // The lights without their shadows, which each view's buffer starts from. The spot and
        // point lights that cast them are ranked for every view the frame draws meshes through,
        // the window's and each render target's, since their maps serve all of them.
        var unshadowed = ubo;
        var views = new List<(System.Numerics.Vector3? Eye, System.Numerics.Matrix4x4? Camera)>();
        if (draws?.WindowViewProjection is { } windowCamera) views.Add((EyeOf(windowCamera), windowCamera));
        foreach (var target in draws?.Targets() ?? [])
            if (draws!.ViewProjectionOf(target) is { } targetCamera) views.Add((EyeOf(targetCamera), targetCamera));
        var casters = Casters(renderWorld, lights, ubo.LightCount, views);
        var shadow = casters is not null && draws?.WindowViewProjection is { } window ? casters.For(window) : null;
        if (shadow is null) renderWorld.Remove<FrameShadow>();
        else
        {
            renderWorld.Set(shadow);
            Apply(ref ubo, shadow);
        }
        // The window's view is drawn into the HDR frame, which holds its light encoded on past 1 for
        // the pass after the scene to bend.
        var windowUbo = ubo;
        windowUbo.Output.Y = 1;
        // Only the window's view has an occlusion of its own, which it is darkened by.
        if (renderWorld.TryGet<AmbientOcclusionSettings>() is { On: true }) windowUbo.AmbientOcclusion.X = 1;
        // And the sun's contact shadows through the scene's distance field, which that pass traces.
        if (AmbientOcclusionRenderer.ContactShadows(renderWorld) is not null) windowUbo.AmbientOcclusion.Y = 1;
        // And its light from all around the light that bounced, which the probes hold.
        if (GlobalIlluminationRenderer.CascadesIn(renderWorld) is > 0 and var cascades)
        {
            windowUbo.Indirect = new System.Numerics.Vector4(1, GlobalIlluminationRenderer.ProbeSpacing,
                SceneFieldPlan.Resolution / GlobalIlluminationRenderer.ProbeSpacing, cascades);
            var quality = renderWorld.TryGet<GlobalIlluminationSettings>()!.Quality;
            windowUbo.Screen = new System.Numerics.Vector4(GlobalIlluminationRenderer.TileAt(quality), 1, 0, 0);
            // And a glossy surface's reflection traced through its depth and the field, looked up
            // in the frame before's picture where the renderer kept it.
            if (renderWorld.TryGet<WindowView>() is { } view && System.Numerics.Matrix4x4.Invert(view.ViewProjection, out var inverse)
                && renderWorld.TryGet<GlobalIlluminationRenderer>() is { } gi)
            {
                var (steps, reach) = GlobalIlluminationRenderer.ReflectionStepsAt(quality);
                windowUbo.ReflectViewProjection = view.ViewProjection;
                windowUbo.ReflectInverseViewProjection = inverse;
                windowUbo.ReflectLastViewProjection = gi.HistoryViewProjection ?? view.ViewProjection;
                windowUbo.ReflectLastInverseViewProjection = System.Numerics.Matrix4x4.Invert(windowUbo.ReflectLastViewProjection, out var lastInverse)
                    ? lastInverse : inverse;
                windowUbo.Reflection = new System.Numerics.Vector4(1, GlobalIlluminationRenderer.GlossyRoughness, steps, gi.HistoryViewProjection is null ? 0 : 1);
                windowUbo.ReflectionReach = new System.Numerics.Vector4(reach, gi.Rays is null ? 0 : 1, 0, 0);
            }
        }
        var binding = Upload(allocator, in windowUbo);
        renderWorld.Set(new FrameLightingBinding(binding, ubo.LightCount, environment is not null));

        // The window's light left linear, as a reflection probe's half-float faces hold it. A probe
        // whose map is of an earlier placement or of lights since changed gives no light to a
        // capture, so a lamp switched off does not go on lighting its room through the room's own
        // reflection, and a probe's second pass bounces the light of its first.
        var capture = ubo;
        capture.Output.X = 1;
        // A probe's faces take the light that bounced from the world's probes, as a render target does.
        capture.Indirect = windowUbo.Indirect;
        if (renderWorld.TryGet<BoundProbes>() is { } boundForCapture)
            for (int i = 0; i < boundForCapture.Slots.Count; i++)
                if (boundForCapture.Slots[i].Captured != boundForCapture.Slots[i].Wanted) capture.Probes[i].CenterAndIntensity.W = 0;
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
                // And the light that bounced, read from screen probes of the target's own where it
                // draws through a camera of its own, as the window's, and its glossy surfaces
                // reflecting the probes and the environment alone, since a reflection is traced
                // through the window's depth.
                targetUbo.Indirect = windowUbo.Indirect;
                if (windowUbo.Indirect.X > 0 && draws.ViewProjectionOf(target) is not null)
                    targetUbo.Screen = windowUbo.Screen with { Y = 1 };
                targets.ByTarget[target] = (own, Upload(allocator, in targetUbo));
            }

        Logger.FrameTrace($"LightingUboPrepare: uploaded {ubo.LightCount} light(s) into a {LightingUboPacker.SizeBytes}-byte UBO.");
    }

    // The probes with a capture to reflect, the four whose boxes come nearest the eye, written into
    // the buffer by slot and named for the model pass, which binds each one's cube and irradiance at
    // its slot.
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
        for (int s = 0; s < spots.Count; s++)
        {
            ubo.Lights[spots[s].Light].Cone.Z = s + 1;
            ubo.SpotShadows[s] = spots[s].ViewProjection;
            ref var four = ref ubo.SpotShadowTexels[s / 4];
            switch (s % 4)
            {
                case 0: four.X = spots[s].TexelPerUnit; break;
                case 1: four.Y = spots[s].TexelPerUnit; break;
                case 2: four.Z = spots[s].TexelPerUnit; break;
                default: four.W = spots[s].TexelPerUnit; break;
            }
        }

        // Each shadowed point light names its slot in the face array, counted from one in its
        // cone's third component, which a light with no shadow leaves at zero.
        var points = shadow.PointLights ?? [];
        for (int p = 0; p < points.Count; p++)
        {
            ubo.Lights[points[p].Light].Cone.Z = p + 1;
            for (int f = 0; f < 6; f++) ubo.PointShadowFaces[p * 6 + f] = points[p].Faces[f];
        }
        ubo.PointShadow = new System.Numerics.Vector4(2 * ShadowFit.PointFaceSlack / shadow.PointFaceSize, points.Count, ShadowFit.FullPointLights, 0);
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

    // Whether a sphere is at least partly inside the view, by the six planes of its view and
    // projection, depth running 0 to 1 as Vulkan's does.
    internal static bool InView(System.Numerics.Matrix4x4 m, System.Numerics.Vector3 center, float radius)
    {
        ReadOnlySpan<System.Numerics.Vector4> planes =
        [
            new(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41),
            new(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41),
            new(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42),
            new(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42),
            new(m.M13, m.M23, m.M33, m.M43),
            new(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43),
        ];
        foreach (var plane in planes)
        {
            var normal = new System.Numerics.Vector3(plane.X, plane.Y, plane.Z);
            var length = normal.Length();
            if (length > 0 && (System.Numerics.Vector3.Dot(normal, center) + plane.W) / length < -radius) return false;
        }
        return true;
    }

    // Where a camera is, near enough, the middle of its near plane.
    internal static System.Numerics.Vector3? EyeOf(System.Numerics.Matrix4x4 viewProjection)
    {
        if (!System.Numerics.Matrix4x4.Invert(viewProjection, out var inverse)) return null;
        var near = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(0, 0, 0, 1), inverse);
        return near.W == 0 ? null : new System.Numerics.Vector3(near.X, near.Y, near.Z) / near.W;
    }

    // The lights that cast shadows, or null when none does or no mesh is drawn. Past the spot and
    // point lights there is room for, those that matter most to the views are taken and ranked
    // (Rank), so a level of many torches shadows those in front of a camera, the brightest there
    // with the most texels.
    private static ShadowCasters? Casters(RenderWorld renderWorld, RenderLights? lights, int count,
        IReadOnlyList<(System.Numerics.Vector3? Eye, System.Numerics.Matrix4x4? Camera)> views)
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

        var spotLights = Rank(lights.All, spotCandidates, views, distance).Take(ShadowFit.MaxSpotLights).ToList();
        var points = Rank(lights.All, pointCandidates, views, distance).Take(ShadowFit.MaxPointLights)
            .Select(i => (i, ShadowFit.FitPoint(lights.All[i].Position, lights.All[i].Range, distance))).ToList();
        if (sun < 0 && spotLights.Count == 0 && points.Count == 0) return null;

        // The spot lights share the spot tile by rank, so each one's texels are as wide as its share of it.
        var spots = new List<(int, System.Numerics.Matrix4x4, float)>();
        foreach (var i in spotLights)
        {
            var light = lights.All[i];
            var size = ShadowFit.SpotTileArea(spots.Count, spotLights.Count, tileSize).Size;
            if (ShadowFit.TryFitSpot(light.Position, light.Direction, light.CosOuter, light.Range, out var viewProjection, out var texel, distance, size))
                spots.Add((i, viewProjection, texel));
        }
        return new ShadowCasters(sun >= 0 ? lights.All[sun].Direction : null, sun, distance, tileSize, spots, points);
    }

    /// <summary>
    /// The candidates in the order they matter to the view, best first: a light whose reach the
    /// camera sees before one behind it, then the one whose light reaching the eye is greatest,
    /// its brightness over one plus the square of how far its reach is from the eye, then the one
    /// whose reach comes nearest.
    /// </summary>
    /// <remarks>
    /// A dim candle by the camera thus gives way to a lamp of forty times its light a few units
    /// on, where reach alone gave the candle the slot. A stable sort keeps the order lights were
    /// made in among equals.
    /// </remarks>
    internal static IEnumerable<int> Rank(IReadOnlyList<RenderLight> lights, IEnumerable<int> candidates, System.Numerics.Vector3? eye,
        System.Numerics.Matrix4x4? camera, float distance) =>
        Rank(lights, candidates, [(eye, camera)], distance);

    /// <summary>
    /// The candidates in the order they matter to any of <paramref name="views"/>, best first: a
    /// light some camera sees before one none does, then the one whose light reaching an eye,
    /// weighed by the share of that view's picture its reach covers, is greatest, then the one
    /// whose reach comes nearest an eye, so a render target looking where the window does not has
    /// the lights it sees shadowed too.
    /// </summary>
    /// <remarks>
    /// The share weighs a lamp lighting a wall across the view above a brighter one lighting a
    /// corner of it, so the light that lights most of the picture keeps its shadows (<see cref="Share"/>).
    /// </remarks>
    internal static IEnumerable<int> Rank(IReadOnlyList<RenderLight> lights, IEnumerable<int> candidates,
        IReadOnlyList<(System.Numerics.Vector3? Eye, System.Numerics.Matrix4x4? Camera)> views, float distance)
    {
        // How far a light's reach is from an eye, 0 inside it, and the distance itself for a light
        // with no range, the nearest of the views' eyes.
        float ReachFrom(int i, System.Numerics.Vector3? eye)
        {
            var light = lights[i];
            if (eye is not { } at) return 0;
            var away = System.Numerics.Vector3.Distance(light.Position, at);
            return light.Range > 0 ? MathF.Max(0, away - light.Range) : away;
        }
        float Reach(int i) => views.Count == 0 ? 0 : views.Min(view => ReachFrom(i, view.Eye));
        // The light reaching a view's eye, its brightness over one plus the square of how far its
        // reach is from the eye, times the share of the view's picture its reach covers, the most
        // of any view.
        float Reaching(int i)
        {
            var light = lights[i];
            var brightness = 0.2126f * light.EmittedColor.X + 0.7152f * light.EmittedColor.Y + 0.0722f * light.EmittedColor.Z;
            if (views.Count == 0) return brightness;
            var radius = light.Range > 0 ? light.Range : distance;
            return views.Max(view =>
            {
                var reach = ReachFrom(i, view.Eye);
                return brightness / (1 + reach * reach) * (view.Camera is { } camera ? Share(camera, light.Position, radius) : 1);
            });
        }
        bool Seen(int i) => views.Count == 0 || views.Any(view => view.Camera is not { } camera
            || InView(camera, lights[i].Position, lights[i].Range > 0 ? lights[i].Range : distance));
        return candidates.OrderBy(i => Seen(i) ? 0 : 1).ThenByDescending(Reaching).ThenBy(Reach);
    }

    /// <summary>
    /// The share of a view's picture, 0 to 1, that a light's reach of <paramref name="radius"/>
    /// around <paramref name="center"/> covers: the box around it put through
    /// <paramref name="camera"/> and held within the picture, the whole picture where the box
    /// reaches round past the eye.
    /// </summary>
    internal static float Share(System.Numerics.Matrix4x4 camera, System.Numerics.Vector3 center, float radius)
    {
        var low = new System.Numerics.Vector2(float.MaxValue);
        var high = new System.Numerics.Vector2(float.MinValue);
        for (int corner = 0; corner < 8; corner++)
        {
            var at = center + radius * new System.Numerics.Vector3((corner & 1) * 2 - 1, (corner >> 1 & 1) * 2 - 1, (corner >> 2) * 2 - 1);
            var clip = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(at, 1), camera);
            if (clip.W <= 1e-4f) return 1;
            var shown = new System.Numerics.Vector2(clip.X, clip.Y) / clip.W;
            (low, high) = (System.Numerics.Vector2.Min(low, shown), System.Numerics.Vector2.Max(high, shown));
        }
        var (from, to) = (System.Numerics.Vector2.Clamp(low, -System.Numerics.Vector2.One, System.Numerics.Vector2.One),
            System.Numerics.Vector2.Clamp(high, -System.Numerics.Vector2.One, System.Numerics.Vector2.One));
        return MathF.Max(0, to.X - from.X) * MathF.Max(0, to.Y - from.Y) / 4;
    }
}

/// <summary>The reflection probes bound this frame, by slot, which the model pass binds the cubes of.</summary>
internal sealed class BoundProbes
{
    /// <summary>Each bound probe, by the slot its cube is bound at.</summary>
    public readonly List<ReflectionProbes.Probe> Slots = [];

    /// <summary>The window's lighting buffer with its light left linear, which a probe's faces are drawn with.</summary>
    public UniformBufferBinding? CaptureBinding;
}

/// <summary>
/// The shadow of each render target that draws meshes this frame, published by
/// <see cref="LightingUboPrepare"/>: its cascades fitted to the target's own camera, or null when
/// nothing casts one, and the lighting buffer it is drawn with.
/// </summary>
/// <remarks>
/// The spot and point lights are the same for every view, ranked for all of them together. The
/// window's shadow is <see cref="FrameShadow"/>, and its buffer <see cref="FrameLightingBinding"/>.
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
internal sealed record FrameLightingBinding(UniformBufferBinding Binding, int LightCount, bool HasEnvironment = false);

