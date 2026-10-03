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
