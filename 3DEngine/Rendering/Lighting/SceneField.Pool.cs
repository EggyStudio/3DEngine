using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

// The pool of the meshes' triangles the builds read, and the parts a mesh is stamped as.
internal sealed partial class SceneFieldRenderer
{
    // The buffer of triangles holding every mesh the frame's builds read, made again larger with
    // those meshes where they do not fit, large enough for the triangles of the build in progress
    // (building) its later parts are added from, since one made for a frame's part alone would
    // overflow with the next part's meshes and be made again at each part, every mesh's corners
    // written again each time.
    private IBuffer Pool(GraphicsDevice device, IEnumerable<SceneFieldPlan.Instance> needed, int building)
    {
        // A mesh by its vertices, with the id of one draw of it, whose vertices' colors it is pooled with.
        var meshes = needed.DistinctBy(instance => instance.Vertices, ReferenceEqualityComparer.Instance)
            .Select(instance => (instance.Vertices, instance.Mesh)).ToList();
        var adding = meshes.Where(mesh => !_pooled.ContainsKey(mesh.Vertices)).ToList();
        var more = adding.Sum(mesh => TrianglesOf(mesh.Vertices));
        if (_corners is null || _cornerCount + more > _cornerCapacity)
        {
            // Made again with the meshes this frame needs, which drops the ones no longer built from.
            if (_corners is not null) Hold(_corners);
            (_corners, _cornerCount) = (null, 0);
            _pooled.Clear();
            var frame = Math.Max(1024, meshes.Sum(m => TrianglesOf(m.Vertices)));
            var wanted = Math.Max(frame, building);
            var free = device.FreeDeviceMemory();
            var asked = PoolCapacity(wanted, frame, null);
            var capacity = Math.Max(frame, Math.Min(PoolCapacity(wanted, frame, free), _poolCeiling));
            // Halved until it fits where the driver's budget says more is free than can be had, as
            // beside another program holding much of the GPU's memory, and kept under that until the
            // field is made again, so a remake does not try the size that failed each frame.
            while (true)
            {
                try
                {
                    _corners = MakePool(device, capacity);
                    break;
                }
                catch (DeviceMemoryException e) when (capacity > frame)
                {
                    Logger.Warn($"The scene's distance field's pool could not take {capacity * 48.0 / 1048576:0} MB. {e.Message}");
                    capacity = _poolCeiling = Math.Max(frame, capacity / 2);
                }
            }
            if (capacity < asked && capacity != _poolTaken)
                Logger.Warn($"The scene's distance field's pool asked {asked * 48.0 / 1048576:0} MB with {free / 1048576.0:0} MB of the GPU's memory free "
                    + $"and took {capacity * 48.0 / 1048576:0} MB, its builds taking their meshes in again more often.");
            (_cornerCapacity, _poolTaken) = (capacity, capacity);
            adding = meshes;
        }
        // The corners added, written here and copied into the pool in the device's own memory, which
        // every build reads, from the first added on; a counted mesh's part of them is written over
        // on the GPU after the copy.
        var start = _cornerCount;
        var mapped = new uint[adding.Sum(mesh => TrianglesOf(mesh.Vertices)) * 12];
        foreach (var (vertices, mesh) in adding)
        {
            // A counted mesh's corners are written on the GPU from its positions before the builds.
            if (_countedMeshes.TryGetValue(vertices, out var counted))
            {
                // Its colors where it has them, or its stream where that holds colors.
                var tinted = counted.Mesh.Colors ?? (counted.Mesh.Stream is { Format: MeshStreamFormat.UNorm8x4 } stream ? stream.Buffer : null);
                _countedPending.Add((counted.Mesh.Positions, tinted, _cornerCount * 3, counted.Vertices, (int)counted.Mesh.First));
                _pooled[vertices] = (_cornerCount, counted.Vertices / 3);
                _cornerCount += counted.Vertices / 3;
                continue;
            }
            if (!_indices.TryGetValue(vertices, out var indices)) continue;
            var count = indices.Length / 3;
            // Each corner's place, and in w its vertex's color, three sRGB bytes, red lowest, white
            // for a mesh with none, and above them a bit set where the edge from it to the
            // triangle's next corner is open, no other triangle's, which field_splat.slang reads to
            // keep a cell beyond the edge in front.
            var open = OpenEdgesOf(vertices, indices);
            var colors = _store?.StreamsOf(mesh)?.Colors is { } given && given.Length == vertices.Length ? given : null;
            for (int i = 0; i < count * 3; i++)
            {
                var at = ((_cornerCount - start) * 3 + i) * 4;
                var position = vertices[indices[i]].Position;
                (mapped[at], mapped[at + 1], mapped[at + 2]) =
                    (BitConverter.SingleToUInt32Bits(position.X), BitConverter.SingleToUInt32Bits(position.Y), BitConverter.SingleToUInt32Bits(position.Z));
                var color = colors?[indices[i]] ?? Color.White;
                mapped[at + 3] = color.R | (uint)color.G << 8 | (uint)color.B << 16 | ((open[i >> 5] >> (i & 31) & 1u) << 24);
            }
            _pooled[vertices] = (_cornerCount, count);
            _cornerCount += count;
        }
        device.FillBuffer(_corners, (ulong)start * 48, MemoryMarshal.AsBytes(mapped.AsSpan(0, (_cornerCount - start) * 12)));
        return _corners;
    }

