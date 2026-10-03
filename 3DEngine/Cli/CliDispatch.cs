namespace Engine;

/// <summary>Answers the four operations the CLI asks for: ping, status, list and run.</summary>
internal static class CliDispatch
{
    public static ulong Frame(World world) => world.TryGetResource<Time>(out var time) ? time.FrameCount : 0;

    /// <summary>The envelope answering <paramref name="request"/>, or <c>null</c> when it is held for a later frame.</summary>
    public static string? Answer(CliRequest request, World world, App app) => request.Operation switch
    {
        "ping" => CliJson.Ok("ping", writer => writer.WriteString("state", "ready"), request.Id),
        "status" => Status(request, world),
        "list" => List(request),
        "run" => Run(request, world, app),
        _ => CliJson.Fail(request.Operation, "UNKNOWN_OPERATION",
            $"'{request.Operation}' is not something this app answers. It answers ping, status, list and run.", request.Id),
    };

    private static string Status(CliRequest request, World world) => CliJson.Ok("status", writer =>
    {
        var config = world.TryGetResource<Config>(out var c) ? c : Config.Default;
        writer.WriteNumber("pid", Environment.ProcessId);
        writer.WriteString("title", config.WindowData.Title);
        writer.WriteString("mode", RunMode.Describe(config));
        writer.WriteNumber("frame", Frame(world));
        if (world.TryGetResource<Time>(out var time))
        {
            writer.WriteNumber("fps", Math.Round(time.SmoothedFps, 1));
            writer.WriteNumber("elapsed", Math.Round(time.ElapsedSeconds, 3));
        }
        writer.WriteBoolean("renderer", RunMode.HasRenderer(world));
        writer.WriteNumber("entities", world.TryGetResource<EcsWorld>(out var ecs) ? ecs.EntityCount : 0);
        writer.WriteNumber("commands", ConsoleCommands.All.Count);
    }, request.Id);

    private static string List(CliRequest request) => CliJson.Ok("list", writer =>
    {
        writer.WritePropertyName("commands");
        writer.WriteStartArray();
        foreach (var command in ConsoleCommands.All)
        {
            writer.WriteStartObject();
            writer.WriteString("name", command.Name);
            writer.WriteString("help", command.Help);
            writer.WriteString("usage", command.Usage);
            writer.WritePropertyName("parameters");
            writer.WriteStartArray();
            foreach (var parameter in command.Parameters)
            {
                writer.WriteStartObject();
                writer.WriteString("name", parameter.Name);
                writer.WriteString("kind", parameter.Kind);
                writer.WriteBoolean("line", parameter.TakesLine);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }, request.Id);

    private static string? Run(CliRequest request, World world, App app)
    {
        var line = (request.Line ?? string.Empty).Trim();
        if (line.Length == 0)
            return CliJson.Fail("command", "BAD_ARGUMENT", "No command was given to run.", request.Id);

        var cut = line.IndexOf(' ');
        var name = cut < 0 ? line : line[..cut];
        if (ConsoleCommands.Find(name) is null)
            return CliJson.Fail("command", "UNKNOWN_COMMAND", $"'{name}' is not a command this app registers. Run 'e3d list' to see what is.", request.Id);

        string? answer;
        CliError? failure;
        ulong? held;
        Func<string?>? pending;
        using (ConsoleHost.Lend(world, app))
        {
            answer = ConsoleCommands.Run(line);
            failure = ConsoleHost.Failure;
            held = ConsoleHost.Held;
            pending = ConsoleHost.Pending;
        }

        var frame = Frame(world);
        if (failure is { } error)
            return CliJson.Envelope("command", success: false, writer => Payload(writer, name, answer, frame), [error], id: request.Id);

        if (pending is not null)
        {
            request.Holding = name;
            request.Held = answer;
            request.Poll = pending;
            request.Release = frame + ConsoleHost.LaterFrames;
            return null;
        }

        if (held is { } release)
        {
            // Answered now and handed back later, so "act, let it settle, then look" is one call.
            request.Holding = name;
            request.Held = answer;
            request.Release = release;
            return null;
        }

        return CliJson.Ok("command", writer => Payload(writer, name, answer, frame), request.Id);
    }

    public static string Release(CliRequest request, ulong frame) =>
        CliJson.Ok("command", writer => Payload(writer, request.Holding ?? string.Empty, request.Held, frame), request.Id);

    private static void Payload(System.Text.Json.Utf8JsonWriter writer, string name, string? answer, ulong frame)
    {
        writer.WriteString("command", name);
        if (answer is null) writer.WriteNull("result");
        else writer.WriteString("result", answer);
        writer.WriteNumber("frame", frame);
    }
}
