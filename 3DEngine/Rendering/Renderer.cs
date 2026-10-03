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
public sealed class Renderer : IDisposable
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
        Logger.Info("Initializing Renderer - setting up diagnostics and render graph...");
        var sw = Stopwatch.StartNew();

        Diagnostics.Initialize(Context.AdapterInfo);
        Logger.Debug("Renderer diagnostics initialized.");

        var server = world.Resource<AssetServer>();
        var mesh = server.LoadSync<ShaderProgram>("shaders/mesh.slang");

        Graph.AddNode("main_pass", new MainPassNode(mesh.Vertex, mesh.Fragment));

        var model = server.LoadSync<ShaderProgram>("shaders/model.slang");
        var immediate = server.LoadSync<ShaderProgram>("shaders/immediate.slang");
        RenderWorld.Set(new ModelRenderer(model.Vertex, model.Fragment));
        RenderWorld.Set(new ImmediateRenderer(immediate.Vertex, immediate.Fragment));
        AddPrepareSystem(new ImmediateUploadPrepare());

        // Render targets first, so the window's passes can sample them.
        Graph.AddNode("targets", new TargetsNode());
        Graph.AddNodeEdge("targets", "main_pass");
        Graph.AddNode("models", new ModelNode());
        Graph.AddNodeEdge("main_pass", "models");
        Graph.AddNode("immediate", new ImmediateNode());
        Graph.AddNodeEdge("models", "immediate");
        Logger.Debug("Default MainPassNode added to render graph.");

        // Pipeline cache deduplicates compiled pipelines across nodes.
        var pipelineCache = new PipelineCache(Context.Graphics);
        RenderWorld.Set(pipelineCache);
        Logger.Debug("PipelineCache created and registered in RenderWorld.");

        // Runs after MeshPrepare so vertex buffers are uploaded before phase items reference them.
        AddPrepareSystem(new QueueMeshPhaseItems());
        Logger.Debug("QueueMeshPhaseItems prepare system registered.");

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

        Logger.FrameTrace("RenderFrame: Running extract systems...");
        RenderWorld.ClearEntities();
        foreach (var sys in _extractSystems)
            sys.Run(world, RenderWorld);

        Logger.FrameTrace("RenderFrame: Beginning frame...");
        var (renderCtx, frameCtx, imageIndex) = Context.BeginFrame(RenderWorld);
        SyncSurfaceInfo(frameCtx.Extent);
        UpdateDiagnostics(frameCtx.Extent);

        Logger.FrameTrace("RenderFrame: Running prepare systems...");
        foreach (var sys in _prepareSystems)
            sys.Run(RenderWorld, renderCtx);

        Logger.FrameTrace("RenderFrame: Executing render graph nodes...");
        ExecuteGraph(renderCtx);

        Logger.FrameTrace("RenderFrame: Ending frame...");
        Context.EndFrame(frameCtx, imageIndex);
    }

    /// <summary>Executes the render graph: per node calls Update → auto-barrier → Run. Ends the active swapchain pass after all nodes.</summary>
    private void ExecuteGraph(RenderContext renderCtx)
    {
        var orderedNodes = Graph.TopologicalOrder();
        var outputStore = new Dictionary<string, SlotValue[]>();
        var layoutTracker = new Dictionary<IImage, ImageLayout>();

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
            node.Run(graphCtx, renderCtx, RenderWorld);

            // Outputs were rendered to, so they are now in ColorAttachmentOptimal.
            var outputs = graphCtx.GetOutputs();
            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i].Type != SlotType.TextureView) continue;
                var imageView = outputs[i].AsTextureView();
                layoutTracker[imageView.Image] = ImageLayout.ColorAttachmentOptimal;
            }
            outputStore[label] = outputs;
        }

        // End the shared swapchain pass that MainPassNode left open.
        var activePass = RenderWorld.TryGet<ActiveSwapchainPass>();
        activePass?.Dispose();
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
        Logger.Debug($"Sub-graph '{name}' execution placeholder - not yet implemented.");
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

        DisposeSystems(_prepareSystems);
        DisposeSystems(_extractSystems);
        Logger.Debug("Render systems disposed.");

        Graph.Dispose();
        RenderWorld.TryGet<ImmediateRenderer>()?.Dispose();
        RenderWorld.TryGet<ModelRenderer>()?.Dispose();
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
