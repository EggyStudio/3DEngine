using System.Globalization;
using System.Numerics;

namespace Engine;

/// <summary>
/// Commands that show the scene's distance field as it holds the scene and say what it is built
/// from, so a cascade can be looked at with <c>./e3d shot</c> while the program runs.
/// </summary>
internal static class SceneFieldCommands
{
    private static readonly ILogger Logger = Log.Category("Engine.SceneField");

    /// <summary>
    /// The frames the light is given once the field is built, before <c>field.wait</c> answers. The
    /// screen's probes keep up to sixteen frames of their light, and the world's probes add a
    /// bounce a frame from the light of the frame before.
    /// </summary>
    internal const int LightFrames = 60;

    [Command("field.show", "Draws a cascade of the scene's distance field over the window as the field holds the scene, or nothing with -1: field.show <cascade>")]
    internal static string Show(int cascade)
    {
        if (!ConsoleHost.World!.TryGetResource<SceneFieldSettings>(out var field) || !field.On)
            return "the scene's distance field is off, which SetSceneField turns on";
        field.Shown = Math.Clamp(cascade, -1, field.Cascades - 1);
        return field.Shown < 0 ? "showing no cascade" : $"showing cascade {field.Shown}";
    }

    [Command("field.march", "Marches the scene's distance field from a point along a way as global illumination's passes march it, on the field read back from the GPU, and answers where it met a surface, in which cascade and cell, and each step's cascade and distance, along the way toward the sun where none is given, and with sun marches on from the surface it met toward the sun as a probe's hit does where the window's shadow map does not hold it: field.march <x> <y> <z> [dx] [dy] [dz] [sun]")]
    internal static string March(float x, float y, float z, float dx = 0, float dy = 0, float dz = 0, string then = "")
    {
        if (then is not ("" or "sun")) return "The last word is sun or nothing.";
        var world = ConsoleHost.World!;
        if (FieldMarch.Read(world, out var why) is not { } march) return why;
        var renderWorld = world.Resource<Renderer>().RenderWorld;
        var index = Illumination.SunOf(renderWorld);
        Vector3? sun = index >= 0 ? -renderWorld.Get<RenderLights>().All[index].Direction : null;
        var way = new Vector3(dx, dy, dz);
        if (way == Vector3.Zero) way = sun ?? Vector3.Zero;
        if (way == Vector3.Zero || then == "sun" && sun is null)
            return "the window has no directional light for a sun to march toward";
        return march.Describe(new Vector3(x, y, z), Vector3.Normalize(way), then == "sun" ? sun : null);
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

    [Command("field.wait", "Answers once the scene's distance field holds the scene as it stands, every cascade built where the eye has it and every mesh built in, and its light has run 60 frames on it, or once this many frames have run without it, 1,800 unless given, saying which, as a picture of the scene waits before it is taken: field.wait [frames]")]
    internal static string Wait(int frames = 1800)
    {
        var world = ConsoleHost.World!;
        var from = ConsoleHost.Time.FrameCount;
        // A field is drawn from the frame global illumination or the field is turned on, so one not
        // there two frames on is none.
        ConsoleHost.Later(() =>
        {
            var waited = ConsoleHost.Time.FrameCount - from;
            string Said(string what, bool warn = false)
            {
                if (warn) Logger.Warn(what);
                else Logger.Info(what);
                return what;
            }
            if (!world.TryGetResource<Renderer>(out var renderer) || renderer.RenderWorld.TryGet<SceneFieldRenderer>()?.Plan is not { } plan)
                return waited >= 2 ? Said("no scene field is drawn, so nothing was waited for") : null;
            if (plan.FramesBuilt >= LightFrames)
                return Said($"waited {waited} frames for the scene field to be built and its light to run {LightFrames} frames on it");
            if (waited >= (ulong)Math.Max(frames, 1))
                return Said($"the scene field was still building after {waited} frames, {plan.Waiting} triangles waiting and {plan.Arriving} meshes arriving", warn: true);
            return null;
        }, (ulong)Math.Max(frames, 1) + 2);
        return "waiting for the scene's distance field";
    }

    [Command("field.state", "Whether the scene's distance field is built and for how many frames, where each of its cascades lies, the far one named, the GPU memory the field takes, how many meshes are still in it, what this frame stamped, the triangles its builds splatted and those left waiting for later frames, and the most a frame splatted since this was last asked, and the frames and the triangles the field took to hold its last change, or what it still waits for")]
    internal static string State()
    {
        if (!ConsoleHost.World!.TryGetResource<Renderer>(out var renderer)
            || renderer.RenderWorld.TryGet<SceneFieldRenderer>()?.Plan is not { } plan)
            return "the scene's distance field is not built";
        // The far cascade, after the counted ones, says so after its place.
        string Far(int c) => plan.HasFar && c == plan.Cascades - 1 ? " (far)" : "";
        var cascades = Enumerable.Range(0, plan.Cascades).Select(c => plan.BuiltOrigin(c) is { } origin
            ? $"cascade {c}: {plan.CellOf(c)} a cell from {origin.X:0.##},{origin.Y:0.##},{origin.Z:0.##}{Far(c)}"
            : $"cascade {c}: not built{Far(c)}");
        var bytes = GpuSceneField.MemoryBytes(plan.Cascades, SceneFieldPlan.Resolution);
        var (from, to, splatted) = plan.LastChange;
        var built = plan.Built ? $"the field is built, for {plan.FramesBuilt} frames running" : "the field is building";
        return string.Join("\n", cascades.Prepend(built).Append($"{bytes / 1024.0 / 1024.0:0.0} MB of the GPU's memory")
            .Append($"{plan.StillCount} still, {plan.Shapes.Count} stamped into {plan.Bricks.Count} bricks")
            .Append($"{plan.Splatted} triangles splatted of a budget of {plan.TriangleBudget}"
                + (plan.TriangleBudget > plan.BaseTriangleBudget ? $", raised from {plan.BaseTriangleBudget} where the frame has room" : "") + $", {plan.Waiting} waiting, "
                + $"at most {plan.TakeMostSplatted()} a frame since this was last asked")
            .Append(to < 0 ? $"frame {plan.Frame}, changing since frame {from}, {splatted} triangles splatted, cascades {string.Join(" ", plan.Unsettled.Cascades)} to build, "
                + $"{plan.Unsettled.Waiting} meshes waiting for their cascades and {plan.Unsettled.Kept} kept for meshes replacing them"
                : $"frame {plan.Frame}, the last change from frame {from} held whole after frame {to}, {to - from + 1} frames, {splatted} triangles splatted"));
    }
}
