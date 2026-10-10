namespace Engine;

/// <summary>
/// Commands that show the scene's distance field as it holds the scene and say what it is built
/// from, so a cascade can be looked at with <c>./e3d shot</c> while the program runs.
/// </summary>
internal static class SceneFieldCommands
{
    [Command("field.show", "Draws a cascade of the scene's distance field over the window as the field holds the scene, or nothing with -1: field.show <cascade>")]
    internal static string Show(int cascade)
    {
        if (!ConsoleHost.World!.TryGetResource<SceneFieldSettings>(out var field) || !field.On)
            return "the scene's distance field is off, which SetSceneField turns on";
        field.Shown = Math.Clamp(cascade, -1, field.Cascades - 1);
        return field.Shown < 0 ? "showing no cascade" : $"showing cascade {field.Shown}";
    }

    [Command("field.rebuild", "Builds the scene's distance field again each frame for this many frames, as many cascades a frame as its budget allows, so profile times a build: field.rebuild <frames>")]
    internal static string Rebuild(int frames)
    {
        if (!ConsoleHost.World!.TryGetResource<Renderer>(out var renderer)
            || renderer.RenderWorld.TryGet<SceneFieldRenderer>()?.Plan is not { } plan)
            return "the scene's distance field is not built";
        plan.Rebuild(frames);
        return $"building {plan.Budget} of its {plan.Cascades} cascades each frame for {Math.Max(1, frames)} frames";
    }

    [Command("gi.state", "How much light bounces: the quality, the cascades of world probes and their rays, the screen's probes, the reflections, and the GPU memory each takes")]
    internal static string IlluminationState() => IlluminationState(ConsoleHost.World!);

    // What gi.state says, which the bounce window shows too.
    internal static string IlluminationState(World from)
    {
        if (!from.TryGetResource<Renderer>(out var renderer)
            || renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>() is not { Probes: { } probes } gi)
            return "no light bounces";
        var quality = renderer.RenderWorld.TryGet<GlobalIlluminationSettings>()?.Quality ?? GlobalIllumination.Off;
        var p = probes.Probes;
        var rays = probes.Texels.Sum(n => p * p * p * n * n);
        // Each cascade's rays and merges, eight bytes a texel each, every cascade's faces, the
        // distances and reach each probe is weighed by, the share of the sky its faces and its merged
        // rays see, and each probe's own light, share and place, four texels of eight bytes, and its
        // rays' sums, four of sixteen.
        var world = probes.Texels.Sum(n => 2L * (p * n) * (p * n) * p * 8) + 6L * p * p * p * probes.Cascades * 8 + probes.ReachBytes
            + probes.SkyBytes + 4L * p * p * p * probes.Cascades * (8 + 16);
        var lines = new List<string>
        {
            $"{quality}: {probes.Cascades} cascades of {p * p * p} probes, {string.Join(", ", probes.Texels.Select(n => n * n))} rays each, {rays} rays a frame",
            $"world probes {world / 1024.0 / 1024.0:0.00} MB",
        };
        if (gi.Screen is { } screen)
            lines.Add($"screen probes every {screen.Tile} pixels, {screen.Across} by {screen.Down}, {screen.Across * screen.Down * 16} rays a frame, "
                      + $"{5.0 * screen.Across * screen.Down * 8 / 1024 / 1024:0.00} MB");
        var (steps, reach) = GlobalIlluminationRenderer.ReflectionStepsAt(quality);
        // The frame before at half the window's size in half floats, its mips a third more, and its
        // depth beside it in floats.
        var history = gi.HistoryViewProjection is null || renderer.RenderWorld.TryGet<SwapchainTarget>() is not { } window ? 0
            : (window.Extent.Width / 2) * (window.Extent.Height / 2) * (8 * 4 / 3.0 + 4) / 1024 / 1024;
        lines.Add($"reflections below roughness {GlobalIlluminationRenderer.GlossyRoughness} in {steps} steps through the depth across {reach} units, "
                  + $"then the field, the frame before kept in {history:0.00} MB");
        if (gi.Rays is { } traced)
            lines.Add($"what the field misses through the device's rays, {traced.Count} copies of {traced.Meshes.Count} meshes in {traced.Bytes / 1024.0 / 1024.0:0.00} MB");
        return string.Join("\n", lines);
    }

