using System.Numerics;
using BepuPhysics;
using BepuPhysics.Constraints;

namespace Engine;

/// <summary>A constraint holding two bodies together, by its handle in the solver.</summary>
/// <param name="Handle">The solver's handle, or -1 for none.</param>
public readonly record struct PhysicsJoint(int Handle)
{
    /// <summary>No joint.</summary>
    public static PhysicsJoint None => new(-1);

    /// <summary>Whether this names a joint that was made.</summary>
    public bool IsValid => Handle >= 0;
}

/// <summary>Joints, which hold two moving bodies together at a point, along an axis, rigidly or within a distance.</summary>
/// <remarks>
/// A joint holds bodies that move, dynamic or kinematic, since the solver moves what it joins. A
/// body held to the world is joined to a kinematic one, which nothing pushes. Destroying a body
/// destroys its joints with it.
/// </remarks>
public sealed partial class PhysicsWorld
{
    // Stiff enough to read as rigid at the default step, and critically damped so it does not ring.
    private static readonly SpringSettings JointSpring = new(30, 1);

    /// <summary>Joins two bodies at a point in the world, about which each may turn freely, as a ball in a socket.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public PhysicsJoint CreateBallJoint(PhysicsBody a, PhysicsBody b, Vector3 point)
    {
        var (ra, rb) = Bodies(a, b);
        return Add(ra, rb, new BallSocket
        {
            LocalOffsetA = Local(ra, point),
            LocalOffsetB = Local(rb, point),
            SpringSettings = JointSpring,
        });
    }

    /// <summary>Joins two bodies at a point in the world, about which they turn only around <paramref name="axis"/>, as a door on its hinge.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public PhysicsJoint CreateHingeJoint(PhysicsBody a, PhysicsBody b, Vector3 point, Vector3 axis)
    {
        var (ra, rb) = Bodies(a, b);
        axis = Vector3.Normalize(axis);
        return Add(ra, rb, new Hinge
        {
            LocalOffsetA = Local(ra, point),
            LocalHingeAxisA = LocalDirection(ra, axis),
            LocalOffsetB = Local(rb, point),
            LocalHingeAxisB = LocalDirection(rb, axis),
            SpringSettings = JointSpring,
        });
    }

    /// <summary>Joins two bodies rigidly, as they are placed when it is made, so they move as one.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public PhysicsJoint CreateWeldJoint(PhysicsBody a, PhysicsBody b)
    {
        var (ra, rb) = Bodies(a, b);
        var inverse = Quaternion.Conjugate(ra.Pose.Orientation);
        return Add(ra, rb, new Weld
        {
            LocalOffset = Vector3.Transform(rb.Pose.Position - ra.Pose.Position, inverse),
            LocalOrientation = Quaternion.Normalize(inverse * rb.Pose.Orientation),
            SpringSettings = JointSpring,
        });
    }

    /// <summary>
    /// Keeps a point on each body, given in the world, between <paramref name="minimum"/> and
    /// <paramref name="maximum"/> units apart, as a rope does with a minimum of 0.
    /// </summary>
    /// <exception cref="ArgumentException">A body is static, or the distances are out of order.</exception>
    public PhysicsJoint CreateDistanceJoint(PhysicsBody a, PhysicsBody b, Vector3 pointA, Vector3 pointB, float minimum, float maximum)
    {
        if (minimum < 0 || maximum < minimum) throw new ArgumentException("A distance joint's minimum is at least 0 and at most its maximum.");
        var (ra, rb) = Bodies(a, b);
        return Add(ra, rb, new DistanceLimit(Local(ra, pointA), Local(rb, pointB), minimum, maximum, JointSpring));
    }

    /// <summary>Removes a joint. One already gone with a destroyed body is passed over.</summary>
    public void DestroyJoint(PhysicsJoint joint)
    {
        if (joint.IsValid && Simulation.Solver.ConstraintExists(new ConstraintHandle(joint.Handle)))
            Simulation.Solver.Remove(new ConstraintHandle(joint.Handle));
    }

    /// <summary>Whether a joint exists, which it stops doing when it or one of its bodies is destroyed.</summary>
    public bool JointExists(PhysicsJoint joint) =>
        joint.IsValid && Simulation.Solver.ConstraintExists(new ConstraintHandle(joint.Handle));

    private PhysicsJoint Add<T>(BodyReference a, BodyReference b, T constraint) where T : unmanaged, ITwoBodyConstraintDescription<T>
    {
        // A joined pair is woken, so a sleeping body starts obeying its new joint.
        a.Awake = true;
        b.Awake = true;
        return new PhysicsJoint(Simulation.Solver.Add(a.Handle, b.Handle, constraint).Value);
    }

    private (BodyReference A, BodyReference B) Bodies(PhysicsBody a, PhysicsBody b)
    {
        if (a.Kind == BodyKind.Static || b.Kind == BodyKind.Static)
            throw new ArgumentException("A joint holds bodies that move. Join a body to the world through a kinematic body.");
        if (a.Handle == b.Handle)
            throw new ArgumentException("A joint holds two different bodies.");
        return (Simulation.Bodies[new BodyHandle(a.Handle)], Simulation.Bodies[new BodyHandle(b.Handle)]);
    }

    // A point in the world as an offset in a body's own space.
    private static Vector3 Local(BodyReference body, Vector3 point) =>
        Vector3.Transform(point - body.Pose.Position, Quaternion.Conjugate(body.Pose.Orientation));

    private static Vector3 LocalDirection(BodyReference body, Vector3 direction) =>
        Vector3.Transform(direction, Quaternion.Conjugate(body.Pose.Orientation));
}
