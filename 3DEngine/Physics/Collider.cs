using System.Numerics;

namespace Engine;

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
    /// <remarks>An <see cref="AnimatedModel"/> shows no meshes the world holds, so an entity it alone draws is refused this, with a warning, and given a capsule or a box.</remarks>
    public static Collider Mesh => new() { Shape = ColliderShape.Mesh };

    /// <summary>The convex hull of the meshes the entity and its descendants show.</summary>
    /// <remarks>An entity an <see cref="AnimatedModel"/> alone draws is refused this, as it is <see cref="Mesh"/>.</remarks>
    public static Collider ConvexHull => new() { Shape = ColliderShape.ConvexHull };
}
