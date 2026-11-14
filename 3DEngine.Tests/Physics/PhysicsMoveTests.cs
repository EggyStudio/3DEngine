using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Physics;

/// <summary>The bodies moved with a world a program moves by a whole step, stepped on as before.</summary>
[Trait("Category", "Unit")]
public class PhysicsMoveTests
{
    // A box falling onto a floor and one sliding over it, at a place, stepped a second, the world
    // moved by a step halfway where one is given, and where each body ended and how fast it goes.
    private static (Vector3[] Places, Vector3[] Speeds) Run(Vector3 at, Vector3? step)
    {
        using var world = new PhysicsWorld(new PhysicsSettings());
        world.CreateStaticBox(at + new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
        PhysicsBody[] bodies =
        [
            world.CreateBox(at + new Vector3(0, 3, 0), new Vector3(0.5f), 1, PhysicsMaterial.Default),
            world.CreateBox(at + new Vector3(-4, 0.5f, 2), new Vector3(0.5f), 1, PhysicsMaterial.Default with { Friction = 0.1f }),
        ];
        world.SetLinearVelocity(bodies[1], new Vector3(3, 0, 0));
        // One more body, put to sleep on the floor before the move, which wakes where it lies.
        var resting = world.CreateBox(at + new Vector3(5, 0.5f, -3), new Vector3(0.5f), 1, PhysicsMaterial.Default);
        bodies = [.. bodies, resting];
        for (int i = 0; i < 60; i++)
        {
            if (i == 30 && step is { } by) world.Move(by);
            world.StepOnce(1 / 60f);
            world.TakePendingContacts();
        }
        return ([.. bodies.Select(world.GetPosition)], [.. bodies.Select(world.GetLinearVelocity)]);
    }

    [Fact]
    public void A_World_Moved_By_A_Whole_Step_Moves_Every_Body_And_The_Floor_Under_It_And_Steps_As_Before()
    {
        var at = new Vector3(8192, 0, -4096);
        var step = new Vector3(-4096, 0, 0);
        var (stillPlaces, stillSpeeds) = Run(at, null);
        var (movedPlaces, movedSpeeds) = Run(at, step);
        // The box falls on the floor moved with it and lands, and the other slides on, as in the
        // world not moved; with the bodies alone moved the box falls through where the floor was.
        for (int i = 0; i < stillPlaces.Length; i++)
        {
            Vector3.Distance(movedPlaces[i], stillPlaces[i] + step).Should().BeLessThan(1e-2f, $"body {i} ends where it does unmoved, moved by the step");
            Vector3.Distance(movedSpeeds[i], stillSpeeds[i]).Should().BeLessThan(1e-2f, $"body {i} goes as fast as unmoved");
        }
    }
}
