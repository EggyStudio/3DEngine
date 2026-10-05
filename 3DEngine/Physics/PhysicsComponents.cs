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
    /// <summary>
    /// The convex hull of the entity's meshes and its descendants', as the mesh is, the smallest
    /// shape without hollows that holds them, which falls, is pushed or is moved as its
    /// <see cref="RigidBody"/> says, as a rock or a barrel of a model's shape.
    /// </summary>
    ConvexHull,
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

    /// <summary>
    /// Which of the 32 layers its body is on, 0 unless set, which decides what it collides with as
    /// <c>SetPhysicsLayersCollide</c> says.
    /// </summary>
    public int Layer;

    /// <summary>A box this size.</summary>
    public static Collider Box(Vector3 size) => new() { Shape = ColliderShape.Box, Size = size };

    /// <summary>A ball this wide in radius.</summary>
    public static Collider Sphere(float radius) => new() { Shape = ColliderShape.Sphere, Radius = radius };

    /// <summary>An upright capsule.</summary>
    public static Collider Capsule(float radius, float height) => new() { Shape = ColliderShape.Capsule, Radius = radius, Height = height };

    /// <summary>The shape of the meshes the entity and its descendants show.</summary>
    public static Collider Mesh => new() { Shape = ColliderShape.Mesh };

    /// <summary>The convex hull of the meshes the entity and its descendants show.</summary>
    public static Collider ConvexHull => new() { Shape = ColliderShape.ConvexHull };
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

    /// <summary>
    /// Whether a dynamic body is swept over each step, as a ball struck hard is, so it meets a thin
    /// wall it would otherwise cross within a step. <c>SetPhysicsBodyContinuous</c> says what it
    /// costs and how fast it holds.
    /// </summary>
    public bool Continuous;

    /// <summary>A body that falls and is pushed.</summary>
    public static RigidBody Dynamic(float mass = 1) => new() { Kind = BodyKind.Dynamic, Mass = mass };

    /// <summary>A body that never moves.</summary>
    public static RigidBody Static => new() { Kind = BodyKind.Static };

    /// <summary>A body moved only by the program.</summary>
    public static RigidBody Kinematic => new() { Kind = BodyKind.Kinematic };
}

/// <summary>The kind of a <see cref="Joint"/>.</summary>
public enum JointKind
{
    /// <summary>Free to turn about the point, as a ball in a socket.</summary>
    Ball,
    /// <summary>Turning only about the axis, as a door.</summary>
    Hinge,
    /// <summary>Rigid, as the two bodies are placed.</summary>
    Weld,
    /// <summary>Kept between two distances, as a rope or a rod.</summary>
    Distance,
}

/// <summary>
/// A joint between two entities' bodies, on an entity of its own, which a scene file holds, so a
/// level hangs a door or a lamp as it places it.
/// </summary>
/// <remarks>
/// <para>
/// The joint entity's place in the world is the point the bodies are joined at, and its up
/// direction a hinge's axis and the middle of a ball joint's cone. <see cref="PhysicsBodies"/>
/// makes it once both bodies are made, adds its <see cref="PhysicsJoint"/> to the entity, and
/// destroys it when the entity goes. A joint that cannot be made, as one to a static body, is
/// given <see cref="PhysicsJoint.None"/> with the reason in the log.
/// </para>
/// <para>
/// A distance joint keeps the joint's point on the first body between <see cref="MinDistance"/>
/// and <see cref="MaxDistance"/> from the second body's middle, the two as far apart as they are
/// when it is made where <see cref="MaxDistance"/> is 0.
/// </para>
/// </remarks>
[SceneComponent]
public struct Joint
{
    /// <summary>Which kind it is.</summary>
    public JointKind Kind;

    /// <summary>The first body's entity.</summary>
    public Entity A;

    /// <summary>The second body's entity.</summary>
    public Entity B;

