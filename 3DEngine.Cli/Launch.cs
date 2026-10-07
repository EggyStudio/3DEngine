using System.Diagnostics;

namespace Engine.Cli;

/// <summary>The checkout this binary belongs to, found by walking up to <c>3DEngine.slnx</c>.</summary>
internal static class Repo
{
    public static string? Root { get; } = Find(AppContext.BaseDirectory) ?? Find(Environment.CurrentDirectory);

    /// <summary>The built examples program, or <c>null</c> when it has not been built.</summary>
    public static string? Examples
    {
        get
        {
            if (Root is null) return null;
            var directory = Path.Combine(Root, "3DEngine.Examples", "bin", "Debug", "net10.0");
            var apphost = Path.Combine(directory, OperatingSystem.IsWindows() ? "3DEngine.Examples.exe" : "3DEngine.Examples");
            return File.Exists(apphost) ? apphost : null;
        }
    }

    /// <summary>Where a launched app's output goes: <c>build/sessions/&lt;name&gt;.log</c>.</summary>
    public static string LogFor(string name) => Path.Combine(Root ?? Path.GetTempPath(), "build", "sessions", $"{name}.log");

    private static string? Find(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "3DEngine.slnx"))) return directory.FullName;
        return null;
    }
}

internal static class Launch
{
    /// <summary>
    /// Starts an example serving, detached, and answers once it reports ready. Given the path of a
    /// program instead of an example's name, as a game built on the engine, it starts that.
    /// </summary>
    public static int Open(Options options, string[] arguments)
    {
        var example = arguments.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "core_3d_camera_free";
        var flags = arguments.Where(a => a != example).ToList();
        if (!flags.Contains("--serve")) flags.Add("--serve");

        // A program of the user's own is started as it is, with its log named after it. On Windows
        // its path names the apphost without the .exe the build gives it, as a script written for
        // every system names it.
        if (!File.Exists(example) && OperatingSystem.IsWindows() && File.Exists(example + ".exe")) example += ".exe";
        string binary;
        List<string> launch;
        if (File.Exists(example))
        {
            binary = Path.GetFullPath(example);
            example = Path.GetFileNameWithoutExtension(example);
            launch = flags;
        }
        else
        {
            // A path names a program, so one that is not there is refused, where it was taken for an
            // example's name and the log's path made from it.
            if (Path.GetFileName(example) != example)
                return Output.Refuse(options, "open", "NOT_FOUND", $"There is no program '{example}'.");
            if (Repo.Root is null)
                return Output.Refuse(options, "open", "NO_CHECKOUT", "This is not inside a 3DEngine checkout. Give 'e3d open' the path of a program, or run one with --serve yourself.");
            if (Repo.Examples is not { } examples)
                return Output.Refuse(options, "open", "NOT_BUILT", "The examples are not built. Run 'dotnet build 3DEngine.slnx' first.");
            binary = examples;
            launch = [example, .. flags];
        }

        var log = Repo.LogFor(example);
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);

        Process started;
        var launched = DateTimeOffset.UtcNow;
        try
        {
            started = Start(binary, launch, log);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return Output.Refuse(options, "open", "LAUNCH_FAILED", $"{example} could not be started: {error.Message}");
        }

        using var program = new Started(started, binary, launched);
        var pid = started.Id;
        var patience = TimeSpan.FromSeconds(Math.Max(options.Timeout, 60));
        var deadline = DateTime.UtcNow + patience;
        while (DateTime.UtcNow < deadline)
        {
            if (Sessions.Live().FirstOrDefault(s => s.State == "ready" && Launched(s, pid, binary, launched)) is { } session)
                return Output.Print(options, CliJson.Ok("open", writer =>
                {
                    writer.WriteNumber("pid", session.Pid);
                    writer.WriteNumber("port", session.Port);
                    writer.WriteString("example", example);
                    writer.WriteString("mode", session.Mode);
                    writer.WriteString("log", log);
                }), data => Console.WriteLine($"{example} is ready ({data.GetProperty("pid").GetInt32()}, {Output.Text(data, "mode")}), log: {log}"));

            if (!program.Alive) break;
            Thread.Sleep(150);
        }

