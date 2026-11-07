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
        var index = GlobalIlluminationRenderer.SunOf(renderWorld);
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

    [Command("gi.state", "How much global illumination is worked out, with the quality, the cascades of world probes and their rays, the screen's probes, the reflections, the GPU memory each takes, and the small emitters carried as lights")]
    internal static string IlluminationState() => IlluminationState(ConsoleHost.World!);

    // What gi.state says, which the global illumination window shows too.
    internal static string IlluminationState(World from)
    {
        if (!from.TryGetResource<Renderer>(out var renderer)
            || renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>() is not { Probes: { } probes } gi)
            return "global illumination is off";
        var quality = renderer.RenderWorld.TryGet<GlobalIlluminationSettings>()?.Quality ?? GlobalIllumination.Off;
        var p = probes.Probes;
        var rays = probes.Texels.Select((n, c) => p * p * p * n * n * probes.RaysAt(c)).Sum();
        // Each cascade's rays, merges and the normals its rays met, eight bytes a texel each, every
        // cascade's faces and their light at the surfaces they are read for, the distances and reach
        // each probe is weighed by, the share of the sky its faces and its merged rays see, and each
        // probe's own light, share and place, four texels of eight bytes, and its rays' sums, four of
        // sixteen.
        var world = probes.Texels.Sum(n => 3L * (p * n) * (p * n) * p * 8) + 12L * p * p * p * probes.Cascades * 8 + probes.ReachBytes
            + probes.SkyBytes + probes.WaysBytes + 4L * p * p * p * probes.Cascades * (8 + 16) + probes.CycleBytes;
        var lines = new List<string>
        {
            $"{quality}: {probes.Cascades} cascades of {p * p * p} probes, {string.Join(", ", probes.Texels.Select((n, c) => n * n * probes.RaysAt(c)))} rays each, {rays} rays a frame",
            $"world probes {world / 1024.0 / 1024.0:0.00} MB",
        };
        // The probes whose rays the frame carried past their interval and moved within their
        // texels, by cascade, and the share of every probe's texels theirs are, which the cycle keeps
        // whole, a cascade of sixteen texels a side holding four times one of eight.
        if (probes.CycleOn && renderer.Context.Graphics is GraphicsDevice device)
        {
            var moving = device.ReadCarriedMoving(probes);
            var movingTexels = moving.Select((m, c) => (long)m * probes.Texels[c] * probes.Texels[c]).Sum();
            var all = probes.Texels.Sum(n => (long)p * p * p * n * n);
            var byCascade = string.Join(", ", moving.Select((m, c) => $"{m} of {p * p * p} ({probes.Texels[c]} texels a side)"));
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"probes carried with their rays moving, by cascade, {byCascade}, {movingTexels} of the {all} texels, {100.0 * movingTexels / Math.Max(all, 1):0.0}%"));
        }
        // A screen probe's nine texels of eight bytes, its light, surface, blend and the frame
        // before's two, and its places' cycle's four, and its words of the glow lights; and for
        // each texel of the depth the glow
        // lights' light on it, six texels of eight bytes, this frame's and the frame before's, and
        // its normal in four.
        static double Megabytes(GpuScreenProbes screen) =>
            ((9.0 * 8 + GpuScreenProbes.GlowSeenBytes) * screen.Across * screen.Down
             + (6.0 * 8 + 4) * screen.Glow.Light.Description.Extent.Width * screen.Glow.Light.Description.Extent.Height) / 1024 / 1024;
        // At Low, which leaves them out, probes stand for the glow lights alone while some are lit
        // (GlowOnly), placed and marching to them once a tile with no ray traced.
        if (renderer.RenderWorld.TryGet<GlobalIlluminationSettings>() is { ScreenOff: true })
            lines.Add("no screen probes, which gi.toggle screen left out, the cascades' light read alone and each pixel marching to the glow lights");
        else if (!GlobalIlluminationRenderer.ScreenProbesAt(quality))
            lines.Add(gi.Screen is { } alone
                ? $"no screen probes at {quality}, the cascades' light read alone, and probes every {alone.Tile} pixels, {alone.Across} by {alone.Down}, "
                  + $"standing for the glow lights alone, {Megabytes(alone):0.00} MB"
                : $"no screen probes at {quality}, the cascades' light read alone");
        else if (gi.Screen is { } screen)
            lines.Add($"screen probes every {screen.Tile} pixels, {screen.Across} by {screen.Down}, {screen.Across * screen.Down * 16} rays a frame, "
                      + $"{Megabytes(screen):0.00} MB");
        var (steps, reach) = GlobalIlluminationRenderer.ReflectionStepsAt(quality);
        // The frame before at half the window's size in half floats, its mips a third more, and its
        // depth beside it in floats.
        var history = gi.HistoryViewProjection is null || renderer.RenderWorld.TryGet<SwapchainTarget>() is not { } window ? 0
            : (window.Extent.Width / 2) * (window.Extent.Height / 2) * (8 * 4 / 3.0 + 4) / 1024 / 1024;
        lines.Add($"reflections below roughness {GlobalIlluminationRenderer.GlossyRoughness} in {steps} steps through the depth across {reach} units, "
                  + $"then the field, the frame before kept in {history:0.00} MB");
        if (gi.Rays is { } traced)
            lines.Add($"what the field misses through the device's rays, {traced.Count} copies of {traced.Meshes.Count} meshes in {traced.Bytes / 1024.0 / 1024.0:0.00} MB");
        // The emitters carried as glow lights, of which the nearest the eye are lit and the rest
        // light nothing a probe carries.
        var (carried, lit) = gi.Glow;
        lines.Add($"{carried} small emitters carried as lights, the {lit} nearest the eye lit");
        return string.Join("\n", lines);
    }

    [Command("gi.rays", "Whether High traces what the scene's distance field misses through the device's own ray tracing, where it has it: gi.rays <on|off>")]
    internal static string Rays(string state)
    {
        if (!ConsoleHost.World!.TryGetResource<GlobalIlluminationSettings>(out var settings))
            return "global illumination is off, and SetGlobalIllumination turns it on";
        settings.RaysOff = state is "off" or "0" or "false";
        var device = ConsoleHost.World!.TryGetResource<Renderer>(out var renderer) && renderer.Context.Graphics is GraphicsDevice { CanQueryRays: true };
        return settings.RaysOff ? "High traces through the field alone"
            : device ? "High traces what the field misses through the device's rays"
            : "the device traces no rays, so High traces through the field alone";
    }

    [Command("gi.reference", "Path traces the window's picture through the device's rays, which global illumination at High holds the meshes for, as the reference global illumination is measured by, into a PNG with its linear light, what each pixel shows and the names of those regions beside it, each path bouncing as many times as given or until it ends, -1 for that, and with specular the first face reflecting the indirect light by its specular share too: gi.reference <png> <samples> [bounces] [specular]")]
    internal static string Reference(string path, int samples, int bounces = -1, string ways = "") =>
        ways is "" or "specular" ? BounceReference.Trace(ConsoleHost.World!, path, samples, bounces, ways == "specular") : "The last word is specular or nothing.";

    [Command("gi.probe", "The light arriving at the probe of a cascade nearest a point against a path-traced reference of it, where its own rays met a surface, over every way merged with the cascades above, and each face's light as the model pass reads it, with the ways that carry most of the face furthest from the reference's, each with how far its rays went against where the reference's path through the texel's middle first met a surface, and how many ways met theirs within a tenth of as far, and whether the frame carried the probe past its interval with its rays moving, its paths' bounces counted after the probe's first hit, 0 as the probes' hits are lit with gi.toggle again off: gi.probe <x> <y> <z> [samples] [bounces] [cascade]")]
    internal static string Probe(float x, float y, float z, int samples = 256, int bounces = -1, int cascade = 0) =>
        BounceReference.Probe(ConsoleHost.World!, new System.Numerics.Vector3(x, y, z), samples, bounces, cascade);

    [Command("gi.screenprobe", "The rays of the window's screen probe under a pixel against a path-traced reference along each, where each was met, on the screen, in the field or beyond its reach, and the light the probe sums against the reference's: gi.screenprobe <x> <y> [samples] [bounces]")]
    internal static string ScreenProbe(int x, int y, int samples = 256, int bounces = -1)
    {
        var world = ConsoleHost.World!;
        if (!world.TryGetResource<GlobalIlluminationSettings>(out var settings) || settings.Quality == GlobalIllumination.Off)
            return "global illumination is off, and SetGlobalIllumination turns it on";
        if (!world.TryGetResource<Renderer>(out var renderer) || renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()?.Screen is not { } screen)
            return "the window has no screen probes";
        if (x < 0 || y < 0 || x / screen.Tile >= screen.Across || y / screen.Tile >= screen.Down)
            return $"no screen probe stands under {x}, {y}, the window's probes being {screen.Across} by {screen.Down} tiles of {screen.Tile} pixels";
        // The probe's rays are written from the next frame on, and read once the frames in flight
        // that might not have written them yet have finished.
        settings.LoggedScreenProbe = y / screen.Tile * screen.Across + x / screen.Tile;
        var from = ConsoleHost.Time.FrameCount;
        ConsoleHost.Later(() =>
        {
            if (ConsoleHost.Time.FrameCount < from + 4) return null;
            settings.LoggedScreenProbe = -1;
            return BounceReference.ScreenProbe(world, screen, x, y, samples, bounces);
        });
        return $"reading the screen probe under {x}, {y}";
    }

    [Command("gi.compare", "The window's linear light against a reference gi.reference wrote, the mean of each channel and its error over each region of the view, and a picture of the difference beside it: gi.compare <png>")]
    internal static string Compare(string path) => BounceReference.Compare(ConsoleHost.World!, path);

    [Command("gi.show", "Draws what global illumination holds over the window, the screen's probes as tiles, their light, filtered, or the frame before's share of it, a cascade's rays' light, its merge or its probes as cubes in the scene, the frame against a reference gi.reference wrote, or nothing: gi.show <none|tiles|light|filtered|history|rays|merged|probes|difference> [cascade or png]")]
    internal static string ShowBounce(string view, string with = "")
    {
        if (!ConsoleHost.World!.TryGetResource<GlobalIlluminationSettings>(out var settings) || settings.Quality == GlobalIllumination.Off)
            return "global illumination is off, and SetGlobalIllumination turns it on";
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

    // What gi.show and the global illumination window say is shown.
    internal static string Shown(GlobalIlluminationSettings settings) => settings.Shown switch
    {
        BounceView.None => "showing the window as it is",
        BounceView.Tiles => "showing the screen's probes as tiles, each a dot of its light",
        BounceView.Light => "showing the light each screen probe's rays brought",
        BounceView.Filtered => "showing each screen probe's light filtered, as the model pass reads it",
        BounceView.History => "showing how much of each screen probe's light the frames before gave, red for none and green for fifteen sixteenths",
        BounceView.Rays => $"showing cascade {settings.ShownCascade}'s rays' light",
        BounceView.Merged => $"showing cascade {settings.ShownCascade} merged with those above",
        BounceView.Probes => $"showing cascade {settings.ShownCascade}'s probes as cubes",
        _ => settings.Reference is { } given
            ? $"showing the frame against the reference of {given.Width} by {given.Height}, red where it is brighter and blue where it is darker"
            : "showing nothing until a reference is given",
    };

    [Command("gi.toggle", "Leaves a part of global illumination out, to see what it gives, among the frame before's light in the screen's probes, their filter, the screen's probes, neither traced nor read, the merge of the cascades, the light past the first bounce from the frame before's probes, indirect light following a light that goes out, the small emissive surfaces carried as lights, the crossing of surfaces that let light through, the surfaces' colors, every surface drawn white, the surfaces' reflection of the indirect light, or every cascade but one, or with meshes on traces the probes' rays through the device's rays at High in place of the field: gi.toggle <history|filter|screen|merge|again|follow|glow|through|albedo|specular|cascade|meshes> <on|off|cascade>")]
    internal static string ToggleBounce(string part, string state)
    {
        if (!ConsoleHost.World!.TryGetResource<GlobalIlluminationSettings>(out var settings))
            return "global illumination is off, and SetGlobalIllumination turns it on";
        var on = state is not ("off" or "0" or "false");
        switch (part)
        {
            case "history": settings.HistoryOff = !on; break;
            case "filter": settings.FilterOff = !on; break;
            case "screen": settings.ScreenOff = !on; break;
            case "merge": settings.MergeOff = !on; break;
            case "again": settings.AgainOff = !on; break;
            case "follow": settings.FollowOff = !on; break;
            case "glow": settings.GlowOff = !on; break;
            case "through": settings.ThroughOff = !on; break;
            case "albedo": settings.AlbedoOff = !on; break;
            case "specular": settings.SpecularOff = !on; break;
            case "meshes": settings.MeshesTraced = on; break;
            case "cascade":
                settings.Alone = int.TryParse(state, System.Globalization.CultureInfo.InvariantCulture, out var alone) ? Math.Max(alone, -1) : -1;
                break;
            default: return $"no part {part}, which is one of history, filter, screen, merge, again, follow, glow, through, albedo, specular, cascade and meshes";
        }
        return Switches(settings);
    }

    // The parts of global illumination left out, as gi.toggle and its window say them.
    internal static string Switches(GlobalIlluminationSettings settings)
    {
        var off = new List<string>();
        if (settings.HistoryOff) off.Add("the frame before's light");
        if (settings.FilterOff) off.Add("the screen's filter");
        if (settings.ScreenOff) off.Add("the screen's probes");
        if (settings.MergeOff) off.Add("the merge");
        if (settings.AgainOff) off.Add("the light past the first bounce");
        if (settings.FollowOff) off.Add("indirect light following a light out");
        if (settings.GlowOff) off.Add("the glow lights");
        if (settings.ThroughOff) off.Add("the crossing of surfaces that let light through");
        if (settings.AlbedoOff) off.Add("the surfaces' colors");
        if (settings.SpecularOff) off.Add("the surfaces' reflection of the indirect light");
        if (settings.Alone >= 0) off.Add($"every cascade but {settings.Alone}");
        var said = off.Count == 0 ? "every part of global illumination is on" : $"{string.Join(", ", off)} left out";
        return settings.MeshesTraced ? $"{said}, the probes' rays traced through the meshes" : said;
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
