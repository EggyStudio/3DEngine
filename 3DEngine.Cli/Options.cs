namespace Engine.Cli;

/// <summary>The flags every verb shares, read from the environment and then the command line.</summary>
internal sealed record Options
{
    public bool Json { get; init; }
    public bool Quiet { get; init; }
    public string? Name { get; init; }
    public int? Pid { get; init; }
    public double Timeout { get; init; } = 30;

    public static (Options Flags, string[] Words) Parse(string[] arguments)
    {
        var options = new Options
        {
            Json = Environment.GetEnvironmentVariable("E3D_FORMAT") is "json",
            Name = Environment.GetEnvironmentVariable("E3D_NAME") is { Length: > 0 } name ? name : null,
        };

        var rest = new List<string>();
        for (var index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--json":
                    options = options with { Json = true };
                    continue;
                case "--quiet" or "-q":
                    options = options with { Quiet = true };
                    continue;
                case "--name" when index + 1 < arguments.Length:
                    options = options with { Name = arguments[++index] };
                    continue;
                case "--session" or "-s" when index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out var pid):
                    index++;
                    options = options with { Pid = pid };
                    continue;
                case "--timeout" or "-t" when index + 1 < arguments.Length && double.TryParse(arguments[index + 1], out var wait):
                    index++;
                    options = options with { Timeout = wait };
                    continue;
                default:
                    rest.Add(arguments[index]);
                    continue;
            }
        }

        return (options, [.. rest]);
    }
}

/// <summary>Exit codes, so a script can branch without reading the envelope.</summary>
internal static class Exit
{
    public const int Ok = 0;
    public const int General = 1;
    public const int BadArguments = 2;
    public const int Precondition = 4;
    public const int Failed = 6;

    public static int For(string code) => code switch
    {
        "NO_SESSION" or "SESSION_UNREACHABLE" or "SESSION_CLOSING" or "NO_RENDERER" or "NO_CHECKOUT" => Precondition,
        "AMBIGUOUS_SESSION" or "BAD_ARGUMENT" or "BAD_REQUEST" or "UNKNOWN_COMMAND" => BadArguments,
        _ => Failed,
    };
}

internal static class Help
{
    public static int Print()
    {
        Console.WriteLine(Text);
        return Exit.Ok;
    }

    /// <summary>
    /// The usage of <paramref name="verb"/> alone, its lines of the list and the flags every verb
    /// shares, or a refusal where it is no verb.
    /// </summary>
    public static int Print(Options options, string verb)
    {
        var name = verb switch { "cmd" => "command", "screenshot" => "shot", _ => verb };
        var lines = Text.Split('\n');
        var usage = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].TrimStart().StartsWith($"e3d {name} ", StringComparison.Ordinal) && lines[i].Trim() != $"e3d {name}") continue;
            usage.Add(lines[i]);
            // The lines under it that carry on its flags, indented past where a verb starts.
            while (i + 1 < lines.Length && lines[i + 1].StartsWith("      ", StringComparison.Ordinal) && !lines[i + 1].TrimStart().StartsWith("e3d ", StringComparison.Ordinal))
                usage.Add(lines[++i]);
        }
        if (usage.Count == 0)
            return Output.Refuse(options, verb, "BAD_ARGUMENT", $"'{verb}' is not a verb. Run 'e3d help' for the list.");
        Console.WriteLine(string.Join('\n', usage));
        Console.WriteLine();
        Console.WriteLine(lines.First(line => line.StartsWith("Every verb takes", StringComparison.Ordinal)));
        return Exit.Ok;
    }

    private const string Text = """
            e3d controls a running 3DEngine app from the terminal.

              e3d status                     what is serving
              e3d open <example> [flags]     start an example serving, and wait until it answers
              e3d open <program> [flags]     the same for a program of your own, by its path
                                             with --headless, --offscreen, --hidden or --frames <n>
              e3d list                       the commands the app answers
              e3d command <name> [args]      run one, or e3d cmd
              e3d eval <code>                compile C# against the running app and run it
              e3d eval -f <file.cs>          the same for a file
              e3d shot <path.png>            capture the window
              e3d record <path> <length>     record it as an animated GIF or PNG, and wait for the file
                  [--every <step>] [--scale <s>] [--play <time>]
              e3d animate <out> <pictures...> --delay <ms> [--loop <n>] [--scale <s>]
                                             make PNG captures into an animated GIF or PNG
              e3d logs [-n <lines>]          the app's log
              e3d stop                       close the app
              e3d doctor                     remove session files of apps that are gone
              e3d shaders <shaders> <cache>  compile a folder of shaders into a cache a program ships

            Every verb takes --json, --quiet, --name <entry>, --session <pid> and --timeout <seconds>.
            The exit code is 0 for success, 2 for bad arguments, 4 for nothing to talk to and 6 where
            the command failed. A verb followed by --help prints its own lines of this and does nothing
            else.
            """;
}
