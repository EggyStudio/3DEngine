using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

[Trait("Category", "Integration")]
public class PhysicsBodiesTests
{
    private static World NewWorld()
    {
        var world = new World();
        world.InsertResource(new EcsWorld());
        world.InsertResource(new PhysicsWorld(new PhysicsSettings()));
        return world;
    }

    [Fact]
    public void A_Scene_File_Describes_Its_Bodies_And_Loading_It_Makes_Them()
    {
        var authoring = NewWorld();
        var ecs = authoring.Resource<EcsWorld>();
        var floor = ecs.Spawn();
        ecs.Add(floor, new Transform(new Vector3(0, -0.5f, 0)));
        ecs.Add(floor, Collider.Box(new Vector3(10, 1, 10)));
        ecs.Add(floor, RigidBody.Static);
        var crate = ecs.Spawn();
        ecs.Add(crate, new Transform(new Vector3(1, 3, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f), Vector3.One));
        ecs.Add(crate, Collider.Box(Vector3.One));
        ecs.Add(crate, RigidBody.Dynamic(2));
        var json = SceneFile.Write(ecs);

        authoring.Resource<PhysicsWorld>().Dispose();
        json.Should().Contain("\"Collider\"").And.Contain("\"RigidBody\"").And.Contain("\"Dynamic\"");

        var loaded = NewWorld();
        var spawned = SceneFile.Read(loaded, json);
        PhysicsBodies.Run(loaded);

        var loadedEcs = loaded.Resource<EcsWorld>();
        var physics = loaded.Resource<PhysicsWorld>();
        var bodies = spawned.Select(e => loadedEcs.GetReadOnly<PhysicsBody>(e)).ToArray();
        bodies.Select(b => b.Kind).Should().BeEquivalentTo([BodyKind.Static, BodyKind.Dynamic]);
        var dynamicBody = bodies.Single(b => b.Kind == BodyKind.Dynamic);
        physics.GetPosition(dynamicBody).Should().Be(new Vector3(1, 3, 0));
        Quaternion.Dot(physics.GetRotation(dynamicBody), Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f)).Should().BeApproximately(1, 1e-4f);
        physics.Dispose();
    }

    [Fact]
    public void A_Body_Made_For_An_Entity_Is_Destroyed_With_It()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var physics = world.Resource<PhysicsWorld>();
        var ball = ecs.Spawn();
        ecs.Add(ball, new Transform(Vector3.Zero));
        ecs.Add(ball, Collider.Sphere(0.5f));
        ecs.Add(ball, RigidBody.Dynamic());
        PhysicsBodies.Run(world);
        var body = ecs.GetReadOnly<PhysicsBody>(ball);
        body.IsValid.Should().BeTrue();

        ecs.Despawn(ball);
        PhysicsBodies.Run(world);
        body.IsValid.Should().BeFalse("its entity is gone");
        physics.Dispose();
    }

    [Fact]
    public void A_Capsule_Beside_A_Character_Controller_Is_A_Character()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var physics = world.Resource<PhysicsWorld>();
        var player = ecs.Spawn();
        ecs.Add(player, new Transform(new Vector3(0, 0.9f, 0)));
        ecs.Add(player, Collider.Capsule(0.4f, 1.8f));
        ecs.Add(player, RigidBody.Dynamic(80));
        ecs.Add(player, CharacterController.Default);
        PhysicsBodies.Run(world);

        var body = ecs.GetReadOnly<PhysicsBody>(player);
        physics.GetPosition(body).Y.Should().BeApproximately(0.9f, 1e-4f, "its middle is where the transform says");
        physics.MoveCharacter(body, Vector3.UnitX);
        physics.IsCharacterGrounded(body).Should().BeFalse("it has not stepped yet");
        physics.Dispose();
    }
}
