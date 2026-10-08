using System.Runtime.InteropServices;
using System.Numerics;

namespace Engine;

/// <summary>
/// What the scene's distance field is built from each frame and where, worked out on the CPU from
/// the meshes drawn into the window that cast shadows: where each cascade lies around the eye,
/// which cascades are built again this frame and from which still meshes, and which meshes are
/// stamped as boxes because they moved.
/// </summary>
/// <remarks>
/// <para>
/// A cascade is <see cref="Resolution"/> cells a side, each cascade's cells twice as wide as the
/// one before's, and lies around a point ahead of the eye, by <see cref="Lead"/> of its width the
/// way the eye looks, so more of it holds what the camera sees, with its corner on a grid of
/// <see cref="SnapCells"/> of its cells, so it moves only when that point has gone that far and
/// the cells it keeps stay where they were. A cascade that moves, or that a still mesh came into or left, is built again, the
/// finest first, as many a frame as the budget says, and until then keeps the place and the meshes
/// it was built with, which the info the passes read says.
/// </para>
/// <para>
/// A mesh drawn the same, by its mesh, its vertices, its world matrix and its sides, for
/// <see cref="SettleFrames"/> frames running is still and built into the cascades around it. One
/// that moved more recently, a skinned one, and a still one whose cascades have not been built
/// again since it came are stamped each frame as boxes, the nearest the eye first to
/// <see cref="MaxShapes"/> boxes, into the bricks of cells each box comes within the band of, and the
/// bricks stamped the frame before are stamped again so a box that left one is gone. A skinned
/// mesh is a box for each joint around the vertices it holds most, posed by the joint, and one that
/// does not bend a box for each part its triangles were cut into, or the box around all of them.
/// </para>
/// </remarks>
internal sealed class SceneFieldPlan
{
    /// <summary>The cells along each side of a cascade.</summary>
    public const int Resolution = 64;

    /// <summary>How many cells from a surface the distances are exact, past which they hold that many.</summary>
    public const int Band = 4;

    /// <summary>How many frames running a mesh is drawn the same before it is still.</summary>
    public const int SettleFrames = 8;

    /// <summary>How many shapes are stamped a frame at most, the nearest the eye.</summary>
    public const int MaxShapes = 256;

    /// <summary>The cells along each side of a brick a stamp writes.</summary>
    public const int BrickCells = 4;

    /// <summary>How many of its cells a cascade's corner moves by at least.</summary>
    public const int SnapCells = 8;

    /// <summary>The share of its width a cascade's middle lies ahead of the eye.</summary>
    public const float Lead = 0.35f;

    /// <summary>
    /// A mesh drawn this frame: its id, the vertices it has, where it is drawn, whether it has an
    /// inside, and its surface's color and the light it gives off, linear, which the field paints
    /// its cells with.
    /// </summary>
    internal readonly record struct Instance(int Mesh, ModelVertex[] Vertices, Matrix4x4 World, bool DoubleSided,
        Vector3 Color = default, Vector3 Emission = default, Part[]? Parts = null);

    /// <summary>
    /// A part of a mesh stamped as a box of its own: the box around the part's vertices at rest in
    /// the mesh's own space, and where the part's pose moves that space to, a joint's matrix for a
    /// skinned mesh's limb and none for a part of a mesh that does not bend.
    /// </summary>
    internal readonly record struct Part(Box Rest, Matrix4x4 Pose);

    /// <summary>A box along the axes.</summary>
    internal readonly record struct Box(Vector3 Min, Vector3 Max)
    {
        public bool Overlaps(Box other) => Min.X <= other.Max.X && Max.X >= other.Min.X && Min.Y <= other.Max.Y && Max.Y >= other.Min.Y
                                           && Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

        public Box Grown(float by) => new(Min - new Vector3(by), Max + new Vector3(by));

        /// <summary>The box around this one's eight corners moved by <paramref name="world"/>.</summary>
        public Box Transformed(Matrix4x4 world)
        {
            var (min, max) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
            for (int c = 0; c < 8; c++)
            {
                var corner = Vector3.Transform(new Vector3((c & 1) == 0 ? Min.X : Max.X, (c & 2) == 0 ? Min.Y : Max.Y, (c & 4) == 0 ? Min.Z : Max.Z), world);
                (min, max) = (Vector3.Min(min, corner), Vector3.Max(max, corner));
            }
            return new Box(min, max);
        }

        /// <summary>The box around a mesh's vertices in its own space.</summary>
        public static Box Of(ModelVertex[] vertices)
        {
            if (vertices.Length == 0) return new Box(Vector3.Zero, Vector3.Zero);
            var (min, max) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
            foreach (var vertex in vertices) (min, max) = (Vector3.Min(min, vertex.Position), Vector3.Max(max, vertex.Position));
            return new Box(min, max);
        }
    }

