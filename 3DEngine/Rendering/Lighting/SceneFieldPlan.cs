using System.Runtime.InteropServices;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// What the scene's distance field is built from each frame and where, worked out on the CPU from
/// the meshes drawn into the window that cast shadows, which says where each cascade lies around
/// the eye, which cascades are built again this frame and from which still meshes, and which meshes
/// are stamped as boxes because they moved.
/// </summary>
/// <remarks>
/// <para>
/// A cascade is <see cref="Resolution"/> cells a side, each cascade's cells twice as wide as the
/// one before's, and lies around the eye, with its corner on a grid of <see cref="SnapCells"/> of
/// its cells, so it moves only when the eye has gone that far, a turn moving none, and the cells it
/// keeps stay where they were. A cascade the eye moved by less than half its width shifts as a
/// clipmap does, the cells it keeps moved on the GPU and the slab it brings in built alone, one axis
/// at a time (SceneFieldPlan.Shifts.cs). One moved farther, or that a still mesh came into or left,
/// is built again whole, the finest first, as many a frame as the budget says, and until then keeps
/// the place and the meshes it was built with, which the info the passes read says.
/// </para>
/// <para>
/// The eye so stands 28 to 36 cells from each side of each cascade, past the outer bands the passes
/// fade across, from a cascade to the next and from the last to what lies past the field
/// (<c>scenefield.slang</c>'s <c>bandHold</c>). A cascade lying a share of its width ahead of the
/// eye the way it looks crosses to the other side with a turn and is built again whole, Sponza's
/// 3.75 million triangles over some sixty frames at the default budget, what stood behind lying
/// outside the fine cascades meanwhile. A program whose camera looks on the scene from afar gives
/// the point the field lies around in the eye's place (<c>SetSceneFieldCenter</c>).
/// </para>
/// <para>
/// A mesh drawn the same, by its mesh, its vertices, its world matrix and its sides, for
/// <see cref="SettleFrames"/> frames running is still and built into the cascades around it, or for
/// <see cref="MobileSettleFrames"/> where it moved within as many frames, so a figure that stops for
/// a while is not built into the cascades and out again. One that moved more recently, a skinned
/// one, and a still one that moved before it settled whose cascades have not been built again since
/// it came, but for one that replaced a still mesh in place, which the cascades not yet built still
/// hold, are stamped each frame as boxes, the nearest the eye first to
/// <see cref="MaxShapes"/> boxes, into the bricks of cells each box comes within the band of, and the
/// bricks stamped the frame before are stamped again so a box that left one is gone. A skinned
/// mesh is a box for each joint around the vertices it holds most, posed by the joint, and one that
/// does not bend a box for each part its triangles were cut into, or the box around all of them. A
/// mesh that came and has not moved, as a scene's on its first load or a world's section streamed
/// in, stands in for nothing until it is built.
/// </para>
/// </remarks>
internal sealed partial class SceneFieldPlan
{
    /// <summary>The cells along each side of a cascade.</summary>
    public const int Resolution = 64;

    /// <summary>How many cells from a surface the distances are exact, past which they hold that many.</summary>
    public const int Band = 4;

    /// <summary>How many frames running a mesh is drawn the same before it is still.</summary>
    public const int SettleFrames = 8;

    /// <summary>
    /// How many frames running a mesh is drawn the same before it is still where it moved within as
    /// many frames, as a walking figure that stops for a while.
    /// </summary>
    /// <remarks>
    /// A mesh that settles marks the cascades around it to be built again, and one that leaves
    /// marks them again, a few milliseconds of the GPU each time, where stamping its box costs half a
    /// microsecond a frame, so a figure that stopped after walking is stamped on until it has stood
    /// this long. The voxel game's animals stand one to eight seconds between walks, up to 960
    /// frames at 120 a second, and 76 figures standing so for 240 to 960 frames among a land of 400
    /// meshes had a cascade built again in a fourth of the frames, 2.5 ms of the GPU's, with 480
    /// frames here, and with 1024 in one frame of fifty, for the figures that stood from the first
    /// frame, settled and then walked, the field's share 0.27 ms.
    /// </remarks>
    public const int MobileSettleFrames = 1024;

    // What a mesh's frames drawn the same are once it has settled, past every count it settles at,
    // and the frame a mesh that never moved moved at.
    private const int Settled = int.MaxValue / 2;
    private const long Never = long.MinValue / 2;

    /// <summary>How many shapes are stamped a frame at most, the nearest the eye.</summary>
    public const int MaxShapes = 256;

    /// <summary>The cells along each side of a brick a stamp writes.</summary>
    public const int BrickCells = 4;

    /// <summary>How many of its cells a cascade's corner moves by at least.</summary>
    public const int SnapCells = 8;

