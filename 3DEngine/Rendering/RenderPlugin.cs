namespace Engine;

/// <summary>
/// Composition-root plugin that wires the Vulkan renderer to the platform window.
/// Creates the <see cref="Renderer"/>, registers extract/prepare systems,
/// initializes the Vulkan graphics context, and handles debounced resize.
/// </summary>
/// <seealso cref="Renderer"/>
/// <seealso cref="RendererContext"/>
/// <seealso cref="AppWindowPlugin"/>
internal sealed class RenderPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Renderer");

    /// <summary>
    /// The seconds of frames after the last resize asked for before the swapchain, the allocator and
    /// the camera are made again, during which the Vulkan lazy path (<c>VK_ERROR_OUT_OF_DATE_KHR</c>)
    /// rebuilds the swapchain alone if it must.
    /// </summary>
    /// <remarks>
    /// Counted in the frames' own time, so a stepped clock steps past it as it steps everything
    /// else, and a resize lands on the same frame however fast the machine runs.
    /// </remarks>
    private const double ResizeDebounceSeconds = 0.15;

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

    /// <summary>
    /// Extract system that hands the flat API's draw lists and stores (<see cref="DrawList"/>,
    /// <see cref="ModelDrawList"/>, <see cref="TextureStore"/>, <see cref="MeshStore"/>) to the
    /// render world. They are handed over rather than copied, because the frame is rendered on the
    /// main thread in <see cref="Stage.Last"/>, which clears the lists once it has drawn them, and the
    /// stores guard themselves with a lock.
    /// </summary>
    private sealed class DrawListExtract : IExtractSystem
    {
        /// <inheritdoc />
        public void Run(World world, RenderWorld renderWorld)
        {
            if (world.TryGetResource<DrawList>(out var drawList))
                renderWorld.Set(drawList);
            if (world.TryGetResource<TextureStore>(out var textures))
                renderWorld.Set(textures);
            if (world.TryGetResource<ModelDrawList>(out var models))
                renderWorld.Set(models);
            if (world.TryGetResource<MeshStore>(out var meshes))
                renderWorld.Set(meshes);
            if (world.TryGetResource<ShaderStore>(out var shaders))
                renderWorld.Set(shaders);
            if (world.TryGetResource<ShaderBufferStore>(out var buffers))
                renderWorld.Set(buffers);
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

        Logger.Info("RenderPlugin: Creating Renderer and wiring extract/prepare systems...");
        var renderer = new Renderer(new RendererContext());
        renderer.AddExtractSystem(new ClearColorExtract());
        renderer.AddExtractSystem(new DrawListExtract());
        renderer.AddExtractSystem(new CameraExtract());
        renderer.AddExtractSystem(new ParticleExtract());
        renderer.AddPrepareSystem(new GpuTexturesPrepare());
        renderer.AddPrepareSystem(new GpuMeshesPrepare());
        app.World.InsertResource(renderer);
        Logger.Debug("Renderer resource registered with extract and prepare systems.");

        app.World.InitResource<DrawList>();
        app.World.InitResource<ModelDrawList>();
        app.World.InitResource<TextureStore>();
        app.World.InitResource<MeshStore>();
        app.World.InitResource<ShaderStore>();
        app.World.InitResource<ShaderBufferStore>();
        app.AddSystem(Stage.Render, new SystemDescriptor(MeshEntityDraws.Run, "RenderPlugin.MeshEntityDraws").MainThreadOnly());
        app.AddSystem(Stage.Render, new SystemDescriptor(AnimatedModelDraws.Run, "RenderPlugin.AnimatedModelDraws").MainThreadOnly());

        // An offscreen run has no window either, and renders into the device's own images.
        if (cfg.Offscreen && !app.World.ContainsResource<AppWindow>() && cfg.Graphics == GraphicsBackend.Vulkan)
            InitializeOffscreen(app, renderer, cfg);

        // A headless run has no window, so the renderer is made and never initialized, and the
        // render system below returns at once every frame.
        if (!app.World.TryGetResource<AppWindow>(out var window) && !renderer.Context.IsInitialized)
        {
            Logger.Info("RenderPlugin: No window (headless run), so the renderer stays uninitialized.");
            app.AddSystem(Stage.Cleanup, new SystemDescriptor(world => world.RemoveResource<Renderer>(), "RenderPlugin.Cleanup").MainThreadOnly());
            return;
        }

        // The resize the window or an offscreen run asks for, carried out by the render system
        // once none has come for a moment.
        var resize = new SurfaceResize(renderer, cfg.Vsync);
        app.World.InsertResource(resize);

        if (window is null)
        {
            // Offscreen: initialized above, with a fixed size and no resizes to follow.
        }
        else if (cfg.Graphics == GraphicsBackend.Vulkan)
        {
            Logger.Info("RenderPlugin: Vulkan backend selected, initializing the graphics context against the SDL window...");
            // Grab the ISurfaceSource that AppWindowPlugin inserted
            var surface = app.World.Resource<ISurfaceSource>();
            renderer.Context.Initialize(surface, cfg.WindowData.Title, cfg.Samples, cfg.Vsync);

            // Seed RenderSurfaceInfo with current window size
            var surfaceInfo = new RenderSurfaceInfo { Width = window.Sdl.Width, Height = window.Sdl.Height };
            renderer.RenderWorld.Set(surfaceInfo);
            Logger.Info($"Initial render surface: {surfaceInfo.Width}x{surfaceInfo.Height}");

            // Handle resize -> update surface info immediately (cheap metadata),
            // but only *flag* the expensive swapchain rebuild for later.
            window.ResizeEvent += (w, h) =>
            {
                if (w <= 0 || h <= 0) return;
                Logger.Debug($"Window resized to {w}x{h}, updating the render surface (rebuild deferred).");
                resize.Request(w, h);
            };
        }
        else
        {
            Logger.Info("RenderPlugin: Not a Vulkan backend, so the Vulkan renderer is not initialized.");
        }

        // Build the base render graph now so "main_pass" exists before any Stage.Startup
        // system tries to attach nodes/edges to it.
        if (renderer.Context.IsInitialized)
            renderer.Initialize(app.World);

        // GPU submission runs in Stage.Last so it executes after every Stage.Render system
        // has emitted ImGui UI / per-frame data. Also resolves debounced resize.
        app.AddSystem(Stage.Last, new SystemDescriptor(world =>
            {
                try
                {
                    Render(world);
                }
                finally
                {
                    // Cleared once the frame is rendered, or would have been, rather than as the next
                    // begins, so what a program draws between frames, into a render texture before
                    // BeginDrawing as raylib's examples do, is drawn in the next frame.
                    world.Resource<DrawList>().Clear();
                    world.Resource<ModelDrawList>().Clear();
                }
            }, "RenderPlugin.Render")
            .MainThreadOnly()
            .Read<ClearColor>()
            .Read<EcsWorld>()
            .Write<Renderer>()
            .Write<DrawList>()
            .Write<ModelDrawList>());

        void Render(World world)
        {
            if (!world.TryGetResource<Renderer>(out var r) || !r.Context.IsInitialized)
                return;
        
            // A minimized window, or an offscreen run made zero across, has nothing to draw
            // into, so the frame's GPU work is skipped until it has a size again.
            if (world.TryGetResource<AppWindow>(out var shown) && shown.Minimized) return;
            if (resize.Empty) return;

            // -- Resolve debounced resize
            if (resize.Pending && world.TryGetResource<Time>(out var time)) resize.Waited += time.DeltaSeconds;
            if (resize.Pending && resize.Waited >= ResizeDebounceSeconds)
            {
                resize.Pending = false;
                if (resize.VsyncChanged)
                {
                    resize.VsyncChanged = false;
                    r.Context.SetVsync(resize.Vsync);
                }
                Logger.Info("Debounce elapsed, resizing the renderer (swapchain, allocator and camera)...");
                r.Context.OnResize();
            }
        
            r.RenderFrame(world);
        }

        // Ensure disposal at app exit (Cleanup stage)
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(world =>
            {
                if (world.TryGetResource<Renderer>(out var r))
                {
                    Logger.Info("RenderPlugin: Cleanup stage, disposing the renderer...");
                    r.Dispose();
                    world.RemoveResource<Renderer>();
                }
            }, "RenderPlugin.Cleanup")
            .MainThreadOnly()
            .Write<Renderer>());

        Logger.Info("RenderPlugin: Build complete.");
    }

    // Brings the renderer up with no window, drawing into images the device makes of the window
    // size the config asks for. A machine with no Vulkan device leaves it down, as a headless
    // run is, and says so.
    private static void InitializeOffscreen(App app, Renderer renderer, Config cfg)
    {
        var (width, height) = ((uint)Math.Max(1, cfg.WindowData.Width), (uint)Math.Max(1, cfg.WindowData.Height));
        try
        {
            var surface = new OffscreenSurface(width, height);
            renderer.Context.Initialize(surface, cfg.WindowData.Title, cfg.Samples, cfg.Vsync);
            // Kept where window.size and window.minimize find it, to resize as a window would be.
            app.World.InsertResource(surface);
            renderer.RenderWorld.Set(new RenderSurfaceInfo { Width = (int)width, Height = (int)height });
            Logger.Info($"RenderPlugin: Offscreen run, rendering {width}x{height} frames with no window.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Vortice.Vulkan.VkException or DllNotFoundException)
        {
            Logger.Warn($"RenderPlugin: Offscreen rendering could not start, so nothing is drawn: {ex.Message}");
        }
    }
}

/// <summary>
/// A resize of what the renderer draws into, asked for by the window or by an offscreen run, and
/// carried out by the render system once none has come for a moment, so a window dragged larger
/// rebuilds its swapchain once rather than every frame.
/// </summary>
internal sealed class SurfaceResize
{
    private readonly Renderer _renderer;

    internal SurfaceResize(Renderer renderer, bool vsync)
    {
        _renderer = renderer;
        Vsync = vsync;
    }

    /// <summary>Whether a resize waits to be carried out.</summary>
    internal bool Pending { get; set; }

    /// <summary>Whether frames wait for the display's refresh, as asked for last, which the next rebuild carries out.</summary>
    public bool Vsync { get; private set; }

    // A change of vsync not yet carried out.
    internal bool VsyncChanged { get; set; }

    /// <summary>The seconds of frames since the last resize was asked for.</summary>
    internal double Waited { get; set; }

    /// <summary>Whether the surface is zero across or zero high, so nothing is drawn.</summary>
    public bool Empty => _renderer.RenderWorld.TryGet<RenderSurfaceInfo>() is { } info && (info.Width <= 0 || info.Height <= 0);

    /// <summary>Asks for frames to be drawn at <paramref name="width"/> by <paramref name="height"/>, none at all while either is zero.</summary>
    public void Request(int width, int height)
    {
        _renderer.RenderWorld.Set(new RenderSurfaceInfo { Width = Math.Max(0, width), Height = Math.Max(0, height) });
        if (width <= 0 || height <= 0) return;
        Pending = true;
        Waited = 0;
    }

    /// <summary>
    /// Asks for frames to wait for the display's refresh or not, which rebuilds the swapchain with
    /// the present mode for it on the next frame, as a game's settings screen changes it.
    /// </summary>
    public void RequestVsync(bool vsync)
    {
        if (vsync == Vsync) return;
        Vsync = vsync;
        VsyncChanged = true;
        Pending = true;
        // Carried out on the next frame, as nothing is being dragged that more requests follow.
        Waited = double.PositiveInfinity;
    }
}
