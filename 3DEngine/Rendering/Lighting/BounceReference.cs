using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Engine;

/// <summary>
/// The measure of the light that bounces: a path-traced reference of the window's picture
/// (<c>gi_reference.slang</c>, <see cref="GraphicsDevice.TraceReference"/>), and the window's own
/// linear light compared with it over named regions of the view, as <c>gi.reference</c> and
/// <c>gi.compare</c> run them.
/// </summary>
/// <remarks>
/// <para>
/// A reference is written as a PNG of its light as the window draws it with
/// <c>SetTonemap(Tonemap.None)</c>, with its linear light beside it in a PFM file and what each
/// pixel shows in a regions file, a copy of the ray scene and the face it turns, and the regions'
/// names in a text file. A region is named by what it is in a room: the top of a slab lying flat is
/// a floor and its underside a ceiling, a standing slab is a wall by its color and the way it faces,
/// and anything thicker is a block by its order and the face seen, a glowing one said to glow.
/// </para>
/// <para>
/// The reference traces the window's meshes as the device's ray tracing holds them, at
/// <see cref="GlobalIllumination.High"/> on a GPU that traces rays: each copy's flat color times its
/// texture's average and the light it gives off, its first face lit as the model pass lights one
/// and every face after as the light that bounces does, the sun as the first directional light, and
/// the sky as the environment map or the ambient lights.
/// </para>
/// </remarks>
internal static class BounceReference
{
    private static readonly string[] Axes = ["+x", "-x", "+y", "-y", "+z", "-z"];

    /// <summary>Traces the window's picture with <paramref name="samples"/> paths a pixel and writes it to <paramref name="path"/> and its files beside it.</summary>
    /// <returns>What was written, or why nothing was.</returns>
    public static string Trace(World world, string path, int samples)
    {
        if (!world.TryGetResource<Renderer>(out var renderer) || renderer.Context.Graphics is not GraphicsDevice device)
            return "there is no renderer";
        var render = renderer.RenderWorld;
        if (!device.CanQueryRays)
            return "the device traces no rays, which the reference is traced through";
        if (render.TryGet<GlobalIlluminationRenderer>()?.Rays is not { } scene)
            return "the window's meshes are not held for the device's rays, which light bouncing at High on a GPU that traces rays holds them for";
        if (render.TryGet<WindowView>() is not { } view || !Matrix4x4.Invert(view.ViewProjection, out var inverse)
            || render.TryGet<SwapchainTarget>() is not { } window)
            return "the window draws no meshes through a camera";
        if (!world.TryGetResource<AssetServer>(out var assets)) return "there is no asset server to compile gi_reference.slang";

        var program = assets.LoadSync<ShaderProgram>("shaders/gi_reference.slang");
        var environment = render.TryGet<EnvironmentMap>() is not null ? render.TryGet<ModelRenderer>()?.Environment : null;
        using var black = environment is null ? device.CreateCubeMap(1, 1, new Half[6 * 4]) : null;
        var (cubeView, cubeSampler) = environment is not null ? (environment.View, environment.Sampler) : (black!.View, black.Sampler);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var traced = device.TraceReference(program.Compute, scene, inverse, (int)window.Extent.Width, (int)window.Extent.Height,
            Math.Max(1, samples), Lights(render, environment is not null), cubeView, cubeSampler);
        var seconds = clock.Elapsed.TotalSeconds;

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        WritePng(full, traced);
        WritePfm(full + ".pfm", traced);
        WriteRegions(full + ".regions", traced);
        var names = Names(scene.Copies, traced.Regions);
        File.WriteAllLines(full + ".regions.txt", names.OrderBy(n => n.Key).Select(n => $"{n.Key}\t{n.Value}"));
        return $"traced {traced.Width} by {traced.Height} at {samples} paths a pixel in {seconds:0.0} s, {scene.Count} copies, " +
               $"{names.Count} regions, into {full} with its .pfm, .regions and .regions.txt";
    }

    /// <summary>One region of the view as <see cref="Measure"/> reads it: its name, its pixels, and the mean light the reference and the frame give it, linear, with the mean of each pixel's difference.</summary>
    internal sealed record RegionLight(string Name, int Pixels, Vector3 Reference, Vector3 Frame, Vector3 Absolute)
    {
        /// <summary>The frame's light less the reference's as a share of the reference's, by luminance.</summary>
        public float Share => Luminance(Frame - Reference) / Math.Max(Luminance(Reference), 1e-4f);
    }