    /// <summary>
    /// A mesh drawn this frame, its id, the vertices it has, where it is drawn, whether it has an
    /// inside, its surface's color and emission, linear, which the field paints its
    /// cells with, its material's roughness and metallic, which a path traced reference lights
    /// it by, and the share of light that passes a unit of its depth (<see cref="ModelMaterial.LightThrough"/>).
    /// </summary>
    internal readonly record struct Instance(int Mesh, ModelVertex[] Vertices, Matrix4x4 World, bool DoubleSided,
        Vector3 Color = default, Vector3 Emission = default, Part[]? Parts = null, float Roughness = 1, float Metallic = 0, float LightThrough = 0);

    /// <summary>
    /// A part of a mesh stamped as a box of its own, the box around the part's vertices at rest in
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

        /// <summary>The box around this one and <paramref name="other"/>.</summary>
        public Box With(Box other) => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

        /// <summary>The box around this one's eight corners moved by <paramref name="world"/>.</summary>
        public Box Transformed(Matrix4x4 world)
        {
            // Moved without a perspective, its middle moved and its half extents taken along each
            // axis of the world by the matrix's sizes, as the corners give it.
            if (world.M14 == 0 && world.M24 == 0 && world.M34 == 0 && world.M44 == 1)
            {
                var (middle, half) = (Vector3.Transform((Min + Max) * 0.5f, world), (Max - Min) * 0.5f);
                var extents = new Vector3(
                    MathF.Abs(world.M11) * half.X + MathF.Abs(world.M21) * half.Y + MathF.Abs(world.M31) * half.Z,
                    MathF.Abs(world.M12) * half.X + MathF.Abs(world.M22) * half.Y + MathF.Abs(world.M32) * half.Z,
                    MathF.Abs(world.M13) * half.X + MathF.Abs(world.M23) * half.Y + MathF.Abs(world.M33) * half.Z);
                return new Box(middle - extents, middle + extents);
            }
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

    /// <summary>
    /// A cascade's build, or the part of it this frame splats, which cascade it is, its corner, its
    /// cells' width, the still meshes within its band it splats, on its last part the box around
    /// the meshes that came or went within it since it was last built, the whole cascade at its first
    /// build, or null where nothing did, as where the eye moved it, and the cells it builds where it
    /// builds those alone.
    /// </summary>
    /// <remarks>
    /// A build of more triangles than a frame's budget is splatted over as many frames as it takes,
    /// its <paramref name="First"/> part clearing the cascade's words and its <paramref name="Last"/>
    /// turning them into the distances the passes read, so until then the passes read the cascade as
    /// it was last built. A build of <paramref name="Cells"/> splats the still meshes within the band
    /// of those cells and a cell around them alone, and turns those cells alone into distances, the
    /// cells around what came or went within a built cascade, or the slab a shift brings in, whose
    /// first part moves its cascade by <paramref name="ShiftCells"/> of its cells along
    /// <paramref name="ShiftAxis"/>, 0 for x, 1 for y and 2 for z, or -1 where it moves none, so the
    /// passes read the cascade at <paramref name="Origin"/> from that frame on, the cells it brought
    /// in empty until the build's last part.
    /// </remarks>
    internal sealed record Build(int Cascade, Vector3 Origin, float Cell, Instance[] Instances, Box? Changed = null, bool First = true, bool Last = true,
        CellBox? Cells = null, int ShiftAxis = -1, int ShiftCells = 0);

    /// <summary>
    /// A mesh stamped as a box, from the world into its own space, the box's middle and half
    /// extents there, the least scale from there to the world, the mesh's color in linear light, and
    /// its emission, linear, none where global illumination carries it as a glow light.
    /// </summary>
    internal readonly record struct Shape(Matrix4x4 ToOwn, Vector3 Center, Vector3 Extents, float Scale, Vector3 Color = default, float LightThrough = 0,
        Vector3 Emission = default);

    private readonly Func<ModelVertex[], Box> _bounds;
    private readonly Func<ModelVertex[], int> _triangles;
    private readonly Func<ModelVertex[], Part[]?> _parts;
    private readonly Func<Instance, Vector3> _tint;
    private readonly Func<Instance, bool> _carried;
    private Dictionary<Instance, int> _seen = new(Keys.Shared);
    // In a frame read whole, the frame each mesh drawn the frame before last moved where that was
    // within the longer count, each drawn now, and the vertices of the meshes gone since.
    private readonly Dictionary<Instance, long> _movedBefore = new(Keys.Shared), _movedNow = new(Keys.Shared);
    private readonly HashSet<ModelVertex[]> _departed = new(ReferenceEqualityComparer.Instance);
    // The world matrices of the meshes gone since, so a mesh drawn through one of them, as a mesh
    // uploaded again each frame or a section edited in place is, is no mesh that came (_movedIn).
    private readonly HashSet<Matrix4x4> _departedWorlds = [];
    private readonly Dictionary<Instance, (Box Bounds, long Added)> _still = new(Keys.Shared);
    private readonly HashSet<Instance> _pending = new(Keys.Shared);
    // Still meshes that left, kept in the field for the meshes replacing them, by the frame they left.
    private readonly Dictionary<Instance, long> _kept = new(Keys.Shared);
    // Still meshes that replaced a kept one in place and wait for cascades built since, which still
    // hold the one they replaced and so are not stamped. Stamped as its boxes, a voxel game's
    // section of ground filled the air above it in the cascades a budget of two left for the next
    // frame, and the room's indirect light went for that frame and came back.
    private readonly HashSet<Instance> _replacing = new(Keys.Shared);
    // Still meshes that waited for their cascades having moved before they settled, which are
    // stamped meanwhile as they were while they moved. One that never moved, as a scene's meshes on
    // its first load or a world's sections streamed in, is not, since a box stands in for a mesh
    // badly where it is larger than what it holds, as while Intel's Sponza built over 330 frames
    // the box of its outer walls filled the building and the sun's contact shadows darkened every
    // room.
    private readonly HashSet<Instance> _movedIn = new(Keys.Shared);
    private readonly Vector3?[] _built;
    private readonly long[] _builtFrame;
    private readonly bool[] _dirty;
    // The box around the meshes that came or went within each cascade since it was last built.
    private readonly Box?[] _changed;
    // How many shapes come within each brick of each cascade this frame and the frame before, the
    // cascades one after another, which a figure of many boxes that come within the same bricks
    // counts at the cost of an add each, where a set of the bricks cost a hash each, and the bricks
    // each shape comes within in each cascade, its first and last along each axis.
    private int[] _stamped = [], _stampedBefore = [];
    // The bricks stamped this frame and the frame before, each by its place in those counts, so a
    // frame reads and clears the bricks it stamped rather than every brick of every cascade.
    private List<int> _bricksAt = [], _bricksAtBefore = [];

    // The meshes drawn the frame before in the order drawn, with the frames each had been drawn the
    // same and whether it is skinned, and whether that order held each mesh once.
    private Instance[] _order = [];
    private int[] _orderFrames = [];
    private bool[] _orderSkinned = [];
    // Whether the mesh in each place of the order moved this frame, as SameOrder found it, and the
    // frame it last moved, which it settles after the longer count for as many frames.
    private bool[] _orderMoved = [];
    private long[] _orderMovedAt = [];
    private int _orderCount;
    private bool _orderWhole;
    private readonly List<(int Shape, int Cascade, int X0, int Y0, int Z0, int X1, int Y1, int Z1)> _reaches = [];
    // The meshes stamped this frame, and their distances from the eye with their places, sorted.
    private Instance[] _nearest = [];
    private long[] _byDistance = [];
    private long _frame;

    /// <summary>A plan of <paramref name="cascades"/> cascades, the first's cells <paramref name="cellSize"/> wide, building <paramref name="budget"/> a frame at most.</summary>
    /// <param name="cascades">How many cascades.</param>
    /// <param name="cellSize">The width of a cell of the finest.</param>
    /// <param name="budget">How many cascades are built a frame at most.</param>
    /// <param name="bounds">The box around a mesh's vertices in its own space, which the caller may keep.</param>
    /// <param name="parts">The parts a mesh that does not bend is stamped as, or null for its one box, which the caller may keep.</param>
    /// <param name="tint">
    /// What a mesh's boxes' color is multiplied by, its vertices' colors' mean in linear light, which
    /// the build blends across each face, or white for none.
    /// </param>
    /// <param name="triangleBudget">How many triangles the builds splat a frame at most, a mesh at least.</param>
    /// <param name="triangles">How many triangles a mesh has, a third of its vertices unless given.</param>
    /// <param name="farCell">The width of a cell of the far cascade after the others, or 0 for none.</param>
    /// <param name="carried">Whether global illumination carries an emissive mesh as a glow light, whose box is stamped with none of its emission.</param>
    public SceneFieldPlan(int cascades, float cellSize, int budget, Func<ModelVertex[], Box>? bounds = null, Func<ModelVertex[], Part[]?>? parts = null,
        Func<Instance, Vector3>? tint = null, int triangleBudget = SceneFieldConfig.DefaultTriangleBudget, Func<ModelVertex[], int>? triangles = null,
        float farCell = 0, Func<Instance, bool>? carried = null)
    {
        _parts = parts ?? (_ => null);
        _tint = tint ?? (_ => Vector3.One);
        _carried = carried ?? (_ => false);
        Counted = Math.Clamp(cascades, 1, 8);
        FarCell = farCell > 0 ? farCell : 0;
        Cascades = Counted + (FarCell > 0 ? 1 : 0);
        CellSize = cellSize;
        Budget = Math.Max(1, budget);
        TriangleBudget = BaseTriangleBudget = Math.Max(1, triangleBudget);
        _triangles = triangles ?? (vertices => vertices.Length / 3);
        _bounds = bounds ?? Box.Of;
        _built = new Vector3?[Cascades];
        _builtFrame = new long[Cascades];
        _dirty = Enumerable.Repeat(true, Cascades).ToArray();
        _changed = new Box?[Cascades];
        _regions = new Box?[Cascades];
    }

    /// <summary>How many cascades, the far one among them where there is one.</summary>
    public int Cascades { get; }

    /// <summary>How many cascades there are but the far one, each twice as coarse as the one before.</summary>
    public int Counted { get; }

    /// <summary>The width of a cell of the far cascade, the last, or 0 where there is none.</summary>
    public float FarCell { get; }

    /// <summary>Whether the last cascade is a far one, its cells <see cref="FarCell"/> wide.</summary>
    public bool HasFar => FarCell > 0;

    /// <summary>The width of a cell of the finest cascade.</summary>
    public float CellSize { get; }

    /// <summary>How many cascades are built a frame at most.</summary>
    public int Budget { get; }

    /// <summary>How many triangles the builds splat a frame at most, a mesh at least, raised where the frame has room (<see cref="Raise"/>).</summary>
    public int TriangleBudget { get; private set; }

    /// <summary>The cascades built this frame, or the parts of their builds splatted this frame.</summary>
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

    /// <summary>Whether a mesh drawn this frame is still, built into the cascades around it rather than stamped.</summary>
    public bool IsStill(in Instance instance) => _still.ContainsKey(instance);

    /// <summary>Whether the last frame drew the meshes the frame before did in the same order, and was read by place.</summary>
    public bool ReadByPlace { get; private set; }

    /// <summary>
    /// The cascade the probes of cascade <paramref name="probes"/> of <paramref name="cascades"/>
    /// stand in, its own number but for the last, which stands in the far cascade where there is one,
    /// as <c>scenefield.slang</c>'s <c>cascadeAt</c> has it.
    /// </summary>
    public int CascadeOfProbes(int probes, int cascades) => HasFar && probes == cascades - 1 ? Cascades - 1 : probes;

    /// <summary>The width of a cell of a cascade.</summary>
    public float CellOf(int cascade) => cascade >= Counted && HasFar ? FarCell : CellSize * (1 << cascade);

    /// <summary>
    /// The width of a cell of a far cascade for <paramref name="reach"/>, 0.85 of its width, at least
    /// twice as wide as the last counted cascade's of <paramref name="cascades"/> from
    /// <paramref name="cellSize"/>, or 0 for a reach of none or past every finite distance.
    /// </summary>
    /// <remarks>
    /// The cascade's cells keep the width they took while its middle lay 0.35 of its width ahead of
    /// the eye and its side reached that far, so the scene is held as finely as then. Lying around the
    /// eye, its sides lie 28 to 36 of its cells off, about half the reach, past which the passes take
    /// the light from beyond the field.
    /// </remarks>
    /// <remarks>
    /// It is rounded up to a sixteenth of twice the last's cell, so a reach read from a camera's far
    /// plane, which rounding moves a little from frame to frame, gives the same cell and the field is
    /// not made again.
    /// </remarks>
    public static float FarCellFor(float reach, int cascades, float cellSize)
    {
        if (!(reach > 0) || !float.IsFinite(reach)) return 0;
        var doubled = cellSize * (1 << Math.Clamp(cascades, 1, 8));
        var step = doubled / 16;
        return Math.Max(doubled, MathF.Ceiling(reach / (0.85f * Resolution) / step) * step);
    }

    /// <summary>The eye the cascades lie around this frame, as <see cref="Update"/> was last given it.</summary>
    public Vector3 Eye { get; private set; }

    /// <summary>Where a cascade lies as it was last built, its corner, or null for one not yet built.</summary>
    public Vector3? BuiltOrigin(int cascade) => _built[cascade];

    /// <summary>Where a cascade's corner would lie around <paramref name="eye"/>, its middle on the snap's grid nearest the eye.</summary>
    public Vector3 OriginAround(int cascade, Vector3 eye)
    {
        var cell = CellOf(cascade);
        var step = cell * SnapCells;
        var snapped = new Vector3(MathF.Floor(eye.X / step + 0.5f), MathF.Floor(eye.Y / step + 0.5f), MathF.Floor(eye.Z / step + 0.5f)) * step;
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
        Eye = eye;
        Builds.Clear();
        Shapes.Clear();
        Bricks.Clear();
        BrickRanges.Clear();
        BrickShapes.Clear();
        _reaches.Clear();

        var moving = new List<Instance>();
        // The meshes drawn this frame that have not settled and are not skinned, which a still
        // mesh that left may be replaced by.
        var unsettled = new List<Instance>();
        Arriving = 0;
        ReadByPlace = SameOrder(drawn);
        if (ReadByPlace)
        {
            // The meshes drawn the frame before, in the same order, each one frame more the same,
            // read by place, so a scene that did not change costs a comparison a mesh, the frames
            // of those not yet still counted in the order alone and those still in the
            // dictionary as well, which a frame read whole reads them from. A mesh in a
            // place drawn the frame before through another matrix moved, and where it stood leaves
            // as one gone from a scene read whole, so a figure walking among still meshes costs a
            // comparison a mesh too, where read whole each frame, every mesh in a dictionary, a land
            // of 400 meshes and 76 walking figures of five pieces cost 0.9 ms of the field's 2.4 in
            // a build that is not optimized.
            List<Instance>? left = null;
            var span = Span(drawn);
            for (int i = 0; i < _orderCount; i++)
            {
                ref readonly var instance = ref span[i].Instance;
                var skinned = span[i].Skinned;
                var frames = _orderFrames[i];
                if (_orderMoved[i])
                {
                    if (frames >= Settled)
                    {
                        _seen.Remove(_order[i]);
                        if (_still.ContainsKey(_order[i])) (left ??= []).Add(_order[i]);
                    }
                    _order[i] = instance;
                    _orderMovedAt[i] = _frame;
                    frames = 0;
                }
                if (frames >= Settled) continue;
                frames = skinned ? 0 : frames + 1 >= SettleAfter(_orderMovedAt[i]) ? Settled : frames + 1;
                _orderFrames[i] = frames;
                if (skinned) moving.Add(instance);
                else if (frames < Settled)
                {
                    if (_orderMovedAt[i] != Never) moving.Add(instance);
                    else Arriving++;
                    unsettled.Add(instance);
                }
                else
                {
                    _seen[instance] = frames;
                    Settle(instance, moved: _orderMovedAt[i] != Never);
                }
            }
            foreach (var instance in left ?? [])
            {
                if (ReplacedBy(instance, unsettled)) _kept[instance] = _frame;
                else Leave(instance);
            }
        }
        else
        {
            // The frames counted and the moves made in the order since a frame was last read whole.
            _movedBefore.Clear();
            _movedNow.Clear();
            for (int i = 0; i < _orderCount; i++)
            {
                if (_orderSkinned[i]) continue;
                if (_orderFrames[i] < Settled) _seen[_order[i]] = _orderFrames[i];
                if (_frame - _orderMovedAt[i] < MobileSettleFrames) _movedBefore[_order[i]] = _orderMovedAt[i];
            }
            // This frame's meshes, and the vertices of those drawn the frame before and not now, so
            // a mesh drawn now and not before of vertices one gone had is taken as that one moved,
            // as a place read by place finds.
            const int unread = int.MinValue;
            var seen = new Dictionary<Instance, int>(drawn.Count, Keys.Shared);
            foreach (var (instance, _) in drawn) seen.TryAdd(instance, unread);
            _departed.Clear();
            _departedWorlds.Clear();
            for (int i = 0; i < _orderCount; i++)
                if (!_orderSkinned[i] && !seen.ContainsKey(_order[i]))
                {
                    _departed.Add(_order[i].Vertices);
                    _departedWorlds.Add(_order[i].World);
                }
            // Each mesh's frames drawn the same, a skinned one never still.
            foreach (var (instance, skinned) in drawn)
            {
                ref var counted = ref CollectionsMarshal.GetValueRefOrNullRef(seen, instance);
                if (counted != unread) continue;
                var movedAt = skinned ? Never
                    : !_seen.ContainsKey(instance) && _departed.Contains(instance.Vertices) ? _frame : _movedBefore.GetValueOrDefault(instance, Never);
                if (movedAt != Never) _movedNow[instance] = movedAt;
                var before = _seen.GetValueOrDefault(instance);
                var frames = skinned ? 0 : before >= Settled || before + 1 >= SettleAfter(movedAt) ? Settled : before + 1;
                counted = frames;
                if (frames >= Settled) continue;
                // A mesh that came and has not moved casts nothing until it is built (_movedIn).
                if (skinned || movedAt != Never || _departedWorlds.Contains(instance.World)) moving.Add(instance);
                else Arriving++;
                if (!skinned) unsettled.Add(instance);
            }
            _seen = seen;

            // Still meshes that came or went make the cascades around them dirty. One replaced in
            // place, by a mesh of other vertices drawn through the same matrix where it was, as a
            // section of a game's world edited, stays in the field as it was until the one replacing
            // it is still, where the boxes that one would be stamped as stood in for it badly.
            foreach (var (instance, frames) in seen)
                if (frames >= Settled && !_still.ContainsKey(instance)) Settle(instance, moved: _movedNow.ContainsKey(instance));
            foreach (var instance in _kept.Keys.Where(seen.ContainsKey).ToArray()) _kept.Remove(instance);
            foreach (var instance in _still.Keys.Where(instance => !seen.ContainsKey(instance) && !_kept.ContainsKey(instance)).ToArray())
            {
                if (ReplacedBy(instance, unsettled)) _kept[instance] = _frame;
                else Leave(instance);
            }
            KeepOrder(drawn, seen);
        }

        // A mesh kept for one replacing it leaves once nothing unsettled replaces it, the one that
        // did having settled, or after twice the frames a mesh takes to settle.
        foreach (var (instance, since) in _kept.ToArray())
            if (!ReplacedBy(instance, unsettled) || _frame - since > 2 * SettleFrames)
            {
                foreach (var replacing in _pending)
                    if (Replacing(replacing, instance)) _replacing.Add(replacing);
                _kept.Remove(instance);
                Leave(instance);
            }

        // Cascades the eye has moved past, then as many dirty ones as the budget allows, finest first.
        _forced = _rebuildFrames > 0;
        if (_forced)
        {
            _rebuildFrames--;
            Array.Fill(_dirty, true);
        }
        for (int c = 0; c < Cascades; c++)
            if (_built[c] != OriginAround(c, eye)) _dirty[c] = true;
        var rebuilt = Splat(eye);
        _mostSplatted = Math.Max(_mostSplatted, Splatted);

        // A still mesh is stamped until every built cascade it comes within the band of has been
        // built since it came, but for one that replaced a mesh in place (_replacing).
        foreach (var instance in _pending.ToArray())
        {
            var (bounds, added) = _still[instance];
            var waiting = false;
            for (int c = 0; c < Cascades && !waiting; c++)
                waiting = _built[c] is { } origin && _builtFrame[c] < added && bounds.Overlaps(CascadeBox(c, origin).Grown(Band * CellOf(c)));
            if (waiting)
            {
                if (_movedIn.Contains(instance)) moving.Add(instance);
            }
            else
            {
                _pending.Remove(instance);
                _replacing.Remove(instance);
                _movedIn.Remove(instance);
            }
        }
        FramesBuilt = Built ? FramesBuilt + 1 : 0;

        // The nearest meshes' shapes, a box for each part of each, posed, to as many as a frame
        // stamps, and the bricks they come within the band of in each built cascade.
        // A brick's place in the counts is its place along each axis in four bits, x lowest, and
        // its cascade above them, read back by shifts rather than a division an axis.
        const int side = Resolution / BrickCells, axisBits = 4;
        const int perCascade = side * side * side;
        System.Diagnostics.Debug.Assert(side == 1 << axisBits, "a brick's place along an axis takes axisBits bits");
        if (_stamped.Length != Cascades * perCascade)
        {
            (_stamped, _stampedBefore) = (new int[Cascades * perCascade], new int[Cascades * perCascade]);
            (_bricksAt, _bricksAtBefore) = ([], []);
        }
        (_stamped, _stampedBefore) = (_stampedBefore, _stamped);
        (_bricksAt, _bricksAtBefore) = (_bricksAtBefore, _bricksAt);
        foreach (var at in _bricksAt) _stamped[at] = 0;
        _bricksAt.Clear();
        // A mesh replacing one the field still holds is not stamped, the one it replaces standing in.
        // The rest nearest the eye first, those as near in the order drawn, each sorted by its
        // distance's bits above its place, which order as the distances do since none is negative.
        var count = 0;
        if (_nearest.Length < moving.Count) (_nearest, _byDistance) = (new Instance[moving.Count * 2], new long[moving.Count * 2]);
        foreach (var instance in moving)
            if ((_kept.Count == 0 || !Replaces(instance)) && !_replacing.Contains(instance))
            {
                _byDistance[count] = (long)BitConverter.SingleToInt32Bits(Vector3.DistanceSquared(instance.World.Translation, eye)) << 32 | (uint)count;
                _nearest[count++] = instance;
            }
        Array.Sort(_byDistance, 0, count);
        for (int n = 0; n < count && Shapes.Count < MaxShapes; n++)
        {
            ref readonly var instance = ref _nearest[(int)(_byDistance[n] & 0xFFFFFFFF)];
            var tint = instance.Color * _tint(instance);
            var emission = instance.Emission == Vector3.Zero || _carried(instance) ? Vector3.Zero : instance.Emission;
            foreach (var (own, pose) in PartsWithin(instance, MaxShapes - Shapes.Count - (count - n - 1)))
            {
                var w = pose * instance.World;
                if (!Matrix4x4.Invert(w, out var toOwn)) continue;
                var scale = MathF.Min(new Vector3(w.M11, w.M12, w.M13).Length(), MathF.Min(new Vector3(w.M21, w.M22, w.M23).Length(), new Vector3(w.M31, w.M32, w.M33).Length()));
                Shapes.Add(new Shape(toOwn, (own.Min + own.Max) / 2, (own.Max - own.Min) / 2, scale, tint, instance.LightThrough, emission));
                var (min, max) = own.Transformed(w);
                for (int c = 0; c < Cascades; c++)
                {
                    if (_built[c] is not { } origin) continue;
                    var cell = CellOf(c);
                    var brick = BrickCells * cell;
                    var (low, high) = ((min - origin - new Vector3(Band * cell)) / brick, (max - origin + new Vector3(Band * cell)) / brick);
                    var (fx, fy, fz) = (MathF.Max(0, low.X), MathF.Max(0, low.Y), MathF.Max(0, low.Z));
                    var (lx, ly, lz) = (MathF.Min(side - 1, high.X), MathF.Min(side - 1, high.Y), MathF.Min(side - 1, high.Z));
                    if (fx > lx || fy > ly || fz > lz) continue;
                    var (x0, y0, z0, x1, y1, z1) = ((int)fx, (int)fy, (int)fz, (int)lx, (int)ly, (int)lz);
                    _reaches.Add((Shapes.Count - 1, c, x0, y0, z0, x1, y1, z1));
                    for (int z = z0; z <= z1; z++)
                        for (int y = y0; y <= y1; y++)
                        {
                            var at = ((c * side + z) * side + y) * side + x0;
                            for (int x = x0; x <= x1; x++, at++)
                                if (_stamped[at]++ == 0) _bricksAt.Add(at);
                        }
                }
            }
        }
        // The bricks stamped now, each with a run of its shapes, and last frame's, which go back to
        // the still field where no shape covers them now, but in a cascade built again this frame,
        // which the build wrote whole. The counts become where each brick's run begins.
        foreach (var at in _bricksAtBefore)
            if (_stamped[at] == 0 && _stampedBefore[at] > 0 && !rebuilt[at >> 3 * axisBits]) _bricksAt.Add(at);
        var runs = 0;
        foreach (var at in _bricksAt)
        {
            var shapes = _stamped[at];
            Bricks.Add((at >> 3 * axisBits, at & side - 1, at >> axisBits & side - 1, at >> 2 * axisBits & side - 1));
            BrickRanges.Add((runs, shapes));
            _stamped[at] = runs;
            runs += shapes;
        }
        CollectionsMarshal.SetCount(BrickShapes, runs);
        var filled = CollectionsMarshal.AsSpan(BrickShapes);
        foreach (var (shape, c, x0, y0, z0, x1, y1, z1) in _reaches)
            for (int z = z0; z <= z1; z++)
                for (int y = y0; y <= y1; y++)
                {
                    var at = ((c * side + z) * side + y) * side + x0;
                    for (int x = x0; x <= x1; x++, at++) filled[_stamped[at]++] = shape;
                }
        // Each brick's count put back where the fill left the end of its run, which the frame after
        // reads for the bricks stamped now.
        for (int b = 0; b < _bricksAt.Count; b++) _stamped[_bricksAt[b]] = BrickRanges[b].Count;
        Track();
    }

    // Meshes told apart as their records are and hashed by their vertices and where they stand
    // alone, since a record's hash of every field, the matrix's sixteen among them, is a call a
    // field in a build that is not optimized, and the plan hashes each mesh that moves a few times
    // a frame.
    private sealed class Keys : IEqualityComparer<Instance>
    {
        public static readonly Keys Shared = new();

        public bool Equals(Instance a, Instance b) => a.Equals(b);

        public int GetHashCode(Instance instance) =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(instance.Vertices), instance.World.M41, instance.World.M42, instance.World.M43);
    }

