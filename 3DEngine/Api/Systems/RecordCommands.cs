namespace Engine;

/// <summary>
/// The recorder's commands, beside <c>shot</c>, which takes one picture where these take many, in
/// BevyCSharp's words (SHARED.md).
/// </summary>
internal static class RecordCommands
{
    /// <summary>Records what the window shows as an animated GIF or PNG, a picture at each step for a length.</summary>
    /// <remarks>
    /// <para>
    /// The length is seconds (<c>5s</c>), frames of the clock (<c>120f</c>) or a count of pictures
    /// (<c>8</c>), and the step after <c>every</c> is milliseconds (<c>250ms</c>), seconds or frames,
    /// a tenth of a second unless given. <c>scale 0.5</c> keeps each picture half as wide and high,
    /// and <c>play 100ms</c> shows every frame for that long, where each is otherwise shown for the
    /// time between it and the next, so a step of a second plays at a second.
    /// </para>
    /// <para>
    /// It answers at once, and the file appears once the length is reached and the last picture has
    /// come back off the GPU and been written, under its own name only when whole, which
    /// <c>./e3d record</c> waits for by <c>record.status</c>. <c>record.stop</c> ends it sooner, with
    /// what it took.
    /// </para>
    /// </remarks>
    [Command("record", "Records the window as an animated GIF or PNG: record <path> <length> [every <step>] [scale <s>] [play <time>]")]
    internal static string Record(string line)
    {
        // The path first, in quotes where it holds a space, as e3d sends one, then the measures.
        var words = ConsoleCommands.Split(line);
        if (words.Length < 2)
        {
            ConsoleHost.Fail("BAD_ARGUMENT", "record needs a path to write to and a length, as in record walk.gif 5s every 250ms.");
            return "record <path> <length> [every <step>] [scale <s>] [play <time>]";
        }
        var world = ConsoleHost.World!;
        if (!world.TryGetResource<Renderer>(out var renderer) || !renderer.Context.IsInitialized)
        {
            ConsoleHost.Fail("NO_RENDERER", "This app draws nothing to record (a headless run draws nothing). Start one with a window, hidden, or offscreen.");
            return "no renderer, so nothing was recorded";
        }

        if (Recorder.Parse(Path.GetFullPath(words[0]), words[1..], out var problem) is not { } request)
        {
            ConsoleHost.Fail("BAD_ARGUMENT", problem!);
            return problem!;
        }
        try
        {
            Recorder.Start(request);
        }
        catch (ArgumentException error)
        {
            ConsoleHost.Fail("BAD_ARGUMENT", error.Message);
            return error.Message;
        }
        catch (InvalidOperationException error)
        {
            ConsoleHost.Fail("BUSY", error.Message);
            return error.Message;
        }
        var length = request.Count is { } count ? $"{count} pictures" : request.Length?.ToString();
        return $"recording {length} to {request.Path}, a picture every {request.Step}";
    }

    /// <summary>Stops a recording, which writes its file once the pictures it asked for are back.</summary>
    [Command("record.stop", "Stops the recording, which writes its file once its last pictures are back")]
    internal static string Stop()
    {
        if (Recorder.Stop() is null)
        {
            ConsoleHost.Fail("NOT_RECORDING", "Nothing is being recorded.");
            return "nothing is being recorded";
        }
        return "stopping, and the file is written once the last pictures are back";
    }

    /// <summary>How the recording stands, or what the last one wrote or why it wrote nothing.</summary>
    /// <remarks>
    /// Recording with the pictures taken, finishing while the last ones come back, writing its file,
    /// then <c>wrote</c> with the count and the path, or <c>failed</c> with why, which
    /// <c>./e3d record</c> asks until the file is there.
    /// </remarks>
    [Command("record.status", "Says how the recording stands, or what the last one wrote")]
    internal static string Status() => Recorder.Status;
}
