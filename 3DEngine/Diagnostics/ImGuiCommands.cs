using System.Globalization;
using ImGuiNET;
using SDL3;

namespace Engine;

/// <summary>
/// Commands that read and set how Dear ImGui is drawn, so its windows of their own are turned on in
/// a running program and looked at from <c>./e3d</c>.
/// </summary>
internal static class ImGuiCommands
{
    [Command("imgui.viewports", "Lets ImGui's windows leave the main one for windows of their own, or keeps them in, or with no value lists ImGui's viewports: imgui.viewports [on]")]
    internal static string Viewports(string on = "")
    {
        if (ImGui.GetCurrentContext() == IntPtr.Zero) return Refuse("NO_IMGUI", "this program draws no ImGui");
        if (!SdlImGuiViewports.Installed)
            return Refuse("NO_VIEWPORTS", $"ImGui keeps its windows inside the main one here, since SDL's {SDL.GetCurrentVideoDriver()} driver cannot place a window on the desktop");

        var io = ImGui.GetIO();
        if (on.Length > 0)
        {
            bool? value = on.ToLowerInvariant() switch { "on" or "true" or "1" => true, "off" or "false" or "0" => false, _ => null };
            if (value is not { } turned) return Refuse("BAD_ARGUMENT", $"'{on}' is not on or off");
            io.ConfigFlags = turned ? io.ConfigFlags | ImGuiConfigFlags.ViewportsEnable : io.ConfigFlags & ~ImGuiConfigFlags.ViewportsEnable;
            return turned ? "viewports on" : "viewports off";
        }

        // Each viewport with where it is, its size, and its window and swapchain where it has them.
        var viewports = ImGui.GetPlatformIO().Viewports;
        var lines = new List<string> { (io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0 ? "viewports on" : "viewports off" };
        for (int i = 0; i < viewports.Size; i++)
        {
            var viewport = viewports[i];
            var line = string.Create(CultureInfo.InvariantCulture,
                $"{(i == 0 ? "main" : $"viewport {i}")} at {viewport.Pos.X:0},{viewport.Pos.Y:0} size {viewport.Size.X:0}x{viewport.Size.Y:0}");
            if (viewport.PlatformHandle != 0)
            {
                var flags = SDL.GetWindowFlags(viewport.PlatformHandle);
                line += $" window {SDL.GetWindowID(viewport.PlatformHandle)} {((flags & SDL.WindowFlags.Hidden) != 0 ? "hidden" : "shown")}";
            }
            if (SdlImGuiViewports.Made(viewport) is { } surface) line += $" swapchain {surface.Size.Width}x{surface.Size.Height}";
            lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    [Command("imgui.shot", "Writes the next frame of one of ImGui's windows of its own to a PNG, by its number in imgui.viewports: imgui.shot <viewport> <path>")]
    internal static string Shot(int viewport, string path)
    {
        if (ImGui.GetCurrentContext() == IntPtr.Zero) return Refuse("NO_IMGUI", "this program draws no ImGui", "imgui.shot");
        var viewports = ImGui.GetPlatformIO().Viewports;
        if (viewport < 1 || viewport >= viewports.Size)
            return Refuse("BAD_ARGUMENT", $"there is no viewport {viewport} of a window of its own, of {viewports.Size - 1}", "imgui.shot");
        if (SdlImGuiViewports.Made(viewports[viewport]) is not { } surface)
            return Refuse("NO_RENDERER", $"viewport {viewport} has no swapchain to capture", "imgui.shot");

        var full = Path.GetFullPath(path);
        string? answer = null;
        try
        {
            GraphicsDevice.RequestWindowCapture(surface, (rgba, width, height) =>
            {
                try
                {
                    PngWriter.Write(full, rgba, width, height);
                    answer = $"captured viewport {viewport}, {width}x{height}, to {full}";
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    answer = $"viewport {viewport}'s frame could not be written to {full}: {error.Message}";
                }
            });
        }
        catch (NotSupportedException error)
        {
            return Refuse("NO_RENDERER", error.Message, "imgui.shot");
        }
        ConsoleHost.Later(() => answer);
        return "";
    }

    private static string Refuse(string code, string why, string command = "imgui.viewports")
    {
        ConsoleHost.Fail(code, $"{command}: {why}");
        return $"{command}: {why}";
    }
}
