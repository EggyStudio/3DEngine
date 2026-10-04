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

    // What a hinge needs to be limited or driven: its bodies, its axis on the first, the frames its
    // angle is measured in, zero as they were placed when it was made, and its limit and motor.
    private sealed class HingeParts
    {
        public required BodyHandle A;
        public required BodyHandle B;
        public required Vector3 LocalAxisA;
        public required Quaternion BasisA;
        public required Quaternion BasisB;
        public ConstraintHandle? Limit;
        public ConstraintHandle? Motor;
    }

    private readonly Dictionary<int, HingeParts> _hinges = [];

    // What a ball joint needs to be limited: its bodies and the limits set on it.
    private sealed class BallParts
    {
        public required BodyHandle A;
        public required BodyHandle B;
        public ConstraintHandle? Swing;
        public ConstraintHandle? Twist;
    }

    private readonly Dictionary<int, BallParts> _balls = [];

    /// <summary>Joins two bodies at a point in the world, about which each may turn freely, as a ball in a socket.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public PhysicsJoint CreateBallJoint(PhysicsBody a, PhysicsBody b, Vector3 point)
    {
        var (ra, rb) = Bodies(a, b);
        var joint = Add(ra, rb, new BallSocket
        {
            LocalOffsetA = Local(ra, point),
            LocalOffsetB = Local(rb, point),
            SpringSettings = JointSpring,
        });
        _balls[joint.Handle] = new BallParts { A = ra.Handle, B = rb.Handle };
        return joint;
    }

    /// <summary>
    /// Keeps a ball joint within a cone, the second body's <paramref name="axis"/> turned no more
    /// than <paramref name="maximumSwing"/> radians from the first's, and twisted about it no more
    /// than <paramref name="maximumTwist"/> radians either way, replacing limits set before, as a
    /// shoulder or a link of a chain. The axis is in the world, and the bodies' turn as they are
    /// when the limit is set is the middle of the cone and the twist. An angle of π or more leaves
    /// that part free.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a ball joint, or an angle is below 0.</exception>
    public void SetBallJointLimit(PhysicsJoint ball, Vector3 axis, float maximumSwing, float maximumTwist)
    {
        if (!IsJointOf(ball, BallSocket.ConstraintTypeId) || !_balls.TryGetValue(ball.Handle, out var parts))
            throw new ArgumentException("The joint is not a ball joint that exists.", nameof(ball));
        if (maximumSwing < 0 || maximumTwist < 0) throw new ArgumentException("A ball joint's limits are angles of at least 0.");
        Remove(ref parts.Swing);
        Remove(ref parts.Twist);

        var (a, b) = (Simulation.Bodies[parts.A], Simulation.Bodies[parts.B]);
        axis = Vector3.Normalize(axis);
        var localAxisA = LocalDirection(a, axis);
        if (maximumSwing < MathF.PI)
            parts.Swing = Simulation.Solver.Add(parts.A, parts.B, new SwingLimit
            {
                AxisLocalA = localAxisA,
                AxisLocalB = LocalDirection(b, axis),
                MaximumSwingAngle = maximumSwing,
                SpringSettings = JointSpring,
            });
        if (maximumTwist < MathF.PI)
        {
            // Frames with their Z along the axis, the same in the world as the bodies are, as a hinge's.
            var basisA = FromTo(Vector3.UnitZ, localAxisA);
            parts.Twist = Simulation.Solver.Add(parts.A, parts.B, new TwistLimit
            {
                LocalBasisA = basisA,
                LocalBasisB = Quaternion.Normalize(Quaternion.Conjugate(b.Pose.Orientation) * a.Pose.Orientation * basisA),
                MinimumAngle = -maximumTwist,
                MaximumAngle = maximumTwist,
                SpringSettings = JointSpring,
            });
        }
        a.Awake = true;
        b.Awake = true;
    }

    /// <summary>Joins two bodies at a point in the world, about which they turn only around <paramref name="axis"/>, as a door on its hinge.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    public PhysicsJoint CreateHingeJoint(PhysicsBody a, PhysicsBody b, Vector3 point, Vector3 axis)
    {
        var (ra, rb) = Bodies(a, b);
        axis = Vector3.Normalize(axis);
        var localAxisA = LocalDirection(ra, axis);
        var joint = Add(ra, rb, new Hinge
        {
            LocalOffsetA = Local(ra, point),
            LocalHingeAxisA = localAxisA,
            LocalOffsetB = Local(rb, point),
            LocalHingeAxisB = LocalDirection(rb, axis),
            SpringSettings = JointSpring,
        });

        // A frame on each body with its Z along the axis, the same in the world as placed, so the
        // hinge's angle starts at zero.
        var basisA = FromTo(Vector3.UnitZ, localAxisA);
        _hinges[joint.Handle] = new HingeParts
        {
            A = ra.Handle,
            B = rb.Handle,
            LocalAxisA = localAxisA,
            BasisA = basisA,
            BasisB = Quaternion.Normalize(Quaternion.Conjugate(rb.Pose.Orientation) * ra.Pose.Orientation * basisA),
        };
        return joint;
    }

    /// <summary>
    /// Keeps a hinge turned between <paramref name="minimum"/> and <paramref name="maximum"/>
    /// radians from where it was made, replacing a limit set before, as a door that opens one way.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a hinge, or the angles are out of order.</exception>
    public void SetHingeLimit(PhysicsJoint hinge, float minimum, float maximum)
    {
        var parts = HingeOf(hinge);
        if (maximum < minimum) throw new ArgumentException("A hinge's minimum angle is at most its maximum.");
        Remove(ref parts.Limit);
        parts.Limit = Simulation.Solver.Add(parts.A, parts.B, new TwistLimit
        {
            LocalBasisA = parts.BasisA,
            LocalBasisB = parts.BasisB,
            MinimumAngle = minimum,
            MaximumAngle = maximum,
            SpringSettings = JointSpring,
        });
    }

    /// <summary>
    /// Turns a hinge at <paramref name="speed"/> radians a second, counterclockwise about its axis
    /// seen from the axis's tip for a positive speed, with no more than
    /// <paramref name="maximumTorque"/>, replacing a motor set before, as a wheel's drive does. A
    /// speed of 0 with a torque holds it still against what pushes it, up to that torque.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a hinge.</exception>
    public void SetHingeMotor(PhysicsJoint hinge, float speed, float maximumTorque)
    {
        var parts = HingeOf(hinge);
        Remove(ref parts.Motor);
        parts.Motor = Simulation.Solver.Add(parts.A, parts.B, new AngularAxisMotor
        {
            LocalAxisA = parts.LocalAxisA,
            // Bepu turns B against A, and a positive speed here turns B counterclockwise seen from the axis's tip.
            TargetVelocity = -speed,
            Settings = new MotorSettings(Math.Max(0, maximumTorque), 1e-4f),
        });
        var (a, b) = (Simulation.Bodies[parts.A], Simulation.Bodies[parts.B]);
        a.Awake = true;
        b.Awake = true;
    }

    /// <summary>Takes a hinge's limit and motor away, leaving it free to turn.</summary>
    public void ClearHingeLimitAndMotor(PhysicsJoint hinge)
    {
        if (!_hinges.TryGetValue(hinge.Handle, out var parts)) return;
        Remove(ref parts.Limit);
        Remove(ref parts.Motor);
    }

    private HingeParts HingeOf(PhysicsJoint hinge) =>
        IsJointOf(hinge, Hinge.ConstraintTypeId) && _hinges.TryGetValue(hinge.Handle, out var parts)
            ? parts
            : throw new ArgumentException("The joint is not a hinge that exists.", nameof(hinge));

    // Removes a constraint a hinge added, unless it went with a body already.
    private void Remove(ref ConstraintHandle? handle)
    {
        if (handle is { } h && Simulation.Solver.ConstraintExists(h)) Simulation.Solver.Remove(h);
        handle = null;
    }

    // The shortest turn taking direction from to direction to.
    private static Quaternion FromTo(Vector3 from, Vector3 to)
    {
        var dot = Vector3.Dot(from, to);
        if (dot > 0.9999f) return Quaternion.Identity;
        if (dot < -0.9999f)
        {
            var side = MathF.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY;
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(from, side)), MathF.PI);
        }
        return Quaternion.Normalize(new Quaternion(Vector3.Cross(from, to), 1 + dot));
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

    /// <summary>
    /// Changes how far apart a distance joint keeps its points, as a winch reeling a rope in does
    /// when it is set a little shorter each frame.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a distance joint that exists, or the distances are out of order.</exception>
    public void SetDistanceJointRange(PhysicsJoint joint, float minimum, float maximum)
    {
        if (minimum < 0 || maximum < minimum) throw new ArgumentException("A distance joint's minimum is at least 0 and at most its maximum.");
        var handle = new ConstraintHandle(joint.Handle);
        if (!IsJointOf(joint, DistanceLimit.ConstraintTypeId))
            throw new ArgumentException("The joint is not a distance joint that exists.", nameof(joint));
        Simulation.Solver.GetDescription(handle, out DistanceLimit limit);
        limit.MinimumDistance = minimum;
        limit.MaximumDistance = maximum;
        Simulation.Solver.ApplyDescription(handle, limit);
        // A sleeping pair is woken, so it obeys the new range.
        Simulation.Awakener.AwakenConstraint(handle);
    }

    /// <summary>Removes a joint, with a hinge's limit and motor and a ball joint's limits. One already gone with a destroyed body is passed over.</summary>
    public void DestroyJoint(PhysicsJoint joint)
    {
        if (_hinges.Remove(joint.Handle, out var parts))
        {
            Remove(ref parts.Limit);
            Remove(ref parts.Motor);
        }
        if (_balls.Remove(joint.Handle, out var ball))
        {
            Remove(ref ball.Swing);
            Remove(ref ball.Twist);
        }
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
        var handle = Simulation.Solver.Add(a.Handle, b.Handle, constraint).Value;
        // A handle is given out again once its joint went with a destroyed body, whose parts are
        // forgotten here, before the new joint's are kept.
        _hinges.Remove(handle);
        _balls.Remove(handle);
        return new PhysicsJoint(handle);
    }

    // Whether a joint exists and is of a kind, since a handle a destroyed body's joint left may
    // since name a joint of another kind, or a hinge's limit.
    private bool IsJointOf(PhysicsJoint joint, int typeId) =>
        JointExists(joint) && Simulation.Solver.GetConstraintReference(new ConstraintHandle(joint.Handle)).TypeBatch.TypeId == typeId;

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
