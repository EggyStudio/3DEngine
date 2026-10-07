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
/// one before's, and lies around the eye with its corner on a grid of <see cref="SnapCells"/> of
/// its cells, so it moves only when the eye has gone that far and the cells it keeps stay where
/// they were. A cascade that moves, or that a still mesh came into or left, is built again, the
/// finest first, as many a frame as the budget says, and until then keeps the place and the meshes
/// it was built with, which the info the passes read says.
/// </para>
/// <para>
/// A mesh drawn the same, by its mesh, its vertices, its world matrix and its sides, for
/// <see cref="SettleFrames"/> frames running is still and built into the cascades around it. One
/// that moved more recently, a skinned one, and a still one whose cascades have not been built
/// again since it came are stamped each frame as the box around its vertices in its own space, the
/// <see cref="MaxShapes"/> nearest the eye, into the bricks of cells that box comes within the band
/// of, and the bricks stamped the frame before are stamped again so a box that left one is gone.
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

    /// <summary>A mesh drawn this frame: its id, the vertices it has, where it is drawn, and whether it has an inside.</summary>
    internal readonly record struct Instance(int Mesh, ModelVertex[] Vertices, Matrix4x4 World, bool DoubleSided);

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
    /// extents there, and the least scale from there to the world.
    /// </summary>
    internal readonly record struct Shape(Matrix4x4 ToOwn, Vector3 Center, Vector3 Extents, float Scale);

    private readonly Func<ModelVertex[], Box> _bounds;
    private Dictionary<Instance, int> _seen = [];
    private readonly Dictionary<Instance, (Box Bounds, long Added)> _still = [];
    private readonly HashSet<Instance> _pending = [];
    private readonly Vector3?[] _built;
    private readonly long[] _builtFrame;
    private readonly bool[] _dirty;
    private HashSet<(int Cascade, int X, int Y, int Z)> _stamped = [];
    private long _frame;

    /// <summary>A plan of <paramref name="cascades"/> cascades, the first's cells <paramref name="cellSize"/> wide, building <paramref name="budget"/> a frame at most.</summary>
    /// <param name="cascades">How many cascades.</param>
    /// <param name="cellSize">The width of a cell of the finest.</param>
    /// <param name="budget">How many cascades are built a frame at most.</param>
    /// <param name="bounds">The box around a mesh's vertices in its own space, which the caller may keep.</param>
    public SceneFieldPlan(int cascades, float cellSize, int budget, Func<ModelVertex[], Box>? bounds = null)
    {
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

    /// <summary>How many meshes are still.</summary>
    public int StillCount => _still.Count;

    /// <summary>The width of a cell of a cascade.</summary>
    public float CellOf(int cascade) => CellSize * (1 << cascade);

    /// <summary>Where a cascade lies as it was last built, its corner, or null for one not yet built.</summary>
    public Vector3? BuiltOrigin(int cascade) => _built[cascade];

    /// <summary>Where a cascade's corner would lie around <paramref name="eye"/>.</summary>
    public Vector3 OriginAround(int cascade, Vector3 eye)
    {
        var cell = CellOf(cascade);
        var step = cell * SnapCells;
        var snapped = new Vector3(MathF.Floor(eye.X / step), MathF.Floor(eye.Y / step), MathF.Floor(eye.Z / step)) * step;
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
    /// eye the cascades lie around.
    /// </summary>
    public void Update(Vector3 eye, IReadOnlyList<(Instance Instance, bool Skinned)> drawn)
    {
        _frame++;
        Builds.Clear();
        Shapes.Clear();
        Bricks.Clear();

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
            if (_built[c] != OriginAround(c, eye)) _dirty[c] = true;
        var rebuilt = new bool[Cascades];
        for (int c = 0, budget = Budget; c < Cascades && budget > 0; c++)
        {
            if (!_dirty[c]) continue;
            budget--;
            var origin = OriginAround(c, eye);
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

        // The nearest shapes, and the bricks they come within the band of in each built cascade.
        var stamped = new HashSet<(int Cascade, int X, int Y, int Z)>();
        foreach (var instance in moving.OrderBy(i => Vector3.DistanceSquared(i.World.Translation, eye)).Take(MaxShapes))
        {
            var own = _bounds(instance.Vertices);
            if (!Matrix4x4.Invert(instance.World, out var toOwn)) continue;
            var w = instance.World;
            var scale = MathF.Min(new Vector3(w.M11, w.M12, w.M13).Length(), MathF.Min(new Vector3(w.M21, w.M22, w.M23).Length(), new Vector3(w.M31, w.M32, w.M33).Length()));
            Shapes.Add(new Shape(toOwn, (own.Min + own.Max) / 2, (own.Max - own.Min) / 2, scale));
            var bounds = own.Transformed(w);
            for (int c = 0; c < Cascades; c++)
            {
                if (_built[c] is not { } origin) continue;
                var brick = BrickCells * CellOf(c);
                var near = bounds.Grown(Band * CellOf(c));
                var first = Vector3.Max(Vector3.Zero, (near.Min - origin) / brick);
                var last = Vector3.Min(new Vector3(Resolution / BrickCells - 1), (near.Max - origin) / brick);
                if (first.X > last.X || first.Y > last.Y || first.Z > last.Z) continue;
                for (int z = (int)first.Z; z <= (int)last.Z; z++)
                    for (int y = (int)first.Y; y <= (int)last.Y; y++)
                        for (int x = (int)first.X; x <= (int)last.X; x++)
                            stamped.Add((c, x, y, z));
            }
        }
        // Last frame's bricks go back to the still field where no shape covers them now, but in a
        // cascade built again this frame, which the build wrote whole.
        Bricks.AddRange(stamped);
        foreach (var brick in _stamped)
            if (!rebuilt[brick.Cascade] && !stamped.Contains(brick)) Bricks.Add(brick);
        _stamped = stamped;
    }

    // Marks dirty every cascade whose band a box comes within, where the cascade has been built.
    private void MarkAround(Box bounds)
    {
        for (int c = 0; c < Cascades; c++)
            if (_built[c] is { } origin && bounds.Overlaps(CascadeBox(c, origin).Grown(Band * CellOf(c))))
                _dirty[c] = true;
    }
}
