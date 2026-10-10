using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The small surfaces that give off light, which the bounce carries as lights of their own rather
/// than by the rays that meet them: which they are, and their boxes as <c>glow.slang</c>'s
/// <c>GlowLight</c> reads them in the bounce's lights.
/// </summary>
/// <remarks>
/// <para>
/// A probe's rays meet a small emitter seldom and take it for a whole texel of the sky where they
/// do, and the world's probes stand above a floor at their spacing, so the faces a floor reads see
/// none of a lamp lying on it: a glowing block on a floor lit the floor beside it through the
/// screen probes' sixteen rays alone, in eight lobes where some rays met it, to the two blocks
/// they reach, and from no farther. Carried as a light, its light reaches a surface in closed form
/// from the faces of its box, which a path-traced floor beside a block reads to within 3%.
/// </para>
/// <para>
/// Each pixel takes it in the model pass as the screen probes around the pixel saw it, each of
/// which marches the field to the halves of each face of each light in reach and keeps sixteen
/// levels of how much of it it sees, a soft shadow from the field's distances; a pixel no screen
/// probe stands on a surface like its own marches to the four brightest itself, once each, as do
/// the pixels past the screen's probes. A probe's ray's hit takes the two brightest, so the walls a
/// lamp lights bounce it on. Emitters square to the axes that touch and give off the same light
/// are one light, so a stack of glowing blocks is not four lights each hidden by the others. The
/// field marks the cells such an emitter paints, so a ray that meets it does not take its light a
/// second time; a reflection keeps it.
/// </para>
/// <para>
/// An emitter is carried where its box's longest side in the world is no more than twice the first
/// cascade's probe spacing, which does not change while it stands still, so the field's mark holds
/// between its builds. Its light reaches as far as it lights a surface by a fiftieth, 17 blocks for
/// the voxel game's glowstone. The <see cref="Most"/> carried emitters nearest the eye are lights
/// each frame; one past them lights nothing a probe carries to a floor, its cells marked all the
/// same, so a level of more lamps than that lights only the nearest.
/// </para>
/// </remarks>
internal static class GlowLights
{
    /// <summary>How many carried emitters are lights at most, the nearest the eye, as <c>MostGlowLights</c> in <c>glow.slang</c>.</summary>
    public const int Most = 64;

    /// <summary>The bytes of a light: its middle and reach, its three half sides and its light.</summary>
    public const int Bytes = 80;

    /// <summary>Where the lights start in the bounce's lights, after the sun, the sky, the lamps and the cascades' corners, their count first.</summary>
    public const int Offset = 64 + 16 * 64 + 4 * 16;

    /// <summary>The bytes the count and the lights take.</summary>
    public const int Size = 16 + Most * Bytes;

    // The light a surface at the reach takes from one, a fiftieth of its own, past which the
    // light is let go, over a quarter of the box's surface as the box's mean face seen from afar.
    private const float Faint = 0.02f;

    /// <summary>Whether an instance that gives off <paramref name="emission"/> is carried as a light, its box in the world no longer than twice the first cascade's spacing.</summary>
    public static bool Carries(Vector3 emission, SceneFieldPlan.Box box, Matrix4x4 world, float firstCell)
    {
        if (emission is { X: <= 0, Y: <= 0, Z: <= 0 }) return false;
        var size = box.Max - box.Min;
        var longest = MathF.Max(size.X * new Vector3(world.M11, world.M12, world.M13).Length(),
            MathF.Max(size.Y * new Vector3(world.M21, world.M22, world.M23).Length(), size.Z * new Vector3(world.M31, world.M32, world.M33).Length()));
        return longest <= 2 * GlobalIlluminationRenderer.ProbeSpacing * firstCell;
    }

    /// <summary>A light as it is written: the middle of its box, half its side along each of the box's axes, its light and its surface over its box's.</summary>
    internal readonly record struct Light(Vector3 Middle, Vector3 X, Vector3 Y, Vector3 Z, Vector3 Glow, float Share);

