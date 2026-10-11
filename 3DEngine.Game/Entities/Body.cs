using System.Numerics;

namespace Engine.Game;

/// <summary>
/// A box that stands for the player or a mob, the player's 0.6 blocks wide and 1.8 tall as
/// Minecraft's is, moved through the blocks one axis at a time and stopped by the first solid box
/// on each, climbing onto a slab or a stair in its way.
/// </summary>
/// <remarks>
/// The box moves up or down first, then along x, then along z, as Minecraft moves an entity, so
/// walking into a wall while falling slides down it. A move is cut into steps under half a block,
/// so a fast fall cannot pass through a floor between two frames.
/// </remarks>
public sealed class Body(float halfWidth, float height)
{
    private const float Skin = 1e-4f;
    // How high the box climbs onto what it walks into, as onto a slab or a stair.
    private const float StepHeight = 0.6f;

    /// <summary>The player's box.</summary>
    public Body() : this(0.3f, 1.8f)
    {
    }

    /// <summary>Half the box's width along x and along z.</summary>
    public float HalfWidth { get; } = halfWidth;

    public float Height { get; } = height;

    /// <summary>The middle of the box's bottom face.</summary>
    public Vector3 Position;

    public Vector3 Velocity;

    /// <summary>Whether the box stands on a block, as of the last move.</summary>
    public bool OnGround { get; private set; }

    public Vector3 Min => Position - new Vector3(HalfWidth, 0, HalfWidth);

    public Vector3 Max => Position + new Vector3(HalfWidth, Height, HalfWidth);

    /// <summary>Whether the box takes up any of the boxes a block would stand in at a place, which the block is not placed into.</summary>
    public bool Overlaps(int x, int y, int z, IReadOnlyList<Box> boxes)
    {
        foreach (var box in boxes)
            if (Overlap(new Vector3(x, y, z) + box.Min, new Vector3(x, y, z) + box.Max, Min, Max)) return true;
        return false;
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
            var grounded = landed || OnGround;
            if (step.X != 0 && (keepToEdges && !Supported(world, Position + new Vector3(step.X, 0, 0)) || !StepUp(world, grounded, new Vector3(step.X, 0, 0)) && Blocked(world, 0, step.X)))
            {
                Velocity.X = 0;
                step.X = 0;
            }
            if (step.Z != 0 && (keepToEdges && !Supported(world, Position + new Vector3(0, 0, step.Z)) || !StepUp(world, grounded, new Vector3(0, 0, step.Z)) && Blocked(world, 2, step.Z)))
            {
                Velocity.Z = 0;
                step.Z = 0;
            }
        }
        OnGround = landed || Velocity.Y <= 0 && Supported(world, Position);
    }

    // Climbs a box on the ground onto what stops a step, a slab or a stair, up to StepHeight, where
    // the box fits there, as Minecraft lets an entity step up half a block and a little more, and
    // settles it on top. Whether it did, so the step is not taken a second time.
    private bool StepUp(VoxelWorld world, bool grounded, Vector3 step)
    {
        if (!grounded || Fits(world, Position + step)) return false;
        var raised = Position + new Vector3(0, StepHeight, 0);
        if (!Fits(world, raised) || !Fits(world, raised + step)) return false;
        Position = raised + step;
        Blocked(world, 1, -StepHeight);
        return true;
    }

    // Moves the box along one axis, or as far as the nearest solid box in the way and no further.
    private bool Blocked(VoxelWorld world, int axis, float distance)
    {
        var moved = Position;
        Add(ref moved, axis, distance);
        Vector3 min = moved - new Vector3(HalfWidth, 0, HalfWidth), max = moved + new Vector3(HalfWidth, Height, HalfWidth);
        // The box's leading side before the move. A box it already overlaps, as after a teleport
        // into the ground, is behind that side and does not stop it, so the player can walk out.
        var near = axis == 1 ? (distance > 0 ? Position.Y + Height : Position.Y) : (axis == 0 ? Position.X : Position.Z) + (distance > 0 ? HalfWidth : -HalfWidth);
        var stop = distance > 0 ? float.MaxValue : float.MinValue;
        var hit = false;
        // From a block under the box too, since a fence's box stands half a block above its cell.
        for (int y = Floor(min.Y + Skin) - 1; y <= Floor(max.Y - Skin); y++)
            for (int z = Floor(min.Z + Skin); z <= Floor(max.Z - Skin); z++)
                for (int x = Floor(min.X + Skin); x <= Floor(max.X - Skin); x++)
                    foreach (var box in world.BoxesAt(x, y, z))
                    {
                        Vector3 low = new Vector3(x, y, z) + box.Min, high = new Vector3(x, y, z) + box.Max;
                        if (!Overlap(low, high, min, max)) continue;
                        float lo = Component(low, axis), hi = Component(high, axis);
                        if (distance > 0 ? lo < near - Skin : hi > near + Skin) continue;
                        hit = true;
                        // Where the box's middle stands when its side touches the near face of what stops it.
                        if (distance > 0) stop = MathF.Min(stop, lo - (axis == 1 ? Height : HalfWidth));
                        else stop = MathF.Max(stop, hi + (axis == 1 ? 0 : HalfWidth));
                    }
        if (hit) Set(ref moved, axis, stop);
        Position = moved;
        return hit;
    }

    // Whether two boxes overlap by more than a hair on every axis.
    private static bool Overlap(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax) =>
        aMin.X < bMax.X - Skin && aMax.X > bMin.X + Skin && aMin.Y < bMax.Y - Skin && aMax.Y > bMin.Y + Skin && aMin.Z < bMax.Z - Skin && aMax.Z > bMin.Z + Skin;

    /// <summary>Whether the box at a position overlaps nothing solid.</summary>
    public bool Fits(VoxelWorld world, Vector3 at)
    {
        Vector3 min = at - new Vector3(HalfWidth, 0, HalfWidth), max = at + new Vector3(HalfWidth, Height, HalfWidth);
        for (int y = Floor(min.Y + Skin) - 1; y <= Floor(max.Y - Skin); y++)
            for (int z = Floor(min.Z + Skin); z <= Floor(max.Z - Skin); z++)
                for (int x = Floor(min.X + Skin); x <= Floor(max.X - Skin); x++)
                    foreach (var box in world.BoxesAt(x, y, z))
                        if (Overlap(new Vector3(x, y, z) + box.Min, new Vector3(x, y, z) + box.Max, min, max)) return false;
        return true;
    }

    // Whether a solid box's top lies under the box's footprint at a position, within a hair of its feet.
    private bool Supported(VoxelWorld world, Vector3 at)
    {
        for (int y = Floor(at.Y - 0.01f) - 1; y <= Floor(at.Y - 0.01f); y++)
            for (int z = Floor(at.Z - HalfWidth + Skin); z <= Floor(at.Z + HalfWidth - Skin); z++)
                for (int x = Floor(at.X - HalfWidth + Skin); x <= Floor(at.X + HalfWidth - Skin); x++)
                    foreach (var box in world.BoxesAt(x, y, z))
                    {
                        var top = y + box.Max.Y;
                        if (MathF.Abs(at.Y - top) > 0.01f) continue;
                        if (x + box.Min.X < at.X + HalfWidth - Skin && x + box.Max.X > at.X - HalfWidth + Skin
                            && z + box.Min.Z < at.Z + HalfWidth - Skin && z + box.Max.Z > at.Z - HalfWidth + Skin) return true;
                    }
        return false;
    }

    private static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

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
