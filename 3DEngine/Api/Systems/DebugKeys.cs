using System.Globalization;

namespace Engine;

/// <summary>
/// The keys every program answers of the engine's own, F1 hiding the interface, F2 writing a
/// screenshot, or held a recording, and F3 showing the debug overlay, which <see cref="E3D.SetUiHiddenKey"/>,
/// <see cref="E3D.SetScreenshotKey"/> and <see cref="E3D.SetDebugOverlayKey"/> move or turn off, a
/// world resource.
/// </summary>
/// <remarks>
/// F1, F2 and F3 are the keys other games commonly give the three, so a player finds them where
/// they look first. A press reaches the program as well, which reads it as any key.
/// </remarks>
internal sealed class DebugKeys
{
    /// <summary>The key that hides and shows the interface drawn inside <see cref="E3D.BeginUi"/>, or <see cref="Key.Null"/> for none.</summary>
    public Key HideUi { get; set; } = Key.F1;

    /// <summary>The key that writes a screenshot, and records while held, or <see cref="Key.Null"/> for none.</summary>
    public Key Screenshot { get; set; } = Key.F2;

    /// <summary>The key that shows and hides the debug overlay, with Shift its profiler, or <see cref="Key.Null"/> for none.</summary>
    public Key Overlay { get; set; } = Key.F3;

    /// <summary>
    /// The folder screenshots and recordings go into, <c>screenshots</c> beside the program unless
    /// <see cref="E3D.SetScreenshotFolder"/> or a test points it elsewhere.
    /// </summary>
    public string Folder { get; set; } = E3D.ScreenshotFolderAsked ?? Path.Combine(AppContext.BaseDirectory, "screenshots");

    /// <summary>What is called with each screenshot's path once it is written and each recording's folder once its last picture is, or nothing.</summary>
    public Action<string>? Written { get; set; } = E3D.ScreenshotWrittenAsked;

    /// <summary>How long the key is held before it records, half a second of the clock.</summary>
    public const double RecordAfterSeconds = 0.5;

    /// <summary>How many pictures a recording takes a second of the clock.</summary>
    public const int RecordedPerSecond = 15;

    /// <summary>
    /// The most pictures one recording takes, ten seconds, so a key held long does not fill the
    /// folder with pictures.
    /// </summary>
    public int MostRecorded { get; set; } = 150;

    /// <summary>The last recording's writing, done once its pictures are written and its folder handed on, which a test waits for.</summary>
    internal Task? Recorded { get; private set; }

    // When the key went down by the clock, or below zero where it is up, and the recording it
    // makes while held.
    private double _downAt = -1;
    private Recording? _recording;

    // A recording's folder, how many pictures it asked for, when it takes the next, and the
    // writes of those that came back, which a worker makes so the frame goes on.
    private sealed class Recording(string folder, double next)
    {
        public string Folder { get; } = folder;
        public int Count { get; set; }
        public double Next { get; set; } = next;
        public List<Task> Writes { get; } = [];
    }

    /// <summary>The time a screenshot is named by, the machine's unless a test holds it still.</summary>
    public Func<DateTime> Clock { get; set; } = static () => DateTime.Now;

    // The names given this run, since a file is written a frame or two after its press, so a second
    // press in the same second finds no file of the first's name yet.
    private readonly HashSet<string> _given = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The path the next screenshot taken at <paramref name="now"/> goes to, named by the time, the
    /// second in the same second <c>_2</c> and the third <c>_3</c>, past any of those names
    /// already given this run or standing in the folder.
    /// </summary>
    public string Next(DateTime now) => Named(now, ".png");

    // The next name of the time in the folder with the extension, or with none a recording's
    // folder, past any given this run or standing there as a file or a folder.
    private string Named(DateTime now, string extension)
    {
        var stem = now.ToString("yyyy-MM-dd_HH.mm.ss", CultureInfo.InvariantCulture);
        for (var count = 1; ; count++)
        {
            var path = Path.Combine(Folder, (count == 1 ? stem : $"{stem}_{count}") + extension);
            if (!_given.Contains(path) && !File.Exists(path) && !Directory.Exists(path) && _given.Add(path)) return path;
        }
    }

