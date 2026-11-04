using System.Globalization;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// Points the screenshot key into the day's folder of pictures where an example runs from this
/// repository, <c>.github/dump/&lt;date&gt;/</c>, each screenshot and recording named by its time,
/// the example and a word, with its line in that day's <c>captions.md</c>, as NORM.md's N 4.10 keeps
/// the pictures a session reads.
/// </summary>
/// <remarks>
/// F2 pressed writes a screenshot and F2 held records fifteen pictures a second as PNGs in a folder
/// of their own (<see cref="E3D.SetScreenshotKey"/>), whose line gives the count and the rate, so a
/// reader knows the recording's time. <c>build/dump-webp.sh</c> makes a recording one animated WebP
/// after, since the engine carries no WebP encoder. Run from anywhere else, the key keeps its folder
/// beside the program.
/// </remarks>
internal static class Dump
{
    // As many pictures a second as the screenshot key records.
    private const int RecordedPerSecond = 15;

    private static readonly Lock Gate = new();

    /// <summary>Points the screenshot key into the day's folder for the example named.</summary>
    public static void Point(string example)
    {
        if (Repository() is not { } root)
            return;
        var day = Path.Combine(root, ".github", "dump", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        SetScreenshotFolder(day, written => Note(day, example, written));
    }

    // The repository the program runs from, the first folder above it holding .git and .github.
    private static string? Repository()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (Directory.Exists(Path.Combine(folder.FullName, ".github")) && Path.Exists(Path.Combine(folder.FullName, ".git")))
                return folder.FullName;
        return null;
    }

    // Names a written screenshot or recording by the time the engine named it by, the example and a
    // word, and writes its line, on the engine's thread that wrote it.
    private static void Note(string day, string example, string written)
    {
        lock (Gate)
        {
            var recording = Directory.Exists(written);
            // The engine names by the date and the time, as 2026-11-04_19.36.12 with _2 past the first.
            var time = Path.GetFileNameWithoutExtension(written) is { Length: >= 19 } stem ? stem[11..].Replace('.', '-') : DateTime.Now.ToString("HH-mm-ss", CultureInfo.InvariantCulture);
            var name = $"{time}-{example}-{(recording ? "recording" : "screenshot")}";
            var target = Path.Combine(day, recording ? name : name + ".png");
            if (Path.Exists(target))
                return;
            string line;
            if (recording)
            {
                Directory.Move(written, target);
                var count = Directory.GetFiles(target, "*.png").Length;
                line = string.Create(CultureInfo.InvariantCulture,
                    $"{name} | {example}, a recording held on F2, {count} pictures at {RecordedPerSecond} a second, {count / (double)RecordedPerSecond:0.0} seconds, as PNGs numbered from 0001.png.");
            }
            else
            {
                File.Move(written, target);
                line = $"{name}.png | {example}, a screenshot taken with F2.";
            }
            File.AppendAllText(Path.Combine(day, "captions.md"), line + "\n");
        }
    }
}
