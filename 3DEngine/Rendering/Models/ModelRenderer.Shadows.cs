using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Engine;

internal sealed partial class ModelRenderer
{
    /// <summary>
    /// Draws the meshes of <paramref name="target"/>, the window's at 0, into the shadow map, as
    /// <paramref name="shadow"/>'s lights see them.
    /// </summary>
    /// <remarks>
    /// Each view drawing its own shadow draws it into the same map before its pass, so a
    /// render target's cascades follow its camera, and views that share a shadow and are drawn one
    /// after another draw it once, by the first of them, with that view's meshes, as the point
    /// lights' faces, which look the same from every camera, are drawn once a frame by the first
    /// view.
    /// </remarks>
    public void DrawShadow(RenderContext renderContext, RenderWorld renderWorld, FrameShadow shadow, int target = 0)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (draws is null || meshes is null || textures is null || renderContext.Device is not GraphicsDevice device) return;
        // A shadow shared by views drawn one after another is drawn once, the map holding it still.
        BeginFrameOfSets(renderContext);
        if (_shadowsFrame != _frames) (_shadowsFrame, ShadowMapsDrawn, _drawnShadow) = (_frames, 0, null);
        if (ReferenceEquals(shadow, _drawnShadow)) return;
        _drawnShadow = shadow;
        ShadowMapsDrawn++;

        var map = ShadowMapFor(device, shadow.TileSize);
        if (!EnsureShadowPipelines(device, map.RenderPass, renderWorld)) return;
        var view = CastingBatches(renderContext, target, device, draws, meshes, textures, renderWorld);

        // One clear for the whole map, then each cascade drawn into its own tile.
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            map.RenderPass, map.Framebuffer, map.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
        // The cascades in the first tiles, and the spot lights in the last, sharing it when there are several.
        for (int t = 0; t < shadow.Cascades.Count; t++)
        {
            var (x, y) = ShadowFit.TileOrigin(t, shadow.TileSize);
            pass.SetViewport(x, y, shadow.TileSize, shadow.TileSize, 0, 1);
            pass.SetScissor(x, y, (uint)shadow.TileSize, (uint)shadow.TileSize);
            Count(t < CascadeNames.Length ? CascadeNames[t] : $"cascade {t}", DrawShadowBatches(pass, view, shadow.Cascades[t].ViewProjection));
        }
        var spots = shadow.SpotLights ?? [];
        for (int s = 0; s < spots.Count; s++)
        {
            var (x, y, size) = ShadowFit.SpotTileArea(s, spots.Count, shadow.TileSize);
            pass.SetViewport(x, y, size, size, 0, 1);
            pass.SetScissor(x, y, (uint)size, (uint)size);
            Count("spot lights", DrawShadowBatches(pass, view, spots[s].ViewProjection));
        }
        pass.EndRenderPass();

