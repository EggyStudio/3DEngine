using System.Numerics;

namespace Engine;

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

    /// <summary>
    /// A hinge's motor, in degrees a second, with no more than <see cref="MotorTorque"/>, or a
    /// slider's, in units a second with no more than that force, none where it is 0.
    /// </summary>
    public float MotorSpeed, MotorTorque;

    /// <summary>A ball joint's cone, how far it swings and twists in degrees, none where both are 0.</summary>
    public float Swing, Twist;

    /// <summary>A distance joint's range, and a slider's limits along its up from where it is made, none where both are 0.</summary>
    public float MinDistance, MaxDistance;
}
