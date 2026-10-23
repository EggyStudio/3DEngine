using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Engine;

/// <summary>
/// The measure of global illumination, a path-traced reference of the window's picture
/// (<c>gi_reference.slang</c>, <see cref="GraphicsDevice.TraceReference"/>) and the window's own
/// linear light compared with it over named regions of the view, as <c>gi.reference</c> and
/// <c>gi.compare</c> run them.
/// </summary>
/// <remarks>
/// <para>
/// A reference is written as a PNG of its light as the window draws it with
/// <c>SetTonemap(Tonemap.None)</c>, with its linear light beside it in a PFM file and what each
/// pixel shows in a regions file, a copy of the ray scene and the face it turns, and the regions'
/// names in a text file. A region is named by what it is in a room. The top of a slab lying flat is
/// a floor and its underside a ceiling, a standing slab is a wall by its color and the way it faces,
/// and anything thicker is a block by its order and the face seen, an emissive one said to be so.
/// </para>
/// <para>
/// The reference traces the window's meshes as the device's ray tracing holds them, at
/// <see cref="GlobalIllumination.High"/> on a GPU that traces rays, with each copy's color times
/// its texture's average and its mesh's vertices' colors at the hit, blended as the model pass
/// blends them, and its emission, its first face lit as the model pass lights one
/// and every face after as global illumination does, the sun as the first directional light, and
/// the sky as the environment map or the ambient lights.
/// </para>
/// </remarks>
internal static class BounceReference
{
    private static readonly string[] Axes = ["+x", "-x", "+y", "-y", "+z", "-z"];

    /// <summary>
    /// Traces the window's picture with <paramref name="samples"/> paths a pixel, each bouncing
    /// <paramref name="bounces"/> times at most or until Russian roulette ends it where that is
    /// negative, and writes it to <paramref name="path"/> and its files beside it.
    /// </summary>
    /// <returns>What was written, or why nothing was.</returns>
    public static string Trace(World world, string path, int samples, int bounces = -1)
    {
        if (Prepare(world, out var why) is not { } tracer) return why;
        var render = tracer.Render;
        if (render.TryGet<WindowView>() is not { } view || !Matrix4x4.Invert(view.ViewProjection, out var inverse)
            || render.TryGet<SwapchainTarget>() is not { } window)
            return "the window draws no meshes through a camera";

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var traced = tracer.Trace(inverse, (int)window.Extent.Width, (int)window.Extent.Height, samples, bounces, null);
        var seconds = clock.Elapsed.TotalSeconds;
        var scene = tracer.Scene;

        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        WritePng(full, traced);
        WritePfm(full + ".pfm", traced);
        WriteRegions(full + ".regions", traced);
        var names = Names(scene.Copies, traced.Regions);
        File.WriteAllLines(full + ".regions.txt", names.OrderBy(n => n.Key).Select(n => $"{n.Key}\t{n.Value}"));
        var bounced = bounces < 0 ? "" : bounces == 1 ? ", one bounce" : $", {bounces} bounces";
        return $"traced {traced.Width} by {traced.Height} at {samples} paths a pixel{bounced} in {seconds:0.0} s, {scene.Count} copies, " +
               $"{names.Count} regions, into {full} with its .pfm, .regions and .regions.txt";
    }

    // What a reference is traced with, the device, the render world, the window's meshes for the
    // device's rays and the compiled tracer.
    private sealed record Tracer(GraphicsDevice Device, RenderWorld Render, GpuRayScene Scene, ShaderProgram Program)
    {
        // Traces the picture through the camera inverse gives, or with probe the light arriving at
        // that point from each way through a probe's octahedron of width by height texels.
        public TracedReference Trace(Matrix4x4 inverse, int width, int height, int samples, int bounces, Vector3? probe)
        {
            var environment = Render.TryGet<EnvironmentMap>() is not null ? Render.TryGet<ModelRenderer>()?.Environment : null;
            using var black = environment is null ? Device.CreateCubeMap(1, 1, new Half[6 * 4]) : null;
            var (cubeView, cubeSampler) = environment is not null ? (environment.View, environment.Sampler) : (black!.View, black.Sampler);
            // A bounce is a face after the first, so the faces a path meets are one more, 32 at most
            // where Russian roulette ends the path.
            return Device.TraceReference(Program.Compute, Scene, inverse, width, height, Math.Max(1, samples), Lights(Render, environment is not null),
                cubeView, cubeSampler, bounces < 0 ? 32 : bounces + 1, probe: probe);
        }
    }