    /// <summary>
    /// Writes the lights the carried emitters among <paramref name="drawn"/> make, those nearest
    /// <paramref name="eye"/>, into <paramref name="bytes"/>, the count and then each light, and
    /// gives how many there were in all, written or not.
    /// </summary>
    /// <param name="bytes">The count's sixteen bytes and the lights', <see cref="Size"/> in all.</param>
    /// <param name="drawn">The meshes the field gathered this frame.</param>
    /// <param name="eye">The window's eye, the nearest to which are written.</param>
    /// <param name="firstCell">The width of a cell of the field's first cascade, which says which emitters are carried.</param>
    /// <param name="boxOf">The box around a mesh's vertices in its own space.</param>
    /// <param name="shareOf">A mesh's surface over its box's, 1 for a box.</param>
    public static int Write(Span<byte> bytes, IReadOnlyList<(SceneFieldPlan.Instance Instance, bool Skinned)> drawn, Vector3 eye, float firstCell,
        Func<ModelVertex[], SceneFieldPlan.Box> boxOf, Func<ModelVertex[], float> shareOf)
    {
        var lights = Gather(drawn, firstCell, boxOf, shareOf);
        lights.Sort((a, b) => Vector3.DistanceSquared(a.Middle, eye).CompareTo(Vector3.DistanceSquared(b.Middle, eye)));
        var floats = MemoryMarshal.Cast<byte, float>(bytes);
        var count = Math.Min(lights.Count, Most);
        floats[0] = count;
        for (int n = 0; n < count; n++)
        {
            var (middle, x, y, z, glow, share) = lights[n];
            var surface = 8 * (x.Length() * y.Length() + y.Length() * z.Length() + z.Length() * x.Length()) * share;
            var brightest = MathF.Max(glow.X, MathF.Max(glow.Y, glow.Z));
            var reach = MathF.Sqrt(brightest * surface / (4 * Faint));
            var light = floats.Slice(4 + n * Bytes / 4, Bytes / 4);
            (light[0], light[1], light[2], light[3]) = (middle.X, middle.Y, middle.Z, reach);
            (light[4], light[5], light[6], light[7]) = (x.X, x.Y, x.Z, x.Length());
            (light[8], light[9], light[10], light[11]) = (y.X, y.Y, y.Z, y.Length());
            (light[12], light[13], light[14], light[15]) = (z.X, z.Y, z.Z, z.Length());
            (light[16], light[17], light[18], light[19]) = (glow.X, glow.Y, glow.Z, share);
        }
        return lights.Count;
    }

    /// <summary>
    /// The lights the carried emitters among <paramref name="drawn"/> make: one for each, or for
    /// each group of boxes square to the world's axes that touch and give off the same light, one
    /// for the group, its box around them all and its share of that box's surface what of theirs no
    /// other covers.
    /// </summary>
    /// <remarks>
    /// Four glowing blocks stacked two by two were four lights, each marched to through the others
    /// and each giving off from the faces the others cover, so the stack threw a ring of light that
    /// came and went around it. As one light of a box two blocks wide its light is the stack's.
    /// </remarks>
    internal static List<Light> Gather(IReadOnlyList<(SceneFieldPlan.Instance Instance, bool Skinned)> drawn, float firstCell,
        Func<ModelVertex[], SceneFieldPlan.Box> boxOf, Func<ModelVertex[], float> shareOf)
    {
        var lights = new List<Light>();
        var square = new List<(SceneFieldPlan.Box Box, Vector3 Glow, float Surface)>();
        foreach (var (instance, _) in drawn)
        {
            if (instance.Emission is { X: <= 0, Y: <= 0, Z: <= 0 }) continue;
            var (box, w) = (boxOf(instance.Vertices), instance.World);
            if (!Carries(instance.Emission, box, w, firstCell)) continue;
            var share = shareOf(instance.Vertices);
            if (Square(w))
            {
                var world = box.Transformed(w);
                square.Add((world, instance.Emission, SurfaceOf(world) * share));
                continue;
            }
            var half = (box.Max - box.Min) / 2;
            lights.Add(new Light(Vector3.Transform((box.Min + box.Max) / 2, w), new Vector3(w.M11, w.M12, w.M13) * half.X,
                new Vector3(w.M21, w.M22, w.M23) * half.Y, new Vector3(w.M31, w.M32, w.M33) * half.Z, instance.Emission, share));
        }

        // The boxes that touch, found through a grid of the largest box's size, and joined.
        var parent = Enumerable.Range(0, square.Count).ToArray();
        int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        var touching = new List<(int A, int B)>();
        if (square.Count > 1)
        {
            var size = square.Max(s => MathF.Max(s.Box.Max.X - s.Box.Min.X, MathF.Max(s.Box.Max.Y - s.Box.Min.Y, s.Box.Max.Z - s.Box.Min.Z)));
            size = MathF.Max(size, 1e-4f);
            var grid = new Dictionary<(int, int, int), List<int>>();
            (int, int, int) Key(Vector3 p) => ((int)MathF.Floor(p.X / size), (int)MathF.Floor(p.Y / size), (int)MathF.Floor(p.Z / size));
            for (int i = 0; i < square.Count; i++)
            {
                var key = Key(square[i].Box.Min);
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = [];
                list.Add(i);
            }
            for (int i = 0; i < square.Count; i++)
            {
                var (kx, ky, kz) = Key(square[i].Box.Min);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                            if (grid.TryGetValue((kx + dx, ky + dy, kz + dz), out var near))
                                foreach (var j in near)
                                    if (j > i && Touch(square[i].Box, square[j].Box) && Vector3.DistanceSquared(square[i].Glow, square[j].Glow) < 1e-8f)
                                    {
                                        touching.Add((i, j));
                                        parent[Root(i)] = Root(j);
                                    }
            }
        }
        var covered = new Dictionary<int, float>();
        foreach (var (a, b) in touching)
            covered[Root(a)] = covered.GetValueOrDefault(Root(a)) + 2 * Contact(square[a].Box, square[b].Box);
        foreach (var group in Enumerable.Range(0, square.Count).GroupBy(Root))
        {
            var (min, max) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
            float surface = 0;
            foreach (var i in group)
            {
                (min, max) = (Vector3.Min(min, square[i].Box.Min), Vector3.Max(max, square[i].Box.Max));
                surface += square[i].Surface;
            }
            var union = new SceneFieldPlan.Box(min, max);
            var share = Math.Clamp((surface - covered.GetValueOrDefault(group.Key)) / Math.Max(SurfaceOf(union), 1e-8f), 0.05f, 1);
            var half = (max - min) / 2;
            lights.Add(new Light((min + max) / 2, new Vector3(half.X, 0, 0), new Vector3(0, half.Y, 0), new Vector3(0, 0, half.Z), square[group.Key].Glow, share));
        }
        return lights;
    }

