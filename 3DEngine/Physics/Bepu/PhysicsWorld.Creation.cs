using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuMesh = BepuPhysics.Collidables.Mesh;
using BepuBox = BepuPhysics.Collidables.Box;

namespace Engine;

/// <summary>Body and static collider creation: dynamic, kinematic, and static factories for every supported shape.</summary>
public sealed partial class PhysicsWorld
{
    /// <summary>Wraps a shape index in a <see cref="CollidableDescription"/> with the engine's default speculative margin.</summary>
    private static CollidableDescription Coll(TypedIndex shape) => 
        new(shape, 0.1f);

    /// <summary>Registers a convex shape and adds a dynamic body for it, returning the engine handle.</summary>
    private PhysicsBody RegisterDynamic<TShape>(in TShape shape, Vector3 position, float mass, PhysicsMaterial? material, int entityId)
        where TShape : unmanaged, IConvexShape
    {
        var idx = Simulation.Shapes.Add(shape);
        var inertia = shape.ComputeInertia(mass);
        var handle = Simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(position, Quaternion.Identity), inertia, Coll(idx), new BodyActivityDescription(0.01f)));
        if (entityId != 0) _bodyToEntity[handle.Value] = entityId;
        return WithMaterial(new PhysicsBody(this, handle.Value, BodyKind.Dynamic), material);
    }

    /// <summary>Registers a convex shape and adds a kinematic body for it, returning the engine handle.</summary>
    private PhysicsBody RegisterKinematic<TShape>(in TShape shape, Vector3 position, PhysicsMaterial? material, int entityId)
        where TShape : unmanaged, IConvexShape
    {
        var idx = Simulation.Shapes.Add(shape);
        var handle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(
            new RigidPose(position, Quaternion.Identity), Coll(idx), new BodyActivityDescription(0.01f)));
        if (entityId != 0) _bodyToEntity[handle.Value] = entityId;
        return WithMaterial(new PhysicsBody(this, handle.Value, BodyKind.Kinematic), material);
    }

    /// <summary>Registers a shape and adds an immovable static collider for it, returning the engine handle.</summary>
    private PhysicsBody RegisterStatic<TShape>(in TShape shape, Vector3 position, int entityId, PhysicsMaterial? material = null)
        where TShape : unmanaged, IShape
    {
        var idx = Simulation.Shapes.Add(shape);
        var handle = Simulation.Statics.Add(new StaticDescription(position, Quaternion.Identity, idx));
        if (entityId != 0) _staticToEntity[handle.Value] = entityId;
        return WithMaterial(new PhysicsBody(this, handle.Value, BodyKind.Static), material);
    }

    private readonly BodyMaterials _materials = new();

    // A body made with a material takes it, and one made without has the world's friction and no
    // bounce, whatever the body its handle belonged to before had.
    private PhysicsBody WithMaterial(PhysicsBody body, PhysicsMaterial? material)
    {
        _materials.Clear(body);
        if (material is { } m) SetMaterial(body, m);
        return body;
    }

    /// <summary>
    /// Gives a body a friction and a bounce of its own. Two bodies touching mix their frictions as
    /// the square root of their product, as Box2D does, and take the larger bounce.
    /// </summary>
    /// <remarks>
    /// The solver has no bounce of its own and stops a pair at the surface, so a pair that starts
    /// touching at <see cref="BounceThreshold"/> or faster is pushed apart at its bounce times the
    /// speed it closed at, shared by the two bodies' masses. A bounce of 1 keeps most of the speed
    /// and loses a little to the step. The damping is folded into the world's, the largest any
    /// body was given, since the integrator damps every body alike.
    /// </remarks>
    public void SetMaterial(PhysicsBody body, PhysicsMaterial material)
    {
        _materials.Set(body, material);
        ref var cb = ref CallbacksRef;
        cb.LinearDamping = MathF.Max(cb.LinearDamping, material.LinearDamping);
        cb.AngularDamping = MathF.Max(cb.AngularDamping, material.AngularDamping);
    }

    // -- Dynamic

    /// <inheritdoc />
    public PhysicsBody CreateSphere(Vector3 position, float radius, float mass = 1, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterDynamic(new Sphere(radius), position, mass, material, entityId);

    /// <inheritdoc />
    public PhysicsBody CreateBox(Vector3 position, Vector3 halfExtents, float mass = 1, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterDynamic(new BepuBox(halfExtents.X * 2, halfExtents.Y * 2, halfExtents.Z * 2), position, mass, material,
            entityId);

    /// <inheritdoc />
    public PhysicsBody CreateCapsule(Vector3 position, float radius, float height, float mass = 1, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterDynamic(new Capsule(radius, height), position, mass, material, entityId);

    /// <inheritdoc />
    public PhysicsBody CreateCylinder(Vector3 position, float radius, float height, float mass = 1, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterDynamic(new Cylinder(radius, height), position, mass, material, entityId);

    // -- Static

    /// <inheritdoc />
    public PhysicsBody CreateStaticSphere(Vector3 position, float radius, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new Sphere(radius), position, entityId, material);

    /// <inheritdoc />
    public PhysicsBody CreateStaticBox(Vector3 position, Vector3 halfExtents, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new BepuBox(halfExtents.X * 2, halfExtents.Y * 2, halfExtents.Z * 2), position, entityId, material);

    /// <inheritdoc />
    public PhysicsBody CreateStaticCapsule(Vector3 position, float radius, float height, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new Capsule(radius, height), position, entityId, material);

    /// <inheritdoc />
    public PhysicsBody CreateGroundPlane(float y = 0, float halfSize = 500, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new BepuBox(halfSize * 2, 1f, halfSize * 2), new Vector3(0, y - 0.5f, 0), entityId, material);

    /// <inheritdoc />
    /// <remarks>
    /// A triangle collides only from its front, which is the side its corners go around clockwise
    /// seen from, the opposite of a model's winding, which <c>CreatePhysicsStaticModel</c> turns over.
    /// </remarks>
    public PhysicsBody CreateStaticMesh(Vector3 position, ReadOnlySpan<Vector3> vertices, ReadOnlySpan<int> indices,
        PhysicsMaterial? material = null, int entityId = 0)
    {
        if (indices.Length % 3 != 0)
            throw new ArgumentException("indices length must be a multiple of 3.", nameof(indices));
        int triCount = indices.Length / 3;
        BufferPool.Take<Triangle>(triCount, out var triangles);
        for (int i = 0; i < triCount; i++)
        {
            triangles[i] = new Triangle(
                vertices[indices[i * 3 + 0]],
                vertices[indices[i * 3 + 1]],
                vertices[indices[i * 3 + 2]]);
        }

        var mesh = new BepuMesh(triangles, Vector3.One, BufferPool);
        var idx = Simulation.Shapes.Add(mesh);
        var handle = Simulation.Statics.Add(new StaticDescription(position, Quaternion.Identity, idx));
        if (entityId != 0) _staticToEntity[handle.Value] = entityId;
        return WithMaterial(new PhysicsBody(this, handle.Value, BodyKind.Static), material);
    }

    // -- Convex hulls

    /// <summary>
    /// A body shaped as the convex hull of <paramref name="points"/>, the smallest shape without
    /// hollows that holds them all, as a rock, a barrel or a crate of a model's shape, placed with
    /// the points' origin at <paramref name="origin"/>, falling and pushed unless
    /// <paramref name="kind"/> says otherwise.
    /// </summary>
    /// <remarks>
    /// The hull is turned about its center of mass, which the solver keeps, while the body's
    /// position, as it is read and set and as its entity's <see cref="Transform"/> is written, is
    /// where the points' origin is, so a mesh drawn at it sits in its hull.
    /// </remarks>
    /// <exception cref="ArgumentException">The points lie in a plane or on a line, or are fewer than four.</exception>
    public PhysicsBody CreateConvexHull(Vector3 origin, ReadOnlySpan<Vector3> points, float mass = 1, BodyKind kind = BodyKind.Dynamic,
        PhysicsMaterial? material = null, int entityId = 0)
    {
        if (points.Length < 4) throw new ArgumentException("A convex hull needs at least four points.", nameof(points));
        ConvexHullHelper.CreateShape(points.ToArray(), BufferPool, out var center, out ConvexHull hull);
        if (hull.FaceToVertexIndicesStart.Length < 4)
        {
            hull.Dispose(BufferPool);
            throw new ArgumentException("The points lie in a plane or on a line, which holds nothing.", nameof(points));
        }
        var body = kind switch
        {
            BodyKind.Static => RegisterStatic(hull, origin + center, entityId, material),
            BodyKind.Kinematic => RegisterKinematic(hull, origin + center, material, entityId),
            _ => RegisterDynamic(hull, origin + center, mass, material, entityId),
        };
        // A static hull is never moved, so it keeps no offset, and is read at its center.
        if (kind != BodyKind.Static) _origins[body.Handle] = -center;
        return body;
    }

    // -- Kinematic

    /// <inheritdoc />
    public PhysicsBody CreateKinematicSphere(Vector3 position, float radius, PhysicsMaterial? material = null,
        int entityId = 0) =>
        RegisterKinematic(new Sphere(radius), position, material, entityId);

    /// <inheritdoc />
    public PhysicsBody CreateKinematicBox(Vector3 position, Vector3 halfExtents, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterKinematic(new BepuBox(halfExtents.X * 2, halfExtents.Y * 2, halfExtents.Z * 2), position, material,
            entityId);

    /// <inheritdoc />
    public PhysicsBody CreateKinematicCapsule(Vector3 position, float radius, float height, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterKinematic(new Capsule(radius, height), position, material, entityId);
}