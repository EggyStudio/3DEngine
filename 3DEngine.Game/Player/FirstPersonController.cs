using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>
/// Walks, sprints, sneaks, jumps and flies the player's body from the keys, turns the view with
/// the mouse, and gives the camera at the player's eyes, with the speeds of Minecraft's creative mode.
/// </summary>
public sealed class FirstPersonController
{
    private const float Gravity = 32;
    private const float FallLimit = 78;
    // A jump of a block and a quarter under that gravity, which clears one block as Minecraft's does.
    private const float JumpSpeed = 9;
    private const float Walk = 4.3f, Sprint = 5.6f, Sneak = 1.3f, Fly = 10.9f, FlySprint = 21.6f, FlyVertical = 7.5f;
    private const float StandingEyes = 1.62f, SneakingEyes = 1.27f;
    // In water the body sinks slowly, rises while Space is held and moves at a little over half speed.
    private const float SwimGravity = 6, SinkLimit = 3, SwimUp = 3.9f, SwimSpeed = 0.6f;
    private const float DoubleTap = 0.3f;

    private double _lastSpace = double.NegativeInfinity;
    private float _eyes = StandingEyes;
    private float _fov;

    public FirstPersonController() => _fov = FieldOfView;

    public PlayerBody Body { get; } = new();

    /// <summary>The turn about the vertical in radians, 0 looking north (toward -z) and growing to the left.</summary>
    public float Yaw { get; set; }

    /// <summary>The tilt in radians, positive looking up.</summary>
    public float Pitch { get; set; }

    public bool Flying { get; set; }

    public bool Sprinting { get; private set; }

    public bool Sneaking { get; private set; }

    /// <summary>Whether the body's middle is in water, as of the last update.</summary>
    public bool Swimming { get; private set; }

    /// <summary>Whether the eye is in water, which tints the view.</summary>
    public bool EyeInWater { get; private set; }

    /// <summary>The vertical field of view in degrees while walking, widened while sprinting.</summary>
    public float FieldOfView { get; set; } = 70;

    /// <summary>Radians turned for each pixel the mouse moves.</summary>
    public float Sensitivity { get; set; } = 0.0025f;

    public Vector3 Eye => Body.Position + new Vector3(0, _eyes, 0);

    public Vector3 Look => new(-MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), -MathF.Cos(Yaw) * MathF.Cos(Pitch));

    public Camera3D Camera => new(Eye, Eye + Look, Vector3.UnitY, _fov);

    /// <summary>The way the player faces in degrees from north, clockwise, as a compass reads it.</summary>
    public float Heading
    {
        get => ((-Yaw * 180 / MathF.PI) % 360 + 360) % 360;
        set => Yaw = -value * MathF.PI / 180;
    }

    /// <summary>
    /// Turns, steers and moves the body for a frame of so many seconds, the mouse turning the view
    /// where <paramref name="look"/> is set, as while the cursor is held, and the keys moving the
    /// player where <paramref name="move"/> is, as unless a text field has them.
    /// </summary>
    public void Update(VoxelWorld world, float seconds, bool look, bool move)
    {
        if (look)
        {
            var mouse = GetMouseDelta();
            Yaw -= mouse.X * Sensitivity;
            Pitch = Math.Clamp(Pitch - mouse.Y * Sensitivity, -1.55f, 1.55f);
        }

        float forward = 0, side = 0;
        bool up = false, down = false, sprintKey = false;
        if (move)
        {
            forward = (IsKeyDown(Key.W) ? 1 : 0) - (IsKeyDown(Key.S) ? 1 : 0);
            side = (IsKeyDown(Key.D) ? 1 : 0) - (IsKeyDown(Key.A) ? 1 : 0);
            up = IsKeyDown(Key.Space);
            down = IsKeyDown(Key.LeftShift);
            sprintKey = IsKeyDown(Key.LeftControl);
            if (IsKeyPressed(Key.Space))
            {
                // Two presses of Space in quick succession start or stop flying, as in creative mode.
                if (GetTime() - _lastSpace < DoubleTap)
                {
                    Flying = !Flying;
                    Body.Velocity.Y = 0;
                    _lastSpace = double.NegativeInfinity;
                }
                else _lastSpace = GetTime();
            }
        }
        if (forward <= 0) Sprinting = false;
        else if (sprintKey) Sprinting = true;
        var middle = Body.Position + new Vector3(0, 0.9f, 0);
        Swimming = !Flying && world.GetBlock((int)MathF.Floor(middle.X), (int)MathF.Floor(middle.Y), (int)MathF.Floor(middle.Z)) == BlockId.Water;
        Sneaking = down && !Flying && !Swimming;

        var ahead = new Vector3(-MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
        var right = new Vector3(MathF.Cos(Yaw), 0, -MathF.Sin(Yaw));
        var wish = ahead * forward + right * side;
        if (wish.LengthSquared() > 1) wish = Vector3.Normalize(wish);
        var speed = (Flying ? (Sprinting ? FlySprint : Fly) : Sneaking ? Sneak : Sprinting ? Sprint : Walk) * (Swimming ? SwimSpeed : 1);

        // The velocity eases toward the one wished for, quickly on the ground and slowly in the air,
        // so a jump keeps most of its run.
        var grip = 1 - MathF.Exp(-(Flying ? 8 : Body.OnGround ? 14 : 2.5f) * seconds);
        Body.Velocity.X += (wish.X * speed - Body.Velocity.X) * grip;
        Body.Velocity.Z += (wish.Z * speed - Body.Velocity.Z) * grip;
        if (Flying)
            Body.Velocity.Y += (((up ? 1 : 0) - (down ? 1 : 0)) * FlyVertical - Body.Velocity.Y) * grip;
        else if (Swimming)
        {
            Body.Velocity.Y = up ? SwimUp : MathF.Max(Body.Velocity.Y - SwimGravity * seconds, -SinkLimit);
        }
        else
        {
            Body.Velocity.Y = MathF.Max(Body.Velocity.Y - Gravity * seconds, -FallLimit);
            if (up && Body.OnGround) Body.Velocity.Y = JumpSpeed;
        }

        Body.Move(world, Body.Velocity * seconds, keepToEdges: Sneaking && Body.OnGround);
        if (Flying && Body.OnGround && down) Flying = false;

        _eyes += ((Sneaking ? SneakingEyes : StandingEyes) - _eyes) * (1 - MathF.Exp(-20 * seconds));
        var eye = Eye;
        EyeInWater = world.GetBlock((int)MathF.Floor(eye.X), (int)MathF.Floor(eye.Y), (int)MathF.Floor(eye.Z)) == BlockId.Water;
        var widened = Sprinting && wish != Vector3.Zero ? FieldOfView * 1.12f : FieldOfView;
        _fov += (widened - _fov) * (1 - MathF.Exp(-10 * seconds));
    }

    /// <summary>Puts the player's feet at a place, still.</summary>
    public void Teleport(Vector3 feet)
    {
        Body.Position = feet;
        Body.Velocity = Vector3.Zero;
    }
}
