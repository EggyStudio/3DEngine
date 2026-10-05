using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Physics;

/// <summary>The step on several workers giving the same result every run, which a game's replay and its tests rely on.</summary>
[Trait("Category", "Unit")]
public class PhysicsDeterminismTests
{
    // A pile of bouncing boxes and a crowd of characters walking through it, stepped two seconds,
    // and where every body ended.
    private static Vector3[] Run()
    {
        // On the workers whatever its size, which is the path whose order could vary.
        using var world = new PhysicsWorld(new PhysicsSettings { ThreadedAbove = 0 });
        world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(40, 0.5f, 40));
        var bodies = new List<PhysicsBody>();
        for (int i = 0; i < 200; i++)
            bodies.Add(world.CreateBox(new Vector3(i % 10 - 5, 1 + i / 100 * 2 + i % 7 * 0.3f, i / 10 % 10 - 5), new Vector3(0.4f), 1, PhysicsMaterial.Default with { Friction = 0.5f, Restitution = 0.6f }));
        var characters = new List<PhysicsBody>();
        for (int i = 0; i < 80; i++)
            characters.Add(world.CreateCharacter(new Vector3(i % 10 * 1.5f - 7, 0, -12 - i / 10 * 1.5f), 0.35f, 1.6f));
        for (int step = 0; step < 120; step++)
        {
            foreach (var character in characters) world.MoveCharacter(character, new Vector3(0, 0, 3));
            world.StepOnce(1 / 60f);
            world.TakePendingContacts();
        }
        return [.. bodies.Concat(characters).Select(world.GetPosition)];
    }

    [Fact]
    public void The_Step_On_Four_Workers_Ends_Every_Body_In_The_Same_Place_Every_Run()
    {
        new PhysicsSettings().WorkerThreads.Should().Be(4, "the default the test is about");
        var first = Run();
        for (int run = 0; run < 3; run++)
            Run().Should().Equal(first, "the same scene stepped the same way ends the same, to the bit");
    }
}
