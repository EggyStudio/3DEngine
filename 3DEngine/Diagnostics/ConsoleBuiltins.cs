using System.Reflection;
using System.Text;

namespace Engine;

/// <summary>The console commands every app has: the app, frames, the log, entities, resources and the schedule.</summary>
internal static class ConsoleBuiltins
{
    [Command("app.quit", "Asks the app to close, as the window's close button does")]
    internal static string Quit()
    {
        ConsoleHost.World!.GetOrInsertResource(() => new AppExit()).Requested = true;
        return "closing";
    }

    [Command("frames.wait", "Answers once this many more frames have run: frames.wait <count>")]
    internal static string Wait(int count)
    {
        var target = ConsoleHost.Time.FrameCount + (ulong)Math.Max(0, count);
        ConsoleHost.Hold(target);
        return $"waited {count} frame(s)";
    }

    [Command("log.tail", "The last lines logged: log.tail <count>")]
    internal static string Tail(int count)
    {
        var lines = ConsoleLog.All();
        var text = new StringBuilder();
        foreach (var line in lines.Skip(Math.Max(0, lines.Length - Math.Max(1, count))))
        {
            text.Append('[').Append(line.Frame).Append("] ");
            if (line.Level >= LogLevel.Warning) text.Append(line.Level.ToString().ToUpperInvariant()).Append(' ');
            text.Append(line.Text);
            if (line.Count > 1) text.Append(" (x").Append(line.Count).Append(')');
            text.Append('\n');
        }
        return text.ToString().TrimEnd();
    }

    [Command("shot", "Writes the next frame to a PNG file and answers once it is written: shot <path>")]
    internal static string Shot(string path)
    {
        var full = Path.GetFullPath(path);
        string? outcome = null;
        var written = false;
        if (Screenshots.Request(ConsoleHost.World!, full, result => { outcome = result; written = true; }) is { } refusal)
        {
            ConsoleHost.Fail("NO_RENDERER", refusal);
            return refusal;
        }

        ConsoleHost.Later(() =>
        {
            if (!written) return null;
            if (outcome is not null) ConsoleHost.Fail("NO_CAPTURE", outcome);
            return outcome ?? $"captured {full}";
        });
        return $"capturing {full}";
    }

    [Command("entity.count", "How many entities are alive")]
    internal static string EntityCount() => ConsoleHost.Ecs.EntityCount.ToString();

    [Command("entity.list", "Entities and their component types, up to a limit: entity.list <limit>")]
    internal static string EntityList(int limit)
    {
        var ecs = ConsoleHost.Ecs;
        var entities = ecs.ComponentTypes.SelectMany(ecs.EntitiesOf).Distinct().Order().ToList();
        var text = new StringBuilder();
        foreach (var entity in entities.Take(Math.Max(1, limit)))
            text.Append(entity).Append(": ").AppendJoin(", ", ecs.ComponentTypesOf(entity).Select(t => t.Name)).Append('\n');
        if (entities.Count > limit) text.Append($"... {entities.Count - limit} more\n");
        return entities.Count == 0 ? "no entities with components" : text.ToString().TrimEnd();
    }

    [Command("entity.get", "An entity's components with their fields: entity.get <id>")]
    internal static string EntityGet(int id)
    {
        var ecs = ConsoleHost.Ecs;
        var types = ecs.ComponentTypesOf(id);
        if (types.Count == 0)
        {
            ConsoleHost.Fail("NOT_FOUND", $"Entity {id} has no components.");
            return $"entity {id} has no components";
        }

        var text = new StringBuilder();
        foreach (var type in types)
            text.Append(type.Name).Append(' ').Append(Describe(ecs.GetBoxed(id, type))).Append('\n');
        return text.ToString().TrimEnd();
    }

    [Command("component.list", "Every component type, with how many entities have it")]
    internal static string ComponentList()
    {
        var ecs = ConsoleHost.Ecs;
        return string.Join('\n', ecs.ComponentTypes.Select(t => $"{t.Name} {ecs.CountOf(t)}"));
    }

    [Command("resource.list", "Every resource in the world, by type")]
    internal static string ResourceList() =>
        string.Join('\n', ConsoleHost.World!.ResourceTypes.Select(t => t.Name).Order(StringComparer.Ordinal));

    [Command("schedule.list", "The systems of every stage, in the order they run")]
    internal static string ScheduleList()
    {
        if (ConsoleHost.App is not { } app) return "no app";
        var text = new StringBuilder();
        foreach (var stage in StageOrder.AllInOrder())
        {
            text.Append(stage).Append('\n');
            foreach (var name in app.Schedule.SystemNames(stage)) text.Append("  ").Append(name).Append('\n');
        }
        return text.ToString().TrimEnd();
    }

    // A component's public fields and properties, one level deep. Reflection is used here and
    // nowhere in the engine's hot paths, because it reads any component without a schema.
    private static string Describe(object? value)
    {
        if (value is null) return "{}";
        var type = value.GetType();
        var parts = new List<string>();
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            parts.Add($"{field.Name}={Format(field.GetValue(value))}");
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
        {
            try { parts.Add($"{property.Name}={Format(property.GetValue(value))}"); }
            catch (TargetInvocationException) { }
        }
        return parts.Count == 0 ? value.ToString() ?? "{}" : "{ " + string.Join(", ", parts) + " }";
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        Array array => $"[{array.Length}]",
        float f => f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
