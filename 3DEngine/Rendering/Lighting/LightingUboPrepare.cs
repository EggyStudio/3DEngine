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
        // existing - the count is what the shader iterates against.
        var ubo = LightingUboPacker.Pack(lights?.All ?? (IReadOnlyList<RenderLight>)System.Array.Empty<RenderLight>());
        var shadow = Shadow(renderWorld, lights, ubo.LightCount);
        if (shadow is null) renderWorld.Remove<FrameShadow>();
        else
        {
            renderWorld.Set(shadow);
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
            ubo.PointShadow = new System.Numerics.Vector4(2 * ShadowFit.PointFaceSlack / ShadowFit.PointFaceSize, points.Count, 0, 0);
        }

        var environment = renderWorld.TryGet<EnvironmentMap>();
        if (environment is not null)
            ubo.Environment = new System.Numerics.Vector4(environment.Intensity, environment.MipLevels - 1, 1, 0);

        var sizeBytes = (ulong)LightingUboPacker.SizeBytes;

        var alloc = allocator.Allocate(sizeBytes, BufferUsage.Uniform);
        var span = allocator.Map(alloc);
        MemoryMarshal.Write(span, in ubo);
        allocator.Unmap(alloc);

        renderWorld.Set(new FrameLightingBinding(
            new UniformBufferBinding(alloc.Buffer, Binding: 0, alloc.Offset, sizeBytes),
            ubo.LightCount,
            environment is not null));

        Logger.FrameTrace($"LightingUboPrepare: uploaded {ubo.LightCount} light(s) into a {sizeBytes}-byte UBO.");
    }

    // The first directional light that casts shadows, its cascades fitted to the camera of the
    // first mesh drawn into the window, and the first spot light that does, or null when there is
    // neither or no mesh.
    private static FrameShadow? Shadow(RenderWorld renderWorld, RenderLights? lights, int count)
    {
        if (lights is null || renderWorld.TryGet<ModelDrawList>() is not { } draws) return null;

        var distance = renderWorld.TryGet<ShadowSettings>()?.Distance is > 0 and var d ? d : ShadowFit.Distance;
        int sun = -1;
        var spotLights = new List<int>();
        var points = new List<(int, System.Numerics.Matrix4x4[])>();
        for (int i = 0; i < count; i++)
        {
            var light = lights.All[i];
            if (!light.CastsShadows) continue;
            if (sun < 0 && light.Kind == LightKind.Directional) sun = i;
            if (spotLights.Count < ShadowFit.MaxSpotLights && light.Kind == LightKind.Spot) spotLights.Add(i);
            if (points.Count < ShadowFit.MaxPointLights && light.Kind == LightKind.Point)
                points.Add((i, ShadowFit.FitPoint(light.Position, light.Range, distance)));
        }
        if (sun < 0 && spotLights.Count == 0 && points.Count == 0) return null;

        if (draws.WindowViewProjection is not { } camera) return null;
        (System.Numerics.Matrix4x4, float)[] cascades = sun >= 0 ? ShadowFit.FitCascades(camera, lights.All[sun].Direction, distance) : [];
        if (cascades.Length == 0) sun = -1;

        // The spot lights share the spot tile, so each one's texels are as wide as its share of it.
        var spots = new List<(int, System.Numerics.Matrix4x4, float)>();
        var size = ShadowFit.SpotTileArea(0, spotLights.Count).Size;
        foreach (var i in spotLights)
        {
            var light = lights.All[i];
            if (ShadowFit.TryFitSpot(light.Position, light.Direction, light.CosOuter, light.Range, out var viewProjection, out var texel, distance, size))
                spots.Add((i, viewProjection, texel));
        }
        return sun < 0 && spots.Count == 0 && points.Count == 0 ? null : new FrameShadow(sun, cascades, spots, points);
    }
}

/// <summary>
/// Render-world resource published by <see cref="LightingUboPrepare"/>: the
/// <see cref="UniformBufferBinding"/> for this frame's lighting UBO and the count
/// of valid entries inside it. Renamed each frame; consumers should read it
/// transiently rather than caching.
/// </summary>
/// <param name="Binding">Buffer binding suitable for <see cref="IGraphicsDevice.UpdateDescriptorSet"/>.</param>
/// <param name="LightCount">Number of valid <see cref="LightUboEntry"/> entries in the buffer.</param>
/// <param name="HasEnvironment">Whether an <see cref="EnvironmentMap"/> lights the frame.</param>
public sealed record FrameLightingBinding(UniformBufferBinding Binding, int LightCount, bool HasEnvironment = false);