    /// <summary>A cascade built this frame: which, its corner, its cells' width and the still meshes that come within its band.</summary>
    internal sealed record Build(int Cascade, Vector3 Origin, float Cell, Instance[] Instances);

    /// <summary>
    /// A mesh stamped as a box: from the world into its own space, the box's middle and half
    /// extents there, the least scale from there to the world, and the mesh's color in linear light.
    /// </summary>
    internal readonly record struct Shape(Matrix4x4 ToOwn, Vector3 Center, Vector3 Extents, float Scale, Vector3 Color = default);

    private readonly Func<ModelVertex[], Box> _bounds;
    private readonly Func<ModelVertex[], Part[]?> _parts;
    private Dictionary<Instance, int> _seen = [];
    private readonly Dictionary<Instance, (Box Bounds, long Added)> _still = [];
    private readonly HashSet<Instance> _pending = [];
    private readonly Vector3?[] _built;
    private readonly long[] _builtFrame;
    private readonly bool[] _dirty;
    // How many shapes come within each brick of each cascade this frame and the frame before, the
    // cascades one after another, which a figure of many boxes that come within the same bricks
    // counts at the cost of an add each, where a set of the bricks cost a hash each, and the bricks
    // each shape comes within in each cascade, its first and last along each axis.
    private int[] _stamped = [], _stampedBefore = [];
    private readonly List<(int Shape, int Cascade, int X0, int Y0, int Z0, int X1, int Y1, int Z1)> _reaches = [];
    private long _frame;

    /// <summary>A plan of <paramref name="cascades"/> cascades, the first's cells <paramref name="cellSize"/> wide, building <paramref name="budget"/> a frame at most.</summary>
    /// <param name="cascades">How many cascades.</param>
    /// <param name="cellSize">The width of a cell of the finest.</param>
    /// <param name="budget">How many cascades are built a frame at most.</param>
    /// <param name="bounds">The box around a mesh's vertices in its own space, which the caller may keep.</param>
    /// <param name="parts">The parts a mesh that does not bend is stamped as, or null for its one box, which the caller may keep.</param>
    public SceneFieldPlan(int cascades, float cellSize, int budget, Func<ModelVertex[], Box>? bounds = null, Func<ModelVertex[], Part[]?>? parts = null)
    {
        _parts = parts ?? (_ => null);
        Cascades = Math.Clamp(cascades, 1, 8);
        CellSize = cellSize;
        Budget = Math.Max(1, budget);
        _bounds = bounds ?? Box.Of;
        _built = new Vector3?[Cascades];
        _builtFrame = new long[Cascades];
        _dirty = Enumerable.Repeat(true, Cascades).ToArray();
    }

    /// <summary>How many cascades.</summary>
    public int Cascades { get; }

    /// <summary>The width of a cell of the finest cascade.</summary>
    public float CellSize { get; }

    /// <summary>How many cascades are built a frame at most.</summary>
    public int Budget { get; }

    /// <summary>The cascades built this frame.</summary>
    public List<Build> Builds { get; } = [];

    /// <summary>The shapes stamped this frame.</summary>
    public List<Shape> Shapes { get; } = [];

    /// <summary>The bricks stamped this frame, by cascade and place in bricks.</summary>
    public List<(int Cascade, int X, int Y, int Z)> Bricks { get; } = [];

    /// <summary>
    /// The shapes that come within each of <see cref="Bricks"/>, where its run of
    /// <see cref="BrickShapes"/> begins and how long it is, none for a brick only put back.
    /// </summary>
    public List<(int First, int Count)> BrickRanges { get; } = [];

    /// <summary>The shapes of each brick one run after another, by their place in <see cref="Shapes"/>.</summary>
    public List<int> BrickShapes { get; } = [];

    /// <summary>How many meshes are still.</summary>
    public int StillCount => _still.Count;

    /// <summary>The width of a cell of a cascade.</summary>
    public float CellOf(int cascade) => CellSize * (1 << cascade);

    /// <summary>Where a cascade lies as it was last built, its corner, or null for one not yet built.</summary>
    public Vector3? BuiltOrigin(int cascade) => _built[cascade];