    private static Tracer? Prepare(World world, out string why)
    {
        why = "";
        if (!world.TryGetResource<Renderer>(out var renderer) || renderer.Context.Graphics is not GraphicsDevice device)
            why = "there is no renderer";
        else if (!device.CanQueryRays)
            why = "the device traces no rays, which the reference is traced through";
        else if (renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()?.Rays is not { } scene)
            why = "the window's meshes are not held for the device's rays, which global illumination at High on a GPU that traces rays holds them for";
        else if (!world.TryGetResource<AssetServer>(out var assets))
            why = "there is no asset server to compile gi_reference.slang";
        else
            return new Tracer(device, renderer.RenderWorld, scene, assets.LoadSync<ShaderProgram>("shaders/gi_reference.slang"));
        return null;
    }

    /// <summary>
    /// The light arriving at a probe as the probe holds it and as a reference has it, with where the
    /// probe sits and how many ways it has; where its own rays met a surface, how many ways, how many of
    /// them before their interval began, and the mean light they brought against the reference's
    /// along the same ways; over every way, the mean light merged with the cascades above against
    /// the reference's; and for each face, +x, -x, +y, -y, +z and -z, the light the model pass reads,
    /// that gathered from the merge, and the reference's.
    /// </summary>
    internal sealed record ProbeLight(int Cascade, Vector3 Middle, int Ways, int Hits, int Early, Vector3 HitRays, Vector3 HitReference,
        Vector3 Merged, Vector3 Reference, Vector3[] Faces, Vector3[] FacesMerged, Vector3[] FacesReference, ProbeWay[] Each);

    /// <summary>
    /// One way of a probe's octahedron, where its own ray met a surface, 0 within its interval, 1
    /// before it began and 2 nowhere, the cascade above's light taken, the light its ray brought, the
    /// light merged with the cascades above, the reference's along it, and its texel's share of the
    /// sphere.
    /// </summary>
    internal sealed record ProbeWay(Vector3 Way, int Met, Vector3 Ray, Vector3 Merged, Vector3 Reference, float Share);

