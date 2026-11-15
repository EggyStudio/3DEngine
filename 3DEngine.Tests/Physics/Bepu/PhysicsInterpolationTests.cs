using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Physics.Bepu;

[Trait("Category", "Unit")]
public class PhysicsInterpolationTests
{
    // A body with no gravity moving at one unit a step along X, from x = 0.
    private static (PhysicsWorld World, EcsWorld Ecs, int Entity, PhysicsBody Body) Moving()
    {
        var world = new PhysicsWorld(new PhysicsSettings { Gravity = Vector3.Zero });
        var ecs = new EcsWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Transform(Vector3.Zero));
        var body = world.CreateKinematicSphere(Vector3.Zero, 0.5f, entityId: entity);
        world.SetLinearVelocity(body, new Vector3(60, 0, 0));
        return (world, ecs, entity, body);
    }

    [Fact]
    public void A_Body_Is_Drawn_Between_Its_Poses_Before_And_After_The_Last_Step()
    {
        var (world, ecs, entity, _) = Moving();
        world.StepOnce(1f / 60f);
        world.StepOnce(1f / 60f);

        world.SyncTransforms(ecs, alpha: 0.5f);
        ecs.GetRef<Transform>(entity).Position.X.Should().BeApproximately(1.5f, 1e-3f);

        world.SyncTransforms(ecs);
        ecs.GetRef<Transform>(entity).Position.X.Should().BeApproximately(2f, 1e-3f);
        world.Dispose();
    }

    [Fact]
    public void A_Body_No_Entity_Holds_As_The_Flat_API_Makes_It_Is_Drawn_Between_Its_Last_Two_Steps()
    {
        // A box as CreatePhysicsBox makes it, with no entity, moving at about a unit a step.
        var world = new PhysicsWorld(new PhysicsSettings { Gravity = Vector3.Zero });
        var body = world.CreateBox(Vector3.Zero, new Vector3(0.5f));
        world.SetLinearVelocity(body, new Vector3(60, 0, 0));
        world.StepOnce(1f / 60f);
        var before = world.GetPosition(body);
        world.StepOnce(1f / 60f);
        var after = world.GetPosition(body);

        after.X.Should().BeGreaterThan(before.X + 0.5f, "the box moves about a unit a step");
        world.GetPose(body, alpha: 0.5f).Position.X.Should().BeApproximately((before.X + after.X) / 2, 1e-3f,
            "a body no entity holds is blended from its pose before the last step as an entity's is");
        world.GetPose(body, alpha: 1f).Position.X.Should().BeApproximately(after.X, 1e-6f);
        world.Dispose();
    }

    [Fact]
    public void A_Teleported_Body_Is_Not_Blended_Into()
    {
        var (world, ecs, entity, body) = Moving();
        world.StepOnce(1f / 60f);

        world.SetPosition(body, new Vector3(50, 0, 0));
        world.SyncTransforms(ecs, alpha: 0.1f);

        ecs.GetRef<Transform>(entity).Position.X.Should().Be(50);
        world.Dispose();
    }
}
