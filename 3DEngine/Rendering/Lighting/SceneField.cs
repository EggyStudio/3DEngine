using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>The scene's distance field as the program set it, a world resource the renderer reads each frame.</summary>
/// <seealso cref="Engine3D.SetSceneField"/>
internal sealed class SceneFieldSettings
{
    /// <summary>How many cascades, 0 for no field.</summary>
    public int Cascades { get; set; }

    /// <summary>The width of a cell of the finest cascade, in world units.</summary>
    public float CellSize { get; set; } = SceneFieldConfig.DefaultCellSize;

    /// <summary>How many cascades are built again a frame at most.</summary>
    public int UpdateBudget { get; set; } = SceneFieldConfig.DefaultUpdateBudget;

    /// <summary>The cascade drawn over the window as the field holds the scene, or -1 for none.</summary>
    public int Shown { get; set; } = -1;

    /// <summary>Whether the field is built this frame.</summary>
    public bool On => Cascades > 0 && CellSize > 0;
}

/// <summary>The field the passes read this frame, and whether it holds anything, a field of nothing standing in where it is off.</summary>
internal sealed record SceneFieldBinding(GpuSceneField Field, bool On);

/// <summary>
/// The scene's distance field, built on the GPU from the meshes drawn into the window that cast
/// shadows, around the window's eye, as <see cref="SceneFieldPlan"/> works out each frame, and
/// bound for the passes that read it as <see cref="SceneFieldBinding"/>.
/// </summary>
/// <remarks>
/// <para>
/// The triangles of every mesh a build reads are kept in one buffer in their mesh's own space, a
/// mesh's added the first time a build needs it, so a build hands the GPU only each instance's
/// matrix and where its mesh's triangles start. A mesh whose vertices change is another entry, and
/// the buffer is made again holding the meshes still in the field when it fills.
/// </para>
/// <para>
/// What a frame's recordings hold, their descriptor pools and the buffers of instances, shapes and
/// bricks, is let go <see cref="GpuTextures.RetireFrames"/> frames later, as is a field replaced.
/// </para>
/// </remarks>
internal sealed class SceneFieldRenderer : IDisposable
{
    private GpuSceneField? _field, _off;
    private SceneFieldPlan? _plan;
    private (int Cascades, float CellSize, int Budget) _made;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;

    private IBuffer? _corners;
    private int _cornerCapacity, _cornerCount;
    private readonly Dictionary<ModelVertex[], (int First, int Count)> _pooled = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ModelVertex[], uint[]> _indices = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ModelVertex[], SceneFieldPlan.Box> _bounds = new(ReferenceEqualityComparer.Instance);
    private readonly List<(SceneFieldPlan.Instance, bool)> _drawn = [];

    // Each skinned mesh's joints as it was last posed, which a frame that does not pose it again
    // keeps, each joint's box around the vertices at rest it holds most, and the parts a mesh that
    // does not bend is cut into, each kept for its vertices.
    private readonly Dictionary<int, Matrix4x4[]> _joints = [];
    private readonly Dictionary<ModelVertex[], SceneFieldPlan.Box?[]> _limbs = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ModelVertex[], SceneFieldPlan.Part[]?> _parts = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The meshes drawn into the window this frame that cast shadows, or the render targets' where
    /// the window draws none, each with whether it is skinned, as the field gathered them.
    /// </summary>
    internal IReadOnlyList<(SceneFieldPlan.Instance Instance, bool Skinned)> Drawn => _drawn;

    /// <summary>The corners of a mesh drawn this frame, three a triangle in its own space, or null for one not drawn.</summary>
    internal Vector3[]? CornersOf(ModelVertex[] vertices)
    {
        if (!_indices.TryGetValue(vertices, out var indices)) return null;
        var corners = new Vector3[indices.Length / 3 * 3];
        for (int i = 0; i < corners.Length; i++) corners[i] = vertices[indices[i]].Position;
        return corners;
    }
    private ModelRenderer.Instance[] _groupInstances = [];

    /// <summary>The plan of the field being built, or null where none is.</summary>
    internal SceneFieldPlan? Plan => _plan;

    /// <summary>The field being built, or null where none is.</summary>
    internal GpuSceneField? Field => _field;

