using System.Diagnostics;
using System.Text.Json;

namespace Engine.Cli;

internal static class Verbs
{
    public static int Status(Options options)
    {
        var sessions = CliSessionFile.All();
        if (sessions.Count == 0)
            return Output.Refuse(options, "status", "NO_SESSION",
                "No app is serving. Start one with 'e3d open <example>', or run a program with --serve.");

        return Output.Print(options, CliJson.Ok("status", writer =>
        {
            writer.WritePropertyName("sessions");
            writer.WriteStartArray();
            foreach (var session in sessions) Sessions.Describe(writer, session);
            writer.WriteEndArray();
        }), data =>
        {
            foreach (var s in data.GetProperty("sessions").EnumerateArray())
                Console.WriteLine($"{Output.Text(s, "state"),-12} {s.GetProperty("pid").GetInt32(),-8} {Output.Text(s, "mode"),-9} " +
                                  $"port {s.GetProperty("port").GetInt32(),-6} frame {s.GetProperty("frame").GetUInt64(),-8} {Output.Text(s, "title")}");
        });
    }

    /// <summary>Compiles a folder of shaders into a cache folder, with no app running.</summary>
    public static int Shaders(Options options, string[] arguments)
    {
        if (arguments.Length != 2)
            return Output.Refuse(options, "shaders", "BAD_ARGUMENT", "Name the shader folder and the cache folder: e3d shaders <shaders> <cache>.");
        if (!Directory.Exists(arguments[0]))
            return Output.Refuse(options, "shaders", "NOT_FOUND", $"There is no folder '{arguments[0]}'.");

        IReadOnlyList<string> compiled;
        try
        {
            compiled = SlangLoader.Precompile(arguments[0], arguments[1]);
        }
        catch (InvalidOperationException ex)
        {
            return Output.Refuse(options, "shaders", "FAILED", ex.Message);
        }

        return Output.Print(options, CliJson.Ok("shaders", writer =>
        {
            writer.WritePropertyName("compiled");
            writer.WriteStartArray();
            foreach (var name in compiled) writer.WriteStringValue(name);
            writer.WriteEndArray();
        }), data => Console.WriteLine($"compiled {data.GetProperty("compiled").GetArrayLength()} shader(s) into {arguments[1]}"));
    }

    public static int List(Options options)
    {
        if (Sessions.Pick(options, "list", out var refusal) is not { } session) return Output.Print(options, refusal);
        return Output.Print(options, CliClient.Send(session, "list", seconds: options.Timeout), data =>
        {
            foreach (var command in data.GetProperty("commands").EnumerateArray())
            {
                var name = Output.Text(command, "name");
                var usage = Output.Text(command, "usage");
                Console.WriteLine($"{(usage.Length > 0 ? $"{name} {usage}" : name),-40} {Output.Text(command, "help")}");
            }
        });
    }

    public static int Command(Options options, string[] arguments)
    {
        if (arguments.Length == 0)
            return Output.Refuse(options, "command", "BAD_ARGUMENT", "Which command? Run 'e3d list' to see what this app answers.");
        if (Sessions.Pick(options, "command", out var refusal) is not { } session) return Output.Print(options, refusal);
        return Output.Print(options, CliClient.Send(session, "run", Line(arguments), options.Timeout));
    }

    public static int Shot(Options options, string[] arguments)
    {
        if (arguments.Length == 0)
            return Output.Refuse(options, "shot", "BAD_ARGUMENT", "Where should the picture go? e3d shot <path.png>");
        if (Sessions.Pick(options, "shot", out var refusal) is not { } session) return Output.Print(options, refusal);

        // The app answers once the capture is written, so the file is whole when this returns.
        var path = Path.GetFullPath(arguments[0]);
        return Output.Print(options, CliClient.Send(session, "run", $"shot \"{path}\"", options.Timeout));
    }

    public static int Stop(Options options)
    {
        if (Sessions.Pick(options, "stop", out var refusal) is not { } session) return Output.Print(options, refusal);

        var answer = CliClient.Send(session, "run", "app.quit", options.Timeout);

        // Waited for rather than assumed, because "asked to close" and "closed" are different
        // answers to "can another one start".
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && session.Running) Thread.Sleep(100);

        if (session.Running)
        {
            // By the id in the session file, never by name, so nothing else is closed by mistake.
            try { Process.GetProcessById(session.Pid).Kill(); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or SystemException)
            {
                return Output.Print(options, answer);
            }
        }

        CliSessionFile.Remove(session.Pid);
        return Output.Print(options, CliJson.Ok("stop", writer =>
        {
            writer.WriteNumber("pid", session.Pid);
            writer.WriteString("title", session.Title);
        }), data => Console.WriteLine($"stopped {Output.Text(data, "title")} ({data.GetProperty("pid").GetInt32()})"));
    }

    public static int Doctor(Options options)
    {
        var swept = CliSessionFile.Prune();
        var root = Repo.Root;
        var slangc = root is null ? null : Path.Combine(root, "build", "tools", "slang", "bin", OperatingSystem.IsWindows() ? "slangc.exe" : "slangc");
        return Output.Print(options, CliJson.Ok("doctor", writer =>
        {
            writer.WriteNumber("swept", swept);
            writer.WriteNumber("serving", Sessions.Live().Count);
            writer.WriteString("sessions", CliSessionFile.Directory);
            writer.WriteString("checkout", root ?? "");
            writer.WriteBoolean("slangc", slangc is not null && File.Exists(slangc));
            writer.WriteBoolean("examplesBuilt", Repo.Examples is not null);
        }), data => Console.WriteLine(
            $"removed {data.GetProperty("swept").GetInt32()} stale session file(s), {data.GetProperty("serving").GetInt32()} serving\n" +
            $"sessions: {Output.Text(data, "sessions")}\ncheckout: {Output.Text(data, "checkout")}\n" +
            $"slangc fetched: {data.GetProperty("slangc").GetBoolean()}, examples built: {data.GetProperty("examplesBuilt").GetBoolean()}"));
    }

    // Words back into a line the app splits again, quoting any that hold spaces.
    private static string Line(string[] words) => string.Join(" ", words.Select(word =>
        word.Length > 0 && !word.Any(char.IsWhiteSpace) ? word : $"\"{word.Replace("\\", "\\\\").Replace("\"", "\\\"")}\""));
}
