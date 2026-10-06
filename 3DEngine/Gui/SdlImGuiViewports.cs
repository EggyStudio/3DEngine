using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using SDL3;

namespace Engine;

/// <summary>
/// Dear ImGui's viewports over SDL windows and Vulkan swapchains, so an ImGui window dragged
/// outside the main window is given a window of its own on the desktop.
/// </summary>
/// <remarks>
/// <para>
/// Off by default. A program turns it on with ImGui's own flag,
/// <c>ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable</c>, and a running session with
/// <c>imgui.viewports true</c>. With it on, ImGui's positions are the desktop's rather than the main
/// window's, as ImGui has them, so a window placed by position adds the main viewport's.
/// </para>
/// <para>
/// ImGui asks for a window and a swapchain in <c>UpdatePlatformWindows</c>, which the ImGui render
/// node calls once the main window's pass has ended, and the node draws each viewport into its own
/// swapchain there, presented with the main window's by the frame's one present, so the windows
/// show the same frame. Each callback is called from ImGui's native code on the main thread, where an
/// exception would end the process, so each catches what it throws and logs it.
/// </para>
/// <para>
/// It is installed only where SDL can place a window on the desktop, on X11, Windows and macOS, and
/// on SDL's offscreen driver, which keeps where its windows are. Wayland lets no program read or set
/// where its windows are, so there every ImGui window stays inside the main one, as ImGui's own SDL
/// backend has it.
/// </para>
/// </remarks>
internal static unsafe class SdlImGuiViewports
{
    private static readonly ILogger Logger = Log.Category("Engine.ImGui");

    /// <summary>The SDL video drivers on which SDL can place a window, and so viewports are offered.</summary>
    internal static readonly string[] Drivers = ["x11", "windows", "cocoa", "offscreen"];

    private static nint _main;
    private static bool _hidden;

    /// <summary>The device the viewports' swapchains are made on, which the ImGui render node gives.</summary>
    internal static GraphicsDevice? Device { get; set; }

    /// <summary>Whether ImGui is given windows of its own here, which needs a driver SDL can place a window on.</summary>
    internal static bool Installed { get; private set; }

