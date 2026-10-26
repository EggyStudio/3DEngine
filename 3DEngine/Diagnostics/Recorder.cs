using System.Globalization;

namespace Engine;

/// <summary>
/// Takes pictures of what a run draws at a step of its clock and writes them as one animated file,
/// for the <c>record</c> command and <see cref="E3D.StartRecording"/>.
/// </summary>
/// <remarks>
/// <para>
/// A picture is asked for at each step, of the frame being drawn, by the capture
/// <see cref="E3D.TakeScreenshot"/> takes, and comes back once the frame has finished on the GPU,
/// kept in memory with the time of the clock it was asked at, made smaller first where a scale was
/// asked for. The step and the length are in the clock's time or its frames, those the clock moved
/// in, so a recording on a set clock (<see cref="Time.FrameSeconds"/>) is the same moments of the
/// game on a fast device and a slow one, and a clock held still while something loads takes no
/// pictures of the wait.
/// </para>
/// <para>
/// The file is written once the length is reached, or the recording is stopped, and every picture
/// asked for has come back, on a worker, so the frames go on while the palette is cut, and under a
/// name of its own until it is whole, so whoever waits for the file never reads half of one. Each
/// frame is shown for the time between it and the next, which is the step, so a step of a second
/// plays at a second, unless a playing time was given for every frame. A recording holds at most
/// <see cref="MostPictures"/> pictures in memory, and stops there with a warning.
/// </para>
/// <para>
/// Its words are BevyCSharp's (SHARED.md), so a recording is asked for the same way of either engine.
/// </para>
/// </remarks>
internal static class Recorder
{
    /// <summary>The step a recording takes pictures at when none is given, a tenth of a second.</summary>
    public const double DefaultStepSeconds = 0.1;

    /// <summary>The most pictures a recording keeps, a minute and a half at the default step.</summary>
    public const int MostPictures = 1000;

    private static readonly Lock Gate = new();
    private static Session? _session;
    private static Task<string>? _last;
    private static string? _writing;
    private static string _said = "nothing has been recorded";