    [Command("gi.rays", "Whether High traces what the scene's distance field misses through the device's own ray tracing, where it has it: gi.rays <on|off>")]
    internal static string Rays(string state)
    {
        if (!ConsoleHost.World!.TryGetResource<GlobalIlluminationSettings>(out var settings))
            return "no light bounces, which SetGlobalIllumination turns on";
        settings.RaysOff = state is "off" or "0" or "false";
        var device = ConsoleHost.World!.TryGetResource<Renderer>(out var renderer) && renderer.Context.Graphics is GraphicsDevice { CanQueryRays: true };
        return settings.RaysOff ? "High traces through the field alone"
            : device ? "High traces what the field misses through the device's rays"
            : "the device traces no rays, so High traces through the field alone";
    }

    [Command("gi.reference", "Path traces the window's picture through the device's rays, which light bouncing at High holds the meshes for, as the reference the light that bounces is measured by, into a PNG with its linear light, what each pixel shows and the names of those regions beside it, its light bouncing as many times as given or until each path ends: gi.reference <png> <samples> [bounces]")]
    internal static string Reference(string path, int samples, int bounces = -1) => BounceReference.Trace(ConsoleHost.World!, path, samples, bounces);

    [Command("gi.probe", "The light arriving at the probe of a cascade nearest a point against a path-traced reference of it, where its own rays met a surface, over every way merged with the cascades above, and each face's light as the model pass reads it: gi.probe <x> <y> <z> [samples] [bounces] [cascade]")]
    internal static string Probe(float x, float y, float z, int samples = 256, int bounces = -1, int cascade = 0) =>
        BounceReference.Probe(ConsoleHost.World!, new System.Numerics.Vector3(x, y, z), samples, bounces, cascade);

    [Command("gi.compare", "The window's linear light against a reference gi.reference wrote, the mean of each channel and its error over each region of the view, and a picture of the difference beside it: gi.compare <png>")]
    internal static string Compare(string path) => BounceReference.Compare(ConsoleHost.World!, path);

    [Command("gi.show", "Draws what the light that bounces holds over the window: the screen's probes as tiles, their light, filtered, or the frame before's share of it, a cascade's rays' light, its merge or its probes as cubes in the scene, the frame against a reference gi.reference wrote, or nothing: gi.show <none|tiles|light|filtered|history|rays|merged|probes|difference> [cascade or png]")]
    internal static string ShowBounce(string view, string with = "")
    {
        if (!ConsoleHost.World!.TryGetResource<GlobalIlluminationSettings>(out var settings) || settings.Quality == GlobalIllumination.Off)
            return "no light bounces, which SetGlobalIllumination turns on";
        if (!Enum.TryParse<BounceView>(view, ignoreCase: true, out var shown) || !Enum.IsDefined(shown))
            return $"no view {view}, which is one of {string.Join(", ", Enum.GetNames<BounceView>().Select(n => n.ToLowerInvariant()))}";
        if (shown is BounceView.Rays or BounceView.Merged or BounceView.Probes && with.Length > 0)
        {
            if (!int.TryParse(with, System.Globalization.CultureInfo.InvariantCulture, out var cascade))
                return $"{with} is not a cascade";
            settings.ShownCascade = Math.Max(cascade, 0);
        }
        if (shown == BounceView.Difference)
        {
            if (with.Length == 0) return "the difference is drawn against a reference, gi.show difference <png>";
            if (GiveReference(settings, with) is { } why) return why;
        }
        settings.Shown = shown;
        return Shown(settings);
    }