    /// <summary>
    /// Writes a screenshot where its key went down this frame, and records while it is held past
    /// <see cref="RecordAfterSeconds"/>, each with a line in the log saying where.
    /// </summary>
    /// <remarks>Run in PreUpdate, once the frame's input is in, so the frame drawn after it is the one written.</remarks>
    internal static void Run(World world)
    {
        if (!world.TryGetResource<DebugKeys>(out var keys) || keys.Screenshot == Key.Null
            || !world.TryGetResource<Input>(out var input)) return;
        var now = world.TryGetResource<Time>(out var time) ? time.ElapsedSeconds : 0;
        var held = input.KeyDown(keys.Screenshot);
        if (input.KeyPressed(keys.Screenshot))
        {
            keys._downAt = now;
            keys.Shoot(world);
        }
        if (keys._recording is { } recording)
        {
            // One at its most while the key is still held ends, and the next waits for a press.
            if (!held || recording.Count >= keys.MostRecorded)
            {
                keys.Finish(recording);
                keys._downAt = -1;
            }
            else
                keys.Take(world, recording, now);
        }
        else if (held && keys._downAt >= 0 && now - keys._downAt >= RecordAfterSeconds)
        {
            keys._recording = new Recording(keys.Named(keys.Clock(), ""), now);
            Directory.CreateDirectory(keys._recording.Folder);
            keys.Take(world, keys._recording, now);
        }
        if (!held)
            keys._downAt = -1;
    }

    // The frame's screenshot, with a line in the log saying where.
    private void Shoot(World world)
    {
        var log = Log.Category("Engine.Screenshot");
        Directory.CreateDirectory(Folder);
        var path = Next(Clock());
        var written = Written;
        if (Screenshots.Request(world, path, failure =>
            {
                if (failure is not null)
                {
                    log.Warn(failure);
                    return;
                }
                log.Info($"Saved screenshot as {path}.");
                written?.Invoke(path);
            }) is { } refusal)
            log.Warn($"No screenshot was taken. {refusal}");
    }

    // The recording's next picture where its time has come, written by a worker once it is back,
    // a step on from the last, or from now where the frames fell behind by more than a step.
    private void Take(World world, Recording recording, double now)
    {
        if (now < recording.Next)
            return;
        var step = 1.0 / RecordedPerSecond;
        recording.Next = Math.Max(recording.Next + step, now);
        recording.Count++;
        var path = Path.Combine(recording.Folder, recording.Count.ToString("D4", CultureInfo.InvariantCulture) + ".png");
        if (Screenshots.Capture(world, (rgba, width, height) =>
            {
                var write = Task.Run(() => PngWriter.Write(path, rgba, width, height));
                lock (recording.Writes)
                    recording.Writes.Add(write);
            }) is { } refusal)
        {
            Log.Category("Engine.Screenshot").Warn($"The recording took no picture. {refusal}");
            recording.Count = MostRecorded;
        }
    }

    // Ends the recording, saying where it went once its last picture is written and handing its
    // folder on.
    private void Finish(Recording recording)
    {
        _recording = null;
        var written = Written;
        var log = Log.Category("Engine.Screenshot");
        // A picture asked for comes back once its frame is drawn, so the writes are gathered once
        // every picture asked for is back, or after five seconds where a frame never came.
        Recorded = Task.Run(async () =>
        {
            Task[] writes = [];
            for (var waited = 0; waited < 5000; waited += 20)
            {
                lock (recording.Writes)
                    writes = [.. recording.Writes];
                if (writes.Length >= recording.Count)
                    break;
                await Task.Delay(20);
            }
            try
            {
                await Task.WhenAll(writes);
                log.Info($"Saved a recording of {writes.Length} pictures, {RecordedPerSecond} a second, in {recording.Folder}.");
                written?.Invoke(recording.Folder);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                log.Warn($"The recording in {recording.Folder} could not be written whole. {error.Message}");
            }
        });
    }
}
