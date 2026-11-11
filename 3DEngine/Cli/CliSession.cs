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
    public bool Running => RunningOrWhy(out _);

    /// <summary>Whether the process is alive, and where it is not, why, the exception's own words where asking it threw.</summary>
    public bool RunningOrWhy(out string? why)
    {
        try
        {
            var exited = Process.GetProcessById(Pid).HasExited;
            why = exited ? "its process has exited" : null;
            return !exited;
        }
        catch (ArgumentException)
        {
            why = "no process runs with that pid";
            return false;
        }
        catch (InvalidOperationException error)
        {
            why = $"its process could not be asked, {error.GetType().Name}: {error.Message}";
            return false;
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
    public static CliSession? Read(string path) => Read(path, out _);

    /// <summary>Reads a session file, or returns <c>null</c> with why, the exception's type and words, when it cannot be read.</summary>
    public static CliSession? Read(string path, out string? why)
    {
        why = null;
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
            why = $"{error.GetType().Name}: {error.Message}";
            return null;
        }
    }

    /// <summary>Every session file that can be read, newest first.</summary>
    /// <remarks>
    /// An app replaces its file around each command it answers, and a listing of the directory made
    /// while the new file is moved over the old can leave the name out, which reports a serving app
    /// as gone. The names of two listings are taken together, with a file still being written
    /// counted under the name it is moved to. On Windows the file cannot be opened for the moment
    /// the move takes, so a read that fails is tried again a few times 10 ms apart, since a single
    /// read can find no session for an app that serves, its process alive.
    /// </remarks>
    public static IReadOnlyList<CliSession> All()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];
        var found = ListedNames().Union(ListedNames()).Select(path => ReadAgain(path, out _)).OfType<CliSession>().ToList();
        found.Sort((left, right) => right.Started.CompareTo(left.Started));
        return found;
    }

    /// <summary>
    /// The sessions of running processes that <paramref name="match"/> takes, looked for again up to
    /// <paramref name="looks"/> times a fifth of a second apart where none is found.
    /// </summary>
    /// <remarks>
    /// A command that names its app can find none while the app serves and answers the commands
    /// before and after it, where several games run at once on a machine of few cores, so a named
    /// app is looked for for a second before it is taken as gone.
    /// </remarks>
    public static IReadOnlyList<CliSession> Matching(Func<CliSession, bool> match, int looks = 5) => Matching(match, out _, looks);

    /// <summary>
    /// The same, with what the first look that found none saw of each file in the folder, or null
    /// where the first look found one.
    /// </summary>
    /// <remarks>
    /// The account of a look that failed, read again after the looks, describes another moment
    /// than the one that failed, and a game's storm on the runner once found no session for a
    /// game whose file the later read found running and matching. Each look keeps what it saw, so
    /// a read that threw or a process that could not be asked, which a look passes over, is said.
    /// </remarks>
    public static IReadOnlyList<CliSession> Matching(Func<CliSession, bool> match, out string? firstMiss, int looks = 5)
    {
        firstMiss = null;
        for (int look = 1; ; look++)
        {
            var (found, seen) = Look(match);
            if (found.Count > 0 || look >= looks) return found;
            firstMiss ??= seen;
            LookAgain();
        }
    }

    /// <summary>
    /// The sessions of running processes a client may talk to, those <paramref name="asked"/> takes
    /// or every one where it is null, looked for again as <see cref="Matching(Func{CliSession, bool}, out string?, int)"/>
    /// looks, with what the first look that found none saw.
    /// </summary>
    /// <remarks>
    /// A storm's look that named no session once found none for a game that served before and
    /// after it, a named look being looked for again and an unnamed one not.
    /// </remarks>
    public static IReadOnlyList<CliSession> Serving(Func<CliSession, bool>? asked, out string? firstMiss) =>
        Matching(asked ?? (static _ => true), out firstMiss);

    // One look at the folder: the sessions of running processes that match, and what the look saw
    // of each file, whether it was read, why not where it threw, and whether its process runs.
    private static (List<CliSession> Found, string Seen) Look(Func<CliSession, bool> match)
    {
        if (!System.IO.Directory.Exists(Directory)) return ([], $"the sessions folder {Directory} did not exist");
        string[] names;
        try
        {
            names = [.. ListedNames().Union(ListedNames())];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return ([], $"the sessions folder {Directory} could not be listed, {error.GetType().Name}: {error.Message}");
        }
        var found = new List<CliSession>();
        var seen = new List<string>();
        foreach (var path in names)
        {
            var name = Path.GetFileName(path);
            if (ReadAgain(path, out var unread) is not { } session)
            {
                seen.Add($"{name} not read, {unread ?? "it was gone"}");
                continue;
            }
            if (!session.RunningOrWhy(out var gone))
                seen.Add($"{name} read, pid {session.Pid}, passed over since {gone}");
            else if (!match(session))
                seen.Add($"{name} read, pid {session.Pid}, which runs and is not the one asked for");
            else
            {
                seen.Add($"{name} read, pid {session.Pid}, which runs and matches");
                found.Add(session);
            }
        }
        return (found, seen.Count == 0 ? $"the sessions folder {Directory} held no session file"
            : $"the sessions folder {Directory} held {string.Join("; ", seen)}");
    }

    // The session files in the folder, a file still being written counted under the name it is
    // moved to.
    private static IEnumerable<string> ListedNames() => System.IO.Directory.GetFiles(Directory, "*.json*")
        .Select(name => name.EndsWith(".json.tmp", StringComparison.Ordinal) ? name[..^".tmp".Length] : name)
        .Where(name => name.EndsWith(".json", StringComparison.Ordinal));

    /// <summary>
    /// What a look that found no session saw, on one line: the folder, and each file in it with the
    /// pid it names, whether that pid runs, and why it was passed over, by <paramref name="match"/>
    /// where a pid or a name was given.
    /// </summary>
    /// <remarks>
    /// A game the runner drove once stopped serving with its process alive and no line in its log
    /// that it closed or exited, the only ways it removes its file, so the error says what was
    /// there to be found.
    /// </remarks>
    public static string Account(Func<CliSession, bool>? match = null)
    {
        if (!System.IO.Directory.Exists(Directory)) return $"The sessions folder {Directory} does not exist.";
        var files = System.IO.Directory.GetFiles(Directory).Order(StringComparer.Ordinal).ToList();
        if (files.Count == 0) return $"The sessions folder {Directory} holds no file.";
        string Seen(string file)
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".json.tmp", StringComparison.Ordinal)) return $"{name}, a file being written";
            if (!name.EndsWith(".json", StringComparison.Ordinal)) return $"{name}, not a session file";
            if (ReadAgain(file, out var unread) is not { } session) return $"{name}, which could not be read, {unread ?? "it was gone"}";
            var who = $"{name}, pid {session.Pid}, {session.Name} titled {session.Title}, its heartbeat {(DateTimeOffset.UtcNow - session.Heartbeat).TotalSeconds:0.0} s old";
            if (!session.Running) return $"{who}, passed over since that pid does not run";
            if (match is not null && !match(session)) return $"{who}, passed over since it runs and is not the one asked for";
            return $"{who}, which runs and matches, found when the folder was read again after the looks";
        }
        return $"The sessions folder {Directory} holds {string.Join("; ", files.Select(Seen))}.";
    }

    /// <summary>The wait between looks for a session that matches, which a test makes the moment it writes one.</summary>
    internal static Action LookAgain { get; set; } = static () => Thread.Sleep(200);

    /// <summary>The wait between reads of a file that cannot be read, which a test makes the moment it lets the file go.</summary>
    internal static Action Pause { get; set; } = static () => Thread.Sleep(10);

    // A session file read, and read again a few times 10 ms apart where it cannot be read, as a file
    // being moved over cannot on Windows for a moment.
    private static CliSession? ReadAgain(string path, out string? why)
    {
        for (int attempt = 0; ; attempt++)
        {
            var session = Read(path, out why);
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
