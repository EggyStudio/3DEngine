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

    [Command("monitors", "Each monitor, its current mode, and the modes it can be set to in fullscreen")]
    internal static string Monitors()
    {
        var text = new StringBuilder();
        for (int i = 0; i < Engine3D.GetMonitorCount(); i++)
        {
            text.AppendLine($"{i} {Engine3D.GetMonitorName(i)}: {Engine3D.GetMonitorWidth(i)}x{Engine3D.GetMonitorHeight(i)} at {Engine3D.GetMonitorRefreshRate(i)} Hz");
            text.AppendLine("  " + string.Join(", ", Engine3D.GetMonitorModes(i).Select(m => $"{m.Width}x{m.Height}@{m.RefreshRate}")));
        }
        return text.Length == 0 ? "no monitors" : text.ToString().TrimEnd();
    }

    [Command("frames.wait", "Answers once this many more frames have run: frames.wait <count>")]
    internal static string Wait(int count)
    {
        var target = ConsoleHost.Time.FrameCount + (ulong)Math.Max(0, count);
        ConsoleHost.Hold(target);
        return $"waited {count} frame(s)";
    }

    [Command("profile", "Where a frame's time goes, averaged over about a second, with the program's own values")]
    internal static string Profile() =>
        ConsoleHost.World!.TryGetResource<FrameProfile>(out var profile) ? profile.Report() : "no profile";

    [Command("profile.reset", "Starts the profile's averages afresh")]
    internal static string ProfileReset()
    {
        if (ConsoleHost.World!.TryGetResource<FrameProfile>(out var profile)) profile.Reset();
        return "reset";
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

    [Command("state.list", "Every state machine, the value it is in and the values it has")]
    internal static string StateList()
    {
        if (!ConsoleHost.World!.TryGetResource<StateTransitions>(out var transitions)) return "no states";
        var text = new StringBuilder();
        foreach (var (state, current, values) in transitions.Describe(ConsoleHost.World))
            text.AppendLine($"{state} = {current ?? "(not added)"}  [{string.Join(", ", values)}]");
        return text.Length == 0 ? "no states" : text.ToString().TrimEnd();
    }

    [Command("state.set", "Moves a state machine to a value at the next frame and answers once it has: state.set <State> <Value>")]
    internal static string StateSet(string state, string value)
    {
        var world = ConsoleHost.World!;
        var refusal = world.TryGetResource<StateTransitions>(out var transitions)
            ? transitions.TryQueue(world, state, value)
            : "The app has no states.";
        if (refusal is not null)
        {
            ConsoleHost.Fail("BAD_STATE", refusal);
            return "not moved";
        }

        // Answered a frame later, so the transition and its enter systems have run by the time the
        // caller looks.
        ConsoleHost.Hold(ConsoleHost.Time.FrameCount + 1);
        return $"{state} -> {value}";
    }

    [Command("scene.save", "Writes every entity not spawned from a model to a scene file: scene.save <path>")]
    internal static string SceneSave(string path)
    {
        var full = Path.GetFullPath(path);
        SceneFile.Save(ConsoleHost.Ecs, full);
        return $"saved {full}";
    }

    [Command("scene.load", "Spawns the entities of a scene file beside what is there: scene.load <path>")]
    internal static string SceneLoad(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
        {
            ConsoleHost.Fail("NOT_FOUND", $"There is no file '{full}'.");
            return "not loaded";
        }
        try
        {
            return $"spawned {SceneFile.Load(ConsoleHost.World!, full).Count} entities";
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
        {
            ConsoleHost.Fail("BAD_FILE", ex.Message);
            return "not loaded";
        }
    }

    [Command("memory", "What the program holds, as name and number pairs: managed memory, entities, the asset server's assets, and the GPU's buffers, images, descriptor sets, pipelines and memory")]
    internal static string Memory()
    {
        // Read as the program left them, without a collection, so a climb shows as it happens and
        // a reader can force one with its own spacing between reads.
        var managed = GC.GetTotalMemory(forceFullCollection: false);
        var heap = GC.GetGCMemoryInfo().HeapSizeBytes;
        var line = $"managed {managed} heap {heap} gen2 {GC.CollectionCount(2)}";
        if (ConsoleHost.World?.TryGetResource<EcsWorld>(out var ecs) == true)
            line += $" entities {ecs.EntityCount} entityIds {ecs.EntityIdRange}";
        // The files the asset server knows, which a level that lets nothing go climbs by as it streams.
        if (ConsoleHost.World?.TryGetResource<AssetServer>(out var server) == true)
            line += $" assets {server.TrackedAssetCount}";
        if (ConsoleHost.World?.TryGetResource<Renderer>(out var renderer) == true && renderer.Context.Graphics is GraphicsDevice device)
        {
            var usage = device.Usage;
            line += $" buffers {usage.Buffers} images {usage.Images} descriptorSets {usage.DescriptorSets} pipelines {usage.Pipelines}"
                    + $" memoryBlocks {usage.MemoryBlocks} blockBytes {usage.BlockBytes} usedBytes {usage.UsedBytes}";
        }
        return line;
    }

    [Command("memory.collect", "The same as memory, read after a full garbage collection, so what is held shows apart from what is waiting to be collected")]
    internal static string MemoryCollected()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return Memory();
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
        {
            text.Append(entity);
            if (ecs.NameOf(entity) is { } name) text.Append(" \"").Append(name).Append('"');
            if (ecs.ParentOf(entity) is var parent and not 0) text.Append(" under ").Append(parent);
            text.Append(": ").AppendJoin(", ", ecs.ComponentTypesOf(entity).Select(t => t.Name)).Append('\n');
        }
        if (entities.Count > limit) text.Append($"... {entities.Count - limit} more\n");
        return entities.Count == 0 ? "no entities with components" : text.ToString().TrimEnd();
    }

    [Command("entity.find", "The id of the first entity with a name: entity.find <name>")]
    internal static string EntityFind(string name)
    {
        var entity = ConsoleHost.Ecs.FindByName(name);
        if (entity == 0) ConsoleHost.Fail("NOT_FOUND", $"No entity is named '{name}'.");
        return entity == 0 ? $"no entity named {name}" : entity.ToString();
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

    [Command("entity.spawn", "Spawns an entity with a Name and answers its id: entity.spawn <name>")]
    internal static string EntitySpawn(string name)
    {
        var ecs = ConsoleHost.Ecs;
        var entity = ecs.Spawn();
        ecs.SetName(entity, name);
        return entity.ToString();
    }

    [Command("entity.despawn", "Despawns an entity and its children: entity.despawn <id>")]
    internal static string EntityDespawn(int id)
    {
        var ecs = ConsoleHost.Ecs;
        if (!ecs.IsAlive(ecs.Handle(id)))
        {
            ConsoleHost.Fail("NOT_FOUND", $"Entity {id} is not alive.");
            return $"no entity {id}";
        }
        ecs.DespawnRecursive(id);
        return $"despawned {id}";
    }

    [Command("entity.add", "Adds a component with its default values, which entity.set then changes: entity.add <id> <Component>")]
    internal static string EntityAdd(int id, string componentName)
    {
        var ecs = ConsoleHost.Ecs;
        if (!ecs.IsAlive(ecs.Handle(id)))
        {
            ConsoleHost.Fail("NOT_FOUND", $"Entity {id} is not alive.");
            return $"no entity {id}";
        }

        var candidates = ComponentTypesNamed(componentName);
        if (candidates.Count != 1)
        {
            var message = candidates.Count == 0
                ? $"No component type is called {componentName}."
                : $"{componentName} could be any of {string.Join(", ", candidates.Select(t => t.FullName))}.";
            ConsoleHost.Fail(candidates.Count == 0 ? "NOT_FOUND" : "AMBIGUOUS", message);
            return message;
        }

        var type = candidates[0];
        if (ecs.GetBoxed(id, type) is not null)
        {
            ConsoleHost.Fail("EXISTS", $"Entity {id} already has a {type.Name}. Change it with entity.set.");
            return $"entity {id} already has a {type.Name}";
        }

        var value = DefaultOf(type);
        ecs.AddBoxed(id, value);
        return $"{type.Name} {Describe(value)}";
    }

    // Value types named so in the loaded assemblies, the engine's first. A component is any
    // struct, so the name is all there is to go on, and an ambiguous one is refused.
    private static List<Type> ComponentTypesNamed(string name)
    {
        var found = new List<Type>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.OfType<Type>().ToArray(); }
            foreach (var type in types)
                if (type is { IsValueType: true, IsEnum: false, IsPrimitive: false, IsGenericTypeDefinition: false, IsPublic: true }
                    && type.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    found.Add(type);
        }
        var engine = found.Where(t => t.Namespace == "Engine").ToList();
        return engine.Count == 1 ? engine : found;
    }

    // A component as a new one should start: its static Default or Identity when it has one, else
    // a constructor whose parameters all have defaults, else the zero value. Several components
    // are wrong at zero (a Transform of scale zero, a Material that is transparent black).
    private static object DefaultOf(Type type)
    {
        foreach (var name in new[] { "Default", "Identity" })
        {
            if (type.GetProperty(name, BindingFlags.Public | BindingFlags.Static) is { } property && property.PropertyType == type)
                return property.GetValue(null)!;
            if (type.GetField(name, BindingFlags.Public | BindingFlags.Static) is { } field && field.FieldType == type)
                return field.GetValue(null)!;
        }

        var optional = type.GetConstructors().FirstOrDefault(c => c.GetParameters() is { Length: > 0 } ps && ps.All(p => p.HasDefaultValue));
        if (optional is not null)
            return optional.Invoke(optional.GetParameters().Select(p => p.DefaultValue).ToArray());

        return Activator.CreateInstance(type)!;
    }

    [Command("entity.set", "Sets one field of an entity's component: entity.set <id> <Component.Field> <value>, with vectors and colors as 1,2,3 and an array's items split by ; as 0,1,0;1,0,0")]
    internal static string EntitySet(int id, string path, string value)
    {
        var ecs = ConsoleHost.Ecs;
        var dot = path.IndexOf('.');
        if (dot <= 0)
        {
            ConsoleHost.Fail("BAD_ARGUMENT", "Name the field as Component.Field, as in Transform.Position.");
            return "name the field as Component.Field";
        }

        var (componentName, fieldName) = (path[..dot], path[(dot + 1)..]);
        var type = ecs.ComponentTypesOf(id).FirstOrDefault(t => t.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase));
        if (type is null || ecs.GetBoxed(id, type) is not { } boxed)
        {
            ConsoleHost.Fail("NOT_FOUND", $"Entity {id} has no {componentName}.");
            return $"entity {id} has no {componentName}";
        }

        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        var property = field is null ? type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase) : null;
        var fieldType = field?.FieldType ?? (property is { CanWrite: true } ? property.PropertyType : null);
        if (fieldType is null)
        {
            ConsoleHost.Fail("NOT_FOUND", $"{type.Name} has no field or settable property {fieldName}.");
            return $"{type.Name} has no field {fieldName}";
        }

        if (!TryParse(value, fieldType, out var parsed))
        {
            ConsoleHost.Fail("BAD_ARGUMENT", $"'{value}' is not a {fieldType.Name}.");
            return $"not a {fieldType.Name}: {value}";
        }

        if (field is not null) field.SetValue(boxed, parsed);
        else property!.SetValue(boxed, parsed);
        ecs.SetBoxed(id, boxed);
        return $"{type.Name} {Describe(boxed)}";
    }

    // Reads a word as the field's type: numbers, flags, text, enums by name, vectors, quaternions
    // and colors as comma-separated numbers, and an array as its items split by semicolons, each
    // read as the element type, so a mesh's positions are 0,1,0;-1,-1,0;1,-1,0.
    private static bool TryParse(string word, Type type, out object? value)
    {
        if (type.IsArray && type.GetElementType() is { } element)
        {
            var items = word.Length == 0 ? [] : word.Split(';', StringSplitOptions.TrimEntries);
            var array = Array.CreateInstance(element, items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                if (!TryParse(items[i], element, out var item))
                {
                    value = null;
                    return false;
                }
                array.SetValue(item, i);
            }
            value = array;
            return true;
        }

        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var numbers = word.Split(',', StringSplitOptions.TrimEntries)
            .Select(n => float.TryParse(n, System.Globalization.NumberStyles.Float, invariant, out var f) ? f : float.NaN).ToArray();
        value = type switch
        {
            _ when type == typeof(string) => word,
            _ when type == typeof(float) && numbers.Length == 1 && !float.IsNaN(numbers[0]) => numbers[0],
            _ when type == typeof(double) && double.TryParse(word, System.Globalization.NumberStyles.Float, invariant, out var d) => d,
            _ when type == typeof(int) && int.TryParse(word, invariant, out var i) => i,
            _ when type == typeof(bool) && bool.TryParse(word, out var b) => b,
            _ when type.IsEnum && Enum.TryParse(type, word, ignoreCase: true, out var e) => e,
            _ when type == typeof(System.Numerics.Vector2) && numbers.Length == 2 => new System.Numerics.Vector2(numbers[0], numbers[1]),
            _ when type == typeof(System.Numerics.Vector3) && numbers.Length == 3 => new System.Numerics.Vector3(numbers[0], numbers[1], numbers[2]),
            _ when type == typeof(System.Numerics.Vector4) && numbers.Length == 4 => new System.Numerics.Vector4(numbers[0], numbers[1], numbers[2], numbers[3]),
            _ when type == typeof(System.Numerics.Quaternion) && numbers.Length == 4 => new System.Numerics.Quaternion(numbers[0], numbers[1], numbers[2], numbers[3]),
            _ when type == typeof(Color) && numbers.Length is 3 or 4 => new Color((byte)numbers[0], (byte)numbers[1], (byte)numbers[2], numbers.Length == 4 ? (byte)numbers[3] : (byte)255),
            _ => null,
        };
        return value is not null && !(value is float f2 && float.IsNaN(f2));
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