        // Each shadowed point light's six faces, a layer of the point map each for the lights that
        // matter most and a quarter of one for the rest. A layer is cleared once and each face it
        // holds drawn into its square.
        var points = shadow.PointLights ?? [];
        if (points.Count == 0 || _pointsFrame == _frames) return;
        _pointsFrame = _frames;
        var pointMap = PointShadowMap(device, shadow.PointFaceSize);
        var layers = new SortedDictionary<int, List<(int X, int Y, int Size, Matrix4x4 Face)>>();
        for (int p = 0; p < points.Count; p++)
            for (int f = 0; f < 6; f++)
            {
                var (layer, x, y, size) = ShadowFit.PointFaceArea(p, f, shadow.PointFaceSize);
                if (!layers.TryGetValue(layer, out var faces)) layers[layer] = faces = [];
                faces.Add((x, y, size, points[p].Faces[f]));
            }
        foreach (var (layer, faces) in layers)
        {
            var facePass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
                pointMap.RenderPass, pointMap.Framebuffers[layer], pointMap.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
            foreach (var (x, y, size, face) in faces)
            {
                facePass.SetViewport(x, y, size, size, 0, 1);
                facePass.SetScissor(x, y, (uint)size, (uint)size);
                Count("point lights", DrawShadowBatches(facePass, view, face));
            }
            facePass.EndRenderPass();
        }
    }

    // The shadow pass's pipelines, a solid one and one a material cuts out, made the first time a
    // depth-only pass draws, for the depth-only passes every shadow map and depth target share.
    private bool EnsureShadowPipelines(GraphicsDevice device, IRenderPass depthOnly, RenderWorld renderWorld)
    {
        if (_shadowVertexSpv.IsEmpty) return false;
        if (_shadowPipeline is not null) return true;
        _shadowVertexShader = device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _shadowVertexSpv));
        _shadowPipeline = MakePipeline(device, depthOnly, renderWorld, _shadowVertexShader, fragment: null, shadow: true);
        if (!_shadowMaskSpv.IsEmpty)
        {
            _shadowMaskShader = device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _shadowMaskSpv));
            _shadowMaskPipeline = MakePipeline(device, depthOnly, renderWorld, _shadowVertexShader, _shadowMaskShader, shadow: true);
        }
        return true;
    }

    // The view's batches, which its model pass draws after, those that cast a shadow gathered into
    // _shadowBatches as the kind of shadow their draws cast, a masked one with its maps, which its
    // fragment stage cuts it out by.
    private View CastingBatches(RenderContext renderContext, int target, GraphicsDevice device, ModelDrawList draws, GpuMeshes meshes,
        GpuTextures textures, RenderWorld renderWorld)
    {
        BeginFrameOfSets(renderContext);
        var view = ViewBatches(target, device, draws, meshes, textures, renderWorld.TryGet<ShaderStore>());
        _shadowBatches.Clear();
        foreach (ref readonly var batch in CollectionsMarshal.AsSpan(view.Batches))
        {
            if (batch.Shadow == ShadowKind.None) continue;
            _shadowBatches.Add(batch.Shadow == ShadowKind.Masked && batch.Set is null
                ? batch with { Set = MaterialSet(device, textures, draws.Span[batch.Custom]) }
                : batch);
        }
        return view;
    }

    /// <summary>
    /// Draws the depth of the meshes that cast a shadow into <paramref name="target"/>, the window's
    /// unless another is named, into <paramref name="depth"/>, through the cameras they were
    /// recorded with, ahead of that view's pass, for the ambient occlusion worked out from it and
    /// the screen's probes stood on it.
    /// </summary>
    /// <remarks>
    /// It draws with the shadow pass's pipelines, whose depth-only pass is the same at any size, so a
    /// mesh that casts no shadow is left out, and a blended one keeps texels as often as it is opaque.
    /// </remarks>
    internal void DrawDepth(RenderContext renderContext, RenderWorld renderWorld, ShadowMap depth, int target = 0)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            depth.RenderPass, depth.Framebuffers[0], depth.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
        if (draws is null || meshes is null || textures is null || renderContext.Device is not GraphicsDevice device) return;
        if (!EnsureShadowPipelines(device, depth.RenderPass, renderWorld)) return;
        var view = CastingBatches(renderContext, target, device, draws, meshes, textures, renderWorld);
        pass.SetViewport(0, 0, depth.Extent.Width, depth.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, depth.Extent.Width, depth.Extent.Height);

        var (ring, offset) = (view.Ring!, view.Offset);
        IPipeline? bound = null;
        Matrix4x4? pushed = null;
        var (frustum, culledThrough) = (default(Frustum), default(Matrix4x4?));
        foreach (ref readonly var batch in CollectionsMarshal.AsSpan(_shadowBatches))
        {
            if (culledThrough != batch.ViewProjection) (frustum, culledThrough) = (new Frustum(batch.ViewProjection, depth: false), batch.ViewProjection);
            if (!frustum.SeesAny(batch, view.Blocks)) continue;
            var pipeline = batch.Shadow == ShadowKind.Masked ? _shadowMaskPipeline! : _shadowPipeline!;
            if (!ReferenceEquals(pipeline, bound))
            {
                pass.SetPipeline(pipeline);
                (bound, pushed) = (pipeline, null);
            }
            if (pushed != batch.ViewProjection)
            {
                var viewProjection = batch.ViewProjection;
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in viewProjection)));
                pushed = viewProjection;
            }
            if (batch.Shadow == ShadowKind.Masked) pass.SetBindGroup(pipeline, batch.Set!);
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring], [0, offset]);
            pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
            Count("depth", DrawSeen(pass, batch, view.Blocks, frustum));
        }
    }

    // The view's batches that cast a shadow, gathered into _shadowBatches, drawn as a light sees
    // them through lightViewProjection, answering how many calls they took. A solid shadow reads
    // the world matrix alone, and a masked one its color and cutoff too.
    private int DrawShadowBatches(TrackedRenderPass pass, View view, Matrix4x4 lightViewProjection)
    {
        var calls = 0;
        var (ring, offset) = (view.Ring!, view.Offset);
        var push = MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in lightViewProjection));
        pass.SetPipeline(_shadowPipeline!);
        pass.PushConstants(_shadowPipeline!, ShaderStageFlags.Vertex, 0, push);
        IPipeline bound = _shadowPipeline!;
        var frustum = new Frustum(lightViewProjection, depth: true);
        foreach (ref readonly var batch in CollectionsMarshal.AsSpan(_shadowBatches))
        {
            if (!frustum.SeesAny(batch, view.Blocks)) continue;
            var pipeline = batch.Shadow == ShadowKind.Masked ? _shadowMaskPipeline! : _shadowPipeline!;
            if (!ReferenceEquals(pipeline, bound))
            {
                pass.SetPipeline(pipeline);
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, push);
                bound = pipeline;
            }
            if (batch.Shadow == ShadowKind.Masked) pass.SetBindGroup(pipeline, batch.Set!);
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring], [0, offset]);
            pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
            calls += DrawSeen(pass, batch, view.Blocks, frustum);
        }
        return calls;
    }

    private static readonly string[] CascadeNames = ["cascade 0", "cascade 1", "cascade 2", "cascade 3"];

    // The point lights' faces, six layers a light, made when a point light first casts a shadow,
    // or a stand-in of two texels for the lights' set to bind before then.
    private ShadowMap PointShadowMap(GraphicsDevice device, int faceSize)
    {
        if (_pointShadowMap is { } made && made.Extent.Width == faceSize) return made;
        if (_pointShadowMap is { } old) _retiredMaps.Add((_frames, old));
        _pointShadowMap = device.CreateShadowMap((uint)faceSize, ShadowFit.PointLayers);
        device.Name(_pointShadowMap.DepthView.Image, "Point light shadow faces");
        return _pointShadowMap;
    }

    // The map of the cascades and spot lights, two tiles on a side, made again when the tile size changes.
    private ShadowMap ShadowMapFor(GraphicsDevice device, int tileSize)
    {
        if (_shadowMap is { } made && made.Extent.Width == 2 * tileSize) return made;
        if (_shadowMap is { } old) _retiredMaps.Add((_frames, old));
        _shadowMap = device.CreateShadowMap((uint)(2 * tileSize));
        device.Name(_shadowMap.DepthView.Image, "Shadow map");
        return _shadowMap;
    }

    private ShadowMap NoPointShadowMap(GraphicsDevice device) => _noPointShadowMap ??= device.CreateShadowMap(1, 2);
}
