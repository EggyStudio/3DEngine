using Diagnostics;

namespace Engine;

/// <summary>The console commands that write the frame being drawn to a PNG file.</summary>
internal static class ShotCommands
{
    [Command("shot", "Writes the frame being drawn, the picture at its own size, to a PNG file and answers once it is written, with the frame's number: shot <path>")]
    internal static string Shot(string path) => Take(path, shown: false);

    [Command("shot.shown", "Writes the frame being drawn as the window shows it to a PNG file, the picture brought onto the window with the interface over it, and answers once it is written: shot.shown <path>")]
    internal static string ShotShown(string path) => Take(path, shown: true);

    private static string Take(string path, bool shown)
    {
        var full = Path.GetFullPath(path);
        // The frame drawn after this command is the one the time counted at the top of it, so
        // another program run to that many frames draws the same one.
        var frame = ConsoleHost.Time.FrameCount;
        string? outcome = null;
        var written = false;
        if (Screenshots.Request(ConsoleHost.World!, full, result => { outcome = result; written = true; }, shown) is { } refusal)
        {
            ConsoleHost.Fail("NO_RENDERER", refusal);
            return refusal;
        }

        ConsoleHost.Later(() =>
        {
            if (!written) return null;
            if (outcome is not null) ConsoleHost.Fail("NO_CAPTURE", outcome);
            return outcome ?? $"captured {full} at frame {frame}";
        });
        return $"capturing {full}";
    }
}