    /// <summary>
    /// The window's linear light against the reference at <paramref name="path"/>, the mean of each
    /// channel and its error over each region, and a picture of the difference written beside it.
    /// </summary>
    public static string Compare(World world, string path)
    {
        if (Measure(world, path, out var why, out var differencePath) is not { } regions) return why!;
        static string Rgb(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:0.000} {v.Y:0.000} {v.Z:0.000}");
        static string Signed(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:+0.000;-0.000;0.000} {v.Y:+0.000;-0.000;0.000} {v.Z:+0.000;-0.000;0.000}");
        var lines = new List<string> { "region, pixels, reference, frame, frame less reference, its share, mean of each pixel's difference" };
        lines.AddRange(regions.Select(r =>
            string.Create(CultureInfo.InvariantCulture, $"{r.Name}, {r.Pixels}, {Rgb(r.Reference)}, {Rgb(r.Frame)}, {Signed(r.Frame - r.Reference)}, {r.Share * 100:+0;-0;0}%, {Rgb(r.Absolute)}")));
        lines.Add($"the difference, red where the frame is brighter and blue where it is darker, in {differencePath}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// The regions of at least 50 pixels the reference at <paramref name="path"/> names, most pixels
    /// first, with the window's linear light against it, then every region at once as
    /// <c>every region</c>, and the picture of the difference written at
    /// <paramref name="differencePath"/>; or null and <paramref name="why"/> where there is no
    /// reference or no frame to compare.
    /// </summary>
    internal static IReadOnlyList<RegionLight>? Measure(World world, string path, out string? why, out string differencePath)
    {
        var full = Path.GetFullPath(path);
        differencePath = Path.Combine(Path.GetDirectoryName(full)!, Path.GetFileNameWithoutExtension(full) + "-difference.png");
        why = null;
        if (!File.Exists(full + ".pfm") || !File.Exists(full + ".regions"))
        {
            why = $"no reference at {full}, which gi.reference writes";
            return null;
        }
        if (!world.TryGetResource<Renderer>(out var renderer) || renderer.Context.Graphics is not GraphicsDevice device
            || renderer.RenderWorld.TryGet<BloomRenderer>()?.Decoded is not { } decoded)
        {
            why = "the window's scene is not decoded to linear light, which light bouncing has it decoded for";
            return null;
        }
        var (width, height, reference) = ReadPfm(full + ".pfm");
        var regions = ReadRegions(full + ".regions", width * height);
        if (decoded.Extent.Width != width || decoded.Extent.Height != height)
        {
            why = $"the reference is {width} by {height} and the window {decoded.Extent.Width} by {decoded.Extent.Height}";
            return null;
        }
        var frame = device.ReadFloats(decoded.ColorView.Image);
        var names = File.Exists(full + ".regions.txt")
            ? File.ReadAllLines(full + ".regions.txt").Select(l => l.Split('\t')).Where(p => p.Length == 2)
                .ToDictionary(p => uint.Parse(p[0], CultureInfo.InvariantCulture), p => p[1])
            : [];

        var sums = new Dictionary<uint, (int Count, Vector3 Reference, Vector3 Frame, Vector3 Absolute)>();
        var difference = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            var r = new Vector3(reference[i * 3], reference[i * 3 + 1], reference[i * 3 + 2]);
            var f = new Vector3(frame[i * 4], frame[i * 4 + 1], frame[i * 4 + 2]);
            if (!float.IsFinite(f.X + f.Y + f.Z)) f = Vector3.Zero;
            if (regions[i] != 0)
            {
                var (count, rs, fs, abs) = sums.GetValueOrDefault(regions[i]);
                sums[regions[i]] = (count + 1, rs + r, fs + f, abs + Vector3.Abs(f - r));
            }
            // Red where the frame is brighter than the reference, blue where it is darker, by the
            // share of the reference's light, over the reference's light dimmed.
            var (lr, lf) = (Luminance(r), Luminance(f));
            var share = (lf - lr) / (lr + 0.05f);
            var backdrop = (byte)Math.Clamp(MathF.Sqrt(Math.Max(lr, 0)) * 60, 0, 60);
            difference[i * 4] = (byte)Math.Clamp(backdrop + Math.Max(share, 0) * 255, 0, 255);
            difference[i * 4 + 1] = backdrop;
            difference[i * 4 + 2] = (byte)Math.Clamp(backdrop + Math.Max(-share, 0) * 255, 0, 255);
            difference[i * 4 + 3] = 255;
        }
        PngWriter.Write(differencePath, difference, width, height);

        var measured = sums.Where(s => s.Value.Count >= 50).OrderByDescending(s => s.Value.Count)
            .Select(s => new RegionLight(names.GetValueOrDefault(s.Key, $"region {s.Key}"), s.Value.Count,
                s.Value.Reference / s.Value.Count, s.Value.Frame / s.Value.Count, s.Value.Absolute / s.Value.Count))
            .ToList();
        var total = sums.Values.Sum(v => v.Count);
        if (total > 0)
            measured.Add(new RegionLight("every region", total, sums.Values.Aggregate(Vector3.Zero, (a, v) => a + v.Reference) / total,
                sums.Values.Aggregate(Vector3.Zero, (a, v) => a + v.Frame) / total, sums.Values.Aggregate(Vector3.Zero, (a, v) => a + v.Absolute) / total));
        return measured;
    }

    private static float Luminance(Vector3 c) => 0.2126f * c.X + 0.7152f * c.Y + 0.0722f * c.Z;

    // The lights as gi_reference.slang's ReferenceLights: the sun, the first directional light, and
    // whether it casts shadows; the sky, the environment map's intensity where there is one and the
    // ambient lights' sum where there is not; and the point and spot lights, sixteen at most.
    private static byte[] Lights(RenderWorld render, bool environment)
    {
        var bytes = new byte[GraphicsDevice.ReferenceLightsBytes];
        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
        var all = render.TryGet<RenderLights>()?.All ?? [];
        var sun = all.FindIndex(light => light.Kind == LightKind.Directional);
        if (sun >= 0)
        {
            var toward = -all[sun].Direction;
            (floats[0], floats[1], floats[2], floats[3]) = (toward.X, toward.Y, toward.Z, 1);
            (floats[4], floats[5], floats[6], floats[7]) = (all[sun].EmittedColor.X, all[sun].EmittedColor.Y, all[sun].EmittedColor.Z, all[sun].CastsShadows ? 1 : 0);
        }
        var sky = Vector3.Zero;
        if (environment && render.TryGet<EnvironmentMap>() is { } map)
        {
            sky = new Vector3(map.Intensity);
            floats[11] = 1;
        }
        else
            foreach (var light in all.Where(light => light.Kind == LightKind.Ambient)) sky += light.EmittedColor;
        (floats[8], floats[9], floats[10]) = (sky.X, sky.Y, sky.Z);

        var lamps = MemoryMarshal.Cast<byte, LightUboEntry>(bytes.AsSpan(64));
        var count = 0;
        foreach (var light in all)
        {
            if (light.Kind is not (LightKind.Point or LightKind.Spot) || count == lamps.Length) continue;
            lamps[count++] = new LightUboEntry
            {
                PositionAndKind = new Vector4(light.Position, (int)light.Kind),
                DirectionAndRange = new Vector4(light.Direction, light.Range),
                ColorAndShadow = new Vector4(light.EmittedColor, light.CastsShadows ? 1 : 0),
                Cone = new Vector4(light.CosInner, light.CosOuter, 0, 0),
            };
        }
        floats[12] = count;
        return bytes;
    }

    // Each region's name by what it is in a room.
    private static Dictionary<uint, string> Names(IReadOnlyList<RayInstance> copies, uint[] regions)
    {
        var seen = regions.Where(r => r != 0).Distinct().Order().ToList();
        var blocks = new Dictionary<int, int>();
        var names = new Dictionary<uint, string>();
        foreach (var region in seen)
        {
            var (copy, axis) = ((int)((region - 1) / 8), (int)((region - 1) % 8));
            if (copy >= copies.Count || axis >= Axes.Length) continue;
            var instance = copies[copy];
            var (min, max) = Bounds(instance);
            var size = max - min;
            var sides = new[] { size.X, size.Y, size.Z };
            var order = sides.Select((s, i) => (s, i)).OrderBy(p => p.s).ToArray();
            var flat = order[0].s < 0.2f * order[1].s;
            var glow = instance.Emission.LengthSquared() > 0 ? "glowing " : "";
            var color = ColorWord(instance.Color);
            string name;
            if (flat && order[0].i == 1 && axis is 2 or 3)
                name = glow.Length > 0 ? $"glowing {color} panel's {(axis == 2 ? "top" : "underside")}" : axis == 2 ? $"{color} floor" : $"{color} ceiling";
            else if (flat && order[0].i != 1 && axis / 2 == order[0].i)
                name = $"{glow}{color} wall facing {Axes[axis]}";
            else if (flat)
                name = $"{glow}{color} {(order[0].i != 1 ? "wall" : glow.Length > 0 ? "panel" : "floor")}'s edge facing {Axes[axis]}";
            else
            {
                if (!blocks.TryGetValue(copy, out var number)) blocks[copy] = number = blocks.Count + 1;
                var face = axis switch { 2 => "top", 3 => "underside", _ => $"side facing {Axes[axis]}" };
                name = string.Create(CultureInfo.InvariantCulture, $"{glow}{color} block {number} ({size.Y:0.#} high), {face}");
            }
            names[region] = name;
        }
        // Two regions of one name, as two floors, are told apart by their copies.
        foreach (var group in names.GroupBy(n => n.Value).Where(g => g.Count() > 1).ToList())
            foreach (var (region, name) in group)
                names[region] = $"{name} (copy {(region - 1) / 8})";
        return names;
    }

    // A copy's box in the world, from its mesh's box at rest moved by its matrix.
    private static (Vector3 Min, Vector3 Max) Bounds(RayInstance instance)
    {
        if (instance.Mesh is not ModelVertex[] { Length: > 0 } vertices) return (Vector3.Zero, Vector3.Zero);
        var (lo, hi) = (vertices[0].Position, vertices[0].Position);
        foreach (var v in vertices) (lo, hi) = (Vector3.Min(lo, v.Position), Vector3.Max(hi, v.Position));
        var (min, max) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
        for (int corner = 0; corner < 8; corner++)
        {
            var p = Vector3.Transform(new Vector3((corner & 1) == 0 ? lo.X : hi.X, (corner & 2) == 0 ? lo.Y : hi.Y, (corner & 4) == 0 ? lo.Z : hi.Z), instance.World);
            (min, max) = (Vector3.Min(min, p), Vector3.Max(max, p));
        }
        return (min, max);
    }

    // A linear color's word: white, gray or black where its channels are alike, its hue otherwise.
    private static string ColorWord(Vector3 c)
    {
        var (high, low) = (MathF.Max(c.X, MathF.Max(c.Y, c.Z)), MathF.Min(c.X, MathF.Min(c.Y, c.Z)));
        if (high - low < 0.25f * Math.Max(high, 1e-4f)) return high > 0.4f ? "white" : high > 0.05f ? "gray" : "black";
        var hue = c.X >= c.Y && c.X >= c.Z ? (c.Y - c.Z) / (high - low) : c.Y >= c.Z ? 2 + (c.Z - c.X) / (high - low) : 4 + (c.X - c.Y) / (high - low);
        hue = (hue * 60 + 360) % 360;
        return hue switch { < 20 or >= 330 => "red", < 45 => "orange", < 70 => "yellow", < 160 => "green", < 200 => "cyan", < 260 => "blue", _ => "magenta" };
    }

    private static void WritePng(string path, TracedReference traced)
    {
        var rgba = new byte[traced.Width * traced.Height * 4];
        for (int i = 0; i < traced.Width * traced.Height; i++)
        {
            for (int c = 0; c < 3; c++)
            {
                var linear = Math.Clamp(traced.Light[i * 3 + c], 0, 1);
                var encoded = linear <= 0.0031308f ? linear * 12.92f : 1.055f * MathF.Pow(linear, 1 / 2.4f) - 0.055f;
                rgba[i * 4 + c] = (byte)MathF.Round(encoded * 255);
            }
            rgba[i * 4 + 3] = 255;
        }
        PngWriter.Write(path, rgba, traced.Width, traced.Height);
    }

    // A PFM file of the linear light: its header, then three little-endian floats a pixel with rows
    // from the bottom, as the format has them.
    private static void WritePfm(string path, TracedReference traced)
    {
        using var file = File.Create(path);
        file.Write(Encoding.ASCII.GetBytes($"PF\n{traced.Width} {traced.Height}\n-1.0\n"));
        for (int y = traced.Height - 1; y >= 0; y--)
            file.Write(MemoryMarshal.AsBytes(traced.Light.AsSpan(y * traced.Width * 3, traced.Width * 3)));
    }

    /// <summary>A PFM file's linear light, three floats a pixel with rows from the top, and its size.</summary>
    internal static (int Width, int Height, float[] Light) ReadPfm(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var lines = 0;
        var at = 0;
        while (lines < 3) if (bytes[at++] == '\n') lines++;
        var header = Encoding.ASCII.GetString(bytes, 0, at).Split('\n');
        var size = header[1].Split(' ');
        var (width, height) = (int.Parse(size[0], CultureInfo.InvariantCulture), int.Parse(size[1], CultureInfo.InvariantCulture));
        var rows = MemoryMarshal.Cast<byte, float>(bytes.AsSpan(at, width * height * 12));
        var light = new float[width * height * 3];
        for (int y = 0; y < height; y++)
            rows.Slice((height - 1 - y) * width * 3, width * 3).CopyTo(light.AsSpan(y * width * 3));
        return (width, height, light);
    }

    // The regions as little-endian words a pixel, rows from the top.
    private static void WriteRegions(string path, TracedReference traced) => File.WriteAllBytes(path, MemoryMarshal.AsBytes(traced.Regions.AsSpan()).ToArray());

    private static uint[] ReadRegions(string path, int pixels)
    {
        var words = MemoryMarshal.Cast<byte, uint>(File.ReadAllBytes(path)).ToArray();
        return words.Length >= pixels ? words : [.. words, .. new uint[pixels - words.Length]];
    }
}
