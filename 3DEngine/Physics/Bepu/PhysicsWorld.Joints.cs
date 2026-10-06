using System.Numerics;
using BepuPhysics;
using BepuPhysics.Constraints;

namespace Engine;

/// <summary>Joints, which hold two moving bodies together at a point, along an axis, rigidly or within a distance.</summary>
/// <remarks>
/// A joint holds bodies that move, dynamic or kinematic, since the solver moves what it joins. A
/// body held to the world is joined to a kinematic one, which nothing pushes. Destroying a body
/// destroys its joints with it. Two bodies a joint holds do not collide with each other, since the
/// joint decides how they move, and an axle inside its wheel would otherwise rub against it.
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

    // The bodies each joint holds, which do not collide with each other.
    private readonly JoinedPairs _joined = new();

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
    internal PhysicsJoint CreateBallJoint(PhysicsBody a, PhysicsBody b, Vector3 point)
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
    internal void SetBallJointLimit(PhysicsJoint ball, Vector3 axis, float maximumSwing, float maximumTwist)
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
        Wake(a);
        Wake(b);
    }

    /// <summary>Joins two bodies at a point in the world, about which they turn only around <paramref name="axis"/>, as a door on its hinge.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    internal PhysicsJoint CreateHingeJoint(PhysicsBody a, PhysicsBody b, Vector3 point, Vector3 axis)
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
    internal void SetHingeLimit(PhysicsJoint hinge, float minimum, float maximum)
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
    internal void SetHingeMotor(PhysicsJoint hinge, float speed, float maximumTorque)
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
        Wake(a);
        Wake(b);
    }

    /// <summary>Takes a hinge's limit and motor away, leaving it free to turn.</summary>
    internal void ClearHingeLimitAndMotor(PhysicsJoint hinge)
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

    // A slider's constraints past the one that keeps it on its line: what keeps the bodies from
    // turning against each other, and its limits and motor, each replaced as it is set again.
    private sealed class SliderParts
    {
        public required BodyHandle A;
        public required BodyHandle B;
        public required Vector3 LocalOffsetA;
        public required Vector3 LocalAxisA;
        public ConstraintHandle? Lock;
        public ConstraintHandle? Limit;
        public ConstraintHandle? Motor;
    }

    private readonly Dictionary<int, SliderParts> _sliders = [];

    /// <summary>
    /// Joins two bodies so the second slides along <paramref name="axis"/> against the first and
    /// neither turns against the other, starting as they are placed, as a drawer, a sliding door or
    /// a lift on its frame.
    /// </summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    internal PhysicsJoint CreateSliderJoint(PhysicsBody a, PhysicsBody b, Vector3 axis)
    {
        var (ra, rb) = Bodies(a, b);
        axis = Vector3.Normalize(axis);
        var localAxisA = LocalDirection(ra, axis);
        // The second body's middle, as a point fixed to the first, which it is kept on a line through.
        var localOffsetA = Local(ra, rb.Pose.Position);
        var joint = Add(ra, rb, new PointOnLineServo
        {
            LocalOffsetA = localOffsetA,
            LocalOffsetB = Vector3.Zero,
            LocalDirection = localAxisA,
            ServoSettings = ServoSettings.Default,
            SpringSettings = JointSpring,
        });
        _sliders[joint.Handle] = new SliderParts
        {
            A = ra.Handle,
            B = rb.Handle,
            LocalOffsetA = localOffsetA,
            LocalAxisA = localAxisA,
            Lock = Simulation.Solver.Add(ra.Handle, rb.Handle, new AngularServo
            {
                TargetRelativeRotationLocalA = Quaternion.Normalize(Quaternion.Conjugate(ra.Pose.Orientation) * rb.Pose.Orientation),
                ServoSettings = ServoSettings.Default,
                SpringSettings = JointSpring,
            }),
        };
        return joint;
    }

    /// <summary>
    /// Keeps a slider between <paramref name="minimum"/> and <paramref name="maximum"/> units along
    /// its axis from where it was made, replacing limits set before, as a drawer that stops out and in.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a slider, or the distances are out of order.</exception>
    internal void SetSliderLimit(PhysicsJoint slider, float minimum, float maximum)
    {
        var parts = SliderOf(slider);
        if (maximum < minimum) throw new ArgumentException("A slider's minimum is at most its maximum.");
        Remove(ref parts.Limit);
        parts.Limit = Simulation.Solver.Add(parts.A, parts.B, new LinearAxisLimit
        {
            LocalOffsetA = parts.LocalOffsetA,
            LocalOffsetB = Vector3.Zero,
            LocalAxis = parts.LocalAxisA,
            MinimumOffset = minimum,
            MaximumOffset = maximum,
            SpringSettings = JointSpring,
        });
        WakeSlider(parts);
    }

    /// <summary>
    /// Drives a slider at <paramref name="speed"/> units a second along its axis, the second body
    /// toward the axis's tip for a positive speed, with no more than <paramref name="maximumForce"/>,
    /// replacing a motor set before, as a lift's winch. A speed of 0 holds it against what pushes it,
    /// up to that force.
    /// </summary>
    /// <exception cref="ArgumentException">The joint is not a slider.</exception>
    internal void SetSliderMotor(PhysicsJoint slider, float speed, float maximumForce)
    {
        var parts = SliderOf(slider);
        Remove(ref parts.Motor);
        parts.Motor = Simulation.Solver.Add(parts.A, parts.B, new LinearAxisMotor
        {
            LocalOffsetA = parts.LocalOffsetA,
            LocalOffsetB = Vector3.Zero,
            LocalAxis = parts.LocalAxisA,
            TargetVelocity = speed,
            Settings = new MotorSettings(Math.Max(0, maximumForce), 1e-4f),
        });
        WakeSlider(parts);
    }

    /// <summary>How far the second body of a slider is along its axis from where it was made.</summary>
    /// <exception cref="ArgumentException">The joint is not a slider.</exception>
    internal float GetSliderPosition(PhysicsJoint slider)
    {
        var parts = SliderOf(slider);
        var a = Simulation.Bodies[parts.A].Pose;
        var b = Simulation.Bodies[parts.B].Pose;
        var anchor = a.Position + Vector3.Transform(parts.LocalOffsetA, a.Orientation);
        return Vector3.Dot(b.Position - anchor, Vector3.Transform(parts.LocalAxisA, a.Orientation));
    }

    private void WakeSlider(SliderParts parts)
    {
        var (a, b) = (Simulation.Bodies[parts.A], Simulation.Bodies[parts.B]);
        Wake(a);
        Wake(b);
    }

    private SliderParts SliderOf(PhysicsJoint slider) =>
        IsJointOf(slider, PointOnLineServo.ConstraintTypeId) && _sliders.TryGetValue(slider.Handle, out var parts)
            ? parts
            : throw new ArgumentException("The joint is not a slider that exists.", nameof(slider));

    /// <summary>Joins two bodies rigidly, as they are placed when it is made, so they move as one.</summary>
    /// <exception cref="ArgumentException">A body is static.</exception>
    internal PhysicsJoint CreateWeldJoint(PhysicsBody a, PhysicsBody b)
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
    internal PhysicsJoint CreateDistanceJoint(PhysicsBody a, PhysicsBody b, Vector3 pointA, Vector3 pointB, float minimum, float maximum)
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
    internal void SetDistanceJointRange(PhysicsJoint joint, float minimum, float maximum)
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

    /// <summary>Removes a joint, with a hinge's limit and motor, a ball joint's limits and a slider's lock, limits and motor. One already gone with a destroyed body is passed over.</summary>
    internal void DestroyJoint(PhysicsJoint joint)
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
        if (_sliders.Remove(joint.Handle, out var slider))
        {
            Remove(ref slider.Lock);
            Remove(ref slider.Limit);
            Remove(ref slider.Motor);
        }
        _joined.Remove(joint.Handle);
        if (joint.IsValid && Simulation.Solver.ConstraintExists(new ConstraintHandle(joint.Handle)))
            Simulation.Solver.Remove(new ConstraintHandle(joint.Handle));
    }

    /// <summary>Whether a joint exists, which it stops doing when it or one of its bodies is destroyed.</summary>
    internal bool JointExists(PhysicsJoint joint) =>
        joint.IsValid && Simulation.Solver.ConstraintExists(new ConstraintHandle(joint.Handle));

    private PhysicsJoint Add<T>(BodyReference a, BodyReference b, T constraint) where T : unmanaged, ITwoBodyConstraintDescription<T>
    {
        // A joined pair is woken, so a sleeping body starts obeying its new joint.
        Wake(a);
        Wake(b);
        var handle = Simulation.Solver.Add(a.Handle, b.Handle, constraint).Value;
        // A handle is given out again once its joint went with a destroyed body, whose parts are
        // forgotten here, before the new joint's are kept.
        _hinges.Remove(handle);
        _balls.Remove(handle);
        _sliders.Remove(handle);
        _joined.Remove(handle);
        _joined.Add(handle, a.Handle.Value, b.Handle.Value);
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
