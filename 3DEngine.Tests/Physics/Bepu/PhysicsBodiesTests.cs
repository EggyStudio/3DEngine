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
    public void A_Colliders_Layer_Goes_Through_A_Scene_File_Onto_Its_Body()
    {
        var authoring = NewWorld();
        var ecs = authoring.Resource<EcsWorld>();
        var ghost = ecs.Spawn();
        ecs.Add(ghost, new Transform(Vector3.Zero));
        ecs.Add(ghost, Collider.Sphere(0.5f) with { Layer = 4 });
        ecs.Add(ghost, RigidBody.Dynamic());
        var json = SceneFile.Write(ecs);
        authoring.Resource<PhysicsWorld>().Dispose();
        json.Should().Contain("\"Layer\": 4");

        var loaded = NewWorld();
        var spawned = SceneFile.Read(loaded, json);
        PhysicsBodies.Run(loaded);
        var physics = loaded.Resource<PhysicsWorld>();
        physics.GetLayer(loaded.Resource<EcsWorld>().GetReadOnly<PhysicsBody>(spawned.Single())).Should().Be(4);
        physics.Dispose();
    }

    [Fact]
    public void A_Rigid_Bodys_Continuous_Goes_Through_A_Scene_File_Onto_Its_Body()
    {
        var authoring = NewWorld();
        var ecs = authoring.Resource<EcsWorld>();
        var ball = ecs.Spawn();
        ecs.Add(ball, new Transform(Vector3.Zero));
        ecs.Add(ball, Collider.Sphere(0.1f));
        ecs.Add(ball, RigidBody.Dynamic() with { Continuous = true });
        var json = SceneFile.Write(ecs);
        authoring.Resource<PhysicsWorld>().Dispose();

        var loaded = NewWorld();
        var spawned = SceneFile.Read(loaded, json);
        PhysicsBodies.Run(loaded);
        var physics = loaded.Resource<PhysicsWorld>();
        var body = loaded.Resource<EcsWorld>().GetReadOnly<PhysicsBody>(spawned.Single());
        physics.Simulation.Bodies[new BepuPhysics.BodyHandle(body.Handle)].Collidable.Continuity.Mode
            .Should().Be(BepuPhysics.Collidables.ContinuousDetectionMode.Continuous);
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

    // A square of two triangles facing up, from -1 to 1 on X and Z, wound as a model winds them.
    private static readonly Vector3[] Square =
    [
        new(-1, 0, -1), new(-1, 0, 1), new(1, 0, 1),
        new(-1, 0, -1), new(1, 0, 1), new(1, 0, -1),
    ];

    [Fact]
    public void A_Mesh_Collider_Is_The_Shape_Of_The_Mesh_As_Its_Entity_Scales_It()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var physics = world.Resource<PhysicsWorld>();
        var floor = ecs.Spawn();
        ecs.Add(floor, new Transform(new Vector3(0, 1, 0), Quaternion.Identity, new Vector3(10, 1, 10)));
        ecs.Add(floor, new Mesh(Square));
        ecs.Add(floor, Collider.Mesh);
        ecs.Add(floor, RigidBody.Dynamic());
        var ball = physics.CreateSphere(new Vector3(6, 3, -6), 0.5f);

        PhysicsBodies.Run(world);
        for (int i = 0; i < 120; i++) physics.StepOnce(1f / 60);

        ecs.GetReadOnly<PhysicsBody>(floor).Kind.Should().Be(BodyKind.Static, "a mesh never moves");
        physics.GetPosition(ball).Y.Should().BeApproximately(1.5f, 0.05f, "it rests on the floor, scaled to reach it, a unit up");
        physics.Dispose();
    }

    [Fact]
    public void A_Mesh_Collider_Waits_For_The_Meshes_Under_Its_Entity()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var level = ecs.Spawn();
        ecs.Add(level, new Transform(Vector3.Zero));
        ecs.Add(level, Collider.Mesh);
        ecs.Add(level, RigidBody.Static);

        PhysicsBodies.Run(world);
        ecs.Has<PhysicsBody>(level).Should().BeFalse("nothing under it has a mesh yet, as a model still loading has not");

        var room = ecs.Spawn();
        ecs.Add(room, new Transform(new Vector3(0, 2, 0)));
        ecs.Add(room, new Mesh(Square));
        ecs.SetParent(room, level);
        PhysicsBodies.Run(world);

        ecs.Has<PhysicsBody>(level).Should().BeTrue("its descendant's mesh shapes it");
        world.Resource<PhysicsWorld>().Raycast(new Vector3(0.5f, 5, 0.5f), -Vector3.UnitY, 10, out var hit).Should().BeTrue();
        hit.Point.Y.Should().BeApproximately(2, 1e-3f, "the child's mesh is where the child places it");
        world.Resource<PhysicsWorld>().Dispose();
    }

    [Fact]
    public void A_Scene_Files_Hinge_Hangs_Its_Door_Where_The_Joint_Entity_Stands_And_Keeps_Its_Limits()
    {
        var authoring = new EcsWorld();
        var post = authoring.Spawn();
        authoring.Add(post, new Transform(Vector3.Zero));
        authoring.Add(post, Collider.Box(new Vector3(0.1f, 2, 0.1f)));
        authoring.Add(post, RigidBody.Kinematic);
        var door = authoring.Spawn();
        authoring.Add(door, new Transform(new Vector3(0.6f, 0, 0)));
        authoring.Add(door, Collider.Box(new Vector3(1, 2, 0.1f)));
        authoring.Add(door, RigidBody.Dynamic());
        var hinge = authoring.Spawn();
        authoring.Add(hinge, new Transform(new Vector3(0.05f, 0, 0)));
        authoring.Add(hinge, new Joint { Kind = JointKind.Hinge, A = authoring.Handle(post), B = authoring.Handle(door), MinAngle = -45, MaxAngle = 45 });
        var json = SceneFile.Write(authoring);

        var world = new World();
        world.InsertResource(new EcsWorld());
        using var physics = new PhysicsWorld(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = 1f / 60, Gravity = Vector3.Zero });
        world.InsertResource(physics);
        var spawned = SceneFile.Read(world, json);
        PhysicsBodies.Run(world);
        var ecs = world.Resource<EcsWorld>();

        var made = ecs.GetReadOnly<PhysicsJoint>(spawned[2]);
        physics.JointExists(made).Should().BeTrue("the joint is made once both bodies are");
        var doorBody = ecs.GetReadOnly<PhysicsBody>(spawned[1]);
        physics.ApplyImpulse(doorBody, new Vector3(0, 0, 6), new Vector3(0.5f, 0, 0));
        var widest = 0f;
        for (int i = 0; i < 120; i++)
        {
            physics.StepOnce(1f / 60);
            var at = physics.GetPosition(doorBody) - new Vector3(0.05f, 0, 0);
            widest = MathF.Max(widest, MathF.Abs(float.RadiansToDegrees(MathF.Atan2(-at.Z, at.X))));
            at.Y.Should().BeApproximately(0, 0.05f, "it turns about the hinge's up axis, not along it");
        }
        widest.Should().BeInRange(30, 55, "pushed, it swings to its limit of 45, past it by what one step of the soft limit allows, and no further");

        ecs.Despawn(spawned[2]);
        PhysicsBodies.Run(world);
        physics.JointExists(made).Should().BeFalse("the joint goes with its entity");
    }

    [Fact]
    public void A_Joint_That_Cannot_Be_Made_Is_Refused_Once()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var floor = ecs.Spawn();
        ecs.Add(floor, new Transform(Vector3.Zero));
        ecs.Add(floor, Collider.Box(Vector3.One));
        ecs.Add(floor, RigidBody.Static);
        var ball = ecs.Spawn();
        ecs.Add(ball, new Transform(new Vector3(0, 2, 0)));
        ecs.Add(ball, Collider.Sphere(0.5f));
        ecs.Add(ball, RigidBody.Dynamic());
        var joint = ecs.Spawn();
        ecs.Add(joint, new Transform(Vector3.Zero));
        ecs.Add(joint, new Joint { Kind = JointKind.Ball, A = ecs.Handle(floor), B = ecs.Handle(ball) });

        PhysicsBodies.Run(world);
        PhysicsBodies.Run(world);

        ecs.GetReadOnly<PhysicsJoint>(joint).IsValid.Should().BeFalse("a static body cannot be joined, and the joint is marked so it is not tried again");
        world.Resource<PhysicsWorld>().Dispose();
    }
}
