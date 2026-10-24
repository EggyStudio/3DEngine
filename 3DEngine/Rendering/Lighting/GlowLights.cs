using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The small emissive surfaces, which global illumination carries as lights of their own rather
/// than by the rays that meet them, which they are and their boxes as <c>glow.slang</c>'s
/// <c>GlowLight</c> reads them in the lights of global illumination.
/// </summary>
/// <remarks>
/// <para>
/// A probe's rays meet a small emitter seldom and take it for a whole texel of the sky where they
/// do, and the world's probes stand above a floor at their spacing, so the faces a floor reads see
/// none of a lamp lying on it. An emissive block on a floor lit the floor beside it through the
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
/// lamp lights reflect its light on. Emitters square to the axes that touch and emit the same light
/// are one light, so a stack of emissive blocks is not four lights each hidden by the others. The
/// field marks the cells such an emitter paints, so a ray that meets it does not take its light a
/// second time; a reflection keeps it.
/// </para>
/// <para>
/// An emitter is carried where its box's longest side in the world is no more than twice the first
/// cascade's probe spacing, which does not change while it stands still, so the field's mark holds
/// between its builds. A mesh of many emitters, as a voxel game draws its blocks of a kind, is cut
/// into its parts, its triangles joined where they share a corner, and is carried where each part
/// is, each a light of its own (<see cref="PartsOf"/>). Its light reaches as far as it lights a
/// surface by a fiftieth, 17 blocks for the voxel game's glowstone. The <see cref="Most"/> carried
/// emitters nearest the eye are lights each frame; one past them lights nothing a probe carries to
/// a floor, its cells marked all the same, so a level of more lamps than that lights only the
/// nearest. The field marks a mesh whole, so a mesh of many parts past the nearest lights none of
/// the ground around those parts, where taken whole it lit it through the probes' rays.
/// </para>
/// </remarks>
internal static class GlowLights
{
    /// <summary>How many carried emitters are lights at most, the nearest the eye, as <c>MostGlowLights</c> in <c>glow.slang</c>.</summary>
    public const int Most = 64;

    /// <summary>The bytes of a light, its middle and reach, its three half sides and its light.</summary>
    public const int Bytes = 80;

    /// <summary>Where the lights start in the lights of global illumination, after the sun, the sky, the lamps and the cascades' corners, their count first.</summary>
    public const int Offset = 64 + 16 * 64 + GlobalIlluminationRenderer.MostCascades * 16;

    /// <summary>
    /// The bytes ahead of the lights, the count and the least corner of the box every light's
    /// reach lies in, and the box's most corner.
    /// </summary>
    public const int Head = 32;

    /// <summary>The bytes the count, the box and the lights take.</summary>
    public const int Size = Head + Most * Bytes;

    // The light a surface at the reach takes from one, a fiftieth of its own, past which the
    // light is let go, over a quarter of the box's surface as the box's mean face seen from afar.
    private const float Faint = 0.02f;

    /// <summary>
    /// Whether an instance emitting <paramref name="emission"/> is carried as lights, every part of
    /// its mesh carried as <see cref="Carries(Vector3, SceneFieldPlan.Box, Matrix4x4, float)"/> carries a box.
    /// </summary>
    public static bool Carries(Vector3 emission, IReadOnlyList<Part> parts, Matrix4x4 world, float firstCell)
    {
        if (parts.Count == 0) return false;
        foreach (var part in parts)
            if (!Carries(emission, part.Box, world, firstCell)) return false;
        return true;
    }

    /// <summary>
    /// A part of an emitter's mesh, the box around its triangles in the mesh's space, which of the
    /// box's faces its triangles lie on (<see cref="Light.Faces"/>), and its surface over theirs.
    /// </summary>
    internal readonly record struct Part(SceneFieldPlan.Box Box, float Share, int Faces = 0);

