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
        ApplyDamping(material);
        return new PhysicsBody(this, handle.Value, BodyKind.Dynamic);
    }

    /// <summary>Registers a convex shape and adds a kinematic body for it, returning the engine handle.</summary>
    private PhysicsBody RegisterKinematic<TShape>(in TShape shape, Vector3 position, PhysicsMaterial? material, int entityId)
        where TShape : unmanaged, IConvexShape
    {
        var idx = Simulation.Shapes.Add(shape);
        var handle = Simulation.Bodies.Add(BodyDescription.CreateKinematic(
            new RigidPose(position, Quaternion.Identity), Coll(idx), new BodyActivityDescription(0.01f)));
        if (entityId != 0) _bodyToEntity[handle.Value] = entityId;
        ApplyDamping(material);
        return new PhysicsBody(this, handle.Value, BodyKind.Kinematic);
    }

    /// <summary>Registers a shape and adds an immovable static collider for it, returning the engine handle.</summary>
    private PhysicsBody RegisterStatic<TShape>(in TShape shape, Vector3 position, int entityId)
        where TShape : unmanaged, IShape
    {
        var idx = Simulation.Shapes.Add(shape);
        var handle = Simulation.Statics.Add(new StaticDescription(position, Quaternion.Identity, idx));
        if (entityId != 0) _staticToEntity[handle.Value] = entityId;
        return new PhysicsBody(this, handle.Value, BodyKind.Static);
    }

    /// <summary>Folds a per-body material's damping into the global integrator (max-of-all in this simple impl).</summary>
    private void ApplyDamping(PhysicsMaterial? material)
    {
        if (material is { } m)
        {
            ref var cb = ref CallbacksRef;
            cb.LinearDamping = MathF.Max(cb.LinearDamping, m.LinearDamping);
            cb.AngularDamping = MathF.Max(cb.AngularDamping, m.AngularDamping);
        }
    }

    // -- Dynamic --

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

    // -- Static --

    /// <inheritdoc />
    public PhysicsBody CreateStaticSphere(Vector3 position, float radius, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new Sphere(radius), position, entityId);

    /// <inheritdoc />
    public PhysicsBody CreateStaticBox(Vector3 position, Vector3 halfExtents, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new BepuBox(halfExtents.X * 2, halfExtents.Y * 2, halfExtents.Z * 2), position, entityId);

    /// <inheritdoc />
    public PhysicsBody CreateStaticCapsule(Vector3 position, float radius, float height, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new Capsule(radius, height), position, entityId);

    /// <inheritdoc />
    public PhysicsBody CreateGroundPlane(float y = 0, float halfSize = 500, PhysicsMaterial? material = null, int entityId = 0) =>
        RegisterStatic(new BepuBox(halfSize * 2, 1f, halfSize * 2), new Vector3(0, y - 0.5f, 0), entityId);

    /// <inheritdoc />
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
        return new PhysicsBody(this, handle.Value, BodyKind.Static);
    }

    // -- Kinematic --

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