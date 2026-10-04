using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Physics;

/// <summary>Each body's friction and bounce, in the simulation.</summary>
[Trait("Category", "Integration")]
public class BodyMaterialTests
{
    private const float Step = 1f / 60f;

    private static PhysicsWorld NewWorld(Vector3? gravity = null) =>
        new(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = Step, MaxStepsPerFrame = 64, Gravity = gravity ?? new Vector3(0, -9.81f, 0) });

    // How high a ball dropped with its bottom 2.5 above a floor comes back up after it first lands.
    private static float Rebound(float bounce)
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 0.5f, 10));
        var ball = world.CreateSphere(new Vector3(0, 3, 0), 0.5f, material: PhysicsMaterial.Default with { Restitution = bounce });
        var landed = false;
        var highest = 0f;
        for (int i = 0; i < 180; i++)
        {
            world.StepOnce(Step);
            var bottom = world.GetPosition(ball).Y - 0.5f;
            if (bottom < 0.05f) landed = true;
            else if (landed) highest = MathF.Max(highest, bottom);
            if (landed && highest > 0 && bottom < 0.05f) break;
        }
        return highest;
    }

    [Fact]
    public void A_Ball_Bounces_Back_About_Its_Bounce_Squared_Of_The_Way_It_Fell()
    {
        Rebound(0).Should().BeLessThan(0.05f, "a ball with no bounce lands and stays");
        Rebound(0.6f).Should().BeInRange(0.7f, 0.95f, "0.6 squared of 2.5 is 0.9, less what the step loses");
        Rebound(0.9f).Should().BeInRange(1.7f, 2.1f, "0.9 squared of 2.5 is 2.03");
    }

    [Fact]
    public void A_Box_On_Ice_Slides_Farther_Than_One_On_Rubber()
    {
        float Slide(float friction)
        {
            using var world = NewWorld();
            world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(50, 0.5f, 50), PhysicsMaterial.Default with { Friction = friction });
            // A flat slab, which slides where a cube would tip over and tumble.
            var box = world.CreateBox(new Vector3(0, 0.1f, 0), new Vector3(0.5f, 0.1f, 0.5f), material: PhysicsMaterial.Default with { Friction = friction });
            box.SetLinearVelocity(new Vector3(5, 0, 0));
            for (int i = 0; i < 120; i++) world.StepOnce(Step);
            return world.GetPosition(box).X;
        }

        var ice = Slide(0.02f);
        ice.Should().BeGreaterThan(7, "on ice it keeps most of its speed for two seconds");
        Slide(1).Should().BeLessThan(1.5f, "on rubber it stops within the 1.3 units a friction of 1 allows from 5 a second");
    }

    [Fact]
    public void Two_Equal_Balls_That_Bounce_Fully_Swap_Their_Speeds()
    {
        using var world = NewWorld(Vector3.Zero);
        var bouncy = PhysicsMaterial.Default with { Restitution = 1 };
        var a = world.CreateSphere(new Vector3(-2, 0, 0), 0.5f, material: bouncy);
        var b = world.CreateSphere(new Vector3(2, 0, 0), 0.5f, material: bouncy);
        a.SetLinearVelocity(new Vector3(3, 0, 0));

        // They meet after a second.
        for (int i = 0; i < 120; i++) world.StepOnce(Step);

        world.GetLinearVelocity(a).X.Should().BeApproximately(0, 0.3f, "the moving one stops");
        world.GetLinearVelocity(b).X.Should().BeApproximately(3, 0.3f, "and the one it hit leaves at its speed");
    }

    [Fact]
    public void A_Scene_Files_Material_Is_Given_To_The_Body_It_Describes()
    {
        var authoring = new EcsWorld();
        var ball = authoring.Spawn();
        authoring.Add(ball, new Transform(new Vector3(0, 3, 0)));
        authoring.Add(ball, Collider.Sphere(0.5f));
        authoring.Add(ball, RigidBody.Dynamic());
        authoring.Add(ball, PhysicsMaterial.Default with { Restitution = 0.9f });
        var floor = authoring.Spawn();
        authoring.Add(floor, new Transform(new Vector3(0, -0.5f, 0)));
        authoring.Add(floor, Collider.Box(new Vector3(20, 1, 20)));
        authoring.Add(floor, RigidBody.Static);

        var world = new World();
        world.InsertResource(new EcsWorld());
        using var physics = NewWorld();
        world.InsertResource(physics);
        var spawned = SceneFile.Read(world, SceneFile.Write(authoring));
        PhysicsBodies.Run(world);
        var body = world.Resource<EcsWorld>().GetReadOnly<PhysicsBody>(spawned[0]);

        var landed = false;
        var highest = 0f;
        for (int i = 0; i < 120; i++)
        {
            physics.StepOnce(Step);
            var bottom = physics.GetPosition(body).Y - 0.5f;
            if (bottom < 0.05f) landed = true;
            else if (landed) highest = MathF.Max(highest, bottom);
        }
        highest.Should().BeGreaterThan(1.5f, "the file's bounce came with it");
    }
}