    /// <summary>
    /// Builds and stamps the frame's cascades and writes where they lie, binding the field for
    /// the frame's passes, or lets it go and binds a field of nothing when it is off.
    /// </summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld)
    {
        Retire();
        if (renderContext.Device is not GraphicsDevice { CanBuildSceneField: true } device)
            return;
        var settings = renderWorld.TryGet<SceneFieldSettings>();
        if (settings is not { On: true })
        {
            Release();
            renderWorld.Set(new SceneFieldBinding(_off ??= device.CreateSceneField(1, 1), false));
            return;
        }

        var wanted = (Math.Clamp(settings.Cascades, 1, 8), settings.CellSize, Math.Max(1, settings.UpdateBudget));
        if (_field is null || _made != wanted)
        {
            Release();
            _made = wanted;
            _plan = new SceneFieldPlan(wanted.Item1, wanted.CellSize, wanted.Item3, BoundsOf, PartsOf);
            _field = device.CreateSceneField(_plan.Cascades, SceneFieldPlan.Resolution);
        }
        renderWorld.Set(new SceneFieldBinding(_field, true));
        // Around the window's camera, or where the window draws no mesh, as a game that draws its
        // scene into a render texture and shows the texture, the first target's that does, whose
        // meshes the field then holds.
        var draws = renderWorld.TryGet<ModelDrawList>();
        var windowDraws = draws?.WindowViewProjection is not null;
        var view = windowDraws || draws?.Targets() is not [var first, ..] ? renderWorld.TryGet<WindowView>()
            : draws.ViewProjectionOf(first) is { } camera && LightingUboPrepare.EyeOf(camera) is { } eye ? new WindowView(camera, eye) : null;
        if (view is null) return;

        Gather(renderWorld, windowDraws);
        var plan = _plan!;
        plan.Update(view.Eye, _drawn, Ahead(view));

        var commands = renderContext.CommandBuffer;
        device.RecordSceneFieldInfo(commands, _field, Info(plan));
        if (plan.Builds.Count == 0 && plan.Bricks.Count == 0) return;

        device.RecordSceneFieldOpen(commands, _field);
        if (plan.Builds.Count > 0)
        {
            var corners = Pool(device, plan.Builds.SelectMany(build => build.Instances));
            foreach (var build in plan.Builds)
            {
                var (instances, count, triangles) = Instances(device, build.Instances);
                Hold(instances);
                Hold(device.RecordSceneFieldBuild(commands, _field, corners, instances, count, triangles,
                    build.Cascade, build.Origin, build.Cell, SceneFieldPlan.Band));
            }
        }
        if (plan.Bricks.Count > 0)
        {
            var (shapes, bricks) = (Shapes(device, plan.Shapes), Bricks(device, plan));
            Hold(shapes);
            Hold(bricks);
            Hold(device.RecordSceneFieldStamp(commands, _field, bricks, plan.Bricks.Count, shapes, plan.Shapes.Count));
        }
        device.RecordSceneFieldClose(commands, _field);
    }