    // A still mesh that left, its cascades made dirty to be built again without it.
    private void Leave(Instance instance)
    {
        MarkAround(_still[instance].Bounds, Follow(instance, _still[instance].Bounds, came: false));
        _still.Remove(instance);
        _pending.Remove(instance);
        _replacing.Remove(instance);
        _movedIn.Remove(instance);
    }

    // Whether a mesh of other vertices drawn through a still mesh's matrix, unsettled, lies where it was.
    private bool ReplacedBy(Instance still, List<Instance> unsettled)
    {
        var bounds = _still[still].Bounds;
        foreach (var instance in unsettled)
            if (instance.World == still.World && !ReferenceEquals(instance.Vertices, still.Vertices)
                && _bounds(instance.Vertices).Transformed(instance.World).Overlaps(bounds))
                return true;
        return false;
    }

    // Whether a mesh replaces one the field keeps for it.
    private bool Replaces(Instance instance)
    {
        foreach (var still in _kept.Keys)
            if (Replacing(instance, still))
                return true;
        return false;
    }

    // Whether a mesh of other vertices drawn through a still mesh's matrix lies where it was.
    private bool Replacing(Instance instance, Instance still) =>
        instance.World == still.World && !ReferenceEquals(instance.Vertices, still.Vertices)
        && _bounds(instance.Vertices).Transformed(instance.World).Overlaps(_still[still].Bounds);

