using System.Numerics;
using BepuPhysics;

namespace Engine;

/// <summary>Character controllers: capsules walked toward a wanted velocity before every step.</summary>
public sealed partial class PhysicsWorld
{
    // Each character by its body's handle.
    private readonly Dictionary<int, Character> _characters = [];

    // Which body handles are characters, read by the narrow phase from its worker threads to give
    // their contacts no friction, and written only between steps.
    private readonly CharacterFlags _characterFlags = new();

    // How fast a character off the ground changes its horizontal velocity toward the one it was asked for, in units a second squared.
    private const float AirAcceleration = 20;

    // Where the ground probes start, in units of the probe spread, kept once rather than made each step.
    private static readonly Vector3[] ProbeDirections = [Vector3.Zero, Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ];

    private sealed class Character
    {
        public required PhysicsBody Body;
        public required float Radius;
        public required float HalfHeight;
        public Vector3 Wanted;
        public float MaxSlopeCos = MathF.Cos(float.DegreesToRadians(45));
        public float JumpSpeed;
        public bool Grounded;
        public Vector3 GroundNormal = Vector3.UnitY;
    }

    /// <summary>
    /// A character's body: an upright capsule <paramref name="height"/> tall with its feet at
    /// <paramref name="feet"/>. Before every step it is walked toward the velocity
    /// <see cref="MoveCharacter"/> last gave it. It slides along what it meets, since its contacts
    /// have no friction, rides over an edge lower than about half its radius on the round of its
    /// foot, follows the ground it stands on and holds still on a slope up to its steepest
    /// (<see cref="SetCharacterMaxSlope"/>, 45 degrees to begin with), and is never turned over or
    /// put to sleep. It is a dynamic body, so it pushes what is lighter and walls stop it.
    /// </summary>
    public PhysicsBody CreateCharacter(Vector3 feet, float radius, float height, float mass = 80, int entityId = 0)
    {
        radius = MathF.Max(radius, 0.01f);
        height = MathF.Max(height, 2 * radius);
        var capsule = new BepuPhysics.Collidables.Capsule(radius, height - 2 * radius);
        var idx = Simulation.Shapes.Add(capsule);
        var inertia = capsule.ComputeInertia(mass);
        inertia.InverseInertiaTensor = default;
        var handle = Simulation.Bodies.Add(BodyDescription.CreateDynamic(
            new RigidPose(feet + new Vector3(0, height / 2, 0), Quaternion.Identity), inertia, Coll(idx), new BodyActivityDescription(-1)));
        if (entityId != 0) _bodyToEntity[handle.Value] = entityId;

        var body = new PhysicsBody(this, handle.Value, BodyKind.Dynamic);
        _characters[handle.Value] = new Character { Body = body, Radius = radius, HalfHeight = height / 2 };
        _characterFlags.Set(handle.Value, true);
        return body;
    }

    /// <summary>
    /// The velocity a character walks at from the next step on, along the ground it stands on. Its
    /// vertical part is ignored, since gravity and the ground decide the character's fall.
    /// </summary>
    public void MoveCharacter(PhysicsBody body, Vector3 velocity)
    {
        if (_characters.TryGetValue(body.Handle, out var character)) character.Wanted = velocity with { Y = 0 };
    }

    /// <summary>Makes a character on the ground leave it upward at <paramref name="speed"/> at the next step.</summary>
    public void JumpCharacter(PhysicsBody body, float speed)
    {
        if (_characters.TryGetValue(body.Handle, out var character) && character.Grounded) character.JumpSpeed = speed;
    }

    /// <summary>The steepest ground, in degrees, a character stands and walks on rather than sliding off.</summary>
    public void SetCharacterMaxSlope(PhysicsBody body, float degrees)
    {
        if (_characters.TryGetValue(body.Handle, out var character))
            character.MaxSlopeCos = MathF.Cos(float.DegreesToRadians(Math.Clamp(degrees, 0, 89)));
    }

    /// <summary>Whether a character stood on ground no steeper than its steepest at the last step.</summary>
    public bool IsCharacterGrounded(PhysicsBody body) => _characters.TryGetValue(body.Handle, out var character) && character.Grounded;

    /// <summary>The way the ground under a character faces, up when it stands on nothing.</summary>
    public Vector3 GetCharacterGroundNormal(PhysicsBody body) =>
        _characters.TryGetValue(body.Handle, out var character) ? character.GroundNormal : Vector3.UnitY;

    private void ForgetCharacter(int handle)
    {
        if (_characters.Remove(handle)) _characterFlags.Set(handle, false);
    }

    // Before a step of dt: finds each character's ground, and sets its velocity to walk where it
    // was asked to, along that ground, with the step's pull of gravity along a slope it may stand
    // on taken out beforehand, since the integrator adds it during the step.
    private void UpdateCharacters(float dt)
    {
        if (_characters.Count == 0) return;
        var gravity = Gravity;
        foreach (var character in _characters.Values)
        {
            var body = character.Body;
            if (!Simulation.Bodies.BodyExists(new BodyHandle(body.Handle))) continue;
            var reference = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
            var center = reference.Pose.Position;

            // Five rays down from inside the capsule, at its middle and around its foot, so an edge
            // under one side of the foot still counts as ground. The highest ground found wins.
            character.Grounded = false;
            character.GroundNormal = Vector3.UnitY;
            var reach = character.HalfHeight + 0.08f;
            float nearest = float.MaxValue;
            var spread = character.Radius * 0.7f;
            foreach (var around in ProbeDirections)
            {
                var offset = around * spread;
                var from = center + offset;
                // A ray at the side starts lower, where the round of the foot is, so it reaches as far below it.
                var length = reach - (character.Radius - MathF.Sqrt(MathF.Max(0, character.Radius * character.Radius - offset.LengthSquared())));
                if (!Raycast(from, -Vector3.UnitY, length, body, out var hit) || hit.Normal.Y < character.MaxSlopeCos) continue;
                if (hit.Distance >= nearest) continue;
                nearest = hit.Distance;
                character.Grounded = true;
                character.GroundNormal = hit.Normal;
            }

            var velocity = reference.Velocity.Linear;
            if (character.Grounded)
            {
                // Along the ground, so a walk up or down a slope follows it, and with the pull along
                // the slope cancelled, so standing still on it stays still.
                var n = character.GroundNormal;
                var along = character.Wanted - Vector3.Dot(character.Wanted, n) * n;
                if (along != Vector3.Zero) along = Vector3.Normalize(along) * character.Wanted.Length();
                var pull = gravity - Vector3.Dot(gravity, n) * n;
                velocity = along - pull * dt + MathF.Min(0, Vector3.Dot(velocity, n)) * n;
                if (character.JumpSpeed > 0)
                {
                    velocity.Y = character.JumpSpeed;
                    character.Grounded = false;
                }
            }
            else if (character.Wanted != Vector3.Zero)
            {
                // In the air, or against ground too steep to stand on, it steers toward where it was
                // asked to go at a limited rate, and asked for nothing it keeps its own motion, so a
                // steep slope slides it down and a jump carries it on.
                var horizontal = velocity with { Y = 0 };
                var change = character.Wanted - horizontal;
                var most = AirAcceleration * dt;
                if (change.Length() > most) change = Vector3.Normalize(change) * most;
                velocity += change;
            }
            character.JumpSpeed = 0;

            reference.Velocity.Linear = velocity;
            reference.Velocity.Angular = Vector3.Zero;
            if (!reference.Awake) Simulation.Awakener.AwakenBody(new BodyHandle(body.Handle));
        }
    }
}
