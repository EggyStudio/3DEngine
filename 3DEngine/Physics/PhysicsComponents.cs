using System.Numerics;

namespace Engine;

/// <summary>The shape of a <see cref="Collider"/>.</summary>
public enum ColliderShape
{
    /// <summary>A box of <see cref="Collider.Size"/>.</summary>
    Box,
    /// <summary>A ball of <see cref="Collider.Radius"/>.</summary>
    Sphere,
    /// <summary>An upright capsule of <see cref="Collider.Radius"/> and <see cref="Collider.Height"/>, end to end.</summary>
    Capsule,
    /// <summary>
    /// The triangles of the entity's <see cref="Mesh"/> and of its descendants' meshes, as placed
    /// under it, which never moves whatever its <see cref="RigidBody"/> says, as a level's floors
    /// and walls. A model a <see cref="ModelRef"/> spawns under the entity counts, once it has.
    /// </summary>
    Mesh,
}

/// <summary>
/// What an entity's body is shaped as, centered on its <see cref="Transform"/>, which a scene file
/// holds, so a level says what is solid. With a <see cref="RigidBody"/> beside it,
/// <see cref="PhysicsBodies"/> makes the body.
/// </summary>
[SceneComponent]
public struct Collider
{
    /// <summary>Which shape it is.</summary>
    public ColliderShape Shape;

    /// <summary>A box's size, edge to edge.</summary>
    public Vector3 Size;

    /// <summary>A sphere's or a capsule's radius.</summary>
    public float Radius;

    /// <summary>A capsule's height, end to end.</summary>
    public float Height;

    /// <summary>Whether it reports what enters it, as contacts, and stops nothing, as a goal or a pickup does.</summary>
    public bool IsTrigger;

    /// <summary>A box this size.</summary>
    public static Collider Box(Vector3 size) => new() { Shape = ColliderShape.Box, Size = size };

    /// <summary>A ball this wide in radius.</summary>
    public static Collider Sphere(float radius) => new() { Shape = ColliderShape.Sphere, Radius = radius };

    /// <summary>An upright capsule.</summary>
    public static Collider Capsule(float radius, float height) => new() { Shape = ColliderShape.Capsule, Radius = radius, Height = height };

    /// <summary>The shape of the meshes the entity and its descendants show.</summary>
    public static Collider Mesh => new() { Shape = ColliderShape.Mesh };
}

/// <summary>
/// How an entity's body moves, falling and pushed (<see cref="BodyKind.Dynamic"/>, with a mass),
/// moved only by the program (<see cref="BodyKind.Kinematic"/>), or never (<see cref="BodyKind.Static"/>).
/// A scene file holds it beside the <see cref="Collider"/>.
/// </summary>
[SceneComponent]
public struct RigidBody
{
    /// <summary>How it moves.</summary>
    public BodyKind Kind;

    /// <summary>A dynamic body's mass.</summary>
    public float Mass;

    /// <summary>A body that falls and is pushed.</summary>
    public static RigidBody Dynamic(float mass = 1) => new() { Kind = BodyKind.Dynamic, Mass = mass };

    /// <summary>A body that never moves.</summary>
    public static RigidBody Static => new() { Kind = BodyKind.Static };

    /// <summary>A body moved only by the program.</summary>
    public static RigidBody Kinematic => new() { Kind = BodyKind.Kinematic };
}

/// <summary>
/// Makes the body of every entity with a <see cref="Collider"/> and a <see cref="RigidBody"/> that
/// has none, and destroys the bodies it made whose entities are gone or no longer have one.
/// </summary>
/// <remarks>
/// A body is made at its entity's place and rotation in the world, and its
/// <see cref="PhysicsBody"/> is added to the entity, so physics writes its pose back into the
/// entity's <see cref="Transform"/>. A dynamic capsule beside a <see cref="CharacterController"/> is
/// made a character. The system runs in <see cref="Stage.PreUpdate"/>, after a scene's entities
/// are spawned, and <c>LoadScene</c> runs it at once, so a level's bodies exist when it returns.
/// </remarks>
public static class PhysicsBodies
{
    // The bodies made here, by handle, with the entity each belongs to.
    internal sealed class Made
    {
        public Dictionary<PhysicsBody, Entity> Bodies { get; } = [];
    }

    /// <summary>Makes the bodies entities describe and destroys those whose entities are gone.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || !world.TryGetResource<PhysicsWorld>(out var physics)) return;
        var made = world.GetOrInsertResource(() => new Made()).Bodies;

        if (made.Count > 0)
            foreach (var (body, entity) in made.ToArray())
                if (!ecs.TryResolve(entity, out var id) || !ecs.Has<PhysicsBody>(id))
                {
                    if (body.IsValid) physics.Destroy(body);
                    made.Remove(body);
                }