    // A mesh drawn the same for SettleFrames frames running, which the cascades around it are built
    // again with and which, where it moved before it settled, is stamped until they have been.
    private void Settle(Instance instance, bool moved)
    {
        var bounds = _bounds(instance.Vertices).Transformed(instance.World);
        _still[instance] = (bounds, _frame);
        _pending.Add(instance);
        if (moved) _movedIn.Add(instance);
        MarkAround(bounds, Follow(instance, bounds, came: true));
    }

    // Whether this frame draws the meshes the frame before did, in the same order, each by its
    // instance, or moved, as its instance drawn through another matrix that the frame before drew
    // nowhere, a skinned one by its mesh, whose parts are posed afresh each frame and which is
    // never still. A frame before that drew a mesh twice is not read by place.
    private bool SameOrder(IReadOnlyList<(Instance Instance, bool Skinned)> drawn)
    {
        if (!_orderWhole || drawn.Count != _orderCount) return false;
        if (_orderMoved.Length < _orderCount) _orderMoved = new bool[_order.Length];
        var span = Span(drawn);
        for (int i = 0; i < _orderCount; i++)
        {
            ref readonly var now = ref span[i];
            ref readonly var before = ref _order[i];
            if (now.Skinned != _orderSkinned[i]) return false;
            if (now.Skinned)
            {
                if (now.Instance.Mesh != before.Mesh) return false;
                _orderMoved[i] = false;
            }
            else if (_orderMoved[i] = !Same(in now.Instance, in before, true))
            {
                if (!Same(in now.Instance, in before, false) || _seen.ContainsKey(now.Instance)) return false;
            }
        }
        return true;
    }

