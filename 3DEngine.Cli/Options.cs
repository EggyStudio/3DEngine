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
        Console.WriteLine("""
            e3d drives a running 3DEngine app from the terminal.

              e3d status                     what is serving
              e3d open <example> [flags]     start an example serving, and wait until it answers
                                             flags: --headless, --offscreen, --hidden, --frames <n>
              e3d list                       the commands the app answers
              e3d command <name> [args]      run one (alias: cmd)
              e3d shot <path.png>            capture the window
              e3d logs [-n <lines>]          the app's log
              e3d stop                       close the app
              e3d doctor                     remove session files of apps that are gone
              e3d shaders <shaders> <cache>  compile a folder of shaders into a cache a program ships

            Flags for every verb: --json, --quiet, --name <entry>, --session <pid>, --timeout <seconds>.
            Exit codes: 0 ok, 2 bad arguments, 4 nothing to talk to, 6 the command failed.
            """);
        return Exit.Ok;
    }
}
