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
