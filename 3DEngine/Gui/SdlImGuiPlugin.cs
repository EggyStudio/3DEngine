using System.Numerics;
using ImGuiNET;
using SDL3;

namespace Engine;

/// <summary>Sets up ImGui, schedules NewFrame/Render systems, and renders via ImGuiRenderer.</summary>
/// <remarks>
/// <para>
/// Creates the ImGui context, configures dark style and keyboard/gamepad navigation, then
/// registers three per-frame systems:
/// <list type="number">
///   <item><description><see cref="Stage.PreUpdate"/>: sets display size, delta time, framebuffer scale, and calls <c>ImGui.NewFrame()</c>.</description></item>
///   <item><description><see cref="Stage.Render"/>: calls <c>ImGui.Render()</c> and (in SDL renderer mode) clears and presents via SDL.</description></item>
///   <item><description><see cref="Stage.Cleanup"/>: tears down the ImGui context and disposes the SDL renderer backend.</description></item>
/// </list>
/// </para>
/// <para>
/// In Vulkan mode, no <see cref="SdlImGuiRenderer"/> is created - the font atlas is built
/// but not uploaded to an SDL texture; the Vulkan ImGui render node handles GPU upload.
/// </para>
/// <para>
/// Dear ImGui keeps its current context in one variable for the whole process, and a program calls
/// it directly between <c>BeginDrawing</c> and <c>EndDrawing</c>, so two apps using it at once would
/// each draw into whichever context was made last, and two threads building fonts at once crash
/// in native code. One app holds ImGui at a time. Building this plugin for a second app while the
/// first is alive throws, and the first lets go when it shuts down (<see cref="App.Shutdown"/> or
/// <c>CloseWindow</c>).
/// </para>
/// </remarks>
/// <seealso cref="SdlImGuiRenderer"/>
/// <seealso cref="SdlImGuiInput"/>
public sealed class SdlImGuiPlugin : IPlugin
{
    // The app whose context ImGui holds, or null.
    private static App? _holder;
    private static readonly Lock HolderGate = new();

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Another app that has not shut down holds ImGui.</exception>
    public void Build(App app)
    {
        lock (HolderGate)
        {
            if (_holder is not null && !ReferenceEquals(_holder, app))
                throw new InvalidOperationException(
                    "Dear ImGui has one context for the whole process, and another app that has not shut down holds it. " +
                    "Shut that app down (App.Shutdown or CloseWindow) before building another with SdlImGuiPlugin or DefaultPlugins.");
            _holder = app;
        }

        var logger = Log.Category("Engine.ImGui");
        logger.Info("SdlImGuiPlugin: Creating ImGui context...");
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        // Docking lets a window be dragged onto another or onto a dock space, which
        // ImGui.DockSpaceOverViewport makes of the whole window. Viewports, which take ImGui
        // windows out of the game's window, need a platform backend the engine does not have.
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard | ImGuiConfigFlags.NavEnableGamepad | ImGuiConfigFlags.DockingEnable;
        ImGui.StyleColorsDark();

        // ImGui's Vulkan adapter. No-op when the graphics backend isn't Vulkan.
        app.AddPlugin(new VulkanImGuiPlugin());

        var cfg = app.World.Resource<Config>();
        bool isVulkan = cfg.Graphics == GraphicsBackend.Vulkan;

        if (!app.World.TryGetResource<AppWindow>(out var existingWindow))
        {
            BuildHeadless(app, cfg, logger);
            return;
        }

        var sdlWindow = existingWindow.Sdl;
        io.DisplaySize = new Vector2(Math.Max(1, sdlWindow.Width), Math.Max(1, sdlWindow.Height));
        io.DeltaTime = 1f / 60f;

        logger.Info($"ImGui initialized - display size: {sdlWindow.Width}x{sdlWindow.Height}");

        if (!isVulkan)
        {
            logger.Info("ImGui using SDL software renderer backend.");
            var renderer = new SdlImGuiRenderer(sdlWindow.Renderer);
            app.World.InsertResource(renderer);
        }
        else
        {
            // Vulkan mode: build the font atlas but keep CPU pixels - the Vulkan ImGui
            // render node uploads them to the GPU.
            logger.Info("ImGui using Vulkan mode - building font atlas only (no SDL renderer).");
            var io2 = ImGui.GetIO();
            io2.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _, out _);
        }

