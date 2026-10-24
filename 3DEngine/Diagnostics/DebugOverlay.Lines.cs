using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Engine;

// What each line of the overlay says, in the words BevyCSharp's overlay says them, the engine and
// the frame over the left column, the frame times beside the strip, the camera, the world and the
// program's sections under them, and the system, the GPU, the display, the renderer and the
// profiler's passes down the right.
internal sealed partial class DebugOverlay
{
    /// <summary>What the overlay says, a list for each place a line goes, an empty line the gap between two parts.</summary>
    /// <param name="Head">The engine's version and the frame, over the strip.</param>
    /// <param name="Beside">The frames' times and the GPU's, beside the strip.</param>
    /// <param name="Left">The camera, the world and the program's sections, under the strip.</param>
    /// <param name="Right">The system, the GPU, the display and the renderer, with the profiler's passes under them.</param>
    internal sealed record Said(List<string> Head, List<string> Beside, List<string> Left, List<string> Right);

    /// <summary>The passes the profiler lists, the slowest, with a line saying how many more there are.</summary>
    private const int ProfilerPasses = 24;

    /// <summary>What the overlay says of a world as it stands.</summary>
    internal Said Lines(World world)
    {
        world.TryGetResource<Time>(out var time);
        world.TryGetResource<Renderer>(out var renderer);
        var device = renderer is { Context.IsInitialized: true } && renderer.Context.Graphics is GraphicsDevice running ? running : null;

        var head = new List<string>
        {
            Say($"3DEngine {Version()} ({Build}, {(device is null ? "no device" : "Vulkan " + device.AdapterInfo.Api)})"),
            time is null ? "No clock" : Say($"{time.SmoothedFps:0} fps, {time.DeltaSeconds * 1000:0.0} ms a frame, frame {time.FrameCount}"),
        };

        var (now, average, lowFps) = Stats(FrameTimes());
        var beside = new List<string>
        {
            Say($"Now {now:0.0} ms, average {average:0.0} ms, 1% low {lowFps:0} fps"),
            GpuTime(world),
        };

        var left = new List<string>();
        left.AddRange(CameraLines(world));
        left.Add("");
        world.TryGetResource<EcsWorld>(out var ecs);
        left.Add(Say($"Entities {ecs?.EntityCount ?? 0}"));
        left.Add(Say($"Meshes {Count<MeshStore>(world, store => store.Count)}, textures {Count<TextureStore>(world, store => store.Count)}, assets {Count<AssetServer>(world, server => server.TrackedAssetCount)}"));
        AddSections(left);

        var right = new List<string>
        {
            Say($".NET {Environment.Version}, {RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}"),
            SystemInfo.Os,
            Say($"CPU {SystemInfo.Cpu}, {Environment.ProcessorCount} logical cores"),
            Say($"Memory {Environment.WorkingSet / 1048576.0:0} MB of {SystemInfo.Memory / 1048576.0:0} MB, managed {GC.GetTotalMemory(false) / 1048576.0:0} MB"),
            "",
        };
        right.AddRange(GpuLines(device));
        right.Add(Display(world, device));
        right.Add("");
        right.AddRange(RendererLines(world, renderer, ecs));
        if (ProfilerShown) AddPasses(right, world);
        return new Said(head, beside, left, right);
    }

    /// <summary>
    /// The program's sections, each after a gap, its name and then its lines, or why it said nothing
    /// where its lines threw.
    /// </summary>
    /// <remarks>
    /// The list is copied before any section is asked, so a section that adds or removes one, its own
    /// among them, changes the next writing and not this one.
    /// </remarks>
    private void AddSections(List<string> left)
    {
        foreach (var (name, lines) in _sections.ToArray())
        {
            left.Add("");
            left.Add(name);
            try
            {
                left.AddRange(lines());
            }
            catch (Exception error)
            {
                // Said where its lines would be rather than logged, since a section asked four times
                // a second would fill the log, and the overlay is where its author looks.
                left.Add($"{name}: {error.GetType().Name}, {error.Message}");
            }
        }
    }

