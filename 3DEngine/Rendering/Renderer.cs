using System.Diagnostics;

namespace Engine;

/// <summary>
/// Orchestrates the rendering pipeline: extract → graph execution (update → auto-barrier → run per node).
/// </summary>
/// <remarks>
/// <para>
/// Each frame follows a fixed pipeline:
/// <list>
///   <item><description><b>Extract</b> copies relevant game-world data into the <see cref="RenderWorld"/>.</description></item>
///   <item><description><b>Graph</b> executes <see cref="INode"/> instances in topological order, with automatic
///   image layout barrier insertion between nodes based on slot data flow.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="RenderWorld"/>
/// <seealso cref="RenderGraph"/>
/// <seealso cref="RendererContext"/>
internal sealed class Renderer : IDisposable
{
    private static readonly ILogger Logger = Log.For<Renderer>();

    private readonly List<IExtractSystem> _extractSystems = new();
    private readonly List<IPrepareSystem> _prepareSystems = new();

    /// <summary>The render-thread resource container.</summary>
    public RenderWorld RenderWorld { get; } = new();

    /// <summary>The render graph defining the execution order of render passes.</summary>
    public RenderGraph Graph { get; } = new();

    /// <summary>The high-level graphics context (device, camera resources, dynamic allocator).</summary>
    public RendererContext Context { get; }

    /// <summary>Frame timing and adapter diagnostics.</summary>
    public RendererDiagnostics Diagnostics { get; } = new();

    private bool _initialized;
    private Extent2D _cachedSurfaceExtent;

    /// <summary>Creates a new <see cref="Renderer"/> with the specified graphics context.</summary>
    /// <param name="context">The renderer context wrapping the graphics device.</param>
    public Renderer(RendererContext context) =>
        Context = context;

