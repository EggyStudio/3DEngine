namespace Engine;

/// <summary>Every console command this process has registered, by name.</summary>
public static class ConsoleCommands
{
    private static readonly ILogger Logger = Log.Category("Engine.Console");
    private static readonly object Gate = new();
    private static readonly SortedDictionary<string, ConsoleCommand> Registered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every command, sorted by name.</summary>
    public static IReadOnlyList<ConsoleCommand> All
    {
        get { lock (Gate) return [.. Registered.Values]; }
    }

    /// <summary>Registers a command, replacing one of the same name.</summary>
    public static void Add(ConsoleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Name)) return;
        lock (Gate) Registered[command.Name] = command;
    }

    /// <summary>Registers a command from a delegate, for a game that builds one at runtime.</summary>
    public static void Add(string name, string help, Func<string[], string?> run) =>
        Add(new ConsoleCommand(name, help, string.Empty, run));

    /// <summary>Removes a command.</summary>
    public static bool Remove(string name)
    {
        lock (Gate) return Registered.Remove(name);
    }

    /// <summary>The command of that name, or <c>null</c>.</summary>
    public static ConsoleCommand? Find(string name)
    {
        lock (Gate) return Registered.GetValueOrDefault(name);
    }

    /// <summary>Runs a line as a command, its first word the command's name and the rest its arguments.</summary>
    /// <returns>The command's answer, a sentence when it is unknown or failed, or <c>null</c> for a blank line.</returns>
    public static string? Run(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var trimmed = line.Trim();
        var cut = trimmed.IndexOf(' ');
        var name = cut < 0 ? trimmed : trimmed[..cut];
        var rest = cut < 0 ? string.Empty : trimmed[(cut + 1)..].Trim();

        if (Find(name) is not { } command) return $"unknown command: {name}";

        try
        {
            // A command that takes the whole line is handed it whole, because splitting it into
            // words and joining them again loses quotes and runs of spaces.
            return command.Parameters is [{ TakesLine: true }]
                ? command.Run([Unwrap(rest)])
                : command.Run(Split(rest));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // The stack goes to the log file, and the answer stays one line a person reads.
            Logger.Error($"Command '{name}' failed.", error);
            return $"{name} failed: {error.Message}";
        }
    }

    /// <summary>Removes one pair of quotes around a whole line, undoing <c>\"</c> and <c>\\</c> inside.</summary>
    public static string Unwrap(string line)
    {
        if (line.Length < 2 || line[0] != '"' || line[^1] != '"') return line;
        var inside = line[1..^1];
        var escaped = false;
        foreach (var character in inside)
        {
            if (escaped) { escaped = false; continue; }
            if (character == '\\') escaped = true;
            else if (character == '"') return line;
        }
        return inside.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    /// <summary>Splits a line into words at spaces outside double quotes.</summary>
    public static string[] Split(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return [];
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var character in line)
        {
            if (character == '"') { quoted = !quoted; continue; }
            if (!quoted && char.IsWhiteSpace(character))
            {
                if (word.Length > 0) words.Add(word.ToString());
                word.Clear();
                continue;
            }
            word.Append(character);
        }
        if (word.Length > 0) words.Add(word.ToString());
        return [.. words];
    }
}