    /// <summary>
    /// The light arriving at the probe of <paramref name="cascade"/> nearest <paramref name="point"/>
    /// against a reference of it, <paramref name="samples"/> paths a way bouncing
    /// <paramref name="bounces"/> times at most, or until Russian roulette ends them where that is
    /// negative, giving, where the probe's own rays met a surface, the light they brought against
    /// the reference's along the same ways, which holds the trace to it; over every way, the light
    /// merged with the cascades above against the reference's, which holds the merge; each face's
    /// light gathered from the merge against the reference's, which holds the gather; and the eight
    /// ways that carry most of the face furthest from the reference's, where each one's ray went, its
    /// merged light, its own ray's and the reference's, which names the ways a face's fault comes
    /// along.
    /// </summary>
    /// <remarks>
    /// The reference leaves out the emission of a glow light global illumination carries where a path
    /// first meets it, as the probe's rays do, since that light reaches a surface as a light of its own
    /// (<see cref="GlowLights"/>) rather than through the probes.
    /// </remarks>
    public static string Probe(World world, Vector3 point, int samples, int bounces = -1, int cascade = 0)
    {
        if (MeasureProbe(world, point, samples, bounces, cascade, out var why) is not { } probe) return why;
        static string Rgb(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:0.000} {v.Y:0.000} {v.Z:0.000}");
        var m = probe.Middle;
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"the probe of cascade {probe.Cascade} at {m.X:0.##},{m.Y:0.##},{m.Z:0.##}, {probe.Ways} ways, the reference {samples} paths a way") +
            (bounces < 0 ? "" : bounces == 1 ? ", one bounce" : $", {bounces} bounces"),
            (probe.Hits == 0 ? "none of its own rays met a surface"
                : $"where its own rays met a surface, {probe.Hits} ways: they bring {Rgb(probe.HitRays)}, the reference {Rgb(probe.HitReference)}, {Share(probe.HitRays, probe.HitReference)}")
            + (probe.Early > 0 ? $", {probe.Early} of them before their interval began" : ""),
            $"every way, merged with the cascades above: {Rgb(probe.Merged)}, the reference {Rgb(probe.Reference)}, {Share(probe.Merged, probe.Reference)}",
            "each face's light: its face as the model pass reads it, gathered from the merge, the reference's, and the face's share",
        };
        string[] names = ["+x", "-x", "+y", "-y", "+z", "-z"];
        for (int f = 0; f < 6; f++)
            lines.Add($"{names[f]}: {Rgb(probe.Faces[f])}, {Rgb(probe.FacesMerged[f])}, {Rgb(probe.FacesReference[f])}, {Share(probe.Faces[f], probe.FacesReference[f])}");

        // Each face's light by where its ways were met, so a face's fault is placed at the probe's own
        // hits or in what the cascade above lends it, each part weighed as the gather weighs it.
        Vector3[] axes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        lines.Add("each face's luminance by where its ways were met, within their interval, before it began and in the cascade above, each part's merged light, the reference's and its share of the face's weight:");
        for (int f = 0; f < 6; f++)
        {
            var axis = axes[f];
            var all = probe.Each.Sum(way => Math.Max(Vector3.Dot(way.Way, axis), 0) * way.Share);
            lines.Add($"{names[f]}: " + string.Join(", ", Enumerable.Range(0, 3).Select(met =>
            {
                var ways = probe.Each.Where(way => way.Met == met).ToArray();
                float Sum(Func<ProbeWay, float> light) => ways.Sum(way => light(way) * Math.Max(Vector3.Dot(way.Way, axis), 0) * way.Share) * MathF.PI / all;
                return string.Create(CultureInfo.InvariantCulture,
                    $"{Sum(way => Luminance(way.Merged)):0.000} {Sum(way => Luminance(way.Reference)):0.000} {Sum(_ => 1 / MathF.PI):0.00}");
            })));
        }

        // The ways that carry most of the face furthest from the reference's light, each way's part
        // of the face weighed as the gather weighs it, by its cosine and its texel's share.
        var furthest = Enumerable.Range(0, 6).MaxBy(f =>
            MathF.Abs(Luminance(probe.FacesMerged[f]) - Luminance(probe.FacesReference[f])) / Math.Max(Luminance(probe.FacesReference[f]), 1e-6f));
        var weights = probe.Each.Sum(way => Math.Max(Vector3.Dot(way.Way, axes[furthest]), 0) * way.Share);
        float Part(ProbeWay way, Vector3 light) => Luminance(light) * Math.Max(Vector3.Dot(way.Way, axes[furthest]), 0) * way.Share * MathF.PI / weights;
        string[] where = ["met a surface within its interval", "met a surface before its interval began", "took the cascade above"];
        lines.Add($"the ways that carry most of the {names[furthest]} face's difference, where its ray went, the merged light, its own ray's, the reference's, and its part of the face against the reference's:");
        foreach (var way in probe.Each.OrderByDescending(way => MathF.Abs(Part(way, way.Merged) - Part(way, way.Reference))).Take(8))
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{way.Way.X:0.00},{way.Way.Y:0.00},{way.Way.Z:0.00}, {where[way.Met]}: {Rgb(way.Merged)}, {Rgb(way.Ray)}, {Rgb(way.Reference)}, {Part(way, way.Merged):0.000} against {Part(way, way.Reference):0.000}"));
        return string.Join("\n", lines);
    }

    /// <summary>What <see cref="Probe"/> reports, or null and why where it cannot be measured, the probe inside a mesh among those.</summary>
    internal static ProbeLight? MeasureProbe(World world, Vector3 point, int samples, int bounces, int cascade, out string why)
    {
        if (Prepare(world, out why) is not { } tracer) return null;
        if (tracer.Render.TryGet<GlobalIlluminationRenderer>()?.Probes is not { } gi || tracer.Render.TryGet<SceneFieldRenderer>()?.Plan is not { } plan)
        {
            why = "global illumination is off";
            return null;
        }
        var c = Math.Clamp(cascade, 0, gi.Cascades - 1);
        if (plan.BuiltOrigin(c) is not { } origin)
        {
            why = $"cascade {c} of the field is not built";
            return null;
        }

        var (p, n) = (gi.Probes, gi.Texels[c]);
        var spacing = plan.CellOf(c) * GlobalIlluminationRenderer.ProbeSpacing;
        var at = Vector3.Clamp(new Vector3(MathF.Floor((point.X - origin.X) / spacing), MathF.Floor((point.Y - origin.Y) / spacing),
            MathF.Floor((point.Z - origin.Z) / spacing)), Vector3.Zero, new Vector3(p - 1));
        var middle = origin + (at + new Vector3(0.5f)) * spacing;
        var (x, y, z) = ((int)at.X, (int)at.Y, (int)at.Z);

        var device = tracer.Device;
        var rays = device.ReadProbeVolume(gi, c, merged: false);
        var merged = device.ReadProbeVolume(gi, c, merged: true);
        var cubes = device.ReadIlluminationCubes(gi);
        var side = p * n;
        Vector4 Texel(float[] volume, int u, int v)
        {
            var i = ((z * side + y * n + v) * side + x * n + u) * 4;
            return new Vector4(volume[i], volume[i + 1], volume[i + 2], volume[i + 3]);
        }
        // The probe stands where the trace moved it, as its faces' alpha says, two more than its
        // move along each axis in its spacing (faceMove in gi.slang).
        float Moved(int axis)
        {
            var alpha = cubes[(((c * p + z) * p + y) * 6 * p + axis * 2 * p + x) * 4 + 3];
            return alpha > 0.5f ? alpha - 2 : 0;
        }
        middle += new Vector3(Moved(0), Moved(1), Moved(2)) * spacing;
        // A probe inside a mesh holds nothing, its alpha 0.
        if (Texel(merged, 0, 0).W < 0.25f)
        {
            why = string.Create(CultureInfo.InvariantCulture, $"the probe of cascade {c} at {middle.X:0.##},{middle.Y:0.##},{middle.Z:0.##} lies inside a mesh and holds nothing");
            return null;
        }
        var reference = tracer.Trace(Matrix4x4.Identity, n, n, samples, bounces, middle).Light;

        var (hitRays, hitReference, mergedAll, referenceAll, hits, early) = (Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, 0, 0);
        var (faces, facesMerged, facesReference, weights) = (new Vector3[6], new Vector3[6], new Vector3[6], new float[6]);
        var each = new ProbeWay[n * n];
        Vector3[] axes = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
        for (int v = 0; v < n; v++)
            for (int u = 0; u < n; u++)
            {
                var r = new Vector3(reference[(v * n + u) * 3], reference[(v * n + u) * 3 + 1], reference[(v * n + u) * 3 + 2]);
                var ray = Texel(rays, u, v);
                var light = Texel(merged, u, v);
                var mergedLight = new Vector3(light.X, light.Y, light.Z);
                // A ray that met a surface holds its alpha at 0, or a half where it met it before its
                // interval began, as a cascade past the first's may, and brings that surface's light.
                if (ray.W > 0.25f && ray.W < 0.75f)
                    early++;
                if (ray.W < 0.75f)
                {
                    hitRays += new Vector3(ray.X, ray.Y, ray.Z);
                    hitReference += r;
                    hits++;
                }
                mergedAll += mergedLight;
                referenceAll += r;
                // Each way weighed as gi_ambient.slang weighs it, by its cosine and its texel's share
                // of the sphere, the weights scaled to sum to pi.
                var onOctahedron = Octahedron((u + 0.5f) / n, (v + 0.5f) / n);
                var way = Vector3.Normalize(onOctahedron);
                var share = 1 / MathF.Pow(onOctahedron.Length(), 3);
                each[v * n + u] = new ProbeWay(way, ray.W < 0.25f ? 0 : ray.W < 0.75f ? 1 : 2, new Vector3(ray.X, ray.Y, ray.Z), mergedLight, r, share);
                for (int f = 0; f < 6; f++)
                {
                    var weight = Math.Max(Vector3.Dot(way, axes[f]), 0) * share;
                    facesMerged[f] += mergedLight * weight;
                    facesReference[f] += r * weight;
                    weights[f] += weight;
                }
            }
        for (int f = 0; f < 6; f++)
        {
            facesMerged[f] *= MathF.PI / weights[f];
            facesReference[f] *= MathF.PI / weights[f];
            var i = (((c * p + z) * p + y) * 6 * p + f * p + x) * 4;
            faces[f] = new Vector3(cubes[i], cubes[i + 1], cubes[i + 2]);
        }
        return new ProbeLight(c, middle, n * n, hits, early, hits > 0 ? hitRays / hits : Vector3.Zero, hits > 0 ? hitReference / hits : Vector3.Zero,
            mergedAll / (n * n), referenceAll / (n * n), faces, facesMerged, facesReference, each);
    }

    // The point of an octahedron a point of its square stands for, as gi.slang's octahedronPoint gives it.
    /// <summary>
    /// What <c>gi.screenprobe</c> reports: each of the sixteen rays of the window's screen probe under
    /// a pixel, as the screen's pass wrote them for the frame, where each was met, on the screen, in
    /// the field or beyond its reach, against the light a path-traced reference sends along the same
    /// way from where the rays start and over the ways it stands for, and the light the probe sums
    /// against the reference's along its ways, over their cells and over the whole hemisphere.
    /// </summary>
    internal static string ScreenProbe(World world, GpuScreenProbes screen, int x, int y, int samples, int bounces) =>
        MeasureScreenProbe(world, screen, samples, bounces, out var why) is { } probe ? ScreenProbeReport(probe, screen.Tile, x, y, samples) : why;

    /// <summary>The lines <see cref="ScreenProbe"/> writes of a probe measured, under the pixel x, y of tiles of the side given.</summary>
    internal static string ScreenProbeReport(ScreenProbeLight probe, int tile, int x, int y, int samples)
    {
        static string Rgb(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:0.000} {v.Y:0.000} {v.Z:0.000}");
        string[] where = ["on the screen", "in the field", "beyond its reach"];
        var (p, n, start) = (probe.Point, probe.Normal, probe.Start);
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture,
                $"the screen probe of tile {x / tile}, {y / tile} under pixel {x}, {y} stands at {p.X:0.###},{p.Y:0.###},{p.Z:0.###} facing {n.X:0.##},{n.Y:0.##},{n.Z:0.##}, the field's cell {probe.Cell:0.###}, its rays leaving from {start.X:0.###},{start.Y:0.###},{start.Z:0.###} and taking the world's cascade {probe.Beyond} beyond their reach; the reference {samples} paths a way over {ScreenProbeSide} by {ScreenProbeSide} ways"),
            $"its light, irradiance: {Rgb(probe.Light)}, the reference along its sixteen ways {Rgb(probe.AlongWays)}, {Share(probe.Light, probe.AlongWays)}, over their cells {Rgb(probe.OverCells)}, {Share(probe.Light, probe.OverCells)}, over the whole hemisphere {Rgb(probe.Hemisphere)}, {Share(probe.Light, probe.Hemisphere)}",
        };
        var (parts, partsReference, partsOverCells, partRays) = (new Vector3[3], new Vector3[3], new Vector3[3], new int[3]);
        foreach (var ray in probe.Rays)
        {
            parts[ray.Met] += ray.Light * ray.Share;
            partsReference[ray.Met] += ray.Reference * ray.Share;
            partsOverCells[ray.Met] += ray.OverCell * ray.Share;
            partRays[ray.Met]++;
        }
        lines.Add("each part of it by where its rays were met, against the reference's along the same rays and over their cells: " + string.Join("; ",
            Enumerable.Range(0, 3).Where(k => partRays[k] > 0).Select(k =>
                $"{where[k]}, {partRays[k]} rays, {Rgb(parts[k])} against {Rgb(partsReference[k])}, {Share(parts[k], partsReference[k])}, and {Rgb(partsOverCells[k])}, {Share(parts[k], partsOverCells[k])}")));
        lines.Add("each ray, its way and its angle from the normal, where it was met and how far, its light, the reference's along it and over its cell:");
        for (int r = 0; r < probe.Rays.Length; r++)
        {
            var ray = probe.Rays[r];
            var degrees = MathF.Acos(Math.Clamp(Vector3.Dot(ray.Way, n), -1, 1)) * 180 / MathF.PI;
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{r + 1,2}: {ray.Way.X:0.00},{ray.Way.Y:0.00},{ray.Way.Z:0.00} at {degrees:0}°, {where[ray.Met]} at {ray.Distance:0.00}: {Rgb(ray.Light)}, the reference {Rgb(ray.Reference)}, {Share(ray.Light, ray.Reference)}, over its cell {Rgb(ray.OverCell)}, {Share(ray.Light, ray.OverCell)}")
                + (ray.Met < 2 ? $"; its own light {Rgb(ray.Own)}" : ""));
        }
        return string.Join("\n", lines);
    }

    // How far a probe's light lies from the reference's, or, since a share of no light is no figure,
    // whether the probe's is dark where the reference's is.
    private static string Share(Vector3 held, Vector3 reference) =>
        Luminance(reference) < 1e-4f ? Luminance(held) < 1e-4f ? "dark as the reference is" : "lit where the reference is dark"
            : string.Create(CultureInfo.InvariantCulture, $"{(Luminance(held) / Luminance(reference) - 1) * 100:+0;-0;0}%");

    /// <summary>The ways along each side of the octahedron the reference of a screen probe is traced over, each ray read from the one it falls in.</summary>
    internal const int ScreenProbeSide = 64;

    /// <summary>One ray of a screen probe, its way, where it was met, 0 on the screen, 1 in the field, 2 beyond its reach, how far, its light, the light without the indirect light of what it met, the reference's along it and over its cell, the ways of the hemisphere it stands for, and its share of the probe's light.</summary>
    internal sealed record ScreenProbeRay(Vector3 Way, int Met, float Distance, Vector3 Light, Vector3 Own, Vector3 Reference, Vector3 OverCell, float Share);

    /// <summary>A screen probe as <see cref="ScreenProbe"/> reads it, its place, its light and the reference's, and its rays.</summary>
    internal sealed record ScreenProbeLight(Vector3 Point, Vector3 Normal, float Cell, Vector3 Start, int Beyond, Vector3 Light, Vector3 AlongWays,
        Vector3 OverCells, Vector3 Hemisphere, ScreenProbeRay[] Rays);

    /// <summary>
    /// What <see cref="ScreenProbe"/> reports of the probe the screen's pass last wrote the rays of
    /// (<c>GlobalIlluminationSettings.LoggedScreenProbe</c>), or null and why where it sent none.
    /// </summary>
    internal static ScreenProbeLight? MeasureScreenProbe(World world, GpuScreenProbes screen, int samples, int bounces, out string why)
    {
        if (Prepare(world, out why) is not { } tracer) return null;
        tracer.Device.WaitIdle();
        var log = MemoryMarshal.Cast<byte, Vector4>(tracer.Device.Map(screen.RayLog)).ToArray();
        static Vector3 Xyz(Vector4 v) => new(v.X, v.Y, v.Z);
        var (p, n, start, sum) = (log[0], log[1], log[2], log[3]);
        if (sum.W <= 0)
        {
            why = "the screen probe sent no rays, standing on no surface or outside the world's cascades";
            return null;
        }

        // The reference along each way from where the rays start, a fine octahedron of them, each
        // ray read from the texel its way falls in.
        const int Side = ScreenProbeSide;
        var traced = tracer.Trace(Matrix4x4.Identity, Side, Side, samples, bounces, Xyz(start)).Light;
        Vector3 Along(Vector3 way)
        {
            var uv = OctahedronOf(way);
            var (u, v) = (Math.Clamp((int)(uv.X * Side), 0, Side - 1), Math.Clamp((int)(uv.Y * Side), 0, Side - 1));
            var i = (v * Side + u) * 3;
            return new Vector3(traced[i], traced[i + 1], traced[i + 2]);
        }
        // The reference's light reaching the surface over the whole hemisphere, each way weighed by
        // its cosine and its texel's share of the sphere, as the world's probes weigh theirs, and
        // over each ray's cell, the texel of the hemisphere's square the ray stands for as
        // gi_screen.slang's hemisphere unfolds it around the normal, since the sixteen rays' ways
        // alone may read a bright floor or a dark opening more or less than the hemisphere holds.
        var normal = Xyz(n);
        var side = MathF.Abs(normal.Y) < 0.99f ? Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitY)) : Vector3.UnitX;
        var across = Vector3.Cross(normal, side);
        var (hemisphere, hemisphereWeight) = (Vector3.Zero, 0f);
        var (cells, cellWeights) = (new Vector3[16], new float[16]);
        for (int v = 0; v < Side; v++)
            for (int u = 0; u < Side; u++)
            {
                var onOctahedron = Octahedron((u + 0.5f) / Side, (v + 0.5f) / Side);
                var way = Vector3.Normalize(onOctahedron);
                var weight = Math.Max(Vector3.Dot(way, normal), 0) / MathF.Pow(onOctahedron.Length(), 3);
                if (weight <= 0) continue;
                var i = (v * Side + u) * 3;
                var light = new Vector3(traced[i], traced[i + 1], traced[i + 2]);
                hemisphere += light * weight;
                hemisphereWeight += weight;
                var local = new Vector3(Vector3.Dot(way, side), Vector3.Dot(way, across), Vector3.Dot(way, normal));
                local /= MathF.Abs(local.X) + MathF.Abs(local.Y) + local.Z;
                var cellU = Math.Clamp((int)((local.X + local.Y + 1) * 2), 0, 3);
                var cellV = Math.Clamp((int)((local.X - local.Y + 1) * 2), 0, 3);
                cells[cellV * 4 + cellU] += light * weight;
                cellWeights[cellV * 4 + cellU] += weight;
            }
        hemisphere *= MathF.PI / Math.Max(hemisphereWeight, 1e-12f);

        var rays = new ScreenProbeRay[16];
        var (alongWays, overCells) = (Vector3.Zero, Vector3.Zero);
        for (int r = 0; r < rays.Length; r++)
        {
            var (light, way, reached, own) = (log[4 + r * 4], log[5 + r * 4], log[6 + r * 4], log[7 + r * 4]);
            var reference = Along(Xyz(way));
            var cell = cells[r] / Math.Max(cellWeights[r], 1e-12f);
            // Each ray's share of the light the probe sums, its weights scaled to sum to pi.
            var share = way.W * MathF.PI / sum.W;
            alongWays += reference * share;
            overCells += cell * share;
            rays[r] = new ScreenProbeRay(Xyz(way), Math.Clamp((int)MathF.Round(light.W), 0, 2), reached.W, Xyz(light), Xyz(own), reference, cell, share);
        }
        return new ScreenProbeLight(Xyz(p), normal, p.W, Xyz(start), (int)n.W, Xyz(sum), alongWays, overCells, hemisphere, rays);
    }

    // The point of the square a unit direction stands for, the octahedron folded flat, as gi.slang's octahedron gives it.
    private static Vector2 OctahedronOf(Vector3 d)
    {
        d /= MathF.Abs(d.X) + MathF.Abs(d.Y) + MathF.Abs(d.Z);
        var p = new Vector2(d.X, d.Y);
        if (d.Z < 0)
            p = new Vector2((1 - MathF.Abs(p.Y)) * (p.X >= 0 ? 1 : -1), (1 - MathF.Abs(p.X)) * (p.Y >= 0 ? 1 : -1));
        return p * 0.5f + new Vector2(0.5f);
    }

    private static Vector3 Octahedron(float u, float v)
    {
        var (x, y) = (u * 2 - 1, v * 2 - 1);
        var d = new Vector3(x, y, 1 - MathF.Abs(x) - MathF.Abs(y));
        if (d.Z < 0)
            (d.X, d.Y) = ((1 - MathF.Abs(y)) * (x >= 0 ? 1 : -1), (1 - MathF.Abs(x)) * (y >= 0 ? 1 : -1));
        return d;
    }

    /// <summary>One region of the view as <see cref="Measure"/> reads it, its name, its pixels, and the mean light the reference and the frame give it, linear, with the mean of each pixel's difference.</summary>
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
        lines.Add($"the difference, red where the frame is brighter and blue where it is darker, in {differencePath}, and the frame's linear light beside it");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// The regions of at least 50 pixels the reference at <paramref name="path"/> names, most pixels
    /// first, with the window's linear light against it, then every region at once as
    /// <c>every region</c>, and the picture of the difference written at
    /// <paramref name="differencePath"/>, with the frame's linear light beside the reference as
    /// <c>-frame.pfm</c>; or null and <paramref name="why"/> where there is no reference or no frame
    /// to compare.
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
            why = "the window's scene is not decoded to linear light, which global illumination has it decoded for";
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
        // The frame's own linear light beside it, so a stretch of it can be read against the reference's.
        var light = new float[width * height * 3];
        for (int i = 0; i < width * height; i++)
            (light[i * 3], light[i * 3 + 1], light[i * 3 + 2]) = (frame[i * 4], frame[i * 4 + 1], frame[i * 4 + 2]);
        WritePfm(Path.Combine(Path.GetDirectoryName(full)!, Path.GetFileNameWithoutExtension(full) + "-frame.pfm"), new TracedReference(width, height, light, []));

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

    // The lights as gi_reference.slang's ReferenceLights, the sun, the first directional light, and
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
            // The map's light on the scene, at the share and tint the program set for it, as the cascades take it.
            sky = map.Intensity * EnvironmentLight.In(render);
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
            var glow = instance.Emission.LengthSquared() > 0 ? "emissive " : "";
            var color = ColorWord(instance.Color);
            string name;
            if (flat && order[0].i == 1 && axis is 2 or 3)
                name = glow.Length > 0 ? $"emissive {color} panel's {(axis == 2 ? "top" : "underside")}" : axis == 2 ? $"{color} floor" : $"{color} ceiling";
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

    // A linear color's word, white, gray or black where its channels are alike and its hue
    // otherwise.
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

    // A PFM file of the linear light, its header and then three little-endian floats a pixel with
    // rows from the bottom, as the format has them.
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