    /// <summary>The profiler's lines, the render graph's passes the slowest first by the GPU's time, then the CPU's, averaged over the profile's last second.</summary>
    private static void AddPasses(List<string> right, World world)
    {
        right.Add("");
        var timings = new Dictionary<string, (double? Gpu, double? Cpu)>(StringComparer.Ordinal);
        if (world.TryGetResource<FrameProfile>(out var profile))
            foreach (var (group, averages) in profile.Snapshot().Groups)
            {
                if (group is not ("gpu" or "cpu")) continue;
                foreach (var (name, ms) in averages)
                {
                    var pass = name[(group.Length + 1)..];
                    var known = timings.GetValueOrDefault(pass);
                    timings[pass] = group == "gpu" ? (ms, known.Cpu) : (known.Gpu, ms);
                }
            }
        if (timings.Count == 0)
        {
            right.Add("Pass timings not measured, as no frame is rendered");
            return;
        }

        right.Add("Passes, the slowest first");
        var slowest = timings.OrderByDescending(pass => pass.Value.Gpu ?? -1).ThenByDescending(pass => pass.Value.Cpu ?? -1).ToList();
        foreach (var (name, (gpu, cpu)) in slowest.Take(ProfilerPasses))
            right.Add($"{name} {(gpu is { } g ? Say($"GPU {g:0.00} ms") : "GPU none")}, {(cpu is { } c ? Say($"CPU {c:0.00} ms") : "CPU none")}");
        if (slowest.Count > ProfilerPasses) right.Add(Say($"and {slowest.Count - ProfilerPasses} more"));
    }

    // Where the window's camera stands and faces, a camera entity's or the frame's first
    // BeginMode3D's, in Minecraft's words, its north minus Z as the engine's forward is.
    private static IEnumerable<string> CameraLines(World world)
    {
        if (Camera(world) is not var (position, forward, fieldOfView)) return ["No camera"];

        // The heading turns from north toward plus X, east, and the pitch is rounded to a tenth and
        // added to zero, so a level view reads 0.0 and not -0.0.
        var heading = (MathF.Atan2(forward.X, -forward.Z) * 180f / MathF.PI + 360f) % 360f;
        var pitch = MathF.Round(MathF.Asin(Math.Clamp(forward.Y, -1f, 1f)) * 180f / MathF.PI, 1) + 0f;
        return
        [
            Say($"XYZ {position.X:0.000} / {position.Y:0.000} / {position.Z:0.000}"),
            Say($"Facing {Compass(heading)} (towards {Towards(forward)}), heading {heading:0.0}, pitch {pitch:0.0}"),
            // In words, since the engine's font has no degree sign.
            fieldOfView is { } fov ? Say($"Field of view {fov:0} degrees") : "Field of view none, the projection is orthographic",
        ];
    }

    /// <summary>The window's camera, its place, the way it looks and its vertical field of view in degrees, or null for an orthographic one.</summary>
    private static (Vector3 Position, Vector3 Forward, float? FieldOfView)? Camera(World world)
    {
        if (world.TryGetResource<EcsWorld>(out var ecs))
            foreach (var (entity, camera) in ecs.Query<Camera>())
            {
                if (camera.Target.IsValid) continue;
                var placed = TransformPropagation.WorldMatrix(ecs, entity);
                var forward = Vector3.TransformNormal(-Vector3.UnitZ, placed);
                return (placed.Translation, forward == Vector3.Zero ? -Vector3.UnitZ : Vector3.Normalize(forward), float.RadiansToDegrees(camera.FovY));
            }

        if (world.TryGetResource<Mode3DCamera>(out var mode3D) && mode3D.Camera is { } flat)
        {
            var look = flat.Target - flat.Position;
            return (flat.Position, look == Vector3.Zero ? -Vector3.UnitZ : Vector3.Normalize(look),
                flat.Projection == CameraProjection.Perspective ? flat.FovY : null);
        }
        return null;
    }

    private static IEnumerable<string> GpuLines(GraphicsDevice? device)
    {
        if (device is null) return ["GPU none, as the run draws nothing"];
        var adapter = device.AdapterInfo;
        var usage = device.Usage;
        return
        [
            Say($"GPU {adapter.Name} ({Words(adapter.DeviceType.ToString())})"),
            adapter.Driver.Length > 0 ? Say($"Driver {adapter.Driver}, Vulkan {adapter.Api}") : "Driver unknown",
            Say($"GPU memory {usage.UsedBytes / 1048576.0:0} MB in use of {usage.BlockBytes / 1048576.0:0} MB allocated"),
        ];
    }

    // The window's size and refresh, or the images an offscreen run draws into.
    private static string Display(World world, GraphicsDevice? device)
    {
        world.TryGetResource<Config>(out var config);
        if (world.TryGetResource<AppWindow>(out var window))
        {
            var refresh = E3D.GetMonitorRefreshRate(E3D.GetCurrentMonitor());
            var scale = E3D.GetWindowScaleDPI().X;
            return Say($"Display {window.Sdl.Width} x {window.Sdl.Height}{(refresh > 0 ? $", {refresh} Hz" : "")}, scale {scale:0.##}, vsync {(config?.Vsync == false ? "off" : "on")}");
        }
        if (device is not null && config is not null) return Say($"Display offscreen, {config.WindowData.Width} x {config.WindowData.Height}");
        return "Display none, a headless run";
    }