    // Whether two meshes are drawn the same, through the same world matrix too where `placed` is
    // set, compared in place, where a record's equality copies each and calls a comparer a field.
    private static bool Same(in Instance a, in Instance b, bool placed) =>
        a.Mesh == b.Mesh && ReferenceEquals(a.Vertices, b.Vertices) && (!placed || a.World == b.World) && a.DoubleSided == b.DoubleSided
        && a.Color == b.Color && a.Emission == b.Emission && ReferenceEquals(a.Parts, b.Parts) && a.Roughness == b.Roughness && a.Metallic == b.Metallic
        && a.LightThrough == b.LightThrough;

    // The meshes drawn, read in place where they are a list or an array, as the field's are.
    private static ReadOnlySpan<(Instance Instance, bool Skinned)> Span(IReadOnlyList<(Instance Instance, bool Skinned)> drawn) => drawn switch
    {
        List<(Instance, bool)> list => CollectionsMarshal.AsSpan(list),
        (Instance, bool)[] array => array,
        _ => drawn.ToArray(),
    };

    // How many frames running a mesh that last moved at the frame given is drawn the same before it
    // is still, the longer count where that was within as many frames.
    private int SettleAfter(long movedAt) => _frame - movedAt < MobileSettleFrames ? MobileSettleFrames : SettleFrames;

