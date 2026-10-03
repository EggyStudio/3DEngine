namespace Engine;

/// <summary>
/// Composition-root plugin that wires the Vulkan renderer to the platform window.
/// Creates the <see cref="Renderer"/>, registers extract/prepare systems,
/// initializes the Vulkan graphics context, and handles debounced resize.
/// </summary>
/// <seealso cref="Renderer"/>
/// <seealso cref="RendererContext"/>
/// <seealso cref="AppWindowPlugin"/>
public sealed class SdlPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.SdlRenderer");

    /// <summary>Delay (ms) after the last resize event before committing the expensive
    /// swapchain + allocator + camera rebuild.  During this window the Vulkan lazy path
    /// (<c>VK_ERROR_OUT_OF_DATE_KHR</c>) handles swapchain-only rebuilds if needed.</summary>
    private const long ResizeDebounceMs = 150;

    /// <summary>Extract system that copies <see cref="ClearColor"/> from the game world to the render world.</summary>
    private sealed class ClearColorExtract : IExtractSystem
    {
        /// <inheritdoc />
        public void Run(World world, RenderWorld renderWorld)
        {
            if (world.TryGetResource<ClearColor>(out var cc))
                renderWorld.Set(cc);
        }
    }

    /// <inheritdoc />
    public void Build(App app)
    {
        // Ensure a default clear color resource exists
        var cfg = app.World.Resource<Config>();
        var color = app.World.GetOrInsertResource(() => cfg.Graphics == GraphicsBackend.Vulkan
            ? new ClearColor(0.675f, 0.086f, 0.173f, 1f)   // Tamarillo red for Vulkan
            : new ClearColor(0.45f, 0.55f, 0.60f, 1.00f)); // blue-ish for SDL
        Logger.Info($"Clear color set (R={color.R:F2}, G={color.G:F2}, B={color.B:F2}, A={color.A:F2}) for {cfg.Graphics} backend.");

        Logger.Info("SdlPlugin: Creating Renderer and wiring extract/prepare systems...");
        var renderer = new Renderer(new RendererContext());
        renderer.AddExtractSystem(new ClearColorExtract());
        renderer.AddExtractSystem(new CameraExtract());
        renderer.AddExtractSystem(new MeshMaterialExtract());
        renderer.AddPrepareSystem(new MeshPrepare());
        renderer.AddPrepareSystem(new TexturePrepare());
        app.World.InsertResource(renderer);
        Logger.Debug("Renderer resource registered with extract and prepare systems.");

        // Initialize Vulkan against SDL window if configured
        var window = app.World.Resource<AppWindow>();

        // -- Debounce state for the expensive higher-level resize --
        // Captured by both the ResizeEvent lambda and the per-frame system lambda.
        bool pendingRendererResize = false;
        long lastResizeTick = 0;

        if (cfg.Graphics == GraphicsBackend.Vulkan)
        {
            Logger.Info("SdlPlugin: Vulkan backend selected - initializing graphics context against SDL window...");
            // Grab the ISurfaceSource that AppWindowPlugin inserted
            var surface = app.World.Resource<ISurfaceSource>();
            renderer.Context.Initialize(surface, cfg.WindowData.Title);

            // Seed RenderSurfaceInfo with current window size
            var surfaceInfo = new RenderSurfaceInfo { Width = window.Sdl.Width, Height = window.Sdl.Height };
            renderer.RenderWorld.Set(surfaceInfo);
            Logger.Info($"Initial render surface: {surfaceInfo.Width}x{surfaceInfo.Height}");

            // Handle resize -> update surface info immediately (cheap metadata),
            // but only *flag* the expensive swapchain rebuild for later.
            window.ResizeEvent += (w, h) =>
            {
                if (w > 0 && h > 0)
                {
                    Logger.Debug($"Window resized to {w}x{h} - updating render surface info (rebuild deferred).");
                    renderer.RenderWorld.Set(new RenderSurfaceInfo { Width = w, Height = h });
                    pendingRendererResize = true;
                    lastResizeTick = Environment.TickCount64;
                }
            };
        }
        else
        {
            Logger.Info("SdlPlugin: Non-Vulkan backend - Vulkan renderer initialization skipped.");
        }

        // Build the base render graph now so "main_pass" exists before any Stage.Startup
        // system tries to attach nodes/edges to it.
        if (renderer.Context.IsInitialized)
            renderer.Initialize(app.World);

        // GPU submission runs in Stage.Last so it executes after every Stage.Render system
        // has emitted ImGui UI / per-frame data. Also resolves debounced resize.
        app.AddSystem(Stage.Last, new SystemDescriptor(world =>
            {
                if (!world.TryGetResource<Renderer>(out var r) || !r.Context.IsInitialized)
                    return;
            
                // -- Resolve debounced resize --
                if (pendingRendererResize && (Environment.TickCount64 - lastResizeTick) >= ResizeDebounceMs)
                {
                    pendingRendererResize = false;
                    Logger.Info("Debounce elapsed - committing renderer resize (swapchain + allocator + camera)...");
                    r.Context.OnResize();
                }
            
                r.RenderFrame(world);
            }, "SdlPlugin.Render")
            .MainThreadOnly()
            .Read<ClearColor>()
            .Read<EcsWorld>()
            .Write<Renderer>());

        // Ensure disposal at app exit (Cleanup stage)
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(world =>
            {
                if (world.TryGetResource<Renderer>(out var r))
                {
                    Logger.Info("SdlPlugin: Cleanup stage - disposing Renderer...");
                    r.Dispose();
                    world.RemoveResource<Renderer>();
                }
            }, "SdlPlugin.Cleanup")
            .MainThreadOnly()
            .Write<Renderer>());

        Logger.Info("SdlPlugin: Build complete.");
    }
}