    // The most triangles a pool is made for since the field was made, lowered where one did not fit,
    // and the last taken, so the pool says what it took once.
    private int _poolCeiling = int.MaxValue, _poolTaken;

    private static IBuffer MakePool(GraphicsDevice device, int triangles) =>
        device.CreateBuffer(new BufferDesc((ulong)triangles * 48, BufferUsage.Storage | BufferUsage.TransferDst,
            device.SharesMemory ? CpuAccessMode.Write : CpuAccessMode.None));

    /// <summary>
    /// How many triangles the pool is made for, twice <paramref name="wanted"/>, those of the build
    /// in progress, rounded up to a power of two, so its later parts fit, or where the device has
    /// less than twice that free, as many as half what it has free holds and those of this frame's
    /// meshes, <paramref name="needed"/>, at least.
    /// </summary>
    internal static int PoolCapacity(int wanted, int needed, ulong? free)
    {
        var whole = Math.Max(1024, (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, wanted * 2)));
        if (free is not { } bytes || (ulong)whole * 48 * 2 <= bytes) return whole;
        return Math.Max(Math.Max(1024, needed), (int)Math.Min(wanted, (long)(bytes / 2 / 48)));
    }

    // Each mesh's open edges as OpenEdges finds them, a bit a corner, kept for its vertices rather
    // than found again each time the pool takes the mesh in.
    private readonly Dictionary<ModelVertex[], uint[]> _openEdges = new(ReferenceEqualityComparer.Instance);

    private uint[] OpenEdgesOf(ModelVertex[] vertices, uint[] indices)
    {
        if (_openEdges.TryGetValue(vertices, out var kept)) return kept;
        if (_openEdges.Count > 8192) _openEdges.Clear();
        var open = OpenEdges(vertices, indices);
        var bits = new uint[(open.Length + 31) / 32];
        for (int i = 0; i < open.Length; i++)
            if (open[i]) bits[i >> 5] |= 1u << (i & 31);
        return _openEdges[vertices] = bits;
    }

    /// <summary>
    /// Whether each edge of a mesh's triangles is open, its edge from each corner to the next, which
    /// no other triangle shares by the places of its ends, so a mesh whose faces keep vertices of
    /// their own, as a cube's do, is closed where its faces meet and a ground plane open at its rim.
    /// </summary>
    internal static bool[] OpenEdges(ModelVertex[] vertices, uint[] indices)
    {
        var count = indices.Length / 3 * 3;
        var shared = new Dictionary<(Vector3, Vector3), int>(count);
        (Vector3, Vector3) Edge(int i)
        {
            var (a, b) = (vertices[indices[i]].Position, vertices[indices[i / 3 * 3 + (i % 3 + 1) % 3]].Position);
            return a.X < b.X || a.X == b.X && (a.Y < b.Y || a.Y == b.Y && a.Z <= b.Z) ? (a, b) : (b, a);
        }
        for (int i = 0; i < count; i++)
        {
            var edge = Edge(i);
            shared[edge] = shared.GetValueOrDefault(edge) + 1;
        }
        var open = new bool[count];
        for (int i = 0; i < count; i++) open[i] = shared[Edge(i)] == 1;
        return open;
    }

    // The parts a mesh that does not bend is stamped as, kept for its vertices.
    private SceneFieldPlan.Part[]? PartsOf(ModelVertex[] vertices)
    {
        if (_parts.TryGetValue(vertices, out var made)) return made;
        if (!_indices.TryGetValue(vertices, out var indices)) return null;
        if (_parts.Count > 4096) _parts.Clear();
        return _parts[vertices] = Cut(vertices, indices);
    }

    /// <summary>
    /// A mesh that does not bend cut by its triangles into up to eight parts, each the box around
    /// its triangles, so a moving table or car is stamped as its shape rather than the box around
    /// all of it, or null for a mesh of fewer than 16 triangles, as a box or a slab, which is its
    /// one box.
    /// </summary>
    /// <remarks>
    /// Each cut is the one along an axis, between the triangles sorted by their middles, that leaves
    /// the two boxes around them the least volume, as a bounding volume hierarchy is built by the
    /// volume it encloses, and is made only where it takes away a third of the box's volume or more,
    /// so a box cut finer than its shape stays one box rather than a shell of thin slabs.
    /// </remarks>
    internal static SceneFieldPlan.Part[]? Cut(ModelVertex[] vertices, uint[] indices)
    {
        if (indices.Length < 3 * 16) return null;
        var count = indices.Length / 3;
        var (lows, highs, middles) = (new Vector3[count], new Vector3[count], new Vector3[count]);
        for (int t = 0; t < count; t++)
        {
            var (a, b, c) = (vertices[indices[t * 3]].Position, vertices[indices[t * 3 + 1]].Position, vertices[indices[t * 3 + 2]].Position);
            (lows[t], highs[t], middles[t]) = (Vector3.Min(a, Vector3.Min(b, c)), Vector3.Max(a, Vector3.Max(b, c)), (a + b + c) / 3);
        }
        static float Volume(Vector3 low, Vector3 high) => MathF.Max(0, high.X - low.X) * MathF.Max(0, high.Y - low.Y) * MathF.Max(0, high.Z - low.Z);

        var parts = new List<SceneFieldPlan.Part>();
        void Halve(int[] triangles, int depth)
        {
            var (low, high) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
            foreach (var t in triangles) (low, high) = (Vector3.Min(low, lows[t]), Vector3.Max(high, highs[t]));
            var (best, cut, sorted) = (0.67f * Volume(low, high), -1, Array.Empty<int>());
            for (int axis = 0; depth > 0 && triangles.Length >= 8 && axis < 3; axis++)
            {
                var order = SortedAlong(triangles, middles, axis);
                // The boxes around the triangles before each place and from it on.
                var (beforeLow, beforeHigh) = (new Vector3[order.Length], new Vector3[order.Length]);
                var (runLow, runHigh) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
                for (int i = 0; i < order.Length; i++)
                    (beforeLow[i], beforeHigh[i]) = (runLow, runHigh) = (Vector3.Min(runLow, lows[order[i]]), Vector3.Max(runHigh, highs[order[i]]));
                (runLow, runHigh) = (new Vector3(float.MaxValue), new Vector3(float.MinValue));
                for (int i = order.Length - 1; i >= 4; i--)
                {
                    (runLow, runHigh) = (Vector3.Min(runLow, lows[order[i]]), Vector3.Max(runHigh, highs[order[i]]));
                    if (order.Length - i < 4) continue;
                    var cost = Volume(beforeLow[i - 1], beforeHigh[i - 1]) + Volume(runLow, runHigh);
                    if (cost < best) (best, cut, sorted) = (cost, i, order);
                }
            }
            if (cut < 0)
            {
                parts.Add(new SceneFieldPlan.Part(new SceneFieldPlan.Box(low, high), Matrix4x4.Identity));
                return;
            }
            Halve(sorted[..cut], depth - 1);
            Halve(sorted[cut..], depth - 1);
        }
        Halve([.. Enumerable.Range(0, count)], 3);
        return parts.Count > 1 ? [.. parts] : null;
    }

    // The triangles sorted by their middles along an axis, those as far along in the order given,
    // as a stable sort leaves them, each keyed by its middle's bits made to order as the floats do
    // above its place, rather than by LINQ's OrderBy, which is too slow to cut the many meshes that
    // come back into the field in one frame, as a streamed world's sections do at a crossing.
    private static int[] SortedAlong(int[] triangles, Vector3[] middles, int axis)
    {
        var keyed = new ulong[triangles.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            var bits = BitConverter.SingleToUInt32Bits(middles[triangles[i]][axis] + 0f);
            keyed[i] = (ulong)((bits & 0x80000000u) != 0 ? ~bits : bits | 0x80000000u) << 32 | (uint)i;
        }
        Array.Sort(keyed);
        var order = new int[triangles.Length];
        for (int i = 0; i < order.Length; i++) order[i] = triangles[(int)(keyed[i] & 0xFFFFFFFFu)];
        return order;
    }
}