    // The meshes drawn into the window this frame that cast shadows, or where the window draws
    // none those drawn into the render targets, each with whether it is skinned, whose posed shape
    // no vertices on the CPU hold.
    private void Gather(RenderWorld renderWorld, bool windowAlone = true)
    {
        _drawn.Clear();
        _indices.Clear();
        // The boxes of vertices no longer drawn go, as an animated mesh's of each frame before.
        if (_bounds.Count > 4096) _bounds.Clear();
        if (renderWorld.TryGet<ModelDrawList>() is not { } draws || renderWorld.TryGet<MeshStore>() is not { } store) return;
        var textures = renderWorld.TryGet<TextureStore>();
        // The joints of the skinned meshes posed this frame, kept for the frames after.
        foreach (var (id, joints, _) in renderWorld.TryGet<GpuMeshes>()?.Poses ?? []) _joints[id] = joints;
        if (_joints.Count > 4096) _joints.Clear();
        // A surface's color, its material's times its texture's average where it has one.
        Vector3 Textured(Vector3 color, int texture) => texture != 0 && textures?.AverageColor(texture) is { } average ? color * average : color;
        foreach (ref readonly var draw in draws.Span)
        {
            if (windowAlone && draw.Target != 0 || !draw.CastsShadow || draw.Points || !store.TryGetData(draw.Mesh, out var vertices, out var indices)) continue;
            _indices[vertices] = indices;
            var one = ModelRenderer.Instance.Of(in draw);
            var skinned = store.IsSkinned(draw.Mesh);
            _drawn.Add((new SceneFieldPlan.Instance(draw.Mesh, vertices, draw.World, draw.DoubleSided,
                Textured(new Vector3(one.Color.X, one.Color.Y, one.Color.Z), draw.Texture), draw.Emission,
                skinned ? Limbs(store, draw.Mesh, vertices) : null), skinned));
        }
        foreach (var group in draws.Groups)
        {
            var template = group.Template;
            if (group.Count == 0 || windowAlone && template.Target != 0 || !template.CastsShadow || template.Points
                || !store.TryGetData(template.Mesh, out var vertices, out var indices)) continue;
            _indices[vertices] = indices;
            var skinned = store.IsSkinned(template.Mesh);
            var limbs = skinned ? Limbs(store, template.Mesh, vertices) : null;
            if (_groupInstances.Length < group.Count) _groupInstances = new ModelRenderer.Instance[Math.Max(group.Count, _groupInstances.Length * 2)];
            group.CopyTo(_groupInstances);
            for (int i = 0; i < group.Count; i++)
            {
                ref readonly var instance = ref _groupInstances[i];
                var (x, y, z) = (instance.WorldX, instance.WorldY, instance.WorldZ);
                var world = new Matrix4x4(x.X, y.X, z.X, 0, x.Y, y.Y, z.Y, 0, x.Z, y.Z, z.Z, 0, x.W, y.W, z.W, 1);
                _drawn.Add((new SceneFieldPlan.Instance(template.Mesh, vertices, world, template.DoubleSided,
                    Textured(new Vector3(instance.Color.X, instance.Color.Y, instance.Color.Z), template.Texture),
                    new Vector3(instance.Emission.X, instance.Emission.Y, instance.Emission.Z), limbs), skinned));
            }
        }
    }

    // The way the window's camera looks, from its far plane's middle, or zero where it cannot be told.
    private static Vector3 Ahead(WindowView view)
    {
        if (!Matrix4x4.Invert(view.ViewProjection, out var inverse)) return Vector3.Zero;
        var far = Vector4.Transform(new Vector4(0, 0, 1, 1), inverse);
        if (MathF.Abs(far.W) < 1e-12f) return Vector3.Zero;
        var way = new Vector3(far.X, far.Y, far.Z) / far.W - view.Eye;
        return way.LengthSquared() > 1e-12f ? Vector3.Normalize(way) : Vector3.Zero;
    }

    private SceneFieldPlan.Box BoundsOf(ModelVertex[] vertices)
    {
        if (!_bounds.TryGetValue(vertices, out var box)) _bounds[vertices] = box = SceneFieldPlan.Box.Of(vertices);
        return box;
    }

