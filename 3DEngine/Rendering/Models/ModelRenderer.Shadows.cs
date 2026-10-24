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

        // The fill past the cascades, in a map of its own at the tile's size, since the four tiles
        // of the cascades' map are taken.
        if (shadow.Fill is { } fill)
        {
            var filled = FillMapFor(device, shadow.TileSize);
            var fillPass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
                filled.RenderPass, filled.Framebuffer, filled.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
            fillPass.SetViewport(0, 0, shadow.TileSize, shadow.TileSize, 0, 1);
            fillPass.SetScissor(0, 0, (uint)shadow.TileSize, (uint)shadow.TileSize);
            Count("fill cascade", DrawShadowBatches(fillPass, view, fill.ViewProjection));
            fillPass.EndRenderPass();
        }

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

    // The shadow pass's stages, made the first time a depth-only pass draws, for the depth-only
    // passes every shadow map and depth target share, whose pipelines are made as first drawn.
    private bool EnsureShadowPipelines(GraphicsDevice device, IRenderPass depthOnly, RenderWorld renderWorld)
    {
        if (_shadowVertexSpv.IsEmpty) return false;
        if (_shadowVertexShader is not null) return true;
        _shadowMaking = (device, depthOnly, renderWorld);
        _shadowVertexShader = device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _shadowVertexSpv));
        if (!_shadowMaskSpv.IsEmpty) _shadowMaskShader = device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _shadowMaskSpv));
        return true;
    }

    // What the shadow pass's pipelines are made with, kept for those made as first drawn.
    private (GraphicsDevice Device, IRenderPass DepthOnly, RenderWorld RenderWorld)? _shadowMaking;

    // The pass's own shadow pipelines, by whether the material cuts the surface out, whether the
    // mesh is of positions alone, and whether depth is clamped, which a shadow map's is, so a
    // caster nearer the light than its box still shadows what the box holds, and the depth the
    // ambient occlusion and the screen's probes read is not, so what the camera's near plane clips
    // stays out of it.
    private readonly IPipeline?[] _shadowPipelines = new IPipeline?[8];

    // How the shadow pass draws a masked surface through a program's shader, the shader, how its
    // vertex stage reads the mesh's streams and CUSTOM0, and whether the mesh is of positions alone.
    private readonly record struct OwnShadow(int Shader, ShaderProgram Program, Streams Streams, StreamFeed? Feed, bool Positions);

    // The program's shadowMain stages by ShaderStore id, and the pipelines its masked surfaces cast
    // their shadows through, by the shader, how they read the mesh, and whether depth is clamped.
    private readonly Dictionary<int, IShader> _shadowFragments = [];
    private readonly Dictionary<(int Shader, Streams Streams, StreamFeed? Feed, bool Positions, bool Clamp), IPipeline> _ownShadowPipelines = [];

    // Beside each of _shadowBatches, the program's way of drawing it, or null for the pass's own.
    private readonly List<OwnShadow?> _ownShadows = [];

    // The shadow pass's pipeline for a batch, solid or cut out by its material, reading the mesh's
    // whole vertex or, for a mesh of positions alone, its positions with the rest zeros at binding
    // 6, clamping depth for a shadow map, each made the first time it is drawn with.
    private IPipeline ShadowPipeline(in Batch batch, bool clamp)
    {
        var masked = IsMasked(batch.Shadow);
        var positions = batch.Mesh.PositionsOnly;
        var (device, depthOnly, renderWorld) = _shadowMaking!.Value;
        return _shadowPipelines[(masked ? 4 : 0) + (positions ? 2 : 0) + (clamp ? 1 : 0)] ??= MakePipeline(device, depthOnly, renderWorld,
            _shadowVertexShader!, masked ? _shadowMaskShader : null, shadow: true, positions: positions, clamp: clamp);
    }

    // A batch's pipeline for a shadow map (clamp) or the depth target, through a program's own
    // stages where own is given and the pass's otherwise.
    private IPipeline ShadowPipeline(in Batch batch, OwnShadow? own, bool clamp) =>
        own is { } program ? OwnShadowPipeline(program, clamp) : ShadowPipeline(batch, clamp);

    // The view's batches, which its model pass draws after, those that cast a shadow gathered into
    // _shadowBatches as the kind of shadow their draws cast, a masked one with its maps, which its
    // fragment stage cuts it out by, or through a program's shader with the program's set.
    private View CastingBatches(RenderContext renderContext, int target, GraphicsDevice device, ModelDrawList draws, GpuMeshes meshes,
        GpuTextures textures, RenderWorld renderWorld)
    {
        BeginFrameOfSets(renderContext);
        var store = renderWorld.TryGet<ShaderStore>();
        var view = ViewBatches(target, device, draws, meshes, textures, store);
        _shadowBatches.Clear();
        _ownShadows.Clear();
        foreach (ref readonly var batch in CollectionsMarshal.AsSpan(view.Batches))
        {
            if (batch.Shadow == ShadowKind.None) continue;
            var own = IsMasked(batch.Shadow) && batch.Custom >= 0 ? OwnShadowOf(device, store, draws.Span[batch.Custom], batch.Mesh) : null;
            _ownShadows.Add(own);
            _shadowBatches.Add(own is not null
                ? batch with { Set = DrawSet(device, renderContext, renderWorld, textures, draws.Span[batch.Custom], store!.Get(draws.Span[batch.Custom].Shader)!) }
                : IsMasked(batch.Shadow) && batch.Set is null
                    ? batch with { Set = MaterialSet(device, textures, draws.Span[batch.Custom]) }
                    : batch);
        }
        return view;
    }

    // How a masked surface drawn through a program's shader casts its shadow, where the program has
    // a vertex stage of its own, which may give the coordinate the texture is cut at as the voxel
    // game's plants' does from a packed stream, or a shadowMain, which cuts it as the program
    // likes. The program's vertex stage is drawn with the light's view-projection pushed in the
    // camera's place, and its shadowMain or shadow.slang's cut after it. Null for a program with
    // neither, whose shadow the pass's own stages draw.
    private OwnShadow? OwnShadowOf(GraphicsDevice device, ShaderStore? store, in ModelDraw draw, GpuMeshes.Entry mesh)
    {
        if (store?.Get(draw.Shader) is not { } program || _shadowMaskShader is null) return null;
        if (!program.Stages.ContainsKey(ShaderStage.Vertex) && program.ShadowFragment is null) return null;

        var modules = CustomModules(device, draw.Shader, program);
        var streams = !modules.ReadsStreams ? Streams.None : mesh.Colors is null ? Streams.Default : Streams.PerVertex;
        StreamFeed? feed = modules.Custom0 is { } custom0 ? StreamFeed.For(custom0, mesh.Custom?.Format) : null;
        return new OwnShadow(draw.Shader, program, streams, feed, mesh.PositionsOnly);
    }

    // The pipeline of a program's stages for a shadow map (clamp) or the depth target, made the
    // first time it is drawn with.
    private IPipeline OwnShadowPipeline(in OwnShadow own, bool clamp)
    {
        var key = (own.Shader, own.Streams, own.Feed, own.Positions, clamp);
        if (_ownShadowPipelines.TryGetValue(key, out var pipeline)) return pipeline;
        var (device, depthOnly, renderWorld) = _shadowMaking!.Value;
        var program = own.Program;
        var modules = CustomModules(device, own.Shader, program);
        var hasVertex = program.Stages.TryGetValue(ShaderStage.Vertex, out var vertex);
        var fragment = program.ShadowFragment is not { } spv ? _shadowMaskShader
            : _shadowFragments.TryGetValue(own.Shader, out var made) ? made
            : _shadowFragments[own.Shader] = device.CreateShader(new ShaderDesc(ShaderStage.Fragment, spv));
        // The instance's every row, which the program's stage takes as the model pass gives it.
        return _ownShadowPipelines[key] = MakePipeline(device, depthOnly, renderWorld, modules.Vertex, fragment,
            material: program.OwnTextures(PassTextures).Count > 0 || program.Buffers.Count > 0 ? SetsFor(device, own.Shader, program).Layout : null,
            streams: own.Streams, inputs: hasVertex ? ShaderProgram.InputLocationSet(vertex!) : null, named: hasVertex ? program.VertexInputs : [],
            shaderName: program.Name, feed: own.Feed, positions: own.Positions, clamp: clamp);
    }

    // A batch's mesh and instances for a shadow or a depth, with the streams and CUSTOM0 a program's
    // vertex stage reads bound as the model pass binds them, and the zeros a mesh of positions
    // alone reads as its normal and texture coordinate.
    private void BindShadowVertices(TrackedRenderPass pass, IGraphicsDevice gfx, in Batch batch, OwnShadow? own, IBuffer ring, ulong offset)
    {
        var streams = own?.Streams ?? Streams.None;
        if (streams == Streams.None)
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring], [0, offset]);
        else
        {
            var (colors, texcoords2) = streams == Streams.PerVertex ? (batch.Mesh.Colors!, batch.Mesh.Texcoords2!) : DefaultStreams(gfx);
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring, colors, texcoords2], [0, offset, 0, 0]);
        }
        if (own?.Feed is { } fed) pass.SetVertexBuffer(5, [fed.PerVertex ? batch.Mesh.Custom!.Value.Buffer : Zeros(gfx)], [0]);
        if (batch.Mesh.PositionsOnly) pass.SetVertexBuffer(6, [Zeros(gfx)], [0]);
        if (batch.Mesh.Counted is null) pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
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
        var batches = CollectionsMarshal.AsSpan(_shadowBatches);
        for (int i = 0; i < batches.Length; i++)
        {
            ref readonly var batch = ref batches[i];
            // A mesh drawn into the shadow maps alone is seen nowhere, so the view's depth holds none.
            if (IsAlone(batch.Shadow)) continue;
            if (culledThrough != batch.ViewProjection) (frustum, culledThrough) = (new Frustum(batch.ViewProjection, depth: false), batch.ViewProjection);
            if (!frustum.SeesAny(batch, view.Blocks)) continue;
            var own = _ownShadows[i];
            var pipeline = ShadowPipeline(batch, own, clamp: false);
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
            if (IsMasked(batch.Shadow)) pass.SetBindGroup(pipeline, batch.Set!);
            BindShadowVertices(pass, device, batch, own, ring, offset);
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
        IPipeline? bound = null;
        // A caster before the light's near plane is drawn onto it where the device clamps depth.
        var clamp = _shadowMaking!.Value.Device.CanClampDepth;
        var frustum = new Frustum(lightViewProjection, depth: true, clipsNear: !clamp);
        var batches = CollectionsMarshal.AsSpan(_shadowBatches);
        for (int i = 0; i < batches.Length; i++)
        {
            ref readonly var batch = ref batches[i];
            if (!frustum.SeesAny(batch, view.Blocks)) continue;
            var own = _ownShadows[i];
            var pipeline = ShadowPipeline(batch, own, clamp);
            if (!ReferenceEquals(pipeline, bound))
            {
                pass.SetPipeline(pipeline);
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, push);
                bound = pipeline;
            }
            if (IsMasked(batch.Shadow)) pass.SetBindGroup(pipeline, batch.Set!);
            BindShadowVertices(pass, _shadowMaking!.Value.Device, batch, own, ring, offset);
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

    // The fill cascade's map, one tile, made again when the tile size changes.
    private ShadowMap FillMapFor(GraphicsDevice device, int tileSize)
    {
        if (_fillMap is { } made && made.Extent.Width == tileSize) return made;
        if (_fillMap is { } old) _retiredMaps.Add((_frames, old));
        _fillMap = device.CreateShadowMap((uint)tileSize);
        device.Name(_fillMap.DepthView.Image, "Shadow fill");
        return _fillMap;
    }

    private ShadowMap NoPointShadowMap(GraphicsDevice device) => _noPointShadowMap ??= device.CreateShadowMap(1, 2);
}