    /// <summary>
    /// Where a cascade's corner would lie around <paramref name="eye"/> looking along
    /// <paramref name="ahead"/>, a unit direction, or zero for a cascade around the eye itself.
    /// </summary>
    public Vector3 OriginAround(int cascade, Vector3 eye, Vector3 ahead = default)
    {
        var cell = CellOf(cascade);
        var step = cell * SnapCells;
        var middle = eye + ahead * (Lead * Resolution * cell);
        var snapped = new Vector3(MathF.Floor(middle.X / step), MathF.Floor(middle.Y / step), MathF.Floor(middle.Z / step)) * step;
        return snapped - new Vector3(Resolution / 2 * cell);
    }

    /// <summary>
    /// Marks every cascade to be built again at each of the next <paramref name="frames"/> frames,
    /// as many a frame as the budget allows, so a profile over them times a build.
    /// </summary>
    public void Rebuild(int frames) => _rebuildFrames = Math.Max(1, frames);

    private int _rebuildFrames;

    // The box a cascade covers from its corner.
    private Box CascadeBox(int cascade, Vector3 origin) => new(origin, origin + new Vector3(Resolution * CellOf(cascade)));

    /// <summary>
    /// Works out the frame's builds, shapes and bricks from the meshes drawn this frame and the
    /// eye the cascades lie around, looking along <paramref name="ahead"/> where it is given.
    /// </summary>
    public void Update(Vector3 eye, IReadOnlyList<(Instance Instance, bool Skinned)> drawn, Vector3 ahead = default)
    {
        _frame++;
        Builds.Clear();
        Shapes.Clear();
        Bricks.Clear();
        BrickRanges.Clear();
        BrickShapes.Clear();
        _reaches.Clear();

        // Each mesh's frames drawn the same, a skinned one never still.
        var seen = new Dictionary<Instance, int>(drawn.Count);
        var moving = new List<Instance>();
        foreach (var (instance, skinned) in drawn)
        {
            if (seen.ContainsKey(instance)) continue;
            var frames = skinned ? 0 : Math.Min(SettleFrames, _seen.GetValueOrDefault(instance) + 1);
            seen[instance] = frames;
            if (frames < SettleFrames) moving.Add(instance);
        }
        _seen = seen;

        // Still meshes that came or went make the cascades around them dirty.
        foreach (var (instance, frames) in seen)
            if (frames >= SettleFrames && !_still.ContainsKey(instance))
            {
                var bounds = _bounds(instance.Vertices).Transformed(instance.World);
                _still[instance] = (bounds, _frame);
                _pending.Add(instance);
                MarkAround(bounds);
            }
        foreach (var instance in _still.Keys.Where(instance => !seen.ContainsKey(instance)).ToArray())
        {
            MarkAround(_still[instance].Bounds);
            _still.Remove(instance);
            _pending.Remove(instance);
        }

        // Cascades the eye has moved past, then as many dirty ones as the budget allows, finest first.
        if (_rebuildFrames > 0)
        {
            _rebuildFrames--;
            Array.Fill(_dirty, true);
        }
        for (int c = 0; c < Cascades; c++)
            if (_built[c] != OriginAround(c, eye, ahead)) _dirty[c] = true;
        var rebuilt = new bool[Cascades];
        for (int c = 0, budget = Budget; c < Cascades && budget > 0; c++)
        {
            if (!_dirty[c]) continue;
            budget--;
            var origin = OriginAround(c, eye, ahead);
            var reach = CascadeBox(c, origin).Grown(Band * CellOf(c));
            Builds.Add(new Build(c, origin, CellOf(c), [.. _still.Where(entry => entry.Value.Bounds.Overlaps(reach)).Select(entry => entry.Key)]));
            (_built[c], _builtFrame[c], _dirty[c], rebuilt[c]) = (origin, _frame, false, true);
        }

        // A still mesh is stamped until every built cascade it comes within the band of has been
        // built since it came.
        foreach (var instance in _pending.ToArray())
        {
            var (bounds, added) = _still[instance];
            var waiting = false;
            for (int c = 0; c < Cascades && !waiting; c++)
                waiting = _built[c] is { } origin && _builtFrame[c] < added && bounds.Overlaps(CascadeBox(c, origin).Grown(Band * CellOf(c)));
            if (waiting) moving.Add(instance);
            else _pending.Remove(instance);
        }

        // The nearest meshes' shapes, a box for each part of each, posed, to as many as a frame
        // stamps, and the bricks they come within the band of in each built cascade.
        const int side = Resolution / BrickCells;
        if (_stamped.Length != Cascades * side * side * side) (_stamped, _stampedBefore) = (new int[Cascades * side * side * side], new int[Cascades * side * side * side]);
        (_stamped, _stampedBefore) = (_stampedBefore, _stamped);
        Array.Clear(_stamped);
        var nearest = moving.OrderBy(i => Vector3.DistanceSquared(i.World.Translation, eye)).ToArray();
        for (int n = 0; n < nearest.Length && Shapes.Count < MaxShapes; n++)
        foreach (var (own, pose) in PartsWithin(nearest[n], MaxShapes - Shapes.Count - (nearest.Length - n - 1)))
        {
            var instance = nearest[n];
            var w = pose * instance.World;
            if (!Matrix4x4.Invert(w, out var toOwn)) continue;
            var scale = MathF.Min(new Vector3(w.M11, w.M12, w.M13).Length(), MathF.Min(new Vector3(w.M21, w.M22, w.M23).Length(), new Vector3(w.M31, w.M32, w.M33).Length()));
            Shapes.Add(new Shape(toOwn, (own.Min + own.Max) / 2, (own.Max - own.Min) / 2, scale, instance.Color));
            var bounds = own.Transformed(w);
            for (int c = 0; c < Cascades; c++)
            {
                if (_built[c] is not { } origin) continue;
                var brick = BrickCells * CellOf(c);
                var near = bounds.Grown(Band * CellOf(c));
                var first = Vector3.Max(Vector3.Zero, (near.Min - origin) / brick);
                var last = Vector3.Min(new Vector3(side - 1), (near.Max - origin) / brick);
                if (first.X > last.X || first.Y > last.Y || first.Z > last.Z) continue;
                _reaches.Add((Shapes.Count - 1, c, (int)first.X, (int)first.Y, (int)first.Z, (int)last.X, (int)last.Y, (int)last.Z));
                for (int z = (int)first.Z; z <= (int)last.Z; z++)
                    for (int y = (int)first.Y; y <= (int)last.Y; y++)
                        for (int x = (int)first.X; x <= (int)last.X; x++)
                            _stamped[((c * side + z) * side + y) * side + x]++;
            }
        }
        // The bricks stamped now, each with a run of its shapes, and last frame's, which go back to
        // the still field where no shape covers them now, but in a cascade built again this frame,
        // which the build wrote whole. The counts become where each brick's run begins.
        var runs = 0;
        for (int c = 0, at = 0; c < Cascades; c++)
            for (int z = 0; z < side; z++)
                for (int y = 0; y < side; y++)
                    for (int x = 0; x < side; x++, at++)
                    {
                        var shapes = _stamped[at];
                        if (shapes == 0 && (_stampedBefore[at] == 0 || rebuilt[c])) continue;
                        Bricks.Add((c, x, y, z));
                        BrickRanges.Add((runs, shapes));
                        _stamped[at] = runs;
                        runs += shapes;
                    }
        CollectionsMarshal.SetCount(BrickShapes, runs);
        var filled = CollectionsMarshal.AsSpan(BrickShapes);
        foreach (var (shape, c, x0, y0, z0, x1, y1, z1) in _reaches)
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        filled[_stamped[((c * side + z) * side + y) * side + x]++] = shape;
        // Each brick's count put back where the fill left the end of its run, which the frame after
        // reads for the bricks stamped now.
        for (int b = 0; b < Bricks.Count; b++)
        {
            var (c, x, y, z) = Bricks[b];
            _stamped[((c * side + z) * side + y) * side + x] = BrickRanges[b].Count;
        }
    }

    // The parts a mesh is stamped as where there are no more than room for, which leaves a box
    // each for the meshes farther from the eye, and the box around all of it where there are, so a
    // crowd of figures past what a frame stamps is stamped as figures near the eye and boxes beyond.
    private Part[] PartsWithin(Instance instance, int room) =>
        (instance.Parts ?? _parts(instance.Vertices)) is { } parts && parts.Length <= room
            ? parts
            : [new Part(_bounds(instance.Vertices), Matrix4x4.Identity)];

    // Marks dirty every cascade whose band a box comes within, where the cascade has been built.
    private void MarkAround(Box bounds)
    {
        for (int c = 0; c < Cascades; c++)
            if (_built[c] is { } origin && bounds.Overlaps(CascadeBox(c, origin).Grown(Band * CellOf(c))))
                _dirty[c] = true;
    }
}
