using System.Numerics;

namespace Engine.Game;

/// <summary>
/// The player's box, 0.6 blocks wide and 1.8 tall as Minecraft's is, moved through the blocks
/// one axis at a time and stopped by the first solid block on each.
/// </summary>
/// <remarks>
/// The box moves up or down first, then along x, then along z, as Minecraft moves an entity, so
/// walking into a wall while falling slides down it. A move is cut into steps under half a block,
/// so a fast fall cannot pass through a floor between two frames.
/// </remarks>
public sealed class PlayerBody
{
    public const float HalfWidth = 0.3f;
    public const float Height = 1.8f;
    private const float Skin = 1e-4f;

    /// <summary>The middle of the box's bottom face.</summary>
    public Vector3 Position;

    public Vector3 Velocity;

    /// <summary>Whether the box stands on a block, as of the last move.</summary>
    public bool OnGround { get; private set; }

    public Vector3 Min => Position - new Vector3(HalfWidth, 0, HalfWidth);

    public Vector3 Max => Position + new Vector3(HalfWidth, Height, HalfWidth);

    /// <summary>Whether the box takes up any of the block at a place, which a block is not placed into.</summary>
    public bool Overlaps(int x, int y, int z)
    {
        Vector3 min = Min, max = Max;
        return min.X < x + 1 && max.X > x && min.Y < y + 1 && max.Y > y && min.Z < z + 1 && max.Z > z;
    }

    /// <summary>
    /// Moves the box by <paramref name="delta"/>, stopping it against the blocks and zeroing its
    /// velocity along each axis it was stopped on. With <paramref name="keepToEdges"/>, as when
    /// sneaking, a step along x or z that would leave no block under the box is not taken.
    /// </summary>
    public void Move(VoxelWorld world, Vector3 delta, bool keepToEdges)
    {
        var largest = MathF.Max(MathF.Abs(delta.X), MathF.Max(MathF.Abs(delta.Y), MathF.Abs(delta.Z)));
        var steps = Math.Max(1, (int)MathF.Ceiling(largest / 0.45f));
        var step = delta / steps;
        var landed = false;

        for (int i = 0; i < steps; i++)
        {
            if (step.Y != 0 && Blocked(world, 1, step.Y))
            {
                landed |= step.Y < 0;
                Velocity.Y = 0;
                step.Y = 0;
            }
            if (step.X != 0 && (keepToEdges && !Supported(world, Position + new Vector3(step.X, 0, 0)) || Blocked(world, 0, step.X)))
            {
                Velocity.X = 0;
                step.X = 0;
            }
            if (step.Z != 0 && (keepToEdges && !Supported(world, Position + new Vector3(0, 0, step.Z)) || Blocked(world, 2, step.Z)))
            {
                Velocity.Z = 0;
                step.Z = 0;
            }
        }
        OnGround = landed || Velocity.Y <= 0 && Supported(world, Position);
    }

    // Moves the box along one axis, or as far as the nearest solid block in the way and no further.
    private bool Blocked(VoxelWorld world, int axis, float distance)
    {
        var moved = Position;
        Add(ref moved, axis, distance);
        Vector3 min = moved - new Vector3(HalfWidth, 0, HalfWidth), max = moved + new Vector3(HalfWidth, Height, HalfWidth);
        // The box's leading side before the move. A block it already overlaps, as after a teleport
        // into the ground, is behind that side and does not stop it, so the player can walk out.
        var near = axis == 1 ? (distance > 0 ? Position.Y + Height : Position.Y) : (axis == 0 ? Position.X : Position.Z) + (distance > 0 ? HalfWidth : -HalfWidth);
        var stop = distance > 0 ? float.MaxValue : float.MinValue;
        var hit = false;
        for (int y = Floor(min.Y + Skin); y <= Floor(max.Y - Skin); y++)
            for (int z = Floor(min.Z + Skin); z <= Floor(max.Z - Skin); z++)
                for (int x = Floor(min.X + Skin); x <= Floor(max.X - Skin); x++)
                {
                    var face = axis == 0 ? x : axis == 1 ? y : z;
                    if (distance > 0 ? face < near - Skin : face + 1 > near + Skin) continue;
                    if (!world.Collides(x, y, z)) continue;
                    hit = true;
                    // Where the box's middle stands when its side touches the block's near face.
                    if (distance > 0) stop = MathF.Min(stop, face - (axis == 1 ? Height : HalfWidth));
                    else stop = MathF.Max(stop, face + 1 + (axis == 1 ? 0 : HalfWidth));
                }
        if (hit) Set(ref moved, axis, stop);
        Position = moved;
        return hit;
    }

    // Whether a block lies under the box's footprint at a position, within a hair of its feet.
    private static bool Supported(VoxelWorld world, Vector3 at)
    {
        var y = Floor(at.Y - 0.01f);
        if (at.Y - (y + 1) > 0.01f) return false;
        for (int z = Floor(at.Z - HalfWidth + Skin); z <= Floor(at.Z + HalfWidth - Skin); z++)
            for (int x = Floor(at.X - HalfWidth + Skin); x <= Floor(at.X + HalfWidth - Skin); x++)
                if (world.Collides(x, y, z)) return true;
        return false;
    }

    private static int Floor(float value) => (int)MathF.Floor(value);

    private static void Add(ref Vector3 v, int axis, float amount)
    {
        if (axis == 0) v.X += amount;
        else if (axis == 1) v.Y += amount;
        else v.Z += amount;
    }

    private static void Set(ref Vector3 v, int axis, float value)
    {
        if (axis == 0) v.X = value;
        else if (axis == 1) v.Y = value;
        else v.Z = value;
    }
}
