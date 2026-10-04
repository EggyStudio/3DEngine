using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

/// <summary>The character controller, a capsule 1.8 tall and 0.4 wide in radius, on a floor at y 0.</summary>
[Trait("Category", "Integration")]
public class PhysicsCharacterTests
{
    private const float Step = 1f / 60f;
    private const float Radius = 0.4f, Height = 1.8f;

    private static PhysicsWorld NewWorld()
    {
        var world = new PhysicsWorld(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = Step, MaxStepsPerFrame = 64 });
        world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(50, 0.5f, 50));
        return world;
    }

    private static void Run(PhysicsWorld world, float seconds)
    {
        for (int i = 0; i < seconds / Step; i++) world.StepOnce(Step);
    }

    private static Vector3 Feet(PhysicsWorld world, PhysicsBody body) => world.GetPosition(body) - new Vector3(0, Height / 2, 0);

    // A ramp of two triangles rising along +X at an angle, from x 1 to x 1 + run, 6 units wide.
    private static void Ramp(PhysicsWorld world, float degrees, float run = 6)
    {
        var rise = run * MathF.Tan(float.DegreesToRadians(degrees));
        Vector3[] vertices = [new(1, 0, -3), new(1 + run, rise, -3), new(1 + run, rise, 3), new(1, 0, 3)];
        world.CreateStaticMesh(Vector3.Zero, vertices, [0, 1, 2, 0, 2, 3]);
    }

    [Fact]
    public void A_Character_Stands_On_A_Floor_And_Reports_Ground()
    {
        using var world = NewWorld();
        var body = world.CreateCharacter(new Vector3(0, 1, 0), Radius, Height);

        Run(world, 1.5f);

        world.IsCharacterGrounded(body).Should().BeTrue();
        Feet(world, body).Y.Should().BeApproximately(0, 0.05f, "it fell a unit and stands on the floor");
        world.GetCharacterGroundNormal(body).Y.Should().BeApproximately(1, 1e-3f);
    }

    [Fact]
    public void A_Wall_Stops_A_Character_Walking_Into_It()
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(3, 1, 0), new Vector3(0.25f, 1, 5));
        var body = world.CreateCharacter(Vector3.Zero, Radius, Height);

        world.MoveCharacter(body, new Vector3(4, 0, 0));
        Run(world, 3);

        world.GetPosition(body).X.Should().BeLessThan(3 - 0.25f - Radius + 0.05f, "the wall's face is at 2.75 and the capsule is 0.4 wide");
        world.GetPosition(body).X.Should().BeGreaterThan(2.2f, "it walked up to the wall");
    }

    [Fact]
    public void A_Character_Slides_Along_A_Wall_It_Meets_At_An_Angle()
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(3, 1, 0), new Vector3(0.25f, 1, 20));
        var body = world.CreateCharacter(Vector3.Zero, Radius, Height);

        world.MoveCharacter(body, Vector3.Normalize(new Vector3(1, 0, 1)) * 4);
        Run(world, 3);

        var at = world.GetPosition(body);
        at.X.Should().BeLessThan(2.4f, "the wall holds it back");
        at.Z.Should().BeGreaterThan(6, "and it keeps going along the wall, where friction would have held it");
    }

    [Fact]
    public void A_Character_Rides_Over_A_Low_Edge()
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(4, 0.075f, 0), new Vector3(2, 0.075f, 3));
        var body = world.CreateCharacter(Vector3.Zero, Radius, Height);

        world.MoveCharacter(body, new Vector3(3, 0, 0));
        Run(world, 1.5f);

        Feet(world, body).Y.Should().BeApproximately(0.15f, 0.05f, "it stands on the step, 15 centimetres up");
        world.GetPosition(body).X.Should().BeGreaterThan(3);
    }

    [Fact]
    public void A_Character_Climbs_A_Step_As_High_As_Its_Radius_And_Not_A_Higher_One()
    {
        using var world = NewWorld();
        // A step 0.35 high from x 2, and in another lane a ledge 0.6 high, past its step height of 0.4.
        world.CreateStaticBox(new Vector3(4, 0.175f, 0), new Vector3(2, 0.175f, 2));
        world.CreateStaticBox(new Vector3(4, 0.3f, 10), new Vector3(2, 0.3f, 2));
        var climber = world.CreateCharacter(Vector3.Zero, Radius, Height);
        var stopped = world.CreateCharacter(new Vector3(0, 0, 10), Radius, Height);

        world.MoveCharacter(climber, new Vector3(3, 0, 0));
        world.MoveCharacter(stopped, new Vector3(3, 0, 0));
        Run(world, 2);

        Feet(world, climber).Y.Should().BeApproximately(0.35f, 0.05f, "it stands on the step");
        world.GetPosition(climber).X.Should().BeGreaterThan(3, "and walked on along it");
        Feet(world, stopped).Y.Should().BeApproximately(0, 0.05f, "a ledge higher than a step stops it");
        world.GetPosition(stopped).X.Should().BeLessThan(2 - Radius + 0.05f);
    }

    [Fact]
    public void A_Character_Crouches_Under_A_Ceiling_And_Stands_Only_Where_There_Is_Room()
    {
        using var world = NewWorld();
        // A ceiling 1.3 high over x 2 to 6.
        world.CreateStaticBox(new Vector3(4, 1.55f, 0), new Vector3(2, 0.25f, 3));
        var body = world.CreateCharacter(Vector3.Zero, Radius, Height);

        world.SetCharacterHeight(body, 1.1f).Should().BeTrue("crouching needs no room");
        world.GetPosition(body).Y.Should().BeApproximately(0.55f, 0.01f, "its feet stay where they were, its middle half its new height up");
        world.MoveCharacter(body, new Vector3(2, 0, 0));
        Run(world, 2);
        world.GetPosition(body).X.Should().BeInRange(3, 5.5f, "crouched, it walks in under the ceiling");
        world.MoveCharacter(body, Vector3.Zero);
        Run(world, 0.2f);

        world.SetCharacterHeight(body, Height).Should().BeFalse("the ceiling is in the way of standing");
        world.MoveCharacter(body, new Vector3(2, 0, 0));
        Run(world, 2);
        world.GetPosition(body).X.Should().BeGreaterThan(6.5f, "it walked out from under the ceiling");
        world.SetCharacterHeight(body, Height).Should().BeTrue("where there is room it stands");
        Run(world, 0.2f);
        world.GetPosition(body).Y.Should().BeApproximately(Height / 2, 0.05f);
    }

    [Fact]
    public void A_Character_Holds_A_Gentle_Slope_And_Slides_Off_A_Steep_One()
    {
        using var gentle = NewWorld();
        Ramp(gentle, 25);
        var standing = gentle.CreateCharacter(new Vector3(4, 2, 0), Radius, Height);
        Run(gentle, 1);
        var settled = gentle.GetPosition(standing);
        Run(gentle, 2);

        gentle.IsCharacterGrounded(standing).Should().BeTrue("25 degrees is under its 45");
        Vector3.Distance(gentle.GetPosition(standing), settled).Should().BeLessThan(0.05f, "standing still on it, it stays");

        using var steep = NewWorld();
        Ramp(steep, 60, run: 3);
        var sliding = steep.CreateCharacter(new Vector3(3, 4, 0), Radius, Height);
        Run(steep, 0.3f);
        var start = steep.GetPosition(sliding);
        steep.IsCharacterGrounded(sliding).Should().BeFalse("on the slope, 60 degrees is past its 45");
        Run(steep, 0.5f);

        steep.GetPosition(sliding).X.Should().BeLessThan(start.X - 0.3f, "it slides down the slope");
    }

    [Fact]
    public void A_Character_Walks_Up_A_Gentle_Slope_And_Jumps_From_The_Ground()
    {
        using var world = NewWorld();
        Ramp(world, 20);
        var body = world.CreateCharacter(Vector3.Zero, Radius, Height);

        world.MoveCharacter(body, new Vector3(3, 0, 0));
        Run(world, 2);
        Feet(world, body).Y.Should().BeGreaterThan(1, "it walked up the ramp");

        world.MoveCharacter(body, Vector3.Zero);
        Run(world, 0.5f);
        var before = world.GetPosition(body).Y;
        world.JumpCharacter(body, 5);
        Run(world, 0.2f);
        world.GetPosition(body).Y.Should().BeGreaterThan(before + 0.5f, "a jump of 5 units a second rises past half a unit in a fifth of a second");
    }
}