    // This frame's meshes in the order drawn, with the frames each has been drawn the same, for
    // the frame after to read by place.
    private void KeepOrder(IReadOnlyList<(Instance Instance, bool Skinned)> drawn, Dictionary<Instance, int> seen)
    {
        if (_order.Length < drawn.Count)
        {
            var room = Math.Max(drawn.Count, _order.Length * 2);
            (_order, _orderFrames, _orderSkinned, _orderMovedAt) = (new Instance[room], new int[room], new bool[room], new long[room]);
        }
        _orderCount = drawn.Count;
        _orderWhole = seen.Count == drawn.Count;
        for (int i = 0; i < drawn.Count; i++)
        {
            var (instance, skinned) = drawn[i];
            (_order[i], _orderFrames[i], _orderSkinned[i]) = (instance, seen.GetValueOrDefault(instance), skinned);
            _orderMovedAt[i] = _movedNow.Count > 0 && _movedNow.TryGetValue(instance, out var movedAt) ? movedAt : Never;
        }
    }

    // The parts a mesh is stamped as where there are no more than room for, which leaves a box
    // each for the meshes farther from the eye, and the box around all of it where there are, so a
    // crowd of figures past what a frame stamps is stamped as figures near the eye and boxes beyond.
    private Part[] PartsWithin(Instance instance, int room)
    {
        if ((instance.Parts ?? _parts(instance.Vertices)) is { } parts && parts.Length <= room) return parts;
        if (_whole.TryGetValue(instance.Vertices, out var whole)) return whole;
        if (_whole.Count > 4096) _whole.Clear();
        return _whole[instance.Vertices] = [new Part(_bounds(instance.Vertices), Matrix4x4.Identity)];
    }

    // The box around all of each mesh as its one part, kept for its vertices.
    private readonly Dictionary<ModelVertex[], Part[]> _whole = new(ReferenceEqualityComparer.Instance);
}