    /// <summary>
    /// The parts of a mesh, its triangles joined where they share a corner, from its corners three a
    /// triangle in its own space, or the mesh whole, <paramref name="box"/> with
    /// <paramref name="share"/>, where its triangles are all joined or it has none.
    /// </summary>
    /// <remarks>
    /// A voxel game draws all its blocks of a kind as one mesh, as Ore draws its ore, whose box is
    /// the land's, so taken whole no such emitter was carried and the ground beside each block took
    /// its light through the screen probes' sixteen rays alone, in patches. Cut into parts, each
    /// block, or each group of blocks that share a corner, is an emitter of its own.
    /// </remarks>
    public static Part[] PartsOf(Vector3[]? corners, SceneFieldPlan.Box box, float share)
    {
        Part[] whole = [new Part(box, share)];
        if (corners is not { Length: >= 6 }) return whole;
        var triangles = corners.Length / 3;
        var parent = new int[triangles];
        for (int i = 0; i < triangles; i++) parent[i] = i;
        int Root(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        // Corners are the same where they lie within a hundred thousandth of the mesh's size.
        var size = MathF.Max((box.Max - box.Min).Length(), 1e-6f);
        var scale = 1e5f / size;
        var first = new Dictionary<(int, int, int), int>();
        for (int c = 0; c < triangles * 3; c++)
        {
            var p = (corners[c] - box.Min) * scale;
            var key = ((int)MathF.Round(p.X), (int)MathF.Round(p.Y), (int)MathF.Round(p.Z));
            if (first.TryGetValue(key, out var other)) parent[Root(c / 3)] = Root(other);
            else first[key] = c / 3;
        }
        var groups = new Dictionary<int, (Vector3 Min, Vector3 Max, double Area)>();
        for (int t = 0; t < triangles; t++)
        {
            var (a, b, c) = (corners[t * 3], corners[t * 3 + 1], corners[t * 3 + 2]);
            var root = Root(t);
            var (min, max, area) = groups.TryGetValue(root, out var group) ? group : (new Vector3(float.MaxValue), new Vector3(float.MinValue), 0);
            groups[root] = (Vector3.Min(min, Vector3.Min(a, Vector3.Min(b, c))), Vector3.Max(max, Vector3.Max(a, Vector3.Max(b, c))),
                area + Vector3.Cross(b - a, c - a).Length() / 2);
        }
        if (groups.Count <= 1) return whole;

        // Which faces of its box each part's triangles lie on, each triangle square to an axis and
        // facing out of the box on that side, or every face where one is not.
        var faces = new Dictionary<int, int>();
        var near = 1e-4f * size;
        for (int t = 0; t < triangles; t++)
        {
            var root = Root(t);
            if (faces.TryGetValue(root, out var known) && known == All) continue;
            var (a, b, c) = (corners[t * 3], corners[t * 3 + 1], corners[t * 3 + 2]);
            var normal = Vector3.Cross(b - a, c - a);
            var length = normal.Length();
            var face = All;
            if (length > 1e-12f)
            {
                normal /= length;
                var (min, max, _) = groups[root];
                for (int k = 0; k < 3; k++)
                {
                    if (MathF.Abs(normal[k]) < 0.99f) continue;
                    var plane = normal[k] > 0 ? max[k] : min[k];
                    if (MathF.Abs(a[k] - plane) <= near && MathF.Abs(b[k] - plane) <= near && MathF.Abs(c[k] - plane) <= near)
                        face = 1 << (2 * k + (normal[k] > 0 ? 1 : 0));
                }
            }
            faces[root] = face == All ? All : faces.GetValueOrDefault(root) | face;
        }
        return [.. groups.Select(g =>
        {
            var (min, max, area) = g.Value;
            var box = new SceneFieldPlan.Box(min, max);
            var mask = faces.GetValueOrDefault(g.Key, All);
            var surface = FacesSurface(max - min, mask);
            return new Part(box, surface <= 1e-12f ? 1 : (float)Math.Clamp(area / surface, 0.05, 1), mask == All ? 0 : mask);
        })];
    }

    // Every face of a box, a bit a face, two an axis, the side toward the axis's way the higher.
    private const int All = 63;

    // The surface of the faces of a box of this size the bits name.
    private static float FacesSurface(Vector3 size, int faces)
    {
        float surface = 0;
        for (int k = 0; k < 3; k++)
        {
            var face = size[(k + 1) % 3] * size[(k + 2) % 3];
            surface += ((faces >> (2 * k)) & 1) * face + ((faces >> (2 * k + 1)) & 1) * face;
        }
        return surface;
    }

    // The faces the bits name seen from the other side of the box, as a mesh drawn from both sides emits.
    private static int Mirrored(int faces) => ((faces & 0b010101) << 1) | ((faces & 0b101010) >> 1);

    /// <summary>Whether an instance emitting <paramref name="emission"/> is carried as a light, its box in the world no longer than twice the first cascade's spacing.</summary>
    public static bool Carries(Vector3 emission, SceneFieldPlan.Box box, Matrix4x4 world, float firstCell)
    {
        if (emission is { X: <= 0, Y: <= 0, Z: <= 0 }) return false;
        var size = box.Max - box.Min;
        var longest = MathF.Max(size.X * new Vector3(world.M11, world.M12, world.M13).Length(),
            MathF.Max(size.Y * new Vector3(world.M21, world.M22, world.M23).Length(), size.Z * new Vector3(world.M31, world.M32, world.M33).Length()));
        return longest <= 2 * GlobalIlluminationRenderer.ProbeSpacing * firstCell;
    }

    /// <summary>A light as it is written, the middle of its box, half its side along each of the box's axes, its light and its surface over its box's.</summary>
    /// <remarks>
    /// <see cref="Faces"/> names which faces of its box emit, a bit a face, two an axis of
    /// <see cref="X"/>, <see cref="Y"/> and <see cref="Z"/>, the side the axis points to the higher,
    /// or 0 for every face, its surface spread over them by <see cref="Share"/>. A block of a voxel
    /// game showing a top and one side lit the ground before its side by a third of the side's light
    /// spread over six faces.
    /// </remarks>
    internal readonly record struct Light(Vector3 Middle, Vector3 X, Vector3 Y, Vector3 Z, Vector3 Glow, float Share, int Faces = 0);

    /// <summary>
    /// Writes the lights the carried emitters among <paramref name="drawn"/> make, those nearest
    /// <paramref name="eye"/>, into <paramref name="bytes"/>, the count and then each light, and
    /// gives how many there were in all, written or not.
    /// </summary>
    /// <param name="bytes">The count's and the box's bytes, <see cref="Head"/>, and the lights', <see cref="Size"/> in all.</param>
    /// <param name="drawn">The meshes the field gathered this frame.</param>
    /// <param name="eye">The window's eye, the nearest to which are written.</param>
    /// <param name="firstCell">The width of a cell of the field's first cascade, which says which emitters are carried.</param>
    /// <param name="boxOf">The box around a mesh's vertices in its own space.</param>
    /// <param name="shareOf">A mesh's surface over its box's, 1 for a box.</param>
    /// <param name="places">
    /// The places the lights kept the frame before, which a light still among them is written in
    /// again, or null to write them in order of their distance from the eye.
    /// </param>
    /// <param name="partsOf">The parts a mesh is cut into (<see cref="PartsOf"/>), or null to take every mesh whole.</param>
    public static int Write(Span<byte> bytes, IReadOnlyList<(SceneFieldPlan.Instance Instance, bool Skinned)> drawn, Vector3 eye, float firstCell,
        Func<ModelVertex[], SceneFieldPlan.Box> boxOf, Func<ModelVertex[], float> shareOf, GlowPlaces? places = null,
        Func<ModelVertex[], IReadOnlyList<Part>>? partsOf = null)
    {
        var lights = Gather(drawn, firstCell, boxOf, shareOf, partsOf);
        lights.Sort((a, b) => Vector3.DistanceSquared(a.Middle, eye).CompareTo(Vector3.DistanceSquared(b.Middle, eye)));
        var floats = MemoryMarshal.Cast<byte, float>(bytes);
        var count = Math.Min(lights.Count, Most);
        Span<int> placeOf = stackalloc int[Most];
        floats[0] = places?.Place(lights, count, placeOf) ?? count;
        // The box no light reaches past, empty where there is none, so a point outside it, as one
        // of the far land, takes no light without being weighed against each.
        var (low, high) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
        for (int n = 0; n < count; n++)
        {
            if (places is null) placeOf[n] = n;
            var (middle, x, y, z, glow, share, faces) = lights[n];
            var surface = 4 * FacesSurface(new Vector3(x.Length(), y.Length(), z.Length()), faces == 0 ? All : faces) * share;
            var brightest = MathF.Max(glow.X, MathF.Max(glow.Y, glow.Z));
            var reach = MathF.Sqrt(brightest * surface / (4 * Faint));
            var light = floats.Slice(Head / 4 + placeOf[n] * Bytes / 4, Bytes / 4);
            (light[0], light[1], light[2], light[3]) = (middle.X, middle.Y, middle.Z, reach);
            (low, high) = (Vector3.Min(low, middle - new Vector3(reach)), Vector3.Max(high, middle + new Vector3(reach)));
            (light[4], light[5], light[6], light[7]) = (x.X, x.Y, x.Z, x.Length());
            (light[8], light[9], light[10], light[11]) = (y.X, y.Y, y.Z, y.Length());
            (light[12], light[13], light[14], light[15]) = (z.X, z.Y, z.Z, z.Length());
            // The share and the faces in one value, the share below 2 and twice the faces above it,
            // as glowShare and glowFaces in glow.slang read them.
            (light[16], light[17], light[18], light[19]) = (glow.X, glow.Y, glow.Z, share + 2 * faces);
        }
        (floats[1], floats[2], floats[3]) = (low.X, low.Y, low.Z);
        (floats[4], floats[5], floats[6]) = (high.X, high.Y, high.Z);
        return lights.Count;
    }

    /// <summary>
    /// The lights the carried emitters among <paramref name="drawn"/> make, one for each, or, for
    /// each group of boxes square to the world's axes that touch and emit the same light, one
    /// for the group, its box around them all and its share of that box's surface what of theirs no
    /// other covers.
    /// </summary>
    /// <remarks>
    /// Four emissive blocks stacked two by two were four lights, each marched to through the others
    /// and each emitting from the faces the others cover, so the stack threw a ring of light that
    /// came and went around it. As one light of a box two blocks wide its light is the stack's.
    /// </remarks>
    internal static List<Light> Gather(IReadOnlyList<(SceneFieldPlan.Instance Instance, bool Skinned)> drawn, float firstCell,
        Func<ModelVertex[], SceneFieldPlan.Box> boxOf, Func<ModelVertex[], float> shareOf, Func<ModelVertex[], IReadOnlyList<Part>>? partsOf = null)
    {
        var lights = new List<Light>();
        var square = new List<(SceneFieldPlan.Box Box, Vector3 Glow, float Surface, float Share, int Faces)>();
        void Add(SceneFieldPlan.Box box, Matrix4x4 w, Vector3 glow, float share, int faces = 0)
        {
            // A flat emitter, as a block's one face showing above the ground around it, is a box a
            // hundredth of its size thick about its face, which lights each side by the face's
            // whole surface. Flat, its faces along its thin side were left out of its light and its
            // surface counted on both sides at half, and the model pass drew the face itself dark
            // where the screen's probes read it.
            var size = box.Max - box.Min;
            var largest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
            var least = 0.01f * largest;
            if (largest > 0 && MathF.Min(size.X, MathF.Min(size.Y, size.Z)) < least)
            {
                var grow = Vector3.Max(Vector3.Zero, new Vector3(least) - size) / 2;
                box = new SceneFieldPlan.Box(box.Min - grow, box.Max + grow);
                if (faces == 0) share = MathF.Min(1, share * 2);
            }
            if (Square(w))
            {
                var world = box.Transformed(w);
                // A side of the box a negative scale turns to the other way names the other face.
                for (int k = 0; k < 3; k++)
                    if ((k == 0 ? w.M11 : k == 1 ? w.M22 : w.M33) < 0 && faces != 0)
                        faces = (faces & ~(3 << (2 * k))) | (((faces >> (2 * k)) & 1) << (2 * k + 1)) | (((faces >> (2 * k + 1)) & 1) << (2 * k));
                square.Add((world, glow, FacesSurface(world.Max - world.Min, faces == 0 ? All : faces) * share, share, faces));
                return;
            }
            var half = (box.Max - box.Min) / 2;
            lights.Add(new Light(Vector3.Transform((box.Min + box.Max) / 2, w), new Vector3(w.M11, w.M12, w.M13) * half.X,
                new Vector3(w.M21, w.M22, w.M23) * half.Y, new Vector3(w.M31, w.M32, w.M33) * half.Z, glow, share, faces));
        }
        foreach (var (instance, _) in drawn)
        {
            if (instance.Emission is { X: <= 0, Y: <= 0, Z: <= 0 }) continue;
            var w = instance.World;
            if (partsOf?.Invoke(instance.Vertices) is { Count: > 1 } parts)
            {
                if (!Carries(instance.Emission, parts, w, firstCell)) continue;
                foreach (var part in parts)
                {
                    // A mesh drawn from both sides emits from the faces behind its triangles too.
                    var (share, faces) = (part.Share, part.Faces);
                    if (instance.DoubleSided && faces != 0)
                    {
                        var both = faces | Mirrored(faces);
                        var size = part.Box.Max - part.Box.Min;
                        share = MathF.Min(1, share * 2 * FacesSurface(size, faces) / MathF.Max(FacesSurface(size, both), 1e-12f));
                        faces = both;
                    }
                    Add(part.Box, w, instance.Emission, share, faces);
                }
                continue;
            }
            var box = boxOf(instance.Vertices);
            if (!Carries(instance.Emission, box, w, firstCell)) continue;
            Add(box, w, instance.Emission, shareOf(instance.Vertices));
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
            // A box alone keeps its faces and share.
            if (group.Count() == 1 && square[group.Key] is var one)
            {
                var halves = (one.Box.Max - one.Box.Min) / 2;
                lights.Add(new Light((one.Box.Min + one.Box.Max) / 2, new Vector3(halves.X, 0, 0), new Vector3(0, halves.Y, 0), new Vector3(0, 0, halves.Z),
                    one.Glow, one.Share, one.Faces));
                continue;
            }
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

/// <summary> The place each glow light is written in, kept from frame to frame. A light among the
/// frame before's keeps its place and a new one takes the first that is free, so what the screen's
/// probes saw of a light the frame before is found in its place as the lights move nearer the eye
/// and farther in the order they are taken in. </summary>
internal sealed class GlowPlaces
{
    private readonly Dictionary<Vector3, int> _before = [];
    private readonly Dictionary<Vector3, int> _now = [];

    /// <summary>The places whose light is the one the frame before had there, a bit a place, the first 32 in the low word.</summary>
    public (uint Low, uint High) Kept { get; private set; }

    /// <summary>
    /// Places the first <paramref name="count"/> of <paramref name="lights"/>, each known by its
    /// middle, in <paramref name="placeOf"/>, and gives how many places are taken up to the last.
    /// </summary>
    public int Place(List<GlowLights.Light> lights, int count, Span<int> placeOf)
    {
        Span<bool> taken = stackalloc bool[GlowLights.Most];
        uint low = 0, high = 0;
        _now.Clear();
        for (int n = 0; n < count; n++)
        {
            placeOf[n] = -1;
            if (_before.TryGetValue(lights[n].Middle, out var place) && !taken[place])
            {
                (placeOf[n], taken[place]) = (place, true);
                if (place < 32) low |= 1u << place;
                else high |= 1u << (place - 32);
            }
        }
        int free = 0, used = 0;
        for (int n = 0; n < count; n++)
        {
            if (placeOf[n] < 0)
            {
                while (taken[free]) free++;
                (placeOf[n], taken[free]) = (free, true);
            }
            _now[lights[n].Middle] = placeOf[n];
            used = Math.Max(used, placeOf[n] + 1);
        }
        // A place no light took holds nothing new.
        for (int place = 0; place < used; place++)
            if (!taken[place])
            {
                if (place < 32) low |= 1u << place;
                else high |= 1u << (place - 32);
            }
        _before.Clear();
        foreach (var (middle, place) in _now) _before[middle] = place;
        Kept = (low, high);
        return used;
    }

    /// <summary>Lets every place go, as a frame that writes no glow light does.</summary>
    public void Forget()
    {
        _before.Clear();
        Kept = (0, 0);
    }
}