        var appWindow = app.World.Resource<AppWindow>();
        appWindow.SDLEvent += SdlImGuiInput.ProcessEvent;
        SdlImGuiIme.Install(sdlWindow.Window);

        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(world =>
            {
                var appWindow = world.Resource<AppWindow>();
                var io = ImGui.GetIO();
                int w = Math.Max(1, appWindow.Sdl.Width);
                int h = Math.Max(1, appWindow.Sdl.Height);
                io.DisplaySize = new Vector2(w, h);
                io.DeltaTime = world.Resource<Time>().DeltaSeconds > 0
                    ? (float)world.Resource<Time>().DeltaSeconds
                    : 1f / 60f;
            
                // Set framebuffer scale for Vulkan mode (SDL renderer sets it in NewFrame).
                if (isVulkan)
                {
                    SDL.GetWindowSizeInPixels(appWindow.Sdl.Window, out int pxW, out int pxH);
                    if (w > 0 && h > 0)
                    {
                        io.DisplayFramebufferScale = new Vector2(pxW / (float)w, pxH / (float)h);
                    }
                }
            
                ImGui.NewFrame();
                if (world.TryGetResource<SdlImGuiRenderer>(out var imguiRenderer))
                {
                    imguiRenderer.NewFrame(appWindow.Sdl.Window);
                }
            }, "SdlImGuiPlugin.PreUpdate")
            .MainThreadOnly()
            .Read<AppWindow>()
            .Read<Time>()
            .Write<SdlImGuiRenderer>());

        app.AddSystem(Stage.Render, new SystemDescriptor(world =>
            {
                // Vulkan mode: ImGuiRenderNode (Stage.Last) closes the frame; doing it
                // here would race Stage.Render systems still calling ImGui.Begin/End.
                if (isVulkan)
                    return;

                var sdl = world.Resource<AppWindow>().Sdl;
                var imGuiRenderer = world.Resource<SdlImGuiRenderer>();
                var clear = world.Resource<ClearColor>();
            
                ImGui.Render();
                var drawData = ImGui.GetDrawData();
            
                SDL.SetRenderDrawColor(sdl.Renderer,
                    (byte)(clear.R * 255),
                    (byte)(clear.G * 255),
                    (byte)(clear.B * 255),
                    (byte)(clear.A * 255));
                SDL.RenderClear(sdl.Renderer);
            
                imGuiRenderer.RenderDrawData(drawData);
                SDL.RenderPresent(sdl.Renderer);
            }, "SdlImGuiPlugin.Render")
            .MainThreadOnly()
            .Read<AppWindow>()
            .Read<ClearColor>()
            .Write<SdlImGuiRenderer>());
        
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(world =>
            {
                world.Resource<AppWindow>().SDLEvent -= SdlImGuiInput.ProcessEvent;
            
                if (world.TryGetResource<SdlImGuiRenderer>(out var imguiRenderer))
                {
                    imguiRenderer.Dispose();
                    world.RemoveResource<SdlImGuiRenderer>();
                }
            
                ImGui.DestroyContext();
                Release(app);
            }, "SdlImGuiPlugin.Cleanup")
            .MainThreadOnly()
            .Write<AppWindow>()
            .Write<SdlImGuiRenderer>());
    }

    // A headless run keeps ImGui working for code that calls it between BeginDrawing and
    // EndDrawing: the frame starts in PreUpdate on the configured size and ends in Last, which the
    // render node does when there is a renderer. Nothing is drawn.
    private static void BuildHeadless(App app, Config config, ILogger logger)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(Math.Max(1, config.WindowData.Width), Math.Max(1, config.WindowData.Height));
        io.DeltaTime = 1f / 60f;
        io.Fonts.GetTexDataAsRGBA32(out IntPtr _, out int _, out int _, out _);
        logger.Info($"ImGui initialized without a window - display size: {io.DisplaySize.X}x{io.DisplaySize.Y}");

        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(world =>
            {
                var time = world.Resource<Time>();
                ImGui.GetIO().DeltaTime = time.DeltaSeconds > 0 ? (float)time.DeltaSeconds : 1f / 60f;
                ImGui.NewFrame();
            }, "SdlImGuiPlugin.PreUpdate")
            .MainThreadOnly()
            .Read<Time>());

        app.AddSystem(Stage.Last, new SystemDescriptor(_ => ImGui.EndFrame(), "SdlImGuiPlugin.EndFrame").MainThreadOnly());
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(_ =>
        {
            ImGui.DestroyContext();
            Release(app);
        }, "SdlImGuiPlugin.Cleanup").MainThreadOnly());
    }

    // Lets another app hold ImGui once this one's context is gone.
    private static void Release(App app)
    {
        lock (HolderGate)
            if (ReferenceEquals(_holder, app)) _holder = null;
    }
}