    /// <summary>A measure of the clock, in seconds or in the frames it moved in.</summary>
    /// <param name="Value">How many.</param>
    /// <param name="InFrames">Whether it counts frames rather than seconds.</param>
    internal readonly record struct Measure(double Value, bool InFrames)
    {
        /// <summary>
        /// Reads <c>250ms</c>, <c>1.5s</c> or <c>15f</c>, or a bare number as what
        /// <paramref name="bare"/> says it is, or nothing for anything else.
        /// </summary>
        public static Measure? Parse(string text, Func<double, Measure?> bare)
        {
            var culture = CultureInfo.InvariantCulture;
            if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase) && double.TryParse(text[..^2], NumberStyles.Float, culture, out var ms) && ms > 0)
                return new Measure(ms / 1000, false);
            if (text.EndsWith('s') && double.TryParse(text[..^1], NumberStyles.Float, culture, out var seconds) && seconds > 0)
                return new Measure(seconds, false);
            if (text.EndsWith('f') && int.TryParse(text[..^1], NumberStyles.None, culture, out var frames) && frames > 0)
                return new Measure(frames, true);
            return double.TryParse(text, NumberStyles.Float, culture, out var number) && number > 0 ? bare(number) : null;
        }

        /// <inheritdoc/>
        public override string ToString() => InFrames
            ? $"{Value} frames"
            : Value < 1 ? $"{Value * 1000:0.#} ms" : $"{Value:0.###} s";
    }

    /// <summary>What a recording is asked for.</summary>
    /// <param name="Path">Where the file goes, a GIF or an animated PNG by its extension.</param>
    /// <param name="Step">How often a picture is taken.</param>
    /// <param name="Length">How long it records, or nothing until it is stopped.</param>
    /// <param name="Count">How many pictures it takes, where it counts them rather than time.</param>
    /// <param name="Scale">How much smaller each picture is kept, one keeping it as it is.</param>
    /// <param name="PlayMilliseconds">How long every frame is shown, or nothing for the time between them.</param>
    internal sealed record Request(string Path, Measure Step, Measure? Length, int? Count, float Scale, int? PlayMilliseconds);

    // A picture asked for, which the device fills on the render thread once its frame has finished.
    private sealed class Picture
    {
        public RgbaFrame? Frame;
        public volatile bool Back;
    }

    private sealed class Session(Request request)
    {
        public readonly Request Request = request;
        public readonly List<(Picture Picture, double Time)> Pending = [];
        public readonly List<(RgbaFrame Frame, double Time)> Frames = [];
        public readonly TaskCompletionSource<string> Written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public double Started = double.NaN;
        public ulong StartedFrame;
        public int Taken;
        public bool Stopping;
    }

    /// <summary>The task of the recording started last, which a test takes the file from once it is written.</summary>
    internal static Task<string>? Last
    {
        get { lock (Gate) return _last; }
    }

    /// <summary>
    /// How the recording stands, as <c>record.status</c> says it, recording or finishing with the
    /// pictures taken, writing its file, or, once none is, what the last one wrote or why it wrote
    /// nothing.
    /// </summary>
    public static string Status
    {
        get
        {
            lock (Gate)
            {
                if (_session is { } session)
                    return session.Stopping
                        ? $"finishing, {session.Taken} pictures taken and {session.Pending.Count} on their way back"
                        : $"recording, {session.Taken} pictures taken";
                return _writing is { } path ? $"writing {path}" : _said;
            }
        }
    }

    /// <summary>Whether a recording is taking pictures or waiting for its last ones.</summary>
    public static bool IsRecording
    {
        get { lock (Gate) return _session is not null; }
    }

    /// <summary>Reads the measures the <c>record</c> command takes after its path, into a request.</summary>
    /// <param name="path">Where the file goes.</param>
    /// <param name="words">
    /// The length, <c>5s</c>, <c>120f</c> or a count of pictures, then any of <c>every &lt;step&gt;</c>,
    /// <c>scale &lt;s&gt;</c> and <c>play &lt;time&gt;</c>.
    /// </param>
    /// <param name="problem">What is wrong with them, where something is.</param>
    public static Request? Parse(string path, IReadOnlyList<string> words, out string? problem)
    {
        problem = null;
        if (words.Count == 0)
        {
            problem = "A recording needs a length, 5s, 120f, or a count of pictures such as 8.";
            return null;
        }

        Measure? length = null;
        int? count = null;
        var given = Measure.Parse(words[0], number => number == Math.Floor(number) ? new Measure(number, true) : null);
        if (given is null)
        {
            problem = $"{words[0]} is not a length, which is seconds (5s), frames (120f) or a count of pictures (8).";
            return null;
        }
        // A bare number counts pictures, which is read as frames of the recording rather than of the clock.
        if (char.IsDigit(words[0][^1])) count = (int)given.Value.Value;
        else length = given;

        var step = new Measure(DefaultStepSeconds, false);
        var scale = 1f;
        int? play = null;
        for (var i = 1; i < words.Count; i += 2)
        {
            if (i + 1 >= words.Count)
            {
                problem = $"{words[i]} needs a value after it.";
                return null;
            }
            var value = words[i + 1];
            switch (words[i])
            {
                case "every" when Measure.Parse(value, number => new Measure(number / 1000, false)) is { } every:
                    step = every;
                    break;
                case "scale" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) && factor > 0f && factor <= 1f:
                    scale = factor;
                    break;
                case "play" when Measure.Parse(value, number => new Measure(number / 1000, false)) is { InFrames: false } shown:
                    play = Math.Max(1, (int)Math.Round(shown.Value * 1000));
                    break;
                default:
                    problem = $"{words[i]} {value} is not understood. A recording takes every <step> (250ms, 1s, 15f), scale <factor> up to 1, and play <time> (100ms) for every frame.";
                    return null;
            }
        }
        return new Request(path, step, length, count, scale, play);
    }

    /// <summary>Starts a recording, which takes its first picture in the frame after.</summary>
    /// <returns>A task finished with the file's path once it is written, or failed with why it was not.</returns>
    /// <exception cref="ArgumentException">The path ends in neither <c>.gif</c> nor <c>.png</c>.</exception>
    /// <exception cref="InvalidOperationException">Another recording is being made.</exception>
    public static Task<string> Start(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsGif(request.Path) && !IsPng(request.Path))
            throw new ArgumentException($"{request.Path} is written as neither a GIF nor an animated PNG, which a recording is by its extension, .gif or .png.");
        lock (Gate)
        {
            if (_session is not null)
                throw new InvalidOperationException($"A recording to {_session.Request.Path} is being made. Stop it first.");
            _session = new Session(request);
            return _last = _session.Written.Task;
        }
    }

    /// <summary>Stops taking pictures, so the file is written once those asked for have come back.</summary>
    /// <returns>The task of the recording being stopped, or nothing where none is being made.</returns>
    public static Task<string>? Stop()
    {
        lock (Gate)
        {
            if (_session is null) return null;
            _session.Stopping = true;
            return _session.Written.Task;
        }
    }

    /// <summary>Takes a picture where one is due, keeps those that came back, and writes the file once it is whole.</summary>
    /// <remarks>Run once a frame before the game's systems, so a picture asked for is of this frame.</remarks>
    internal static void Tick(World world)
    {
        Session? session;
        lock (Gate) session = _session;
        if (session is null) return;

        try
        {
            Gather(session);
            if (!session.Stopping)
            {
                if (!world.TryGetResource<Renderer>(out var renderer) || !renderer.Context.IsInitialized
                    || renderer.Context.Graphics is not GraphicsDevice device)
                    throw new InvalidOperationException("There is no renderer to record from (a headless run draws nothing).");
                var time = world.Resource<Time>();
                Take(session, device, time.ElapsedSeconds, time.FrameCount);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or NotSupportedException)
        {
            Log.Category("Engine.Record").Error($"The recording to {session.Request.Path} ended: {error.Message}");
            Finish(session, null, $"failed: the recording to {session.Request.Path} ended: {error.Message}");
            session.Written.TrySetException(error);
            return;
        }

        if (!session.Stopping || session.Pending.Count > 0) return;
        Finish(session, session.Request.Path, null);
        _ = Task.Run(() => Write(session));
    }

    /// <summary>
    /// Writes what a recording took where the app ends while it records, at once and on this
    /// thread, the pictures still on the GPU left out, since the device goes with the app.
    /// </summary>
    internal static void Shutdown()
    {
        Session? session;
        lock (Gate) session = _session;
        if (session is null) return;
        Gather(session);
        session.Pending.Clear();
        Finish(session, session.Request.Path, null);
        Write(session);
    }

    private static void Gather(Session session)
    {
        for (var i = 0; i < session.Pending.Count; i++)
        {
            var (picture, taken) = session.Pending[i];
            if (!picture.Back) continue;
            session.Pending.RemoveAt(i--);
            if (picture.Frame is not { } frame) continue;
            var scale = session.Request.Scale;
            if (scale < 1f) frame = frame.Resized(Math.Max(1, (int)MathF.Round(frame.Width * scale)), Math.Max(1, (int)MathF.Round(frame.Height * scale)));
            session.Frames.Add((frame, taken));
        }
    }

    private static void Take(Session session, GraphicsDevice device, double now, ulong frames)
    {
        var request = session.Request;
        if (double.IsNaN(session.Started))
        {
            session.Started = now;
            session.StartedFrame = frames;
        }

        // Due once the clock has reached the next step from the start, a millionth of a second
        // early allowed for, since a set clock's frames add up to a step a hair short of it.
        var step = request.Step;
        var due = step.InFrames
            ? frames >= session.StartedFrame + (ulong)Math.Round(session.Taken * step.Value)
            : now >= session.Started + session.Taken * step.Value - 1e-6;
        if (!due) return;

        // A picture falling at or past the length is not taken, which a length in another measure
        // than the step's is known by only once the clock has reached it.
        if (request.Length is { } length && Reached(length, now - session.Started, frames - session.StartedFrame))
        {
            session.Stopping = true;
            return;
        }

        var picture = new Picture();
        device.RequestCapture((rgba, width, height) =>
        {
            picture.Frame = new RgbaFrame(width, height, rgba);
            picture.Back = true;
        });
        session.Pending.Add((picture, now));
        session.Taken++;

        // Ended at once where the count is taken, or where the next picture would fall at or past a
        // length in the step's own measure, so five seconds at a quarter of a second takes twenty,
        // the last a quarter before the end, and the file is not kept waiting a step for nothing.
        if (request.Count is { } count && session.Taken >= count) session.Stopping = true;
        if (request.Length is { } same && same.InFrames == step.InFrames && session.Taken * step.Value >= same.Value - 1e-6) session.Stopping = true;
        if (session.Taken >= MostPictures && !session.Stopping)
        {
            Log.Category("Engine.Record").Warn($"The recording to {request.Path} reached {MostPictures} pictures, the most one keeps, and stops there.");
            session.Stopping = true;
        }
    }

    private static bool Reached(Measure length, double seconds, ulong frames) =>
        length.InFrames ? frames >= (ulong)Math.Round(length.Value) : seconds >= length.Value - 1e-6;

    // Ends a session, saying the file it is writing, or what it came to.
    private static void Finish(Session session, string? writing, string? said)
    {
        lock (Gate)
        {
            if (ReferenceEquals(_session, session)) _session = null;
            _writing = writing;
            if (said is not null) _said = said;
        }
    }

    private static void Write(Session session)
    {
        var request = session.Request;
        try
        {
            var taken = session.Frames.OrderBy(frame => frame.Time).ToArray();
            if (taken.Length == 0) throw new InvalidOperationException($"The recording to {request.Path} took no pictures before it ended.");

            // A frame drawn at another size, the window resized while it recorded, is fitted to the
            // first, since every frame of an animation is the size of the file.
            var (width, height) = (taken[0].Frame.Width, taken[0].Frame.Height);
            var frames = taken.Select(frame => frame.Frame.Resized(width, height)).ToArray();
            var delays = Delays([.. taken.Select(frame => frame.Time)], request);

            var folder = Path.GetDirectoryName(Path.GetFullPath(request.Path));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var part = request.Path + ".part";
            using (var file = File.Create(part))
            {
                if (IsGif(request.Path)) GifWriter.Write(file, frames, delays);
                else PngWriter.Animated(file, frames, delays);
            }
            File.Move(part, request.Path, overwrite: true);
            Log.Category("Engine.Record").Info($"Wrote {frames.Length} frames to {request.Path}.");
            Finish(session, null, $"wrote {frames.Length} frames to {request.Path}");
            session.Written.TrySetResult(request.Path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Log.Category("Engine.Record").Error($"The recording to {request.Path} was not written: {error.Message}");
            Finish(session, null, $"failed: the recording to {request.Path} was not written: {error.Message}");
            session.Written.TrySetException(error);
        }
    }

    /// <summary>
    /// How long each frame is shown, in milliseconds, the time to the next, and for the last the
    /// step, or the time between the two before it where the step counts frames.
    /// </summary>
    internal static int[] Delays(double[] times, Request request)
    {
        var delays = new int[times.Length];
        for (var i = 0; i < times.Length; i++)
        {
            if (request.PlayMilliseconds is { } play)
            {
                delays[i] = play;
                continue;
            }
            var seconds = i + 1 < times.Length ? times[i + 1] - times[i]
                : !request.Step.InFrames ? request.Step.Value
                : i > 0 ? times[i] - times[i - 1]
                : DefaultStepSeconds;
            delays[i] = (int)Math.Round(seconds * 1000);
        }
        return delays;
    }

    /// <summary>
    /// Writes frames given as they are as the file a recording of them would write, for a test with
    /// no GPU to take them from, by the same path a recording's pictures go.
    /// </summary>
    internal static void WriteFrames(Request request, IReadOnlyList<(RgbaFrame Frame, double Time)> frames)
    {
        var session = new Session(request);
        session.Frames.AddRange(frames);
        Write(session);
        if (session.Written.Task.IsFaulted) throw session.Written.Task.Exception!.InnerException!;
    }

    private static bool IsGif(string path) => path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

    private static bool IsPng(string path) => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
}
