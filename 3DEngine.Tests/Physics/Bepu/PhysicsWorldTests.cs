using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

/// <summary>
/// Integration tests for the concrete <see cref="PhysicsWorld"/>: shape creation, stepping,
/// queries, and ECS transform sync.
/// </summary>
[Trait("Category", "Integration")]
public class PhysicsWorldTests
{
    private static PhysicsWorld NewWorld(Action<PhysicsSettings>? configure = null)
    {
        var s = new PhysicsSettings
        {
            UseFixedTimestep = true,
            FixedTimeStep = 1f / 60f,
        };
        configure?.Invoke(s);
        return new PhysicsWorld(s);
    }

    [Fact]
    public void Gravity_Defaults_From_Settings_And_Is_Mutable()
    {
        using var w = NewWorld();

        w.Gravity.Should().Be(new Vector3(0f, -9.81f, 0f));

        w.Gravity = new Vector3(0f, -20f, 0f);

        w.Gravity.Y.Should().Be(-20f);
    }

    [Fact]
    public void CreateSphere_Produces_Existing_Dynamic_Body_At_Position()
    {
        using var w = NewWorld();

        var body = w.CreateSphere(new Vector3(1, 2, 3), radius: 0.5f, mass: 1f);

        body.Kind.Should().Be(BodyKind.Dynamic);
        body.IsValid.Should().BeTrue();
        body.Position.Should().Be(new Vector3(1, 2, 3));
        body.IsAwake.Should().BeTrue();
    }