    /// <summary>A hinge's limits, in degrees from where it is made, none where both are 0.</summary>
    public float MinAngle, MaxAngle;

    /// <summary>A hinge's motor, in degrees a second, with no more than <see cref="MotorTorque"/>, none where the torque is 0.</summary>
    public float MotorSpeed, MotorTorque;

    /// <summary>A ball joint's cone, how far it swings and twists in degrees, none where both are 0.</summary>
    public float Swing, Twist;

    /// <summary>A distance joint's range.</summary>
    public float MinDistance, MaxDistance;
}

/// <summary>Marks an entity whose collider could not be made, so it is not tried again every frame.</summary>
internal struct NoBody;

/// <summary>
/// Makes the body of every entity with a <see cref="Collider"/> and a <see cref="RigidBody"/> that
/// has none, and destroys the bodies it made whose entities are gone or no longer have one.
/// </summary>
/// <remarks>
/// A body is made at its entity's place and rotation in the world, with the entity's
/// <see cref="PhysicsMaterial"/> when it has one, and its <see cref="PhysicsBody"/> is added to the entity, so physics writes its pose back into the
/// entity's <see cref="Transform"/>. A dynamic capsule beside a <see cref="CharacterController"/> is
/// made a character. The system runs in <see cref="Stage.PreUpdate"/>, after a scene's entities
/// are spawned, and <c>LoadScene</c> runs it at once, so a level's bodies exist when it returns.
/// </remarks>
internal static class PhysicsBodies
{
    // The bodies made here, by handle, with the entity each belongs to.
    internal sealed class Made
    {
        public Dictionary<PhysicsBody, Entity> Bodies { get; } = [];
        public Dictionary<PhysicsJoint, Entity> Joints { get; } = [];
    }

    /// <summary>Makes the bodies entities describe and destroys those whose entities are gone.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || !world.TryGetResource<PhysicsWorld>(out var physics)) return;
        var all = world.GetOrInsertResource(() => new Made());
        var made = all.Bodies;
        Joints(ecs, physics, all.Joints);

        if (made.Count > 0)
            foreach (var (body, entity) in made.ToArray())
                if (!ecs.TryResolve(entity, out var id) || !ecs.Has<PhysicsBody>(id))
                {
                    if (body.IsValid) physics.Destroy(body);
                    made.Remove(body);
                }

