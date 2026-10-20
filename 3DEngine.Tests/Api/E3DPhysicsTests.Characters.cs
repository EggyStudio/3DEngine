using System.Diagnostics;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

public sealed partial class E3DPhysicsTests
{
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

    [Fact]
    public void A_Flying_Character_Holds_Its_Height_Is_Stopped_By_A_Wall_And_A_Ceiling_And_Lands_When_The_Flight_Ends()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var physics = GetApp().World.Resource<PhysicsWorld>();
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
        // A wall six units high whose face is at x 1.75, and a ceiling whose underside is 4 up.
        CreatePhysicsStaticBox(new Vector3(2, 3, 0), new Vector3(0.5f, 6, 4));
        CreatePhysicsStaticBox(new Vector3(-5, 4.5f, 0), new Vector3(3, 1, 3));
        // Far past the floor's edge, over nothing at all.
        var hovering = CreatePhysicsCharacter(new Vector3(30, 5, 0), 0.4f, 1.8f);
        var walled = CreatePhysicsCharacter(new Vector3(0, 1, 0), 0.4f, 1.8f);
        // Flown through its controller.
        var roofed = ecs.Spawn();
        ecs.Add(roofed, physics.CreateCharacter(new Vector3(-5, 0, 0), 0.4f, 1.8f, entityId: roofed));
        ecs.Add(roofed, new Transform(new Vector3(-5, 0.9f, 0)));
        ecs.Add(roofed, CharacterController.Default with { Fly = true, Velocity = new Vector3(0, 3, 0) });

        SetPhysicsCharacterFly(hovering, true);
        SetPhysicsCharacterFly(walled, true);
        MovePhysicsCharacter(hovering, new Vector3(0, 2, 0));
        MovePhysicsCharacter(walled, new Vector3(5, 0, 0));
        Frames(60);
        MovePhysicsCharacter(hovering, Vector3.Zero);
        var risen = GetPhysicsBodyPosition(hovering).Y;
        Frames(60);

        risen.Should().BeApproximately(7.9f, 0.1f, "a second up at two units a second from its middle at 5.9");
        GetPhysicsBodyPosition(hovering).Y.Should().BeApproximately(risen, 0.01f, "asked for nothing it holds its height for a second with gravity held off");
        IsPhysicsCharacterGrounded(hovering).Should().BeFalse();
        GetPhysicsBodyPosition(walled).X.Should().BeInRange(1.2f, 1.4f, "the wall's face stops it as it stops a walk");
        GetPhysicsBodyPosition(walled).Y.Should().BeApproximately(1.9f, 0.02f, "at the height it flew in at");
        GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(roofed)).Y.Should().BeInRange(3.0f, 3.12f, "its head stopped at the ceiling's underside");

        SetPhysicsCharacterFly(walled, false);
        MovePhysicsCharacter(walled, Vector3.Zero);
        ecs.GetRef<CharacterController>(roofed) = CharacterController.Default;
        RunUntil(() => IsPhysicsCharacterGrounded(walled) && ecs.GetReadOnly<CharacterController>(roofed).Grounded
                       && GetPhysicsBodyVelocity(walled).Length() < 0.05f)
            .Should().BeTrue("with the flight ended each falls to the floor and stands on it");
        GetPhysicsBodyPosition(walled).Y.Should().BeApproximately(0.9f, 0.03f);
        GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(roofed)).Y.Should().BeApproximately(0.9f, 0.03f);
    }

    [Fact]
    public void A_Vehicle_Settles_On_Its_Springs_Drives_Forward_Turns_Left_And_Brakes_To_A_Stop()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(400, 1, 400));
        var car = CreatePhysicsVehicle(new Vector3(0, 1.2f, 0), new Vector3(1.8f, 0.6f, 3.8f));
        // Frames of a sixtieth of a second, a step each.
        void Steps(int sixtieths) => Frames(sixtieths);

        // At rest it hangs on its four springs, every wheel on the ground, level.
        Steps(120);
        GetPhysicsBodyVelocity(car).Length().Should().BeLessThan(0.05f, "the springs and dampers settle");
        GetPhysicsVehicleWheels(car).Should().HaveCount(4).And.OnlyContain(w => w.Grounded);
        var restY = GetPhysicsBodyPosition(car).Y;
        restY.Should().BeInRange(0.6f, 1.2f, "held up by its wheels, not lying on the ground");
        Vector3.Transform(Vector3.UnitY, GetPhysicsBodyRotation(car)).Y.Should().BeGreaterThan(0.999f);

        // Throttle drives it the way it faces, -Z.
        SetPhysicsVehicleInput(car, 1, 0);
        Steps(120);
        GetPhysicsBodyPosition(car).Z.Should().BeLessThan(-5, "it drove forward");
        GetPhysicsBodyVelocity(car).Z.Should().BeLessThan(-5);

        // Steering left turns its heading toward -X.
        SetPhysicsVehicleInput(car, 0.5f, 1);
        Steps(60);
        Vector3.Transform(-Vector3.UnitZ, GetPhysicsBodyRotation(car)).X.Should().BeLessThan(-0.2f, "a positive steer turns left");

        // The brakes bring it to a stop, upright.
        SetPhysicsVehicleInput(car, 0, 0, brake: true);
        Steps(240);
        GetPhysicsBodyVelocity(car).Length().Should().BeLessThan(0.3f, "the brakes stop it");
        Vector3.Transform(Vector3.UnitY, GetPhysicsBodyRotation(car)).Y.Should().BeGreaterThan(0.99f, "and it did not roll over");
    }
}
