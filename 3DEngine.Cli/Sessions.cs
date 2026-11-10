using System.Text.Json;

namespace Engine.Cli;

/// <summary>Chooses the session a verb talks to.</summary>
internal static class Sessions
{
    public static IReadOnlyList<CliSession> Live() => [.. CliSessionFile.All().Where(session => session.Running)];

    /// <summary>
    /// The one session that matches the flags, or <c>null</c> with a refusal saying why, as when
    /// none is serving, or several are and the flags do not say which.
    /// </summary>
    public static CliSession? Pick(Options options, string command, out string refusal)
    {
        refusal = string.Empty;
        var name = options.Name is { Length: > 0 } named ? named : null;
        Func<CliSession, bool>? asked = options.Pid is null && name is null ? null
            : s => (options.Pid is not { } pid || s.Pid == pid)
                && (name is null || s.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || s.Title.Contains(name, StringComparison.OrdinalIgnoreCase));
        var candidates = asked is null ? Live() : CliSessionFile.Matching(asked);

        // A stale one is only chosen when it is the only one, so a session that is busy loading is
        // still reachable while a livelier one is preferred.
        if (candidates.Any(s => !s.Stale)) candidates = [.. candidates.Where(s => !s.Stale)];

        switch (candidates.Count)
        {
            case 1:
                return candidates[0];
            case 0:
                refusal = CliJson.Fail(command, "NO_SESSION", (asked is null
                    ? "No app is serving. Start one with 'e3d open <example>', or run a program with --serve. "
                    : "No serving app matches that. Run 'e3d status' to see what is serving. ") + CliSessionFile.Account(asked));
                return null;
            default:
                refusal = CliJson.Envelope(command, success: false,
                    writer =>
                    {
                        writer.WritePropertyName("candidates");
                        writer.WriteStartArray();
                        foreach (var session in candidates) Describe(writer, session);
                        writer.WriteEndArray();
                    },
                    [new CliError("AMBIGUOUS_SESSION", $"{candidates.Count} apps are serving: " +
                        string.Join(", ", candidates.Select(s => $"{s.Title} ({s.Pid})")) + ". Say which with --name or --session <pid>.")]);
                return null;
        }
    }

    public static void Describe(Utf8JsonWriter writer, CliSession session)
    {
        writer.WriteStartObject();
        writer.WriteNumber("pid", session.Pid);
        writer.WriteNumber("port", session.Port);
        writer.WriteString("name", session.Name);
        writer.WriteString("title", session.Title);
        writer.WriteString("mode", session.Mode);
        writer.WriteString("state", session.Report);
        writer.WriteNumber("frame", session.Frame);
        writer.WriteBoolean("renderer", session.Renderer);
        writer.WriteString("project", session.Project);
        writer.WriteString("started", session.Started.ToString("O"));
        writer.WriteEndObject();
    }
}
