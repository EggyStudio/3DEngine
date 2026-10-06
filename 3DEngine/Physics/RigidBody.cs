using System.Numerics;

namespace Engine;

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