    private static IEnumerable<string> RendererLines(World world, Renderer? renderer, EcsWorld? ecs)
    {
        var lines = new List<string>();
        var models = renderer?.RenderWorld.TryGet<ModelRenderer>();
        var passes = renderer?.Timings.NodeCpu.Count ?? 0;
        lines.Add(Say($"Passes {passes}, draw calls {models?.DrawCalls ?? 0}"));

        var (draws, triangles) = Submitted(world);
        lines.Add(Say($"Triangles {triangles:N0} in {draws:N0} model draws"));

        int directional = 0, point = 0, spot = 0, shadowed = 0;
        if (ecs is not null)
            foreach (var (_, light) in ecs.Query<Light>())
            {
                if (light.Kind == LightKind.Directional) directional++;
                else if (light.Kind == LightKind.Point) point++;
                else if (light.Kind == LightKind.Spot) spot++;
                if (light.CastsShadows) shadowed++;
            }
        lines.Add(Say($"Lights {directional} directional, {point} point, {spot} spot, {shadowed} casting shadows"));

        lines.Add(renderer?.RenderWorld.TryGet<FrameShadow>() is { } shadow && (shadow.Cascades.Count > 0 || shadow.SpotLights is { Count: > 0 } || shadow.PointLights is { Count: > 0 })
            ? Say($"Shadows {shadow.Cascades.Count} cascades{(shadow.Fill is null ? "" : " and a fill")} of {shadow.TileSize} texels, {shadow.SpotLights?.Count ?? 0} spot and {shadow.PointLights?.Count ?? 0} point lights mapped")
            : "Shadows none");

        var illumination = SceneFieldCommands.IlluminationState(world).Split('\n')[0];
        lines.Add(illumination.StartsWith("global illumination is off", StringComparison.Ordinal) ? "Global illumination off" : $"Global illumination {illumination}");
        return lines;
    }

    // The model draws the frame records and the triangles they hold, each draw's mesh and each
    // instance group's mesh by its instances, before the passes cull what they do not see.
    private static (long Draws, long Triangles) Submitted(World world)
    {
        if (!world.TryGetResource<ModelDrawList>(out var list) || !world.TryGetResource<MeshStore>(out var meshes)) return (0, 0);
        var known = new Dictionary<int, long>();
        long Of(int mesh)
        {
            if (known.TryGetValue(mesh, out var count)) return count;
            return known[mesh] = meshes.TryGetData(mesh, out var vertices, out var indices) ? (indices.Length > 0 ? indices.Length : vertices.Length) / 3 : 0;
        }

        long draws = 0, triangles = 0;
        foreach (var draw in list.Draws)
        {
            draws++;
            triangles += Of(draw.Mesh);
        }
        foreach (var group in list.Groups)
        {
            draws += group.Count;
            triangles += Of(group.Template.Mesh) * group.Count;
        }
        return (draws, triangles);
    }

    // The frame's time on the GPU, its passes' averages added, which the profile measures where
    // the device has timestamp queries.
    private static string GpuTime(World world)
    {
        if (!world.TryGetResource<FrameProfile>(out var profile)) return "GPU time not measured";
        var (_, groups) = profile.Snapshot();
        var gpu = groups.FirstOrDefault(group => group.Group == "gpu").Averages;
        return gpu is { Count: > 0 }
            ? Say($"GPU {gpu.Sum(pass => pass.Milliseconds):0.00} ms a frame over {gpu.Count} passes")
            : "GPU time not measured, as the device has no timestamps here";
    }

    private static long Count<T>(World world, Func<T, long> count) where T : notnull => world.TryGetResource<T>(out var found) ? count(found) : 0;

    /// <summary>The engine's version, without the commit a build adds after a plus.</summary>
    private static string Version()
    {
        var version = typeof(E3D).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }

#if DEBUG
    private const string Build = "debug build";
#else
    private const string Build = "release build";
#endif

    /// <summary>The eight points of the compass a heading in degrees is nearest, north being minus Z.</summary>
    internal static string Compass(float heading) =>
        new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" }[(int)MathF.Round(heading / 45f) % 8];

    /// <summary>The axis and its sign a heading faces nearest along the ground, as Minecraft names it.</summary>
    private static string Towards(Vector3 forward) =>
        MathF.Abs(forward.X) > MathF.Abs(forward.Z) ? (forward.X > 0f ? "positive X" : "negative X") : (forward.Z > 0f ? "positive Z" : "negative Z");

    /// <summary>A name's parts as words, as <c>DiscreteGpu</c> is "discrete gpu".</summary>
    private static string Words(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private static string Say(FormattableString line) => FormattableString.Invariant(line);
}