    /// <summary>Initializes the renderer, setting up diagnostics and the default render graph nodes.</summary>
    /// <param name="world">The game world, used to load shader assets via <see cref="AssetServer"/>.</param>
    public void Initialize(World world)
    {
        if (_initialized) return;
        Logger.Info("Initializing the renderer, its diagnostics and its render graph...");
        var sw = Stopwatch.StartNew();

        Diagnostics.Initialize(Context.AdapterInfo);
        Logger.Debug("Renderer diagnostics initialized.");

        var server = world.Resource<AssetServer>();
        Graph.AddNode("main_pass", new MainPassNode());

        var model = server.LoadSync<ShaderProgram>("shaders/model.slang");
        // Where the device traces rays, the model pass built to trace a reflection the scene's
        // distance field misses, or the plain one where the build is not in the cache and there
        // is no compiler to make it.
        if (Context.Graphics is GraphicsDevice { CanQueryRays: true })
            try
            {
                var loader = new SlangLoader();
                model = loader.Compile(File.ReadAllText(Path.Combine(loader.ImportDirectory, "model.slang")), "model.slang", ["RAY_QUERY"]);
            }
            catch (Exception e) when (e is InvalidOperationException or IOException)
            {
                Logger.Warn($"The model pass traces no rays, its build for them not made: {e.Message}");
            }
        var immediate = server.LoadSync<ShaderProgram>("shaders/immediate.slang");
        var shadow = server.LoadSync<ShaderProgram>("shaders/shadow.slang");
        if (Context.Graphics is GraphicsDevice device)
        {
            device.InitializeSkinning(server.LoadSync<ShaderProgram>("shaders/skin.slang").Compute);
            device.InitializeParticles(server.LoadSync<ShaderProgram>("shaders/particle_step.slang").Compute);
            device.InitializeParticleSort(server.LoadSync<ShaderProgram>("shaders/particle_sort.slang").Compute);
            device.InitializeSceneField(server.LoadSync<ShaderProgram>("shaders/field_splat.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/field_resolve.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/field_stamp.slang").Compute);
            device.InitializeGlobalIllumination(server.LoadSync<ShaderProgram>("shaders/gi_trace.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/gi_merge.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/gi_ambient.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/gi_screen.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/gi_screen_filter.slang").Compute);
            device.InitializeProbeFilter(server.LoadSync<ShaderProgram>("shaders/probe_gather.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/probe_mips.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/probe_prefilter.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/probe_irradiance.slang").Compute,
                server.LoadSync<ShaderProgram>("shaders/env_gather.slang").Compute);
        }
        RenderWorld.Set(new ParticleRenderer(server.LoadSync<ShaderProgram>("shaders/particles.slang")));
        RenderWorld.Set(new ModelRenderer(model, shadow, server.LoadSync<ShaderProgram>("shaders/model_streams.slang")));
        RenderWorld.Set(new ImmediateRenderer(immediate.Vertex, immediate.Fragment, server.LoadSync<ShaderProgram>("shaders/immediate_linear.slang").Fragment));
        var bloom = server.LoadSync<ShaderProgram>("shaders/bloom.slang");
        var composite = server.LoadSync<ShaderProgram>("shaders/composite.slang");
        var fxaa = server.LoadSync<ShaderProgram>("shaders/fxaa.slang");
        var exposure = server.LoadSync<ShaderProgram>("shaders/exposure.slang");
        RenderWorld.Set(new BloomRenderer(bloom, composite, fxaa, exposure,
            server.LoadSync<ShaderProgram>("shaders/dof.slang"), server.LoadSync<ShaderProgram>("shaders/motion_blur.slang"),
            server.LoadSync<ShaderProgram>("shaders/velocity.slang")));
        RenderWorld.Set(new AmbientOcclusionRenderer(server.LoadSync<ShaderProgram>("shaders/ao.slang")));
        RenderWorld.Set(new SceneFieldRenderer());
        RenderWorld.Set(new GlobalIlluminationRenderer());
        RenderWorld.Set(new SceneFieldViewRenderer(server.LoadSync<ShaderProgram>("shaders/field_view.slang")));
        AddPrepareSystem(new ImmediateUploadPrepare());

        // Skinned meshes posed before anything draws them, then render targets, each drawing the
        // shadow map for its own camera before its pass, then the window's shadow, so the window's
        // passes can sample the targets and the map as the window's camera needs it.
        Graph.AddNode("skinning", new SkinningNode());
        // A new environment map is filtered ahead of every pass that lights by it.
        Graph.AddNode("environment", new EnvironmentNode());
        Graph.AddNodeEdge("environment", "skinning");
        // The scene's distance field is built after the skins are posed and before the particles
        // that collide with it and every pass that reads it.
        Graph.AddNode("scene_field", new SceneFieldNode());
        Graph.AddNodeEdge("skinning", "scene_field");
        // Particles are stepped beside the skins, before every pass that might draw them.
        Graph.AddNode("particles", new ParticleNode());
        Graph.AddNodeEdge("scene_field", "particles");
        Graph.AddNode("targets", new TargetsNode());
        Graph.AddNodeEdge("particles", "targets");
        Graph.AddNode("shadows", new ShadowNode());
        Graph.AddNodeEdge("targets", "shadows");
        // The window's ambient occlusion, from a depth of its own drawn ahead of every pass that
        // lights the window's meshes.
        Graph.AddNode("ambient_occlusion", new AmbientOcclusionNode());
        Graph.AddNodeEdge("shadows", "ambient_occlusion");
        // The light that bounces, traced through the field before every pass that lights the window.
        Graph.AddNode("global_illumination", new GlobalIlluminationNode());
        Graph.AddNodeEdge("ambient_occlusion", "global_illumination");
        Graph.AddNode("probes", new ProbeNode());
        Graph.AddNodeEdge("global_illumination", "probes");
        // With bloom on, the window's scene is drawn into the HDR target and spread before the
        // window's pass composites it.
        Graph.AddNode("hdr_scene", new HdrSceneNode());
        Graph.AddNodeEdge("probes", "hdr_scene");
        Graph.AddNode("bloom", new BloomNode());
        Graph.AddNodeEdge("hdr_scene", "bloom");
        Graph.AddNodeEdge("bloom", "main_pass");
        Graph.AddNode("models", new ModelNode());
        Graph.AddNodeEdge("main_pass", "models");
        Graph.AddNode("immediate", new ImmediateNode());
        Graph.AddNodeEdge("models", "immediate");
        // A cascade of the scene's distance field drawn over the window, where a command asks.
        Graph.AddNode("scene_field_view", new SceneFieldViewNode());
        Graph.AddNodeEdge("immediate", "scene_field_view");
        Logger.Debug("Default MainPassNode added to render graph.");

        // Pipeline cache deduplicates compiled pipelines across nodes.
        var pipelineCache = new PipelineCache(Context.Graphics);
        RenderWorld.Set(pipelineCache);
        Logger.Debug("PipelineCache created and registered in RenderWorld.");

        _initialized = true;
        Logger.Info($"Renderer initialized in {sw.ElapsedMilliseconds}ms.");
    }

    /// <summary>Registers an extract system that copies game-world data into the <see cref="RenderWorld"/> each frame.</summary>
    /// <param name="sys">The extract system to add.</param>
    public void AddExtractSystem(IExtractSystem sys) => _extractSystems.Add(sys);