    // Whether a world matrix turns nothing, so a box it moves stays square to the axes.
    private static bool Square(Matrix4x4 w)
    {
        var scale = MathF.Max(MathF.Abs(w.M11), MathF.Max(MathF.Abs(w.M22), MathF.Abs(w.M33)));
        var turned = MathF.Abs(w.M12) + MathF.Abs(w.M13) + MathF.Abs(w.M21) + MathF.Abs(w.M23) + MathF.Abs(w.M31) + MathF.Abs(w.M32);
        return turned <= 1e-5f * MathF.Max(scale, 1e-6f);
    }

    private static float SurfaceOf(SceneFieldPlan.Box box)
    {
        var size = box.Max - box.Min;
        return 2 * (size.X * size.Y + size.Y * size.Z + size.Z * size.X);
    }

    // Whether two boxes touch or overlap, within a thousandth of the smaller one's size.
    private static bool Touch(SceneFieldPlan.Box a, SceneFieldPlan.Box b)
    {
        var slack = 1e-3f * MathF.Min((a.Max - a.Min).Length(), (b.Max - b.Min).Length());
        return a.Min.X <= b.Max.X + slack && b.Min.X <= a.Max.X + slack && a.Min.Y <= b.Max.Y + slack && b.Min.Y <= a.Max.Y + slack
            && a.Min.Z <= b.Max.Z + slack && b.Min.Z <= a.Max.Z + slack;
    }

    // The area of the faces two touching boxes press together, where one's side lies on the
    // other's along an axis, or none where they overlap or meet at an edge.
    private static float Contact(SceneFieldPlan.Box a, SceneFieldPlan.Box b)
    {
        var slack = 1e-3f * MathF.Min((a.Max - a.Min).Length(), (b.Max - b.Min).Length());
        var overlap = Vector3.Max(Vector3.Zero, Vector3.Min(a.Max, b.Max) - Vector3.Max(a.Min, b.Min));
        for (int axis = 0; axis < 3; axis++)
        {
            var meet = MathF.Abs(a.Max[axis] - b.Min[axis]) <= slack || MathF.Abs(b.Max[axis] - a.Min[axis]) <= slack;
            if (meet && overlap[axis] <= slack) return overlap[(axis + 1) % 3] * overlap[(axis + 2) % 3];
        }
        return 0;
    }

    /// <summary>A mesh's surface over its box's, from its triangles' corners three a triangle in its own space, 1 where it has none.</summary>
    public static float ShareOf(Vector3[]? corners, SceneFieldPlan.Box box)
    {
        var size = box.Max - box.Min;
        var boxSurface = 2 * (size.X * size.Y + size.Y * size.Z + size.Z * size.X);
        if (corners is not { Length: >= 3 } || boxSurface <= 1e-12f) return 1;
        double area = 0;
        for (int i = 0; i + 2 < corners.Length; i += 3)
            area += Vector3.Cross(corners[i + 1] - corners[i], corners[i + 2] - corners[i]).Length() / 2;
        return (float)Math.Clamp(area / boxSurface, 0.05, 1);
    }
}
