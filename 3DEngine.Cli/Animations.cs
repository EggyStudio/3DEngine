using System.Globalization;
using System.Text.Json;

namespace Engine.Cli;

/// <summary>The verbs that make animations, one recorded from a running app and one from captures on disk.</summary>
internal static class Animations
{
    /// <summary>
    /// Asks the connected app to record, and waits for the file.
    /// </summary>
    /// <remarks>
    /// The app answers the <c>record</c> command at once and writes the file once the length is
    /// reached by its clock, which on a set clock is no fixed time of the machine's, so this asks
    /// it how the recording stands until it says the file was written or why it was not, rather
    /// than giving up after a time the recording's length does not say.
    /// </remarks>
    public static int Record(Options options, string[] arguments)
    {
        if (Line(arguments) is null)
        {
            return Output.Refuse(
                options, "record", "BAD_ARGUMENT",
                "e3d record <path> <length> [--every <step>] [--scale <s>] [--play <time>], as in e3d record walk.gif 5s --every 250ms");
        }

        if (Sessions.Pick(options, "record", out var refusal) is not { } session)
        {
            return Output.Print(options, refusal);
        }

        var path = Path.GetFullPath(arguments[0]);
        var started = CliClient.Send(session, "run", Line([path, .. arguments[1..]])!, options.Timeout);
        if (Result(started) is null) return Output.Print(options, started);

        while (true)
        {
            Thread.Sleep(250);

            var asked = CliClient.Send(session, "run", "record.status", options.Timeout);
            if (Result(asked) is not { } status) return Output.Print(options, asked);

            if (status.StartsWith("recording", StringComparison.Ordinal)
                || status.StartsWith("finishing", StringComparison.Ordinal)
                || status.StartsWith("writing", StringComparison.Ordinal))
            {
                continue;
            }

            if (status.StartsWith("wrote", StringComparison.Ordinal) && status.EndsWith(path, StringComparison.Ordinal) && File.Exists(path))
            {
                var bytes = new FileInfo(path).Length;
                return Output.Print(
                    options,
                    CliJson.Ok("record", writer =>
                    {
                        writer.WriteString("path", path);
                        writer.WriteNumber("bytes", bytes);
                        writer.WriteString("said", status);
                    }),
                    data => Console.WriteLine($"recorded {data.GetProperty("path").GetString()} ({data.GetProperty("bytes").GetInt64()} bytes)"));
            }

            return Output.Refuse(options, "record", "NOT_RECORDED", $"The app took the recording and wrote no file at {path}: {status}");
        }
    }

    /// <summary>
    /// The <c>record</c> command a client's words make, its path in quotes and its measures after
    /// it, or nothing where they are not a recording's.
    /// </summary>
    internal static string? Line(string[] arguments)
    {
        if (arguments.Length < 2 || arguments[0].StartsWith("--", StringComparison.Ordinal)) return null;

        var line = $"record \"{arguments[0]}\" {arguments[1]}";
        for (var i = 2; i < arguments.Length; i += 2)
        {
            if (i + 1 >= arguments.Length || arguments[i] is not ("--every" or "--scale" or "--play")) return null;
            line += $" {arguments[i][2..]} {arguments[i + 1]}";
        }

        return line;
    }

    /// <summary>Makes pictures on disk into one animation with the engine's own writers.</summary>
    /// <remarks>
    /// What <c>build/animate.py</c> runs where Pillow is not installed. The pictures are PNG, the
    /// kind <c>e3d shot</c> writes, each made smaller by the scale and fitted to the first's size,
    /// and the animation is a GIF or an animated PNG by its extension, written as a recording is.
    /// </remarks>
    public static int Animate(Options options, string[] arguments)
    {
        const string Usage = "e3d animate <out> <pictures...> --delay <ms> [--loop <n>] [--scale <s>]";
        var pictures = new List<string>();
        int delay = 0, loops = 0;
        var scale = 1f;

        for (var i = 0; i < arguments.Length; i++)
        {
            var flag = arguments[i];
            if (!flag.StartsWith("--", StringComparison.Ordinal))
            {
                pictures.Add(flag);
                continue;
            }

            var value = i + 1 < arguments.Length ? arguments[++i] : "";
            var read = flag switch
            {
                "--delay" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out delay) && delay > 0,
                "--loop" => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out loops),
                "--scale" => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && scale > 0f && scale <= 1f,
                _ => false,
            };

            if (!read) return Output.Refuse(options, "animate", "BAD_ARGUMENT", $"{flag} {value} is not understood. {Usage}");
        }

        if (pictures.Count < 2 || delay == 0) return Output.Refuse(options, "animate", "BAD_ARGUMENT", $"An animation needs where it goes, a picture or more and a delay. {Usage}");

        var target = Path.GetFullPath(pictures[0]);
        var gif = target.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        if (!gif && !target.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && !target.EndsWith(".apng", StringComparison.OrdinalIgnoreCase))
            return Output.Refuse(options, "animate", "BAD_ARGUMENT", $"{pictures[0]} is not a GIF or an animated PNG, which the engine writes. A WebP needs Pillow, through build/animate.py.");

        var frames = new List<RgbaFrame>();
        foreach (var picture in pictures.Skip(1))
        {
            if (!picture.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return Output.Refuse(options, "animate", "BAD_ARGUMENT", $"{picture} is not a PNG, which is all the engine reads. A WebP needs Pillow, through build/animate.py.");

            try
            {
                var frame = AnimationReader.Png(File.ReadAllBytes(picture)).Frames[0];
                if (scale < 1f) frame = frame.Resized(Math.Max(1, (int)MathF.Round(frame.Width * scale)), Math.Max(1, (int)MathF.Round(frame.Height * scale)));
                frames.Add(frames.Count == 0 ? frame : frame.Resized(frames[0].Width, frames[0].Height));
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                return Output.Refuse(options, "animate", "BAD_PICTURE", $"{picture} could not be read: {error.Message}");
            }
        }

        var delays = Enumerable.Repeat(delay, frames.Count).ToArray();
        var part = target + ".part";
        using (var file = File.Create(part))
        {
            if (gif) GifWriter.Write(file, frames, delays, loops);
            else PngWriter.Animated(file, frames, delays, loops);
        }

        File.Move(part, target, overwrite: true);
        var bytes = new FileInfo(target).Length;

        return Output.Print(
            options,
            CliJson.Ok("animate", writer =>
            {
                writer.WriteString("path", target);
                writer.WriteNumber("frames", frames.Count);
                writer.WriteNumber("bytes", bytes);
            }),
            data => Console.WriteLine($"wrote {data.GetProperty("frames").GetInt32()} frames to {data.GetProperty("path").GetString()} ({data.GetProperty("bytes").GetInt64()} bytes)"));
    }

    /// <summary>What a command answered, or nothing where it failed.</summary>
    private static string? Result(string answer)
    {
        using var document = JsonDocument.Parse(answer);
        var root = document.RootElement;
        if (!root.GetProperty("success").GetBoolean()) return null;

        return root.TryGetProperty("data", out var data) && data.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.String
            ? result.GetString()
            : "";
    }
}
