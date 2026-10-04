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
            ubo.SpotShadowLight = shadow.SpotLight;
            if (shadow.SpotLight >= 0) ubo.ShadowCascades[ShadowFit.SpotTile] = shadow.SpotViewProjection;
            ubo.CascadeCount = shadow.Cascades.Count;
            Span<float> texels = stackalloc float[LightingUboPacker.MaxCascades];
            for (int i = 0; i < shadow.Cascades.Count; i++)
            {
                ubo.ShadowCascades[i] = shadow.Cascades[i].ViewProjection;
                texels[i] = shadow.Cascades[i].Texel;
            }
            ubo.ShadowTexels = new System.Numerics.Vector4(texels[0], texels[1], texels[2], shadow.SpotTexelPerUnit);
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

        int sun = -1, spot = -1;
        for (int i = 0; i < count; i++)
        {
            if (!lights.All[i].CastsShadows) continue;
            if (sun < 0 && lights.All[i].Kind == LightKind.Directional) sun = i;
            if (spot < 0 && lights.All[i].Kind == LightKind.Spot) spot = i;
        }
        if (sun < 0 && spot < 0) return null;

        (System.Numerics.Matrix4x4, float)[] cascades = [];
        var drawn = false;
        foreach (var draw in draws.Draws)
        {
            if (draw.Target != 0) continue;
            drawn = true;
            if (sun >= 0) cascades = ShadowFit.FitCascades(draw.ViewProjection, lights.All[sun].Direction);
            break;
        }
        if (!drawn) return null;
        if (cascades.Length == 0) sun = -1;

        var spotViewProjection = System.Numerics.Matrix4x4.Identity;
        float spotTexel = 0;
        if (spot >= 0)
        {
            var light = lights.All[spot];
            if (!ShadowFit.TryFitSpot(light.Position, light.Direction, light.CosOuter, light.Range, out spotViewProjection, out spotTexel))
                spot = -1;
        }
        return sun < 0 && spot < 0 ? null : new FrameShadow(sun, cascades, spot, spotViewProjection, spotTexel);
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

