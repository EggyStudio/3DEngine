using System.Globalization;

namespace Engine;

/// <summary>
/// The keys every program answers of the engine's own, F2 writing a screenshot and F3 showing the
/// performance window, which <see cref="E3D.SetScreenshotKey"/> and
/// <see cref="E3D.SetProfileWindowKey"/> move or turn off, a world resource.
/// </summary>
/// <remarks>
/// F2 and F3 are Minecraft's keys for the two, so a player of games made on the engine finds them
/// where that game put them. A press reaches the program as well, which reads it as any key.
/// </remarks>
internal sealed class DebugKeys
{
    /// <summary>The key that writes a screenshot, or <see cref="Key.Null"/> for none.</summary>
    public Key Screenshot { get; set; } = Key.F2;

    /// <summary>The key that shows and hides the performance window, or <see cref="Key.Null"/> for none.</summary>
    public Key ProfileWindow { get; set; } = Key.F3;

    /// <summary>The folder screenshots go into, <c>screenshots</c> beside the program unless a test points it elsewhere.</summary>
    public string Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, "screenshots");

    /// <summary>The time a screenshot is named by, the machine's unless a test holds it still.</summary>
    public Func<DateTime> Clock { get; set; } = static () => DateTime.Now;

    // The names given this run, since a file is written a frame or two after its press, so a second
    // press in the same second finds no file of the first's name yet.
    private readonly HashSet<string> _given = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The path the next screenshot taken at <paramref name="now"/> goes to, named by the time, the
    /// second in the same second <c>_2</c> and the third <c>_3</c>, as Minecraft names its own,
    /// past any of those names already given this run or standing in the folder.
    /// </summary>
    public string Next(DateTime now)
    {
        var stem = now.ToString("yyyy-MM-dd_HH.mm.ss", CultureInfo.InvariantCulture);
        for (var count = 1; ; count++)
        {
            var path = Path.Combine(Folder, (count == 1 ? stem : $"{stem}_{count}") + ".png");
            if (!_given.Contains(path) && !File.Exists(path) && _given.Add(path)) return path;
        }
    }

    /// <summary>Writes a screenshot where its key went down this frame, with a line in the log saying where.</summary>
    /// <remarks>Run in PreUpdate, once the frame's input is in, so the frame drawn after it is the one written.</remarks>
    internal static void Run(World world)
    {
        if (!world.TryGetResource<DebugKeys>(out var keys) || keys.Screenshot == Key.Null
            || !world.TryGetResource<Input>(out var input) || !input.KeyPressed(keys.Screenshot)) return;

        var log = Log.Category("Engine.Screenshot");
        Directory.CreateDirectory(keys.Folder);
        var path = keys.Next(keys.Clock());
        if (Screenshots.Request(world, path, failure =>
            {
                if (failure is null) log.Info($"Saved screenshot as {path}.");
                else log.Warn(failure);
            }) is { } refusal)
            log.Warn($"No screenshot was taken. {refusal}");
    }
}