        if (ecs.Count<Collider>() == 0) return;
        var wanted = new List<(int Entity, Collider Collider, RigidBody Body)>();
        foreach (var (entity, collider, rigid) in ecs.Query<Collider, RigidBody>().Without<PhysicsBody>().Without<NoBody>())
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
            else if (collider.Shape == ColliderShape.ConvexHull)
            {
                if (TrianglesUnder(ecs, entity, placed) is not { } triangles) continue;
                // The triangles come placed about the entity and turned with it, and the hull is
                // made unturned, then turned with the entity below.
                var unturn = Quaternion.Inverse(rotation);
                var points = triangles.Vertices.Select(v => Vector3.Transform(v, unturn)).Distinct().ToArray();
                try
                {
                    body = physics.CreateConvexHull(position, points, rigid.Mass > 0 ? rigid.Mass : 1, rigid.Kind, entityId: entity);
                }
                catch (ArgumentException ex)
                {
                    Log.Category("Engine.Physics").Warn($"Collider: entity {entity}'s meshes make no convex hull, so it has no body: {ex.Message}");
                    ecs.Add(entity, new NoBody());
                    continue;
                }
            }
            else body = Make(physics, ecs, entity, collider, rigid, position);
            if (ecs.TryGet<PhysicsMaterial>(entity, out var material)) physics.SetMaterial(body, material);
            if (collider.IsTrigger) physics.SetTrigger(body, true);
            if (collider.Layer != 0) physics.SetLayer(body, collider.Layer);
            if (rigid.Continuous) physics.SetContinuous(body, true);
            if (rotation != Quaternion.Identity && collider.Shape != ColliderShape.Capsule) physics.SetRotation(body, rotation);
            ecs.Add(entity, body);
            made[body] = ecs.Handle(entity);
        }
        Joints(ecs, physics, all.Joints);
    }

    // Destroys the joints whose entities are gone, and makes those whose bodies are both made.
    private static void Joints(EcsWorld ecs, PhysicsWorld physics, Dictionary<PhysicsJoint, Entity> made)
    {
        if (made.Count > 0)
            foreach (var (joint, entity) in made.ToArray())
                if (!ecs.TryResolve(entity, out var id) || !ecs.Has<PhysicsJoint>(id))
                {
                    physics.DestroyJoint(joint);
                    made.Remove(joint);
                }

        if (ecs.Count<Joint>() == 0) return;
        var wanted = new List<(int Entity, Joint Joint)>();
        foreach (var (entity, joint) in ecs.Query<Joint>().Without<PhysicsJoint>()) wanted.Add((entity, joint));
        foreach (var (entity, joint) in wanted)
        {
            if (!ecs.TryGet<PhysicsBody>(joint.A, out var a) || !ecs.TryGet<PhysicsBody>(joint.B, out var b))
            {
                // A body not made yet, which a later frame makes, or an entity that has none.
                if (ecs.TryResolve(joint.A, out var idA) && ecs.TryResolve(joint.B, out var idB)
                    && ecs.Has<Collider>(idA) && ecs.Has<Collider>(idB)) continue;
                Log.Category("Engine.Physics").Warn($"Joint: entity {entity} joins an entity that has no body, so it is not made.");
                ecs.Add(entity, PhysicsJoint.None);
                continue;
            }
            var placed = TransformPropagation.ComposedWorldMatrix(ecs, entity);
            var point = placed.Translation;
            var up = Vector3.TransformNormal(Vector3.UnitY, placed);
            up = up.LengthSquared() > 1e-8f ? Vector3.Normalize(up) : Vector3.UnitY;
            try
            {
                var made1 = joint.Kind switch
                {
                    JointKind.Hinge => physics.CreateHingeJoint(a, b, point, up),
                    JointKind.Weld => physics.CreateWeldJoint(a, b),
                    JointKind.Distance => Distance(physics, a, b, point, joint),
                    _ => physics.CreateBallJoint(a, b, point),
                };
                if (joint.Kind == JointKind.Hinge && (joint.MinAngle != 0 || joint.MaxAngle != 0))
                    physics.SetHingeLimit(made1, float.DegreesToRadians(joint.MinAngle), float.DegreesToRadians(joint.MaxAngle));
                if (joint.Kind == JointKind.Hinge && joint.MotorTorque > 0)
                    physics.SetHingeMotor(made1, float.DegreesToRadians(joint.MotorSpeed), joint.MotorTorque);
                if (joint.Kind == JointKind.Ball && (joint.Swing > 0 || joint.Twist > 0))
                    physics.SetBallJointLimit(made1, up, float.DegreesToRadians(joint.Swing > 0 ? joint.Swing : 180),
                        float.DegreesToRadians(joint.Twist > 0 ? joint.Twist : 180));
                ecs.Add(entity, made1);
                made[made1] = ecs.Handle(entity);
            }
            catch (ArgumentException ex)
            {
                Log.Category("Engine.Physics").Warn($"Joint: entity {entity} cannot be made: {ex.Message}");
                ecs.Add(entity, PhysicsJoint.None);
            }
        }
    }

    private static PhysicsJoint Distance(PhysicsWorld physics, PhysicsBody a, PhysicsBody b, Vector3 point, Joint joint)
    {
        var other = physics.GetPosition(b);
        var maximum = joint.MaxDistance > 0 ? joint.MaxDistance : Vector3.Distance(point, other);
        return physics.CreateDistanceJoint(a, b, point, other, MathF.Min(MathF.Max(0, joint.MinDistance), maximum), maximum);
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