    [Fact]
    public void CreateStaticBox_Produces_Existing_Static_Body()
    {
        using var w = NewWorld();

        var body = w.CreateStaticBox(Vector3.Zero, new Vector3(1, 1, 1));

        body.Kind.Should().Be(BodyKind.Static);
        body.IsValid.Should().BeTrue();
        body.Velocity.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void CreateKinematicCapsule_Produces_Existing_Kinematic_Body()
    {
        using var w = NewWorld();

        var body = w.CreateKinematicCapsule(Vector3.Zero, 0.5f, 1f);

        body.Kind.Should().Be(BodyKind.Kinematic);
        body.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Destroy_Marks_Body_Invalid()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(Vector3.Zero, 0.5f);

        body.Destroy();

        body.IsValid.Should().BeFalse();
    }

    // Bepu marks a body that has rested long enough as a candidate to sleep as a step ends, and puts
    // a candidate to sleep as the next begins, so a body given speed in between keeps it only where
    // the candidacy is cleared with the speed.
    [Theory]
    [InlineData("velocity")]
    [InlineData("impulse")]
    [InlineData("angular velocity")]
    [InlineData("angular impulse")]
    public void A_Crate_About_To_Sleep_On_A_Floor_Moves_With_The_Speed_It_Is_Given(string given)
    {
        using var w = NewWorld();
        w.CreateGroundPlane();
        var crate = w.CreateBox(new Vector3(0, 0.5f, 0), new Vector3(0.5f));
        var activity = () => w.Simulation.Bodies.GetBodyReference(new BepuPhysics.BodyHandle(crate.Handle)).Activity;
        for (int i = 0; i < 600 && !activity().SleepCandidate; i++) w.Step(1f / 60f);
        activity().SleepCandidate.Should().BeTrue("a crate at rest for the steps the sleep threshold asks is a candidate to sleep");
        crate.IsAwake.Should().BeTrue("a candidate is put to sleep as the next step begins");
        var resting = crate.Position;

        switch (given)
        {
            case "velocity": crate.SetLinearVelocity(new Vector3(3, 0, 0)); break;
            case "impulse": crate.ApplyImpulse(new Vector3(3, 0, 0)); break;
            case "angular velocity": crate.SetAngularVelocity(new Vector3(0, 3, 0)); break;
            case "angular impulse": crate.ApplyAngularImpulse(new Vector3(0, 3, 0)); break;
        }
        w.Step(1f / 60f);

        crate.IsAwake.Should().BeTrue($"a crate given {given} is no longer at rest");
        if (given.StartsWith("angular")) crate.AngularVelocity.Y.Should().BeGreaterThan(1f, "it turns in the next step");
        else crate.Position.X.Should().BeGreaterThan(resting.X + 0.01f, "it moves in the next step");
    }

    [Fact]
    public void Set_LinearVelocity_Wakes_Body_And_Persists_Across_Step()
    {
        using var w = NewWorld(s => s.Gravity = Vector3.Zero);
        var body = w.CreateSphere(Vector3.Zero, 0.5f);
        body.Sleep();

        body.SetLinearVelocity(new Vector3(0, 0, 1));

        body.IsAwake.Should().BeTrue();
        body.Velocity.Z.Should().BeApproximately(1f, 1e-3f);

        w.Step(1f / 60f);

        body.Position.Z.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void Gravity_Pulls_Dynamic_Body_Down_Over_Time()
    {
        using var w = NewWorld();
        var body = w.CreateSphere(new Vector3(0, 10, 0), 0.5f);

        for (int i = 0; i < 30; i++)
            w.Step(1f / 60f);

        body.Position.Y.Should().BeLessThan(10f);
        body.Velocity.Y.Should().BeLessThan(0f);
    }

    [Fact]
    public void Static_Sphere_Is_Hit_By_Raycast()
    {
        using var w = NewWorld();
        w.CreateStaticSphere(new Vector3(0, 0, 5), radius: 1f);
        w.Step(1f / 60f); // build broad-phase tree

        var hit = w.Raycast(Vector3.Zero, new Vector3(0, 0, 1), 100f, out var info);

        hit.Should().BeTrue();
        info.Distance.Should().BeApproximately(4f, 1e-2f);
        info.Body.Kind.Should().Be(BodyKind.Static);
    }

    [Fact]
    public void Raycast_Returns_False_When_Nothing_In_Path()
    {
        using var w = NewWorld();
        w.Step(1f / 60f);

        var hit = w.Raycast(new Vector3(0, 100, 0), new Vector3(1, 0, 0), 10f, out _);

        hit.Should().BeFalse();
    }

    [Fact]
    public void Raycast_Reports_Owning_EntityId()
    {
        using var w = NewWorld();
        w.CreateStaticSphere(new Vector3(0, 0, 5), 1f, entityId: 42);
        w.Step(1f / 60f);

        w.Raycast(Vector3.Zero, new Vector3(0, 0, 1), 100f, out var info).Should().BeTrue();

        info.EntityId.Should().Be(42);
    }

    [Fact]
    public void SyncTransforms_Writes_Body_Pose_Into_Matching_Transform_Component()
    {
        using var w = NewWorld(s => s.Gravity = Vector3.Zero);
        var ecs = new EcsWorld();
        int e = ecs.Spawn();
        ecs.Add(e, new Transform(Vector3.Zero));

        var body = w.CreateSphere(new Vector3(1, 2, 3), 0.5f, entityId: e);

        w.SyncTransforms(ecs);

        ecs.GetRef<Transform>(e).Position.Should().Be(new Vector3(1, 2, 3));
        body.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SyncTransforms_Skips_Entities_Without_Transform_Component()
    {
        using var w = NewWorld();
        var ecs = new EcsWorld();
        int e = ecs.Spawn();
        w.CreateSphere(Vector3.Zero, 0.5f, entityId: e);

        var act = () => w.SyncTransforms(ecs);

        act.Should().NotThrow();
    }

    [Fact]
    public void Fixed_Timestep_Steps_Through_All_The_Time_Given()
    {
        using var w = NewWorld(s =>
        {
            s.UseFixedTimestep = true;
            s.FixedTimeStep = 1f / 60f;
        });
        var body = w.CreateSphere(new Vector3(0, 100, 0), 0.5f);

        // A quarter of a second, the most a frame reports, is fifteen steps of falling.
        w.Step(0.25f);

        w.GetLinearVelocity(body).Y.Should().BeApproximately(-9.81f * 0.25f, 0.01f, "every step of the quarter second fell");
    }
}

