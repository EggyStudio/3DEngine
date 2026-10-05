using SDL3;

namespace Engine;

/// <summary>
/// Commands that resize, minimize, restore and move the window, or, in an offscreen run, the
/// images the device draws into, so the path a resize takes is driven from <c>./e3d</c> with no
/// desktop to drag a window on.
/// </summary>
internal static class WindowCommands
{
    [Command("window.size", "Resizes the window, or the images an offscreen run draws into: window.size <width> <height>")]
    internal static string Size(int width, int height)
    {
        var world = ConsoleHost.World!;
        if (world.TryGetResource<AppWindow>(out var window))
            SDL.SetWindowSize(window.Sdl.Window, Math.Max(1, width), Math.Max(1, height));
        else if (world.TryGetResource<OffscreenSurface>(out var surface))
        {
            surface.Size = ((uint)Math.Max(0, width), (uint)Math.Max(0, height));
            world.TryGetResource<SurfaceResize>(out var resize);
            resize?.Request(width, height);
        }
        else return Refuse("window.size");
        return $"resizing to {width}x{height}";
    }

    [Command("window.vsync", "Turns vsync on or off, which makes the swapchain again on the next frame: window.vsync <on>")]
    internal static string Vsync(bool on)
    {
        if (!ConsoleHost.World!.TryGetResource<SurfaceResize>(out var resize)) return Refuse("window.vsync");
        resize.RequestVsync(on);
        return on ? "vsync on" : "vsync off";
    }

    [Command("window.minimize", "Minimizes the window, or makes an offscreen run zero across, so no frame is drawn until window.restore")]
    internal static string Minimize()
    {
        var world = ConsoleHost.World!;
        if (world.TryGetResource<AppWindow>(out var window)) SDL.MinimizeWindow(window.Sdl.Window);
        else if (world.TryGetResource<OffscreenSurface>(out var surface))
        {
            Remembered = surface.Size;
            surface.Size = (0, 0);
            world.TryGetResource<SurfaceResize>(out var resize);
            resize?.Request(0, 0);
        }
        else return Refuse("window.minimize");
        return "minimized";
    }

    [Command("window.restore", "Restores a minimized window, or an offscreen run to its size before window.minimize")]
    internal static string Restore()
    {
        var world = ConsoleHost.World!;
        if (world.TryGetResource<AppWindow>(out var window)) SDL.RestoreWindow(window.Sdl.Window);
        else if (world.TryGetResource<OffscreenSurface>(out var surface))
        {
            if (surface.Size.Width == 0 || surface.Size.Height == 0) surface.Size = Remembered;
            world.TryGetResource<SurfaceResize>(out var resize);
            resize?.Request((int)surface.Size.Width, (int)surface.Size.Height);
        }
        else return Refuse("window.restore");
        return "restored";
    }

    [Command("window.position", "Moves the window's top left corner on the desktop: window.position <x> <y>")]
    internal static string Position(int x, int y)
    {
        if (!ConsoleHost.World!.TryGetResource<AppWindow>(out var window)) return Refuse("window.position");
        SDL.SetWindowPosition(window.Sdl.Window, x, y);
        return $"moved to {x}, {y}";
    }

    [Command("window.monitor", "Moves the window to a monitor, centered on it, by its number from 0: window.monitor <monitor>")]
    internal static string Monitor(int monitor)
    {
        if (!ConsoleHost.World!.TryGetResource<AppWindow>(out var window)) return Refuse("window.monitor");
        var displays = SDL.GetDisplays(out _) ?? [];
        if (monitor < 0 || monitor >= displays.Length)
        {
            var refusal = $"there is no monitor {monitor}, the monitors being numbered from 0 to {displays.Length - 1}";
            ConsoleHost.Fail("BAD_ARGUMENT", refusal);
            return refusal;
        }
        var centered = (int)(SDL.WindowPosCenteredMask | displays[monitor]);
        SDL.SetWindowPosition(window.Sdl.Window, centered, centered);
        return $"moved to monitor {monitor} of {displays.Length}";
    }

    // The size an offscreen run had before it was minimized, which window.restore gives it back.
    private static (uint Width, uint Height) Remembered = (800, 450);

    private static string Refuse(string command)
    {
        var refusal = $"{command}: this program has no window and draws no frames";
        ConsoleHost.Fail("NO_WINDOW", refusal);
        return refusal;
    }
}