        if (ecs.Count<Collider>() == 0) return;
        var wanted = new List<(int Entity, Collider Collider, RigidBody Body)>();
        foreach (var (entity, collider, rigid) in ecs.Query<Collider, RigidBody>().Without<PhysicsBody>())
            wanted.Add((entity, collider, rigid));

        foreach (var (entity, collider, rigid) in wanted)
        {
            var placed = TransformPropagation.ComposedWorldMatrix(ecs, entity);
            Matrix4x4.Decompose(placed, out _, out var rotation, out var position);
            PhysicsBody body;
            if (collider.Shape == ColliderShape.Mesh)
            {
                // Made once the meshes are there, which a model loading under the entity is not yet.
                if (TrianglesUnder(ecs, entity, placed) is not { } triangles) continue;
                body = physics.CreateStaticMesh(position, triangles.Vertices, triangles.Indices, entityId: entity);
                rotation = Quaternion.Identity;
            }
            else body = Make(physics, ecs, entity, collider, rigid, position);
            if (collider.IsTrigger) physics.SetTrigger(body, true);
            if (rotation != Quaternion.Identity && collider.Shape != ColliderShape.Capsule) physics.SetRotation(body, rotation);
            ecs.Add(entity, body);
            made[body] = ecs.Handle(entity);
        }
    }

    // The triangles of the meshes of an entity and its descendants, turned and scaled into the world
    // about the entity's place, which the body is made at, or null when there are none. A triangle
    // collides from the side its corners go around clockwise, so a mesh's are turned over.
    private static (Vector3[] Vertices, int[] Indices)? TrianglesUnder(EcsWorld ecs, int entity, Matrix4x4 world)
    {
        var vertices = new List<Vector3>();
        var at = world.Translation;
        var pending = new Stack<int>();
        pending.Push(entity);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            foreach (var child in ecs.ChildrenOf(next)) pending.Push(child);
            if (!ecs.TryGet<Mesh>(next, out var mesh) || mesh.Positions is not { Length: >= 3 } positions) continue;
            var placed = TransformPropagation.ComposedWorldMatrix(ecs, next);
            for (int i = 0; i + 2 < positions.Length; i += 3)
            {
                vertices.Add(Vector3.Transform(positions[i], placed) - at);
                vertices.Add(Vector3.Transform(positions[i + 2], placed) - at);
                vertices.Add(Vector3.Transform(positions[i + 1], placed) - at);
            }
        }
        if (vertices.Count == 0) return null;
        return ([.. vertices], [.. Enumerable.Range(0, vertices.Count)]);
    }

    private static PhysicsBody Make(PhysicsWorld physics, EcsWorld ecs, int entity, Collider collider, RigidBody rigid, Vector3 at)
    {
        var half = collider.Size / 2;
        var mass = rigid.Mass > 0 ? rigid.Mass : 1;
        return (rigid.Kind, collider.Shape) switch
        {
            (BodyKind.Dynamic, ColliderShape.Capsule) when ecs.Has<CharacterController>(entity) =>
                physics.CreateCharacter(at - new Vector3(0, collider.Height / 2, 0), collider.Radius, collider.Height, mass, entity),
            (BodyKind.Dynamic, ColliderShape.Box) => physics.CreateBox(at, half, mass, entityId: entity),
            (BodyKind.Dynamic, ColliderShape.Sphere) => physics.CreateSphere(at, collider.Radius, mass, entityId: entity),
            (BodyKind.Dynamic, ColliderShape.Capsule) => physics.CreateCapsule(at, collider.Radius, MathF.Max(0, collider.Height - 2 * collider.Radius), mass, entityId: entity),
            (BodyKind.Kinematic, ColliderShape.Box) => physics.CreateKinematicBox(at, half, entityId: entity),
            (BodyKind.Kinematic, ColliderShape.Sphere) => physics.CreateKinematicSphere(at, collider.Radius, entityId: entity),
            (BodyKind.Kinematic, ColliderShape.Capsule) => physics.CreateKinematicCapsule(at, collider.Radius, MathF.Max(0, collider.Height - 2 * collider.Radius), entityId: entity),
            (_, ColliderShape.Sphere) => physics.CreateStaticSphere(at, collider.Radius, entityId: entity),
            (_, ColliderShape.Capsule) => physics.CreateStaticCapsule(at, collider.Radius, MathF.Max(0, collider.Height - 2 * collider.Radius), entityId: entity),
            _ => physics.CreateStaticBox(at, half, entityId: entity),
        };
    }
}