        // The log says why. An app that will not start says so there and exits, and reporting only
        // the timeout would send whoever asked looking in the wrong place. An app that died says
        // how, its exit code, a crash's on Windows an NTSTATUS, where no line of its log could.
        var alive = program.Alive;
        var exitCode = alive ? null : program.ExitCode;
        return Output.Print(options, CliJson.Envelope("open", success: false,
            writer =>
            {
                writer.WriteString("log", log);
                writer.WriteString("tail", Tail(log, 20));
                if (exitCode is { } code) writer.WriteNumber("exitCode", code);
            },
            [new CliError("NOT_READY", alive
                ? $"{example} did not start serving within {patience.TotalSeconds:0} seconds. Its output is in {log}."
                : $"{example} exited before it was ready{Ending(exitCode)}. Its output is in {log}.")]),
            data => Console.WriteLine(Output.Text(data, "tail")));
    }

    // How a program ended, by its exit code, with what a crash's code means where it is one.
    internal static string Ending(int? exitCode) => exitCode switch
    {
        null => ", how it ended unknown",
        0 => ", with exit code 0, of its own",
        { } code when OperatingSystem.IsWindows() || code < 0 => $", with exit code 0x{code:X8}{Crash(unchecked((uint)code))}",
        > 128 and < 160 => $", with exit code {exitCode}, killed by signal {exitCode - 128}{Signal(exitCode.Value - 128)}",
        { } code => $", with exit code {code}",
    };

    private static string Crash(uint status) => status switch
    {
        0xC0000005 => ", an access violation",
        0xC00000FD => ", a stack overflow",
        0xC0000409 => ", a fail-fast, as .NET's on a fatal error",
        0xC0000374 => ", a corrupted heap",
        0xC000001D => ", an illegal instruction",
        0xC0000094 => ", an integer divided by zero",
        0xE0434352 => ", a .NET exception no one caught",
        0xC0000017 => ", memory exhausted",
        _ => "",
    };

    private static string Signal(int signal) => signal switch
    {
        6 => ", SIGABRT",
        9 => ", SIGKILL",
        11 => ", SIGSEGV",
        _ => "",
    };

    /// <summary>The last lines of a launched app's log, or of a serving app's own log ring.</summary>
    public static int Logs(Options options, string[] arguments)
    {
        var count = 40;
        var index = Array.IndexOf(arguments, "-n");
        if (index >= 0 && index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out var lines)) count = lines;

        if (Sessions.Pick(options, "logs", out var refusal) is not { } session) return Output.Print(options, refusal);
        return Output.Print(options, CliClient.Send(session, "run", $"log.tail {count}", options.Timeout));
    }

    private static Process Start(string binary, IReadOnlyList<string> arguments, string log)
    {
        // The checkout, as the examples expect, or outside one the program's own folder.
        var start = new ProcessStartInfo { WorkingDirectory = Repo.Root ?? Path.GetDirectoryName(binary)!, UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            // The line is given to cmd.exe as written. Given as one argument it was quoted as a
            // program's argument is, each quote inside escaped with a backslash, which cmd.exe
            // keeps, so start took the escaped title for the program and the log's path was not one.
            start.FileName = "cmd.exe";
            start.Arguments = $"/c start \"e3d\" /b \"{binary}\" {string.Join(' ', arguments)} > \"{log}\" 2>&1";
        }
        else
        {
            // exec, so the shell is replaced by the app and the pid is the app's.
            start.FileName = "/bin/sh";
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add($"exec {Quote(binary)} {string.Join(' ', arguments.Select(Quote))} > {Quote(log)} 2>&1");
        }

        return Process.Start(start) ?? throw new InvalidOperationException("the process did not start");
    }

    // The program an open started, held so its exit code can be read once it ends. Elsewhere than
    // Windows the shell is replaced by the program, so the process started is the program. On
    // Windows it is cmd.exe, which ends at once, and the program is found by its file's name and the
    // time it began, and held from the first time it is seen.
    private sealed class Started(Process started, string binary, DateTimeOffset launched) : IDisposable
    {
        private Process? _program = OperatingSystem.IsWindows() ? null : started;

        public bool Alive
        {
            get
            {
                if (_program is null && OperatingSystem.IsWindows()) _program = Find();
                try { return _program is { } program ? !program.HasExited : false; }
                catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
            }
        }

        public int? ExitCode
        {
            get
            {
                try { return _program is { HasExited: true } program ? program.ExitCode : null; }
                catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
            }
        }

        private Process? Find()
        {
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(binary)))
            {
                try
                {
                    // HasExited opens the handle the exit code is read through later.
                    if (process.StartTime.ToUniversalTime() >= launched.UtcDateTime.AddSeconds(-1) && !process.HasExited) return process;
                }
                catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // A process that ended while it was read, or one this user may not read.
                }
                process.Dispose();
            }
            return null;
        }

        public void Dispose()
        {
            _program?.Dispose();
            started.Dispose();
        }
    }

    // Whether a session is the one this open started. On Windows the program is started by cmd.exe's
    // start, so the pid Start gives is cmd.exe's, which ends at once, and the session is known by
    // the program's name, its entry assembly's, which the session carries, and the time it began.
    // Elsewhere the shell is replaced by the program, which keeps its pid.
    private static bool Launched(CliSession session, int pid, string binary, DateTimeOffset launched) =>
        OperatingSystem.IsWindows()
            ? session.Name.Equals(Path.GetFileNameWithoutExtension(binary), StringComparison.OrdinalIgnoreCase) && session.Started >= launched.AddSeconds(-1)
            : session.Pid == pid;

    private static string Quote(string word) => $"'{word.Replace("'", "'\\''")}'";

    private static string Tail(string path, int count)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = new Queue<string>();
            while (reader.ReadLine() is { } line)
            {
                lines.Enqueue(line);
                if (lines.Count > count) lines.Dequeue();
            }
            return lines.Count == 0 ? "(nothing yet)" : string.Join("\n", lines);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"({path} could not be read: {error.Message})";
        }
    }
}
