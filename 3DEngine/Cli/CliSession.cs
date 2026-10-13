using System.Diagnostics;
using System.Text.Json;

namespace Engine;

/// <summary>A serving app as its session file describes it.</summary>
internal sealed record CliSession(
    int Pid, int Port, string Token, string Project, string Name, string Title, string Mode,
    DateTimeOffset Started, bool Renderer, string State, ulong Frame, DateTimeOffset Heartbeat)
{
    /// <summary>How long since the last heartbeat before the session counts as unreachable.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>Whether the heartbeat is older than <see cref="Patience"/>.</summary>
    public bool Stale => DateTimeOffset.UtcNow - Heartbeat > Patience;

    /// <summary>Whether the process is alive.</summary>
    public bool Running
    {
        get
        {
            try { return !Process.GetProcessById(Pid).HasExited; }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return false; }
        }
    }

    /// <summary><c>gone</c>, <c>unreachable</c> or the state the app wrote.</summary>
    public string Report => !Running ? "gone" : Stale ? "unreachable" : State;
}

/// <summary>
/// Reads and writes session files, one per serving process, in <c>E3D_SESSIONS</c> or the user's
/// local application data under <c>3DEngine/sessions</c>.
/// </summary>
/// <remarks>
/// A file is written whole beside its final name and moved into place, so a reader never sees half
/// of one, and is readable by its owner only, because it holds the token.
/// </remarks>
internal static class CliSessionFile
{
    /// <summary>The directory session files live in.</summary>
    public static string Directory { get; set; } =
        Environment.GetEnvironmentVariable("E3D_SESSIONS") is { Length: > 0 } named
            ? named
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify), "3DEngine", "sessions");

    /// <summary>The file for the process <paramref name="pid"/>.</summary>
    public static string PathFor(int pid) => Path.Combine(Directory, $"{pid}.json");

    /// <summary>Writes a session's file.</summary>
    public static void Write(CliSession session)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var final = PathFor(session.Pid);
        var temporary = final + ".tmp";

        using (var stream = File.Create(temporary))
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("pid", session.Pid);
            writer.WriteNumber("port", session.Port);
            writer.WriteString("token", session.Token);
            writer.WriteString("project", session.Project);
            writer.WriteString("name", session.Name);
            writer.WriteString("title", session.Title);
            writer.WriteString("mode", session.Mode);
            writer.WriteString("started", session.Started.ToString("O"));
            writer.WriteBoolean("renderer", session.Renderer);
            writer.WriteString("state", session.State);
            writer.WriteNumber("frame", session.Frame);
            writer.WriteString("heartbeat", session.Heartbeat.ToString("O"));
            writer.WriteEndObject();
        }

        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }

        File.Move(temporary, final, overwrite: true);
    }

    /// <summary>Reads a session file, or returns <c>null</c> when it cannot be read.</summary>
    public static CliSession? Read(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            return new CliSession(
                root.GetProperty("pid").GetInt32(),
                root.GetProperty("port").GetInt32(),
                Text(root, "token"), Text(root, "project"), Text(root, "name"), Text(root, "title"), Text(root, "mode"),
                Moment(root, "started"),
                root.TryGetProperty("renderer", out var renderer) && renderer.ValueKind == JsonValueKind.True,
                Text(root, "state"),
                root.TryGetProperty("frame", out var frame) ? frame.GetUInt64() : 0,
                Moment(root, "heartbeat"));
        }
        catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException
                                          or InvalidOperationException or FormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Every session file that can be read, newest first.</summary>
    /// <remarks>
    /// An app replaces its file around each command it answers, and a listing of the directory made
    /// while the new file is moved over the old can leave the name out, which reported a serving
    /// app as gone about once in a hundred commands. The names of two listings are taken together,
    /// with a file still being written counted under the name it is moved to. On Windows the file
    /// cannot be opened for the moment the move takes, so a read that fails is tried again a few
    /// times 10 ms apart: read once, it found no session for the puzzle game while it served, its
    /// process alive, about once a run of its drive.
    /// </remarks>
    public static IReadOnlyList<CliSession> All()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];
        static IEnumerable<string> Names() => System.IO.Directory.GetFiles(Directory, "*.json*")
            .Select(name => name.EndsWith(".json.tmp", StringComparison.Ordinal) ? name[..^".tmp".Length] : name)
            .Where(name => name.EndsWith(".json", StringComparison.Ordinal));
        var found = Names().Union(Names()).Select(ReadAgain).OfType<CliSession>().ToList();
        found.Sort((left, right) => right.Started.CompareTo(left.Started));
        return found;
    }

    /// <summary>The wait between reads of a file that cannot be read, which a test makes the moment it lets the file go.</summary>
    internal static Action Pause { get; set; } = static () => Thread.Sleep(10);

    // A session file read, and read again a few times 10 ms apart where it cannot be read, as a file
    // being moved over cannot on Windows for a moment.
    private static CliSession? ReadAgain(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            var session = Read(path);
            if (session is not null || attempt == 4) return session;
            Pause();
        }
    }

    /// <summary>Deletes the file for <paramref name="pid"/>.</summary>
    public static void Remove(int pid)
    {
        try { File.Delete(PathFor(pid)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Deletes the files of processes that are gone, and returns how many.</summary>
    public static int Prune()
    {
        var swept = 0;
        foreach (var session in All().Where(session => !session.Running))
        {
            Remove(session.Pid);
            swept++;
        }
        return swept;
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static DateTimeOffset Moment(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && DateTimeOffset.TryParse(value.GetString(), out var moment) ? moment : DateTimeOffset.MinValue;
}
