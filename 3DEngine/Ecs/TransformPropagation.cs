namespace Engine;

/// <summary>
/// Composes the transforms of entities with parents into their <see cref="GlobalTransform"/>, in
/// <see cref="Stage.Render"/>, after physics has written its bodies' transforms and before the
/// renderer reads them.
/// </summary>
internal static class TransformPropagation
{
    /// <summary>A transform as a matrix: scale, then rotation, then translation.</summary>
    /// <remarks>
    /// Built as the product comes out rather than by multiplying three matrices, since every mesh
    /// entity without a parent has its matrix made this way each frame: the rotation's rows, each
    /// scaled by its axis, and the position as the last row.
    /// </remarks>
    public static System.Numerics.Matrix4x4 ToMatrix(in Transform t)
    {
        var m = System.Numerics.Matrix4x4.CreateFromQuaternion(t.Rotation);
        var s = t.Scale;
        m.M11 *= s.X; m.M12 *= s.X; m.M13 *= s.X;
        m.M21 *= s.Y; m.M22 *= s.Y; m.M23 *= s.Y;
        m.M31 *= s.Z; m.M32 *= s.Z; m.M33 *= s.Z;
        m.M41 = t.Position.X; m.M42 = t.Position.Y; m.M43 = t.Position.Z;
        return m;
    }

    /// <summary>The world matrix of <paramref name="entity"/>: its <see cref="GlobalTransform"/>, or its <see cref="Transform"/>, or identity.</summary>
    public static System.Numerics.Matrix4x4 WorldMatrix(EcsWorld ecs, int entity) =>
        ecs.TryGet<GlobalTransform>(entity, out var global) ? global.Matrix
        : ecs.TryGet<Transform>(entity, out var local) ? ToMatrix(local)
        : System.Numerics.Matrix4x4.Identity;

    /// <summary>
    /// The world matrix of <paramref name="entity"/> composed from the <see cref="Transform"/>s of
    /// it and its parents as they are, rather than read from the <see cref="GlobalTransform"/>
    /// written in the last <see cref="Stage.Render"/>, for code that runs after a parent has moved.
    /// </summary>
    public static System.Numerics.Matrix4x4 ComposedWorldMatrix(EcsWorld ecs, int entity)
    {
        var world = System.Numerics.Matrix4x4.Identity;
        for (int depth = 0; entity != 0 && depth < 256; depth++)
        {
            if (ecs.TryGet<Transform>(entity, out var t)) world *= ToMatrix(t);
            entity = ecs.ParentOf(entity);
        }
        return world;
    }

    /// <summary>
    /// Writes the <see cref="GlobalTransform"/> of every parented entity whose chain changed, which
    /// is when its own or an ancestor's <see cref="Transform"/> or <see cref="Parent"/> changed since
    /// propagation last ran, or it has none yet. A chain nothing touched keeps the one it has, so a
    /// static hierarchy costs two comparisons.
    /// </summary>
    /// <remarks>
    /// A write through <see cref="EcsWorld.GetRef{T}(int)"/>, <see cref="EcsWorld.Update{T}(int, T)"/> or a
    /// by-reference query marks a component changed, at the tick of the system making it
    /// (<see cref="ChangeTicks"/>), so a write made after propagation in one frame, in a later
    /// stage or by the program, reaches it the next. A write to a component reached some other way,
    /// as through a span of the store, is not seen until something marks it.
    /// </remarks>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<Parent>() == 0) return;

        // Nothing moved, nothing was parented, and every parented entity has a global transform.
        if (!ecs.AnyChanged<Transform>() && !ecs.AnyChanged<Parent>()
            && ecs.Count<GlobalTransform>() >= ecs.Count<Parent>())
            return;

        var dirty = new Dictionary<int, bool>();
        var done = new Dictionary<int, System.Numerics.Matrix4x4>();
        foreach (var (entity, _) in ecs.Query<Parent>())
            if (Dirty(ecs, entity, dirty, 0) || !ecs.Has<GlobalTransform>(entity))
                Compose(ecs, entity, dirty, done, depth: 0);
    }

    // Whether the entity or anything above it changed. The depth guard covers a cycle made by
    // writing Parent components directly.
    private static bool Dirty(EcsWorld ecs, int entity, Dictionary<int, bool> known, int depth)
    {
        if (known.TryGetValue(entity, out var answer)) return answer;
        answer = ecs.Changed<Transform>(entity) || ecs.Changed<Parent>(entity);
        var parent = ecs.ParentOf(entity);
        if (!answer && parent != 0 && depth < 256) answer = Dirty(ecs, parent, known, depth + 1);
        return known[entity] = answer;
    }

    // An entity's world matrix, kept from its global transform when its chain is clean and composed
    // from its parent's otherwise, writing the global transform of a parented one it composed.
    private static System.Numerics.Matrix4x4 Compose(EcsWorld ecs, int entity, Dictionary<int, bool> dirty,
        Dictionary<int, System.Numerics.Matrix4x4> done, int depth)
    {
        if (done.TryGetValue(entity, out var known)) return known;

        var parent = ecs.ParentOf(entity);
        if (parent != 0 && !Dirty(ecs, entity, dirty, 0) && ecs.TryGet<GlobalTransform>(entity, out var kept))
            return done[entity] = kept.Matrix;

        var local = ecs.TryGet<Transform>(entity, out var t) ? ToMatrix(t) : System.Numerics.Matrix4x4.Identity;
        var world = parent != 0 && depth < 256 ? local * Compose(ecs, parent, dirty, done, depth + 1) : local;

        done[entity] = world;
        if (parent != 0)
        {
            var global = new GlobalTransform { Matrix = world };
            if (ecs.Has<GlobalTransform>(entity)) ecs.Update(entity, global);
            else ecs.Add(entity, global);
        }
        return world;
    }
}