    /// <summary>Whether ImGui takes windows out of the main one: installed, and turned on by the program.</summary>
    internal static bool Enabled => Installed && ImGui.GetCurrentContext() != IntPtr.Zero
                                              && (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0;

    /// <summary>
    /// Gives ImGui the main window as its main viewport's and, on a driver SDL can place a window on,
    /// the callbacks that make, move and close a viewport's window and swapchain.
    /// </summary>
    /// <param name="window">The main SDL window.</param>
    /// <param name="hidden">Whether the app's windows are never shown, as a session's under <c>--hidden</c>, which a viewport's window keeps to.</param>
    public static void Install(nint window, bool hidden)
    {
        (_main, _hidden) = (window, hidden);
        var main = ImGui.GetMainViewport();
        main.PlatformHandle = window;
        var driver = SDL.GetCurrentVideoDriver() ?? "";
        Installed = Drivers.Contains(driver);
        if (!Installed)
        {
            Logger.Info($"ImGui keeps its windows inside the main one, since SDL's {driver} driver cannot place a window on the desktop.");
            return;
        }

        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.PlatformHasViewports | ImGuiBackendFlags.RendererHasViewports;
        var platform = ImGui.GetPlatformIO();
        platform.Platform_CreateWindow = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, void>)&CreateWindow;
        platform.Platform_DestroyWindow = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, void>)&DestroyWindow;
        platform.Platform_ShowWindow = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, void>)&ShowWindow;
        platform.Platform_SetWindowPos = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, Vector2, void>)&SetWindowPos;
        platform.Platform_SetWindowSize = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, Vector2, void>)&SetWindowSize;
        platform.Platform_SetWindowFocus = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, void>)&SetWindowFocus;
        platform.Platform_GetWindowFocus = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, byte>)&GetWindowFocus;
        platform.Platform_GetWindowMinimized = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, byte>)&GetWindowMinimized;
        platform.Platform_SetWindowTitle = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, byte*, void>)&SetWindowTitle;
        platform.Platform_SetWindowAlpha = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, float, void>)&SetWindowAlpha;
        // A position or a size returned by value crosses from C++ differently on each system, so
        // cimgui's setters take a callback that writes it through a pointer instead.
        ImGuiNative.ImGuiPlatformIO_Set_Platform_GetWindowPos(platform.NativePtr, (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, Vector2*, void>)&GetWindowPos);
        ImGuiNative.ImGuiPlatformIO_Set_Platform_GetWindowSize(platform.NativePtr, (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, Vector2*, void>)&GetWindowSize);
        platform.Renderer_CreateWindow = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, void>)&CreateSurface;
        platform.Renderer_DestroyWindow = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, void>)&DestroySurface;
        platform.Renderer_SetWindowSize = (nint)(delegate* unmanaged[Cdecl]<ImGuiViewport*, Vector2, void>)&ResizeSurface;
        UpdateMonitors();
        // A click on a viewport's window that is not focused acts as well as focusing it, as one on
        // the main window does.
        SDL.SetHint(SDL.Hints.MouseFocusClickthrough, "1");
        Logger.Info($"ImGui viewports are offered on SDL's {driver} driver, off until ImGuiConfigFlags.ViewportsEnable is set.");
    }

    // The ImGui frame UpdatePlatformWindows was last called after.
    private static int _updated = -1;

    // Whether the program had viewports on at ImGui's first frame.
    private static bool _onAtFirstFrame;

    /// <summary>
    /// Calls ImGui's <c>NewFrame</c>, holding back a frame a program's turning viewports on between
    /// ImGui's first two frames, which ImGui stops the process for, since the windows it would have
    /// read from its settings file are placed by then.
    /// </summary>
    /// <remarks>
    /// A program sets the flag where it starts, which may be in its first frame as easily as before
    /// it, and from the third frame on ImGui takes it as it comes.
    /// </remarks>
    public static void NewFrame()
    {
        var io = ImGui.GetIO();
        var on = (io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0;
        var frame = ImGui.GetFrameCount();
        if (frame == 0) _onAtFirstFrame = on;
        var held = frame == 1 && on && !_onAtFirstFrame;
        if (held) io.ConfigFlags &= ~ImGuiConfigFlags.ViewportsEnable;
        ImGui.NewFrame();
        if (held) io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
    }

    /// <summary>
    /// Makes, moves and closes the viewports' windows for the frame ImGui has ended, once a frame,
    /// which ImGui asks of a backend that offers viewports whether a program has them on or not.
    /// </summary>
    public static void Update()
    {
        if (!Installed || ImGui.GetCurrentContext() == IntPtr.Zero || _updated == ImGui.GetFrameCount()) return;
        _updated = ImGui.GetFrameCount();
        ImGui.UpdatePlatformWindows();
    }

    /// <summary>Closes every viewport's window and swapchain, while the device is still there to destroy them on.</summary>
    public static void DestroyWindows()
    {
        if (!Installed || ImGui.GetCurrentContext() == IntPtr.Zero) return;
        ImGui.DestroyPlatformWindows();
        // ImGui forgets the main viewport's window with the others, which it is given again, so
        // viewports turned on later still measure from where the main window is.
        var main = ImGui.GetMainViewport();
        main.PlatformHandle = _main;
    }

    /// <summary>Takes the callbacks away with the main window, closing what viewports are left.</summary>
    public static void Uninstall()
    {
        DestroyWindows();
        (Installed, _main, Device, _updated) = (false, 0, null, -1);
    }

    /// <summary>The swapchain of a viewport's window, made where it has none yet and the device is known.</summary>
    internal static GraphicsDevice.WindowSurface? SurfaceOf(ImGuiViewportPtr viewport)
    {
        if (viewport.RendererUserData == 0 && viewport.PlatformHandle != 0 && Device is not null)
            CreateSurfaceOf(viewport.NativePtr);
        return Made(viewport);
    }

    /// <summary>The swapchain of a viewport's window where it has been made, and null where not.</summary>
    internal static GraphicsDevice.WindowSurface? Made(ImGuiViewportPtr viewport) =>
        viewport.RendererUserData == 0 ? null : GCHandle.FromIntPtr(viewport.RendererUserData).Target as GraphicsDevice.WindowSurface;

    /// <summary>The desktop position of the SDL window an event came from, which ImGui's positions are measured from with viewports on.</summary>
    internal static Vector2 PositionOf(uint windowId)
    {
        var window = SDL.GetWindowFromID(windowId);
        if (window == 0 || !SDL.GetWindowPosition(window, out var x, out var y)) return Vector2.Zero;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Tells ImGui of a viewport's window closed, moved or resized by the desktop, and of the
    /// displays changing, from an SDL event.
    /// </summary>
    internal static void Observe(SDL.Event e)
    {
        if (!Installed || ImGui.GetCurrentContext() == IntPtr.Zero) return;
        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.WindowCloseRequested or SDL.EventType.WindowMoved or SDL.EventType.WindowResized:
                var window = SDL.GetWindowFromID(e.Window.WindowID);
                if (window == 0 || window == _main) return;
                var viewport = ImGui.FindViewportByPlatformHandle(window);
                if (viewport.NativePtr == null) return;
                if ((SDL.EventType)e.Type == SDL.EventType.WindowCloseRequested) viewport.PlatformRequestClose = true;
                else if ((SDL.EventType)e.Type == SDL.EventType.WindowMoved) viewport.PlatformRequestMove = true;
                else viewport.PlatformRequestResize = true;
                break;
            case SDL.EventType.DisplayAdded or SDL.EventType.DisplayRemoved or SDL.EventType.DisplayMoved
                or SDL.EventType.DisplayContentScaleChanged or SDL.EventType.DisplayCurrentModeChanged:
                UpdateMonitors();
                break;
        }
    }

    // The displays, which ImGui keeps a viewport's window on, in the list it frees itself, so it is
    // allocated with ImGui's own allocator.
    private static void UpdateMonitors()
    {
        var displays = SDL.GetDisplays(out var count) ?? [];
        var platform = ImGui.GetPlatformIO();
        if (platform.NativePtr->Monitors.Data != 0) ImGuiNative.igMemFree((void*)platform.NativePtr->Monitors.Data);
        var monitors = (ImGuiPlatformMonitor*)ImGuiNative.igMemAlloc((uint)(Math.Max(1, count) * sizeof(ImGuiPlatformMonitor)));
        for (int i = 0; i < count; i++)
        {
            SDL.GetDisplayBounds(displays[i], out var bounds);
            if (!SDL.GetDisplayUsableBounds(displays[i], out var usable)) usable = bounds;
            var scale = SDL.GetDisplayContentScale(displays[i]);
            monitors[i] = new ImGuiPlatformMonitor
            {
                MainPos = new Vector2(bounds.X, bounds.Y),
                MainSize = new Vector2(bounds.W, bounds.H),
                WorkPos = new Vector2(usable.X, usable.Y),
                WorkSize = new Vector2(usable.W, usable.H),
                DpiScale = scale > 0 ? scale : 1,
                PlatformHandle = (void*)displays[i],
            };
        }
        platform.NativePtr->Monitors = new ImVector(count, Math.Max(1, count), (nint)monitors);
    }

    private static nint WindowOf(ImGuiViewport* viewport) => (nint)viewport->PlatformHandle;

    // -- The platform's callbacks

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CreateWindow(ImGuiViewport* native)
    {
        try
        {
            var viewport = new ImGuiViewportPtr(native);
            var flags = SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan
                        | (SDL.GetWindowFlags(_main) & SDL.WindowFlags.HighPixelDensity)
                        | ((viewport.Flags & ImGuiViewportFlags.NoDecoration) != 0 ? SDL.WindowFlags.Borderless : SDL.WindowFlags.Resizable)
                        | ((viewport.Flags & ImGuiViewportFlags.NoTaskBarIcon) != 0 ? SDL.WindowFlags.Utility : 0)
                        | ((viewport.Flags & ImGuiViewportFlags.TopMost) != 0 ? SDL.WindowFlags.AlwaysOnTop : 0);
            var window = SDL.CreateWindow("No Title Yet", Math.Max(1, (int)viewport.Size.X), Math.Max(1, (int)viewport.Size.Y), flags);
            if (window == 0)
            {
                Logger.Warn($"An ImGui viewport's window could not be made: {SDL.GetError()}");
                return;
            }
            SDL.SetWindowPosition(window, (int)viewport.Pos.X, (int)viewport.Pos.Y);
            // The handle names the window to ImGui, and the user data says it is one made here, which
            // the main window's is not.
            viewport.PlatformHandle = window;
            viewport.PlatformUserData = window;
        }
        catch (Exception error)
        {
            Logger.Error($"Making an ImGui viewport's window failed: {error}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void DestroyWindow(ImGuiViewport* native)
    {
        try
        {
            var window = (nint)native->PlatformUserData;
            if (window != 0) SDL.DestroyWindow(window);
            native->PlatformUserData = null;
            native->PlatformHandle = null;
        }
        catch (Exception error)
        {
            Logger.Error($"Closing an ImGui viewport's window failed: {error}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ShowWindow(ImGuiViewport* native)
    {
        try
        {
            if (_hidden) return;
            SDL.SetHint(SDL.Hints.WindowActivateWhenShown, (native->Flags & ImGuiViewportFlags.NoFocusOnAppearing) != 0 ? "0" : "1");
            SDL.ShowWindow(WindowOf(native));
        }
        catch (Exception error)
        {
            Logger.Error($"Showing an ImGui viewport's window failed: {error}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetWindowPos(ImGuiViewport* native, Vector2 position)
    {
        try { SDL.SetWindowPosition(WindowOf(native), (int)position.X, (int)position.Y); }
        catch (Exception error) { Logger.Error($"Moving an ImGui viewport's window failed: {error}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void GetWindowPos(ImGuiViewport* native, Vector2* position)
    {
        try
        {
            SDL.GetWindowPosition(WindowOf(native), out var x, out var y);
            *position = new Vector2(x, y);
        }
        catch (Exception error)
        {
            *position = Vector2.Zero;
            Logger.Error($"Reading where an ImGui viewport's window is failed: {error}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetWindowSize(ImGuiViewport* native, Vector2 size)
    {
        try { SDL.SetWindowSize(WindowOf(native), Math.Max(1, (int)size.X), Math.Max(1, (int)size.Y)); }
        catch (Exception error) { Logger.Error($"Sizing an ImGui viewport's window failed: {error}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void GetWindowSize(ImGuiViewport* native, Vector2* size)
    {
        try
        {
            SDL.GetWindowSize(WindowOf(native), out var width, out var height);
            *size = new Vector2(width, height);
        }
        catch (Exception error)
        {
            *size = Vector2.Zero;
            Logger.Error($"Reading an ImGui viewport's window's size failed: {error}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetWindowFocus(ImGuiViewport* native)
    {
        try { SDL.RaiseWindow(WindowOf(native)); }
        catch (Exception error) { Logger.Error($"Focusing an ImGui viewport's window failed: {error}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte GetWindowFocus(ImGuiViewport* native)
    {
        try { return (SDL.GetWindowFlags(WindowOf(native)) & SDL.WindowFlags.InputFocus) != 0 ? (byte)1 : (byte)0; }
        catch (Exception) { return 0; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte GetWindowMinimized(ImGuiViewport* native)
    {
        try { return (SDL.GetWindowFlags(WindowOf(native)) & SDL.WindowFlags.Minimized) != 0 ? (byte)1 : (byte)0; }
        catch (Exception) { return 0; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetWindowTitle(ImGuiViewport* native, byte* title)
    {
        try { SDL.SetWindowTitle(WindowOf(native), Marshal.PtrToStringUTF8((nint)title) ?? ""); }
        catch (Exception error) { Logger.Error($"Naming an ImGui viewport's window failed: {error}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void SetWindowAlpha(ImGuiViewport* native, float alpha)
    {
        try { SDL.SetWindowOpacity(WindowOf(native), alpha); }
        catch (Exception error) { Logger.Error($"Setting an ImGui viewport's window's opacity failed: {error}"); }
    }

    // -- The renderer's callbacks

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void CreateSurface(ImGuiViewport* native)
    {
        try
        {
            CreateSurfaceOf(native);
        }
        catch (Exception error)
        {
            Logger.Error($"Making an ImGui viewport's swapchain failed: {error}");
        }
    }

    // The surface and swapchain of the viewport's window, kept in its renderer data by a handle,
    // where the device is known. One that cannot be made leaves the window undrawn.
    private static void CreateSurfaceOf(ImGuiViewport* native)
    {
        var window = WindowOf(native);
        if (Device is not { } device || window == 0 || native->RendererUserData != null) return;
        try
        {
            var surface = device.CreateWindowSurface(window);
            native->RendererUserData = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(surface));
        }
        catch (InvalidOperationException error)
        {
            Logger.Warn($"An ImGui viewport's window is left undrawn, since its swapchain could not be made: {error.Message}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void DestroySurface(ImGuiViewport* native)
    {
        try
        {
            if (native->RendererUserData == null) return;
            var handle = GCHandle.FromIntPtr((nint)native->RendererUserData);
            if (handle.Target is GraphicsDevice.WindowSurface surface) Device?.DestroyWindowSurface(surface);
            handle.Free();
            native->RendererUserData = null;
        }
        catch (Exception error)
        {
            Logger.Error($"Destroying an ImGui viewport's swapchain failed: {error}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ResizeSurface(ImGuiViewport* native, Vector2 size)
    {
        try
        {
            if (native->RendererUserData != null && GCHandle.FromIntPtr((nint)native->RendererUserData).Target is GraphicsDevice.WindowSurface surface)
                GraphicsDevice.ResizeWindowSurface(surface);
        }
        catch (Exception error)
        {
            Logger.Error($"Resizing an ImGui viewport's swapchain failed: {error}");
        }
    }
}