    /// <summary>Registers a prepare system that runs before graph execution each frame.</summary>
    /// <param name="sys">The prepare system to add.</param>
    public void AddPrepareSystem(IPrepareSystem sys) => _prepareSystems.Add(sys);

    /// <summary>Executes one full render frame: extract → begin frame → prepare → graph execution → end frame.</summary>
    /// <param name="world">The game world to read data from during the extract phase.</param>
    public void RenderFrame(World world)
    {
        if (!_initialized) Initialize(world);

        var mark = Stopwatch.GetTimestamp();
        Logger.FrameTrace("RenderFrame: Running extract systems...");
        RenderWorld.ClearEntities();
        foreach (var sys in _extractSystems)
            sys.Run(world, RenderWorld);
        Timings.ExtractMs = Lap(ref mark);

        Logger.FrameTrace("RenderFrame: Beginning frame...");
        var (renderCtx, frameCtx, imageIndex) = Context.BeginFrame(RenderWorld);
        SyncSurfaceInfo(frameCtx.Extent);
        UpdateDiagnostics(frameCtx.Extent);
        Timings.BeginFrameMs = Lap(ref mark);

        Logger.FrameTrace("RenderFrame: Running prepare systems...");
        var start = mark;
        var prepareCpu = new List<(string, double)>(_prepareSystems.Count);
        foreach (var sys in _prepareSystems)
        {
            sys.Run(RenderWorld, renderCtx);
            prepareCpu.Add((sys.GetType().Name, Lap(ref mark)));
        }
        Timings.PrepareCpu = prepareCpu;
        Timings.PrepareMs = Stopwatch.GetElapsedTime(start, mark).TotalMilliseconds;

        Logger.FrameTrace("RenderFrame: Executing render graph nodes...");
        ExecuteGraph(renderCtx, frameCtx.InFlightIndex);
        Timings.GraphMs = Lap(ref mark);

        Logger.FrameTrace("RenderFrame: Ending frame...");
        Context.EndFrame(frameCtx, imageIndex);
        Timings.EndFrameMs = Lap(ref mark);
    }

    /// <summary>How long the last frame's rendering took, by phase and by node.</summary>
    public RenderTimings Timings { get; } = new();

