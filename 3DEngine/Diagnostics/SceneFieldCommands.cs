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
    internal static string IlluminationState()
    {
        if (!ConsoleHost.World!.TryGetResource<Renderer>(out var renderer)
            || renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>() is not { Probes: { } probes } gi)
            return "no light bounces";
        var quality = renderer.RenderWorld.TryGet<GlobalIlluminationSettings>()?.Quality ?? GlobalIllumination.Off;
        var p = probes.Probes;
        var rays = probes.Texels.Sum(n => p * p * p * n * n);
        // Each cascade's rays and merges, eight bytes a texel each, and every cascade's faces.
        var world = probes.Texels.Sum(n => 2L * (p * n) * (p * n) * p * 8) + 6L * p * p * p * probes.Cascades * 8;
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