    // A skinned mesh's limbs as it was last posed, each joint's box at rest moved by the joint's
    // matrix, or null where its skin is not known, which stamps the box around all of it.
    private SceneFieldPlan.Part[]? Limbs(MeshStore store, int mesh, ModelVertex[] vertices)
    {
        if (!_limbs.TryGetValue(vertices, out var boxes))
        {
            if (store.SkinOf(mesh) is not { } skin || skin.Joints.Length < vertices.Length * 4) return null;
            var (min, max) = (new Vector3[skin.JointCount], new Vector3[skin.JointCount]);
            Array.Fill(min, new Vector3(float.MaxValue));
            Array.Fill(max, new Vector3(float.MinValue));
            for (int v = 0; v < vertices.Length; v++)
            {
                // The joint that holds the vertex most, whose box it widens.
                int most = 0;
                for (int k = 1; k < 4; k++)
                    if (skin.Weights[v * 4 + k] > skin.Weights[v * 4 + most]) most = k;
                var joint = skin.Joints[v * 4 + most];
                if (joint >= skin.JointCount) continue;
                (min[joint], max[joint]) = (Vector3.Min(min[joint], vertices[v].Position), Vector3.Max(max[joint], vertices[v].Position));
            }
            if (_limbs.Count > 4096) _limbs.Clear();
            _limbs[vertices] = boxes = [.. Enumerable.Range(0, skin.JointCount)
                .Select(j => min[j].X <= max[j].X ? new SceneFieldPlan.Box(min[j], max[j]) : (SceneFieldPlan.Box?)null)];
        }
        var joints = _joints.GetValueOrDefault(mesh);
        var parts = new List<SceneFieldPlan.Part>(boxes.Length);
        for (int j = 0; j < boxes.Length; j++)
            if (boxes[j] is { } rest) parts.Add(new SceneFieldPlan.Part(rest, joints is not null && j < joints.Length ? joints[j] : Matrix4x4.Identity));
        return parts.Count > 0 ? [.. parts] : null;
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
                var order = triangles.OrderBy(t => axis == 0 ? middles[t].X : axis == 1 ? middles[t].Y : middles[t].Z).ToArray();
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

    // Where each cascade lies as it was last built, as scenefield.slang's SceneFieldInfo.
    private static byte[] Info(SceneFieldPlan plan)
    {
        var values = new float[GpuSceneField.InfoBytes / 4];
        (values[0], values[1], values[2]) = (plan.Cascades, SceneFieldPlan.Resolution, SceneFieldPlan.Band);
        for (int c = 0; c < plan.Cascades; c++)
            if (plan.BuiltOrigin(c) is { } origin)
                (values[4 + c * 4], values[5 + c * 4], values[6 + c * 4], values[7 + c * 4]) = (origin.X, origin.Y, origin.Z, plan.CellOf(c));
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    // The buffer of triangles holding every mesh the frame's builds read, made again larger with
    // those meshes where they do not fit.
    private IBuffer Pool(GraphicsDevice device, IEnumerable<SceneFieldPlan.Instance> needed)
    {
        var meshes = needed.Select(instance => instance.Vertices).Distinct(ReferenceEqualityComparer.Instance).Cast<ModelVertex[]>().ToList();
        var adding = meshes.Where(vertices => !_pooled.ContainsKey(vertices)).ToList();
        var more = adding.Sum(vertices => _indices.GetValueOrDefault(vertices)?.Length / 3 ?? 0);
        if (_corners is null || _cornerCount + more > _cornerCapacity)
        {
            // Made again with the meshes this frame needs, which drops the ones no longer built from.
            if (_corners is not null) Hold(_corners);
            _pooled.Clear();
            _cornerCount = 0;
            _cornerCapacity = Math.Max(1024, (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, meshes.Sum(v => _indices.GetValueOrDefault(v)?.Length / 3 ?? 0) * 2)));
            _corners = device.CreateBuffer(new BufferDesc((ulong)_cornerCapacity * 48, BufferUsage.Storage, CpuAccessMode.Write));
            adding = meshes;
        }
        var mapped = MemoryMarshal.Cast<byte, Vector4>(device.Map(_corners));
        foreach (var vertices in adding)
        {
            if (!_indices.TryGetValue(vertices, out var indices)) continue;
            var count = indices.Length / 3;
            // Each corner's w 1 where the edge from it to the triangle's next corner is open, no
            // other triangle's, which field_splat.slang reads to keep a cell beyond the edge in front.
            var open = OpenEdges(vertices, indices);
            for (int i = 0; i < count * 3; i++)
                mapped[_cornerCount * 3 + i] = new Vector4(vertices[indices[i]].Position, open[i] ? 1 : 0);
            _pooled[vertices] = (_cornerCount, count);
            _cornerCount += count;
        }
        device.Unmap(_corners);
        return _corners;
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

    // A build's instances as field_splat.slang's FieldInstance reads them, 96 bytes each, with how
    // many there are and how many triangles they have in all.
    private (IBuffer Buffer, int Count, int Triangles) Instances(GraphicsDevice device, SceneFieldPlan.Instance[] instances)
    {
        var buffer = device.CreateBuffer(new BufferDesc((ulong)Math.Max(1, instances.Length) * 96, BufferUsage.Storage, CpuAccessMode.Write));
        var mapped = MemoryMarshal.Cast<byte, uint>(device.Map(buffer));
        int written = 0, triangles = 0;
        foreach (var instance in instances)
        {
            if (!_pooled.TryGetValue(instance.Vertices, out var pooled) || pooled.Count == 0) continue;
            var record = mapped.Slice(written * 24, 24);
            var (w, c, e) = (instance.World, instance.Color, instance.Emission);
            ReadOnlySpan<float> rows = [w.M11, w.M21, w.M31, w.M41, w.M12, w.M22, w.M32, w.M42, w.M13, w.M23, w.M33, w.M43];
            MemoryMarshal.Cast<float, uint>(rows).CopyTo(record);
            (record[12], record[13], record[14], record[15]) = ((uint)pooled.First, (uint)pooled.Count, instance.DoubleSided ? 1u : 0u, (uint)triangles);
            ReadOnlySpan<float> colors = [c.X, c.Y, c.Z, 1, e.X, e.Y, e.Z, 0];
            MemoryMarshal.Cast<float, uint>(colors).CopyTo(record[16..]);
            triangles += pooled.Count;
            written++;
        }
        device.Unmap(buffer);
        return (buffer, written, triangles);
    }

    // The frame's shapes as field_stamp.slang's FieldShape reads them, one at least.
    private static IBuffer Shapes(GraphicsDevice device, List<SceneFieldPlan.Shape> shapes)
    {
        var buffer = device.CreateBuffer(new BufferDesc((ulong)Math.Max(1, shapes.Count) * 96, BufferUsage.Storage, CpuAccessMode.Write));
        var mapped = MemoryMarshal.Cast<byte, float>(device.Map(buffer));
        for (int s = 0; s < shapes.Count; s++)
        {
            var (m, center, extents, scale, color) = shapes[s];
            ReadOnlySpan<float> values = [m.M11, m.M21, m.M31, m.M41, m.M12, m.M22, m.M32, m.M42, m.M13, m.M23, m.M33, m.M43,
                center.X, center.Y, center.Z, scale, extents.X, extents.Y, extents.Z, 0, color.X, color.Y, color.Z, 0];
            values.CopyTo(mapped[(s * 24)..]);
        }
        device.Unmap(buffer);
        return buffer;
    }

    // Each brick as field_stamp.slang reads it, its cascade, its place packed a byte an axis, and
    // where its run of shapes begins and how long it is, then the runs, a shape's place each.
    private static IBuffer Bricks(GraphicsDevice device, SceneFieldPlan plan)
    {
        var (bricks, ranges, shapes) = (plan.Bricks, plan.BrickRanges, plan.BrickShapes);
        var buffer = device.CreateBuffer(new BufferDesc((ulong)(Math.Max(1, bricks.Count) * 4 + shapes.Count) * 4, BufferUsage.Storage, CpuAccessMode.Write));
        var mapped = MemoryMarshal.Cast<byte, uint>(device.Map(buffer));
        for (int b = 0; b < bricks.Count; b++)
            (mapped[b * 4], mapped[b * 4 + 1], mapped[b * 4 + 2], mapped[b * 4 + 3]) =
                ((uint)bricks[b].Cascade, (uint)(bricks[b].X | bricks[b].Y << 8 | bricks[b].Z << 16), (uint)ranges[b].First, (uint)ranges[b].Count);
        for (int s = 0; s < shapes.Count; s++) mapped[bricks.Count * 4 + s] = (uint)shapes[s];
        device.Unmap(buffer);
        return buffer;
    }

    private void Hold(IDisposable disposable) => _retired.Add((_frame, disposable));

    // Lets the field go, once no frame in flight reads it, on a frame it is off or made again.
    private void Release()
    {
        if (_field is not null) Hold(_field);
        if (_corners is not null) Hold(_corners);
        (_field, _corners, _plan) = (null, null, null);
        (_cornerCapacity, _cornerCount) = (0, 0);
        _pooled.Clear();
        _indices.Clear();
        _bounds.Clear();
        _joints.Clear();
        _limbs.Clear();
        _parts.Clear();
    }

    // Destroys what was let go once the frames in flight that might read it have finished.
    private void Retire()
    {
        _frame++;
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Disposable.Dispose();
            _retired.RemoveAt(i);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        _field?.Dispose();
        _off?.Dispose();
        _corners?.Dispose();
    }
}

/// <summary>Render graph node that builds the scene's distance field ahead of every pass that reads it.</summary>
internal sealed class SceneFieldNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<SceneFieldRenderer>()?.Draw(renderContext, renderWorld);
}
