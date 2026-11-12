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
    /// and walls. A model a <c>ModelRef</c> spawns under the entity counts, once it has.
    /// </summary>
    Mesh,
    /// <summary>
    /// The convex hull of the entity's meshes and its descendants', as placed under it, the
    /// smallest shape without hollows that holds them, which falls, is pushed or is moved as its
    /// <see cref="RigidBody"/> says, as a rock or a barrel of a model's shape.
    /// </summary>
    ConvexHull,
}