    // Reads the reference at path for the difference to be drawn against, or says why it cannot.
    internal static string? GiveReference(GlobalIlluminationSettings settings, string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full + ".pfm")) return $"no reference at {full}, which gi.reference writes";
        var (width, height, light) = BounceReference.ReadPfm(full + ".pfm");
        settings.Reference = (light, width, height, (settings.Reference?.Version ?? 0) + 1);
        return null;
    }

    // What gi.show and the bounce window say is shown.
    internal static string Shown(GlobalIlluminationSettings settings) => settings.Shown switch
    {
        BounceView.None => "showing the window as it is",
        BounceView.Tiles => "showing the screen's probes as tiles, each a dot of its light",
        BounceView.Light => "showing the light each screen probe's rays brought",
        BounceView.Filtered => "showing each screen probe's light filtered, as the model pass reads it",
        BounceView.History => "showing how much of each screen probe's light the frame before's gave, red for none and green for four fifths",
        BounceView.Rays => $"showing cascade {settings.ShownCascade}'s rays' light",
        BounceView.Merged => $"showing cascade {settings.ShownCascade} merged with those above",
        BounceView.Probes => $"showing cascade {settings.ShownCascade}'s probes as cubes",
        _ => settings.Reference is { } given
            ? $"showing the frame against the reference of {given.Width} by {given.Height}, red where it is brighter and blue where it is darker"
            : "showing nothing until a reference is given",
    };

    [Command("gi.toggle", "Leaves a part of the light that bounces out, to see what it gives: the frame before's light in the screen's probes, their filter, the screen's probes, the merge of the cascades, the light that bounces again from the frame before's probes, the bounce following a light that goes out, or every cascade but one: gi.toggle <history|filter|screen|merge|again|follow|cascade> <on|off|cascade>")]
    internal static string ToggleBounce(string part, string state)
    {
        if (!ConsoleHost.World!.TryGetResource<GlobalIlluminationSettings>(out var settings))
            return "no light bounces, which SetGlobalIllumination turns on";
        var on = state is not ("off" or "0" or "false");
        switch (part)
        {
            case "history": settings.HistoryOff = !on; break;
            case "filter": settings.FilterOff = !on; break;
            case "screen": settings.ScreenOff = !on; break;
            case "merge": settings.MergeOff = !on; break;
            case "again": settings.AgainOff = !on; break;
            case "follow": settings.FollowOff = !on; break;
            case "cascade":
                settings.Alone = int.TryParse(state, System.Globalization.CultureInfo.InvariantCulture, out var alone) ? Math.Max(alone, -1) : -1;
                break;
            default: return $"no part {part}, which is one of history, filter, screen, merge, again, follow and cascade";
        }
        return Switches(settings);
    }

    // The parts of the light that bounces left out, as gi.toggle and the bounce window say them.
    internal static string Switches(GlobalIlluminationSettings settings)
    {
        var off = new List<string>();
        if (settings.HistoryOff) off.Add("the frame before's light");
        if (settings.FilterOff) off.Add("the screen's filter");
        if (settings.ScreenOff) off.Add("the screen's probes");
        if (settings.MergeOff) off.Add("the merge");
        if (settings.AgainOff) off.Add("the light that bounces again");
        if (settings.FollowOff) off.Add("the bounce following a light out");
        if (settings.Alone >= 0) off.Add($"every cascade but {settings.Alone}");
        return off.Count == 0 ? "every part of the light that bounces is on" : $"left out: {string.Join(", ", off)}";
    }

    [Command("field.state", "Where each cascade of the scene's distance field lies, how many meshes are still in it, and what this frame stamped")]
    internal static string State()
    {
        if (!ConsoleHost.World!.TryGetResource<Renderer>(out var renderer)
            || renderer.RenderWorld.TryGet<SceneFieldRenderer>()?.Plan is not { } plan)
            return "the scene's distance field is not built";
        var cascades = Enumerable.Range(0, plan.Cascades).Select(c => plan.BuiltOrigin(c) is { } origin
            ? $"cascade {c}: {plan.CellOf(c)} a cell from {origin.X:0.##},{origin.Y:0.##},{origin.Z:0.##}"
            : $"cascade {c}: not built");
        return string.Join("\n", cascades.Append($"{plan.StillCount} still, {plan.Shapes.Count} stamped into {plan.Bricks.Count} bricks"));
    }
}
