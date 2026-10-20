using System.Runtime.InteropServices;
using System.Text;

namespace FeatureTest;

/// <summary>
/// The crash log: on an exception nothing caught, a file beside the program, <c>crash-</c> and the
/// time, saying what was thrown, where the player stood, each zone's last status, the frame's
/// costs, the system and the last lines of the engine's log, for a tester to send with a report.
/// </summary>
/// <remarks>
/// It is written from what the last frame published rather than by asking the engine, which may be
/// part way through a frame as the exception leaves it. The engine writes its own
/// <c>logs/Crash.log</c> as well, with the exception alone. The next run names the newest crash
/// file in its log and on the panel. <c>./e3d command feature.crash</c> throws at the next frame, so
/// a run can see the file written.
/// </remarks>
static class Crash
{
    private const string Prefix = "crash-";
    private static long _started;

    /// <summary>Writes the crash file for any exception nothing catches from now on.</summary>
    public static void Install()
    {
        _started = Environment.TickCount64;
        AppDomain.CurrentDomain.UnhandledException += (_, thrown) => Write(thrown.ExceptionObject);
    }

    /// <summary>The newest crash file beside the program, by its name, or null for none.</summary>
    public static string? Last() =>
        new DirectoryInfo(AppContext.BaseDirectory).GetFiles(Prefix + "*.txt").MaxBy(file => file.LastWriteTimeUtc)?.Name;

    private static void Write(object thrown)
    {
        try
        {
            var now = DateTime.UtcNow;
            var text = new StringBuilder();
            text.AppendLine($"The feature test crashed at {now:yyyy-MM-dd HH:mm:ss}Z, {(Environment.TickCount64 - _started) / 1000.0:0.0} seconds and {FeatureCommands.Frames:N0} frames into its run.");
            text.AppendLine();
            text.AppendLine(thrown.ToString());
            text.AppendLine();
            text.AppendLine($"The player: {FeatureCommands.Where}");
            text.AppendLine("The zones:");
            foreach (var (zone, status) in FeatureCommands.Statuses) text.AppendLine($"  {zone}: {status}");
            text.AppendLine($"The frame: {FeatureCommands.Costs}");
            text.AppendLine($"The system: {RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}, {Environment.ProcessorCount} processors, process {Environment.ProcessId}");
            text.AppendLine();
            text.AppendLine("The engine's log's last lines:");
            foreach (var line in LastLines(Path.Combine(AppContext.BaseDirectory, "logs", "Engine.log"), 40)) text.AppendLine(line);
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, $"{Prefix}{now:yyyyMMdd-HHmmss}.txt"), text.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A crash file that cannot be written leaves the engine's own log to say what happened.
        }
    }

    // The last lines of a file another process may still be writing.
    private static IEnumerable<string> LastLines(string path, int count)
    {
        if (!File.Exists(path)) return ["(no log)"];
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        var lines = new Queue<string>(count);
        while (reader.ReadLine() is { } line)
        {
            if (lines.Count == count) lines.Dequeue();
            lines.Enqueue(line);
        }
        return lines;
    }
}
