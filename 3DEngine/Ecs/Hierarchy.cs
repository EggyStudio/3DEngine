namespace Engine;

/// <summary>An entity's name, for finding it and for tools that list the world.</summary>
public struct Name
{
    /// <summary>The name.</summary>
    public string Value;

    /// <summary>Creates a name.</summary>
    public Name(string value) => Value = value;

    /// <inheritdoc />
    public override readonly string ToString() => Value;
}

/// <summary>
/// The entity this one belongs to, as a handle, so a parent despawned and replaced is not mistaken
/// for the new entity with its id.
/// </summary>
/// <remarks>
/// A child's <see cref="Transform"/> is relative to its parent's, and
/// <see cref="TransformPropagation"/> writes the composed world matrix into its
/// <see cref="GlobalTransform"/> each frame.
/// </remarks>
public struct Parent
{
    /// <summary>The parent.</summary>
    public Entity Value;

    /// <summary>Creates a parent link.</summary>
    public Parent(Entity value) => Value = value;
}

public sealed partial class EcsWorld
{
    /// <summary>Names <paramref name="entity"/>, replacing a name it had.</summary>
    public void SetName(int entity, string name)
    {
        if (Has<Name>(entity)) Update(entity, new Name(name));
        else Add(entity, new Name(name));
    }

    /// <summary>The name of <paramref name="entity"/>, or <c>null</c>.</summary>
    public string? NameOf(int entity) => TryGet<Name>(entity, out var name) ? name.Value : null;

    /// <summary>The first entity with the name <paramref name="name"/>, or 0.</summary>
    /// <remarks>A walk over every named entity, so it is for finding something once, not every frame.</remarks>
    public int FindByName(string name)
    {
        foreach (var (entity, value) in Query<Name>())
            if (value.Value == name) return entity;
        return 0;
    }

    /// <summary>Makes <paramref name="parent"/> the parent of <paramref name="child"/>, or removes the link when it is 0.</summary>
    /// <exception cref="InvalidOperationException">The link would make an entity its own ancestor.</exception>
    public void SetParent(int child, int parent)
    {
        if (parent == 0)
        {
            Remove<Parent>(child);
            return;
        }

        for (var ancestor = parent; ancestor != 0; ancestor = ParentOf(ancestor))
            if (ancestor == child) throw new InvalidOperationException($"Entity {parent} is {child} or one of its children, so it cannot be its parent.");

        var link = new Parent(Handle(parent));
        if (Has<Parent>(child)) Update(child, link);
        else Add(child, link);
    }

    /// <summary>The parent of <paramref name="child"/>, or 0 when it has none or its parent is gone.</summary>
    public int ParentOf(int child) =>
        TryGet<Parent>(child, out var parent) && TryResolve(parent.Value, out var id) ? id : 0;

    /// <summary>The entities whose parent is <paramref name="parent"/>.</summary>
    /// <remarks>A walk over every entity with a parent, so it is for tools and for despawning, not every frame.</remarks>
    public IReadOnlyList<int> ChildrenOf(int parent)
    {
        var children = new List<int>();
        foreach (var (entity, link) in Query<Parent>())
            if (TryResolve(link.Value, out var id) && id == parent) children.Add(entity);
        return children;
    }

    /// <summary>Despawns <paramref name="entity"/> and every entity below it.</summary>
    public void DespawnRecursive(int entity)
    {
        foreach (var child in ChildrenOf(entity)) DespawnRecursive(child);
        Despawn(entity);
    }
}

/// <summary>
/// The world matrix of an entity that has a <see cref="Parent"/>: its own <see cref="Transform"/>
/// composed with every ancestor's, written by <see cref="TransformPropagation"/> each frame.
/// </summary>
/// <remarks>An entity with no parent has none, and its <see cref="Transform"/> is its world transform.</remarks>
public struct GlobalTransform
{
    /// <summary>Model to world space, in <c>System.Numerics</c> order (row vectors).</summary>
    public System.Numerics.Matrix4x4 Matrix;
}

