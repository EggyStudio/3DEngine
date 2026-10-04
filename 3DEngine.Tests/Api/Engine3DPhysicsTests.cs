using System.Diagnostics;
using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>Physics through the flat API, in a headless app whose frames run the fixed steps.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class Engine3DPhysicsTests : IDisposable
{
    public Engine3DPhysicsTests()
    {
        var config = Config.Default with { Headless = true, HeadlessFps = 240 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // Runs frames until the condition holds or two seconds of real time pass.
    private static bool RunUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(2))
        {
            BeginDrawing();
            var done = condition();
            EndDrawing();
            if (done) return true;
        }
        return false;
    }

    [Fact]
    public void A_Box_Dropped_On_A_Floor_Lands_And_Reports_The_Contact()
    {
        var floor = CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10));
        var box = CreatePhysicsBox(new Vector3(0, 1.5f, 0), Vector3.One);

        RunUntil(() => IsPhysicsBodyHit(box)).Should().BeTrue("the box falls a unit onto the floor");
        GetPhysicsContacts().Should().Contain(c => (c.BodyA == box && c.BodyB == floor) || (c.BodyA == floor && c.BodyB == box));

        RunUntil(() => GetPhysicsBodyVelocity(box).Length() < 0.01f).Should().BeTrue("and comes to rest");
        GetPhysicsBodyPosition(box).Y.Should().BeApproximately(0.5f, 0.05f, "resting on the floor, half its size above it");
        GetPhysicsBodyTransform(box).Translation.Y.Should().BeApproximately(0.5f, 0.05f);
    }

    [Fact]
    public void A_Capsule_Lands_Upright_And_A_Trigger_It_Falls_Through_Reports_It()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10));
        var gate = CreatePhysicsTrigger(new Vector3(0, 2, 0), new Vector3(3, 0.5f, 3));
        var capsule = CreatePhysicsCapsule(new Vector3(0, 4, 0), 0.4f, 2);

        var entered = false;
        RunUntil(() =>
        {
            foreach (var contact in GetPhysicsContacts())
                if (contact.BodyA == gate || contact.BodyB == gate)
                {
                    entered = true;
                    var toward = contact.BodyA == capsule ? contact.Normal : -contact.Normal;
                    toward.Y.Should().BeGreaterThan(0.5f, "the trigger is under the falling capsule");
                }
            return entered && GetPhysicsBodyVelocity(capsule).Length() < 0.01f && GetPhysicsBodyPosition(capsule).Y < 1.5f;
        }).Should().BeTrue("the capsule falls through the trigger and comes to rest on the floor");
        GetPhysicsBodyPosition(capsule).Y.Should().BeApproximately(1, 0.05f, "two units tall, it stands with its middle a unit up");
    }

    [Fact]
    public void A_Ball_Lands_On_A_Model_Made_Level_Geometry_And_A_Rope_Holds_Another()
    {
        var ground = LoadModelFromMesh(GenMeshPlane(10, 10, 1, 1));
        CreatePhysicsStaticModel(ground, new Vector3(0, 1, 0), 2).IsValid.Should().BeTrue();
        var ball = CreatePhysicsSphere(new Vector3(0, 4, 0), 0.5f);
        var hook = CreatePhysicsKinematicBox(new Vector3(5, 6, 0), new Vector3(0.2f, 0.2f, 0.2f));
        var weight = CreatePhysicsSphere(new Vector3(5, 5, 0), 0.25f);
        var rope = CreatePhysicsDistanceJoint(hook, weight, new Vector3(5, 6, 0), new Vector3(5, 5, 0), 0, 2);

        var rested = RunUntil(() => GetPhysicsBodyVelocity(ball).Length() < 0.01f && GetPhysicsBodyPosition(ball).Y < 2);
        rested.Should().BeTrue("the ball comes to rest on the model");
        GetPhysicsBodyPosition(ball).Y.Should().BeApproximately(1.5f, 0.05f, "on the plane a unit up, its radius above it");
        GetPhysicsBodyPosition(weight).Y.Should().BeApproximately(4, 0.05f, "the rope holds the weight two units under the hook");
        IsPhysicsJointValid(rope).Should().BeTrue();
        DestroyPhysicsJoint(rope);
        IsPhysicsJointValid(rope).Should().BeFalse();
        UnloadModel(ground);
    }

    [Fact]
    public void A_Platform_Under_A_Moving_Parent_Carries_A_Crate_And_A_Character()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        // A carrier entity moved along +X by the program, with a kinematic platform under it.
        var carrier = ecs.Spawn();
        ecs.Add(carrier, new Transform(Vector3.Zero));
        var platform = ecs.Spawn();
        ecs.Add(platform, new Transform(new Vector3(0, 0.25f, 0)));
        ecs.Add(platform, Collider.Box(new Vector3(6, 0.5f, 6)));
        ecs.Add(platform, RigidBody.Kinematic);
        ecs.SetParent(platform, carrier);
        var crate = ecs.Spawn();
        ecs.Add(crate, new Transform(new Vector3(-1.5f, 1, 0)));
        ecs.Add(crate, Collider.Box(Vector3.One));
        ecs.Add(crate, RigidBody.Dynamic());
        var walker = ecs.Spawn();
        ecs.Add(walker, new Transform(new Vector3(1.5f, 1.4f, 0)));
        ecs.Add(walker, Collider.Capsule(0.3f, 1.8f));
        ecs.Add(walker, RigidBody.Dynamic(80));
        ecs.Add(walker, CharacterController.Default);

        // Settled first, then carried along X at two units a second for a second and a half.
        var clock = 0f;
        RunUntil(() => (clock += GetFrameTime()) > 0.5f);
        var crateFrom = ecs.GetReadOnly<Transform>(crate).Position.X;
        var walkerFrom = ecs.GetReadOnly<Transform>(walker).Position.X;
        clock = 0;
        // The crate's pace over the last half second, an average, since a frame slow under load
        // moves the carrier in one step and the crate's speed at any one instant with it.
        float? crateAtSecond = null;
        var carried = 0f;
        RunUntil(() =>
        {
            ecs.GetRef<Transform>(carrier).Position.X += 2 * GetFrameTime();
            clock += GetFrameTime();
            if (clock >= 1 && crateAtSecond is null) (crateAtSecond, carried) = (ecs.GetReadOnly<Transform>(crate).Position.X, clock);
            return clock > 1.5f;
        });
        var pace = (ecs.GetReadOnly<Transform>(crate).Position.X - crateAtSecond!.Value) / (clock - carried);

        var moved = ecs.GetReadOnly<Transform>(carrier).Position.X;
        moved.Should().BeGreaterThan(2.5f);
        var platformAt = GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(platform));
        platformAt.X.Should().BeApproximately(moved, 0.1f, "the platform followed its parent");
        // The crate is carried by friction, so it takes a moment to catch up with a platform that
        // starts at once, and then keeps its pace. The character walks relative to what it stands
        // on, so it keeps the pace from the start.
        pace.Should().BeApproximately(2, 0.1f, "the crate moves with the platform");
        (ecs.GetReadOnly<Transform>(crate).Position.X - crateFrom).Should().BeGreaterThan(moved * 0.6f, "it rode most of the way");
        (ecs.GetReadOnly<Transform>(walker).Position.X - walkerFrom).Should().BeApproximately(moved, 0.15f, "the character rode the whole way");
        ecs.GetReadOnly<Transform>(platform).Position.Should().Be(new Vector3(0, 0.25f, 0), "the platform's own transform is its place under the parent");
    }

    [Fact]
    public void A_Ray_Finds_The_Body_In_Its_Way()
    {
        var box = CreatePhysicsStaticBox(new Vector3(0, 0, -5), Vector3.One);
        BeginDrawing();
        EndDrawing();

        GetRayCollisionPhysics(new Ray(Vector3.Zero, -Vector3.UnitZ), 100, out var hit).Should().BeTrue();
        hit.Body.Should().Be(box);
        hit.Distance.Should().BeApproximately(4.5f, 0.01f, "the box's near face is half a unit before its center");
        GetRayCollisionPhysics(new Ray(Vector3.Zero, Vector3.UnitZ), 100, out _).Should().BeFalse("nothing is behind");
    }

    [Fact]
    public void An_Impulse_Moves_A_Body_And_Destroying_It_Removes_It()
    {
        SetPhysicsGravity(Vector3.Zero);
        var ball = CreatePhysicsSphere(Vector3.Zero, 0.5f, mass: 2);
        ApplyPhysicsImpulse(ball, new Vector3(4, 0, 0));

        GetPhysicsBodyVelocity(ball).X.Should().BeApproximately(2, 0.01f, "an impulse over the mass");
        DestroyPhysicsBody(ball);
        IsPhysicsBodyValid(ball).Should().BeFalse();
    }

    [Fact]
    public void A_Ray_Through_The_Middle_Of_The_Screen_Points_Where_The_Camera_Looks()
    {
        var camera = new Camera3D(new Vector3(0, 2, 10), new Vector3(0, 2, 0), Vector3.UnitY, 45);
        var ray = GetScreenToWorldRayEx(new Vector2(400, 225), camera, 800, 450);

        Vector3.Distance(ray.Direction, -Vector3.UnitZ).Should().BeLessThan(1e-4f);
        var up = GetScreenToWorldRayEx(new Vector2(400, 0), camera, 800, 450);
        up.Direction.Y.Should().BeGreaterThan(0, "the top of the screen is above the middle");
    }

    [Fact]
    public void The_Default_Plugins_Mark_No_Transform_Of_A_Static_Scene()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 2, 8)));
        var sun = ecs.Spawn();
        ecs.Add(sun, Light.Directional(Vector3.One, 1));
        ecs.Add(sun, new Transform(Vector3.Zero));
        var root = ecs.Spawn();
        ecs.Add(root, new Transform(Vector3.Zero));
        var child = ecs.Spawn();
        ecs.Add(child, new Transform(Vector3.UnitX));
        ecs.Add(child, new Mesh([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)]));
        ecs.Add(child, new Material(Vector4.One));
        ecs.SetParent(child, root);

        // A body with no gravity to fall by, which rests where it was made.
        SetPhysicsGravity(Vector3.Zero);
        var resting = ecs.Spawn();
        ecs.Add(resting, new Transform(new Vector3(0, 3, 0)));
        GetApp().World.Resource<PhysicsWorld>().CreateSphere(new Vector3(0, 3, 0), 0.5f, entityId: resting);

        for (int frame = 0; frame < 3; frame++)
        {
            BeginDrawing();
            EndDrawing();
        }

        ecs.AnyChanged<Transform>().Should().BeFalse("nothing in the scene moved, so no system wrote a transform");
        ecs.AnyChanged<GlobalTransform>().Should().BeFalse("and propagation recomputed no chain");
    }

    [Fact]
    public void A_Paused_Simulation_Holds_Every_Body_Still()
    {
        var box = CreatePhysicsBox(new Vector3(0, 5, 0), Vector3.One);
        SetPhysicsPaused(true);
        for (int frame = 0; frame < 20; frame++)
        {
            BeginDrawing();
            EndDrawing();
        }

        IsPhysicsPaused().Should().BeTrue();
        GetPhysicsBodyPosition(box).Y.Should().Be(5, "no step ran while paused");
        SetPhysicsPaused(false);
        RunUntil(() => GetPhysicsBodyPosition(box).Y < 4.9f).Should().BeTrue("it falls once the simulation runs again");
    }

    [Fact]
    public void A_Character_Walked_Through_The_Flat_API_Stops_At_A_Wall_And_Stands()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
        CreatePhysicsStaticBox(new Vector3(2, 1, 0), new Vector3(0.5f, 2, 10));
        var player = CreatePhysicsCharacter(Vector3.Zero, 0.4f, 1.8f);

        MovePhysicsCharacter(player, new Vector3(5, 0, 0));
        RunUntil(() => false);

        IsPhysicsCharacterGrounded(player).Should().BeTrue();
        GetPhysicsBodyPosition(player).X.Should().BeInRange(1.2f, 1.4f, "the wall's face is at 1.75 and the capsule 0.4 wide");
    }

    [Fact]
    public void A_Character_Controller_Component_Walks_Its_Body_And_Reports_Ground()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var physics = GetApp().World.Resource<PhysicsWorld>();
        physics.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
        var player = ecs.Spawn();
        ecs.Add(player, physics.CreateCharacter(new Vector3(0, 0.5f, 0), 0.4f, 1.8f, entityId: player));
        ecs.Add(player, new Transform(Vector3.Zero));
        ecs.Add(player, CharacterController.Default with { Velocity = new Vector3(0, 0, -2) });

        RunUntil(() => ecs.GetReadOnly<Transform>(player).Position.Z < -1).Should().BeTrue("the controller walks it along -Z");
        ecs.GetReadOnly<CharacterController>(player).Grounded.Should().BeTrue("and it stands on the floor");
    }

    [Fact]
    public void A_Flat_Walk_Of_A_Character_With_A_Controller_Goes_Through_The_Controller()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
        var player = ecs.Spawn();
        ecs.Add(player, new Transform(new Vector3(0, 0.9f, 0)));
        ecs.Add(player, Collider.Capsule(0.4f, 1.8f));
        ecs.Add(player, RigidBody.Dynamic(80));
        ecs.Add(player, CharacterController.Default);
        BeginDrawing();
        EndDrawing();
        var body = ecs.GetReadOnly<PhysicsBody>(player);

        MovePhysicsCharacter(body, new Vector3(3, 0, 0));

        ecs.GetReadOnly<CharacterController>(player).Velocity.Should().Be(new Vector3(3, 0, 0));
        RunUntil(() => GetPhysicsBodyPosition(body).X > 1).Should().BeTrue("the step walks it as the controller says");
    }

    [Fact]
    public void A_Character_Controller_Sets_Its_Step_Height_And_Crouches_And_Stands_By_Its_Height()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var physics = GetApp().World.Resource<PhysicsWorld>();
        physics.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
        // A step 0.6 high, higher than either character's radius of 0.4.
        physics.CreateStaticBox(new Vector3(3, 0.3f, 0), new Vector3(1, 0.3f, 10));
        int Character(float z, float stepHeight)
        {
            var entity = ecs.Spawn();
            ecs.Add(entity, physics.CreateCharacter(new Vector3(0, 0, z), 0.4f, 1.8f, entityId: entity));
            ecs.Add(entity, new Transform(new Vector3(0, 0.9f, z)));
            ecs.Add(entity, CharacterController.Default with { Velocity = new Vector3(2, 0, 0), StepHeight = stepHeight });
            return entity;
        }
        var low = Character(-2, 0);
        var high = Character(2, 0.7f);

        RunUntil(() => ecs.GetReadOnly<Transform>(high).Position.X > 2.5f).Should().BeTrue("the higher step height climbs onto the step");
        ecs.GetReadOnly<Transform>(high).Position.Y.Should().BeGreaterThan(1.4f, "and stands on it");
        ecs.GetReadOnly<Transform>(low).Position.X.Should().BeLessThan(1.7f, "the radius alone does not climb it");

        ecs.GetRef<CharacterController>(low).Velocity = Vector3.Zero;
        ecs.GetRef<CharacterController>(low).Height = 1;
        RunUntil(() => MathF.Abs(GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(low)).Y - 0.5f) < 0.02f)
            .Should().BeTrue("crouched to a unit, its middle is half a unit over its feet");
        ecs.GetRef<CharacterController>(low).Height = 1.8f;
        RunUntil(() => MathF.Abs(GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(low)).Y - 0.9f) < 0.02f)
            .Should().BeTrue("standing again, it is as tall as it was made");
    }
}
