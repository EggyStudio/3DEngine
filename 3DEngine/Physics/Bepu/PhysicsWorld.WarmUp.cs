using System.Numerics;

namespace Engine;

public sealed partial class PhysicsWorld
{
    private static int _warmed;

    /// <summary>
    /// Steps a throwaway world of every kind of body and shape the engine makes, once a process,
    /// so the code each pair of shapes meets with is compiled before a game's own world needs it.
    /// </summary>
    /// <remarks>
    /// The solver is generic over the engine's callbacks, each pair of shapes and each kind of
    /// joint, and .NET compiles each of those the first time it runs. In <c>games/Manor</c> the
    /// first steps with a character on a mesh, doors on hinges and bodies falling asleep compiled
    /// up to 173 methods in a step, 25 to 38 ms, which this brings under 15 ms. A native build
    /// compiles nothing as it runs. <see cref="PhysicsPlugin"/> runs this on a worker as the app is
    /// built, where it costs no frame.
    /// </remarks>
    internal static void WarmUp()
    {
        if (Interlocked.Exchange(ref _warmed, 1) == 1) return;
        using var world = new PhysicsWorld(new PhysicsSettings());
        Vector3[] quad = [new(-10, 0, -10), new(10, 0, -10), new(10, 0, 10), new(-10, 0, 10)];
        world.CreateStaticMesh(Vector3.Zero, quad, [0, 2, 1, 0, 3, 2]);
        world.CreateStaticBox(new Vector3(4, 0.5f, 0), new Vector3(1, 0.5f, 1));
        world.CreateKinematicBox(new Vector3(-4, 0.5f, 0), new Vector3(1, 0.5f, 1));
        var character = world.CreateCharacter(new Vector3(0, 0.1f, 0), 0.35f, 1.8f);
        world.CreateBox(new Vector3(1, 1, 1), new Vector3(0.4f));
        world.CreateSphere(new Vector3(-1, 1, 1), 0.4f);
        world.CreateCapsule(new Vector3(1, 1, -1), 0.3f, 0.6f);
        world.CreateCylinder(new Vector3(-1, 1, -1), 0.3f, 0.6f);
        world.CreateConvexHull(new Vector3(0, 2, 2), [new(0, 0, 0), new(0.5f, 0, 0), new(0, 0.5f, 0), new(0, 0, 0.5f)]);
        var trigger = world.CreateStaticBox(new Vector3(0, 1, 0), new Vector3(2, 1, 2));
        world.SetTrigger(trigger, true);

        // Each kind of joint, with the limits and the motor a door and a rope use, whose
        // constraints the solver has code of its own for.
        var post = world.CreateKinematicBox(new Vector3(6, 2, 6), new Vector3(0.1f, 2, 0.1f));
        var door = world.CreateBox(new Vector3(7, 2, 6), new Vector3(1, 1.5f, 0.05f), 20);
        var hinge = world.CreateHingeJoint(post, door, new Vector3(6, 2, 6), Vector3.UnitY);
        world.SetHingeLimit(hinge, -1.5f, 1.5f);
        world.SetHingeMotor(hinge, 2, 100);
        var arm = world.CreateBox(new Vector3(6, 4, 8), new Vector3(0.2f), 1);
        world.SetBallJointLimit(world.CreateBallJoint(post, arm, new Vector3(6, 4, 7)), Vector3.UnitY, 0.5f, 0.5f);
        world.CreateWeldJoint(arm, world.CreateBox(new Vector3(6, 4, 9), new Vector3(0.2f), 1));
        var rope = world.CreateDistanceJoint(post, world.CreateSphere(new Vector3(6, 1, 4), 0.2f), new Vector3(6, 3, 6), new Vector3(6, 1, 4), 1, 2);
        world.SetDistanceJointRange(rope, 1, 1.5f);

        // Long enough for what rests to fall asleep, then woken by a push, and every query asked.
        for (int i = 0; i < 150; i++)
        {
            world.MoveCharacter(character, i < 60 ? new Vector3(1, 0, 0) : Vector3.Zero);
            if (i == 140) world.ApplyImpulseAt(door, new Vector3(0, 0, 5), new Vector3(7.5f, 2, 6));
            world.StepOnce(1 / 60f);
            world.TakePendingContacts();
        }
        world.Raycast(new Vector3(0, 5, 0), -Vector3.UnitY, 10, out _);
        world.Raycast(new Vector3(0, 5, 0), -Vector3.UnitY, 10, character, out _);
    }
}