/// <summary>
/// Composes the transforms of entities with parents into their <see cref="GlobalTransform"/>, in
/// <see cref="Stage.Render"/>, after physics has written its bodies' transforms and before the
/// renderer reads them.
/// </summary>
public static class TransformPropagation
{
    /// <summary>A transform as a matrix: scale, then rotation, then translation.</summary>
    public static System.Numerics.Matrix4x4 ToMatrix(in Transform t) =>
        System.Numerics.Matrix4x4.CreateScale(t.Scale)
        * System.Numerics.Matrix4x4.CreateFromQuaternion(t.Rotation)
        * System.Numerics.Matrix4x4.CreateTranslation(t.Position);

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
    /// The entities whose chain of transforms changed after propagation ran, gathered at
    /// <see cref="Stage.Last"/> and propagated the next frame, since change bits are cleared when
    /// the next frame begins.
    /// </summary>
    internal sealed class Pending
    {
        public HashSet<int> Entities { get; } = [];
    }

    /// <summary>
    /// Writes the <see cref="GlobalTransform"/> of every parented entity whose chain changed, which
    /// is when its own or an ancestor's <see cref="Transform"/> or <see cref="Parent"/> changed this
    /// frame or after the last run, or it has none yet. A chain nothing touched keeps the one it has, so a
    /// static hierarchy costs a scan of the change bits, 64 entities to a word.
    /// </summary>
    /// <remarks>
    /// A write through <see cref="EcsWorld.GetRef{T}"/>, <see cref="EcsWorld.Update{T}"/> or a
    /// by-reference query marks a component changed. A write to a component reached some other
    /// way, as through a span of the store, is not seen until something marks it.
    /// </remarks>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<Parent>() == 0) return;
        var pending = world.GetOrInsertResource(() => new Pending()).Entities;

        // Nothing moved, nothing was parented, and every parented entity has a global transform.
        if (pending.Count == 0 && !ecs.AnyChanged<Transform>() && !ecs.AnyChanged<Parent>()
            && ecs.Count<GlobalTransform>() >= ecs.Count<Parent>())
            return;

        var dirty = new Dictionary<int, bool>();
        var done = new Dictionary<int, System.Numerics.Matrix4x4>();
        foreach (var (entity, _) in ecs.Query<Parent>())
            if (Dirty(ecs, entity, pending, dirty, 0) || !ecs.Has<GlobalTransform>(entity))
                Compose(ecs, entity, pending, dirty, done, depth: 0);
        pending.Clear();
    }

    /// <summary>
    /// Remembers, at the end of the frame, every entity whose <see cref="Transform"/> or
    /// <see cref="Parent"/> changed this frame, so a write made after <see cref="Run"/> reaches the
    /// global transforms the next frame.
    /// </summary>
    public static void Remember(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || ecs.Count<Parent>() == 0) return;
        bool transforms = ecs.AnyChanged<Transform>(), parents = ecs.AnyChanged<Parent>();
        if (!transforms && !parents) return;

        var pending = world.GetOrInsertResource(() => new Pending()).Entities;
        if (transforms)
            foreach (var (entity, _) in ecs.Query<Transform>().Changed<Transform>()) pending.Add(entity);
        if (parents)
            foreach (var (entity, _) in ecs.Query<Parent>().Changed<Parent>()) pending.Add(entity);
    }

    // Whether the entity or anything above it changed. The depth guard covers a cycle made by
    // writing Parent components directly.
    private static bool Dirty(EcsWorld ecs, int entity, HashSet<int> pending, Dictionary<int, bool> known, int depth)
    {
        if (known.TryGetValue(entity, out var answer)) return answer;
        answer = pending.Contains(entity) || ecs.Changed<Transform>(entity) || ecs.Changed<Parent>(entity);
        var parent = ecs.ParentOf(entity);
        if (!answer && parent != 0 && depth < 256) answer = Dirty(ecs, parent, pending, known, depth + 1);
        return known[entity] = answer;
    }

    // An entity's world matrix, kept from its global transform when its chain is clean and composed
    // from its parent's otherwise, writing the global transform of a parented one it composed.
    private static System.Numerics.Matrix4x4 Compose(EcsWorld ecs, int entity, HashSet<int> pending, Dictionary<int, bool> dirty,
        Dictionary<int, System.Numerics.Matrix4x4> done, int depth)
    {
        if (done.TryGetValue(entity, out var known)) return known;

        var parent = ecs.ParentOf(entity);
        if (parent != 0 && !Dirty(ecs, entity, pending, dirty, 0) && ecs.TryGet<GlobalTransform>(entity, out var kept))
            return done[entity] = kept.Matrix;

        var local = ecs.TryGet<Transform>(entity, out var t) ? ToMatrix(t) : System.Numerics.Matrix4x4.Identity;
        var world = parent != 0 && depth < 256 ? local * Compose(ecs, parent, pending, dirty, done, depth + 1) : local;

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