    // The milliseconds since the mark, which moves to now.
    private static double Lap(ref long mark)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(mark, now).TotalMilliseconds;
        mark = now;
        return elapsed;
    }

    /// <summary>Executes the render graph: per node calls Update → auto-barrier → Run. Ends the active swapchain pass after all nodes.</summary>
    private void ExecuteGraph(RenderContext renderCtx, int inFlightIndex)
    {
        var orderedNodes = Graph.TopologicalOrder();
        var outputStore = new Dictionary<string, SlotValue[]>();
        var layoutTracker = new Dictionary<IImage, ImageLayout>();

        // A timestamp before the graph and after each node, which the GPU writes as it finishes
        // what came before, read back when this slot comes round again.
        var timer = renderCtx.Device as GraphicsDevice;
        if (timer is not null)
        {
            Timings.NodeGpu = timer.BeginTimestamps(renderCtx.CommandBuffer, inFlightIndex);
            timer.Timestamp(renderCtx.CommandBuffer, inFlightIndex, "start");
        }
        var nodeCpu = new List<(string, double)>(orderedNodes.Count);
        var mark = Stopwatch.GetTimestamp();

        foreach (var (label, node) in orderedNodes)
        {
            node.Update(RenderWorld);

            var inputs = Graph.GatherInputs(label, outputStore);

            // Auto-barrier: transition TextureView inputs to ShaderReadOnlyOptimal before sampling.
            foreach (var input in inputs)
            {
                if (input.Type != SlotType.TextureView) continue;

                var imageView = input.AsTextureView();
                var image = imageView.Image;
                var currentLayout = layoutTracker.GetValueOrDefault(image, ImageLayout.Undefined);
                if (currentLayout != ImageLayout.ShaderReadOnlyOptimal)
                {
                    renderCtx.Device.CmdPipelineBarrier(
                        renderCtx.CommandBuffer, image, currentLayout, ImageLayout.ShaderReadOnlyOptimal);
                    layoutTracker[image] = ImageLayout.ShaderReadOnlyOptimal;
                }
            }

            var graphCtx = new RenderGraphContext(inputs, node.Output().Length, RunSubGraph);
            var device = renderCtx.Device as GraphicsDevice;
            device?.BeginDebugLabel(renderCtx.CommandBuffer, label);
            node.Run(graphCtx, renderCtx, RenderWorld);
            device?.EndDebugLabel(renderCtx.CommandBuffer);

            // Outputs were rendered to, so they are now in ColorAttachmentOptimal.
            var outputs = graphCtx.GetOutputs();
            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i].Type != SlotType.TextureView) continue;
                var imageView = outputs[i].AsTextureView();
                layoutTracker[imageView.Image] = ImageLayout.ColorAttachmentOptimal;
            }
            outputStore[label] = outputs;
            nodeCpu.Add((label, Lap(ref mark)));
            timer?.Timestamp(renderCtx.CommandBuffer, inFlightIndex, label);
        }
        Timings.NodeCpu = nodeCpu;

        // End the shared swapchain pass that MainPassNode left open.
        var activePass = RenderWorld.TryGet<ActiveSwapchainPass>();
        activePass?.Dispose();

        foreach (var (_, node) in orderedNodes)
            node.AfterWindowPass(renderCtx, RenderWorld);
    }

    /// <summary>Runs a named sub-graph with forwarded slot values.</summary>
    private void RunSubGraph(string name, SlotValue[] inputs)
    {
        var subGraph = Graph.GetSubGraph(name);
        if (subGraph is null)
        {
            Logger.Warn($"Sub-graph '{name}' not found.");
            return;
        }
        // TODO: implement sub-graph execution (own RenderContext, recursive node iteration).
        Logger.Debug($"Sub-graph '{name}' is not run, since sub-graphs are not implemented.");
    }

    /// <summary>Syncs the render surface dimensions from the current swapchain extent.</summary>
    private void SyncSurfaceInfo(Extent2D extent)
    {
        var surface = RenderWorld.TryGet<RenderSurfaceInfo>();
        if (surface is null)
        {
            surface = new RenderSurfaceInfo();
            RenderWorld.Set(surface);
        }

        if (surface.Apply(ToIntDimension(extent.Width), ToIntDimension(extent.Height)))
        {
            _cachedSurfaceExtent = new Extent2D((uint)surface.Width, (uint)surface.Height);
            Log.For<Renderer>().Info($"Surface resized to {surface.Width}x{surface.Height}");
        }
    }

    /// <summary>Records frame diagnostics (adapter info, extent, surface revision).</summary>
    private void UpdateDiagnostics(Extent2D extent)
    {
        var surface = RenderWorld.TryGet<RenderSurfaceInfo>();
        var effectiveExtent = _cachedSurfaceExtent.Width == 0 ? extent : _cachedSurfaceExtent;
        Diagnostics.RecordFrame(Context.AdapterInfo, effectiveExtent, surface?.Revision ?? 0);
    }

    /// <summary>Clamps an unsigned pixel dimension to a positive integer, minimum 1.</summary>
    private static int ToIntDimension(uint value)
    {
        if (value == 0) return 1;
        if (value > int.MaxValue) return int.MaxValue;
        return (int)value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Logger.Info("Disposing Renderer and underlying graphics context...");

        // The frames in flight still read the buffers, sets and pipelines destroyed below.
        Context.Graphics.WaitIdle();

        DisposeSystems(_prepareSystems);
        DisposeSystems(_extractSystems);
        Logger.Debug("Render systems disposed.");

        Graph.Dispose();
        RenderWorld.TryGet<ImmediateRenderer>()?.Dispose();
        RenderWorld.TryGet<ParticleRenderer>()?.Dispose();
        RenderWorld.TryGet<ModelRenderer>()?.Dispose();
        RenderWorld.TryGet<BloomRenderer>()?.Dispose();
        RenderWorld.TryGet<AmbientOcclusionRenderer>()?.Dispose();
        RenderWorld.TryGet<SceneFieldRenderer>()?.Dispose();
        RenderWorld.TryGet<GlobalIlluminationRenderer>()?.Dispose();
        RenderWorld.TryGet<SceneFieldViewRenderer>()?.Dispose();
        Logger.Debug("Render graph nodes disposed.");

        // Pipeline cache must be disposed before the graphics device.
        RenderWorld.TryGet<PipelineCache>()?.Dispose();
        Logger.Debug("Pipeline cache disposed.");

        Context.Dispose();
        Logger.Info("Renderer disposed.");
    }

    /// <summary>Disposes all <see cref="IDisposable"/> systems in a list and clears it.</summary>
    private static void DisposeSystems<T>(List<T> systems)
    {
        foreach (var sys in systems)
            if (sys is IDisposable disposable)
                disposable.Dispose();

        systems.Clear();
    }
}
