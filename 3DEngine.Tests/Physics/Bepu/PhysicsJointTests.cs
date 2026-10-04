using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Physics;

[Trait("Category", "Integration")]
public class PhysicsJointTests
{
    private const float Step = 1f / 60f;

    private static PhysicsWorld NewWorld() =>
        new(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = Step, MaxStepsPerFrame = 64, Gravity = new Vector3(0, -9.81f, 0) });

    private static void Run(PhysicsWorld world, int steps)
    {
        for (int i = 0; i < steps; i++) world.StepOnce(Step);
    }

    [Fact]
    public void A_Ball_Joint_Swings_A_Body_On_A_Fixed_Arm()
    {
        using var world = NewWorld();
        var anchor = world.CreateKinematicBox(new Vector3(0, 5, 0), new Vector3(0.1f));
        var bob = world.CreateSphere(new Vector3(2, 5, 0), 0.25f);
        var joint = world.CreateBallJoint(anchor, bob, new Vector3(0, 5, 0));

        Run(world, 30);

        var at = world.GetPosition(bob);
        at.Y.Should().BeLessThan(4.5f, "it swings down");
        Vector3.Distance(at, new Vector3(0, 5, 0)).Should().BeApproximately(2, 0.05f, "on an arm as long as it started");
        world.JointExists(joint).Should().BeTrue();
    }

    [Fact]
    public void A_Hinge_Lets_A_Door_Turn_Only_Around_Its_Axis()
    {
        using var world = new PhysicsWorld(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = Step, Gravity = Vector3.Zero });
        var post = world.CreateKinematicBox(Vector3.Zero, new Vector3(0.05f, 1, 0.05f));
        // Clear of the post, so only the hinge holds it, with the hinge at the post's edge.
        var door = world.CreateBox(new Vector3(0.6f, 0, 0), new Vector3(0.5f, 1, 0.05f));
        var hinge = new Vector3(0.05f, 0, 0);
        world.CreateHingeJoint(post, door, hinge, Vector3.UnitY);
        world.ApplyImpulse(door, new Vector3(0, 2, 4), new Vector3(0.5f, 0.5f, 0));

        Run(world, 30);

        var up = Vector3.Transform(Vector3.UnitY, world.GetRotation(door));
        up.Y.Should().BeGreaterThan(0.99f, "the door's up stays up, however it swings");
        var at = world.GetPosition(door);
        Vector3.Distance(at, new Vector3(0.6f, 0, 0)).Should().BeGreaterThan(0.1f, "pushed, it swings around the post");
        at.Y.Should().BeApproximately(0, 0.02f, "and does not rise along the axis");
        new Vector2(at.X - hinge.X, at.Z - hinge.Z).Length().Should().BeApproximately(0.55f, 0.02f, "its middle stays as far from the hinge");
    }

    [Fact]
    public void A_Weld_Keeps_Two_Bodies_Placed_As_They_Were_And_A_Destroyed_Body_Takes_Its_Joints()
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 0.5f, 10));
        var a = world.CreateBox(new Vector3(0, 3, 0), new Vector3(0.5f));
        var b = world.CreateBox(new Vector3(1.5f, 3, 0), new Vector3(0.5f));
        var weld = world.CreateWeldJoint(a, b);

        Run(world, 120);

        Vector3.Distance(world.GetPosition(a), world.GetPosition(b)).Should().BeApproximately(1.5f, 0.05f, "they fell and landed as one");
        world.Destroy(b);
        world.JointExists(weld).Should().BeFalse("the joint went with the body");
        world.DestroyJoint(weld);
    }

    [Fact]
    public void A_Distance_Joint_Holds_A_Falling_Body_On_A_Rope()
    {
        using var world = NewWorld();
        var hook = world.CreateKinematicBox(new Vector3(0, 10, 0), new Vector3(0.1f));
        var weight = world.CreateSphere(new Vector3(0, 9, 0), 0.25f);
        world.CreateDistanceJoint(hook, weight, new Vector3(0, 10, 0), new Vector3(0, 9, 0), 0, 3);

        Run(world, 120);

        world.GetPosition(weight).Y.Should().BeApproximately(7, 0.05f, "the rope stops it three units under the hook");
    }

    // The angle a door has swung about the vertical through the hinge, from where it was made along +X.
    private static float DoorAngle(PhysicsWorld world, PhysicsBody door, Vector3 hinge)
    {
        var at = world.GetPosition(door) - hinge;
        return float.RadiansToDegrees(MathF.Atan2(-at.Z, at.X));
    }

    [Fact]
    public void A_Limited_Hinge_Stays_Between_Its_Angles_However_Hard_It_Is_Pushed()
    {
        using var world = new PhysicsWorld(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = Step, Gravity = Vector3.Zero });
        var post = world.CreateKinematicBox(Vector3.Zero, new Vector3(0.05f, 1, 0.05f));
        var door = world.CreateBox(new Vector3(0.6f, 0, 0), new Vector3(0.5f, 1, 0.05f));
        var hinge = new Vector3(0.05f, 0, 0);
        var joint = world.CreateHingeJoint(post, door, hinge, Vector3.UnitY);
        world.SetHingeLimit(joint, 0, float.DegreesToRadians(45));

        // Pushed toward -Z, which turns it the positive way about +Y, hard enough to go round several
        // times. With nothing to slow it, it rings between its limits, and leaves them only by the
        // little a joint's spring gives.
        world.ApplyImpulse(door, new Vector3(0, 0, -3), new Vector3(0.5f, 0, 0));
        float least = float.MaxValue, most = float.MinValue;
        for (int i = 0; i < 120; i++)
        {
            world.StepOnce(Step);
            var angle = DoorAngle(world, door, hinge);
            (least, most) = (MathF.Min(least, angle), MathF.Max(most, angle));
        }
        most.Should().BeInRange(40, 50, "it swings out to 45 degrees");
        least.Should().BeGreaterThan(-3, "and back no further than where it was made");
    }

    [Fact]
    public void A_Motor_Turns_A_Hinge_At_Its_Speed()
    {
        using var world = new PhysicsWorld(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = Step, Gravity = Vector3.Zero });
        var axle = world.CreateKinematicBox(Vector3.Zero, new Vector3(0.05f));
        var wheel = world.CreateBox(Vector3.Zero, new Vector3(1, 1, 0.2f));
        var joint = world.CreateHingeJoint(axle, wheel, Vector3.Zero, Vector3.UnitZ);
        world.SetHingeMotor(joint, MathF.PI / 2, 1000);

        Run(world, 60);

        var spin = world.GetAngularVelocity(wheel);
        spin.Z.Should().BeApproximately(MathF.PI / 2, 0.05f, "a quarter turn a second, counterclockwise about the axis");
        new Vector2(spin.X, spin.Y).Length().Should().BeLessThan(0.01f, "only around the hinge's axis");
        world.ClearHingeLimitAndMotor(joint);
    }

    [Fact]
    public void A_Joint_Holds_Only_Bodies_That_Move()
    {
        using var world = NewWorld();
        var floor = world.CreateStaticBox(Vector3.Zero, Vector3.One);
        var ball = world.CreateSphere(new Vector3(0, 3, 0), 0.5f);
        var make = () => world.CreateBallJoint(floor, ball, Vector3.Zero);
        make.Should().Throw<ArgumentException>();
    }
}
