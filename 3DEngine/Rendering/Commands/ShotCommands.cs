namespace Engine;

/// <summary>The console command that writes the frame being drawn to a PNG file.</summary>
internal static class ShotCommands
{
    [Command("shot", "Writes the frame being drawn to a PNG file and answers once it is written, with the frame's number: shot <path>")]
    internal static string Shot(string path)
    {
        var full = Path.GetFullPath(path);
        // The frame drawn after this command is the one the time counted at the top of it, so
        // another program run to that many frames draws the same one.
        var frame = ConsoleHost.Time.FrameCount;
        string? outcome = null;
        var written = false;
        if (Screenshots.Request(ConsoleHost.World!, full, result => { outcome = result; written = true; }) is { } refusal)
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
