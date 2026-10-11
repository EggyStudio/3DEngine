namespace Engine.Game;

/// <summary>A cell a mob's feet stand in on its way.</summary>
public readonly record struct Step(int X, int Y, int Z);

/// <summary>
/// Finds a mob's way between two cells through the blocks, as Minecraft's walking mobs find theirs:
/// along the ground, through the eight cells around, up a block, down as far as three, and through
/// water.
/// </summary>
/// <remarks>
/// A cell can be stood in where its block and those above it up to the mob's height stop nothing,
/// and the block under it is stood on, a whole block, a slab or stairs and not a fence, or the cell
/// is water, which the mob swims in. A step to a corner is taken only where both cells beside it
/// along the way are open, so the mob does not cut a wall's corner. A shut door is open to a mob that
/// opens doors and to no other. The search is A* over the cells within a reach of the start, and
/// gives up after so many cells, so a goal walled off costs a bounded search and no way.
/// </remarks>
public static class Pathfinder
{
    private const float Water = 8, Climb = 1, Door = 1;
    private static readonly (int X, int Z)[] Around = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    /// <summary>
    /// The cells from the one after <paramref name="from"/> to <paramref name="to"/>, or null where
    /// none is found within <paramref name="reach"/> blocks of the start along x and z and
    /// <paramref name="most"/> cells searched.
    /// </summary>
    public static List<Step>? Find(VoxelWorld world, MobKind kind, Step from, Step to, int reach = 24, int most = 2000)
    {
        if (!Stands(world, kind, to)) return null;
        var tall = (int)MathF.Ceiling(kind.Height);
        var open = new PriorityQueue<Step, float>();
        var came = new Dictionary<Step, Step>();
        var cost = new Dictionary<Step, float> { [from] = 0 };
        open.Enqueue(from, Guess(from, to));
        var searched = 0;

        while (open.TryDequeue(out var at, out _))
        {
            if (at == to) return Walk(came, from, to);
            if (++searched > most) return null;
            var here = cost[at];
            foreach (var (dx, dz) in Around)
            {
                var diagonal = dx != 0 && dz != 0;
                // A corner is cut only where the two cells beside it are open at this height.
                if (diagonal && (!Clear(world, kind, at.X + dx, at.Y, at.Z, tall) || !Clear(world, kind, at.X, at.Y, at.Z + dz, tall))) continue;
                if (Next(world, kind, at, dx, dz, tall) is not { } next) continue;
                if (Math.Abs(next.X - from.X) > reach || Math.Abs(next.Z - from.Z) > reach) continue;
                var step = here + (diagonal ? 1.414f : 1) + (next.Y > at.Y ? Climb : 0) + (at.Y - next.Y) * 0.25f
                    + (Blocks.IsWater(world.GetBlock(next.X, next.Y, next.Z)) ? Water : 0)
                    + (IsShutDoor(world.GetBlock(next.X, next.Y, next.Z)) ? Door : 0);
                if (cost.TryGetValue(next, out var known) && known <= step) continue;
                cost[next] = step;
                came[next] = at;
                open.Enqueue(next, step + Guess(next, to));
            }
        }
        return null;
    }

    /// <summary>Whether a mob of a kind can stand with its feet in a cell.</summary>
    public static bool Stands(VoxelWorld world, MobKind kind, Step at)
    {
        var tall = (int)MathF.Ceiling(kind.Height);
        return Clear(world, kind, at.X, at.Y, at.Z, tall) && (Floor(world, at.X, at.Y - 1, at.Z) || Blocks.IsWater(world.GetBlock(at.X, at.Y, at.Z)));
    }

    /// <summary>Whether a block is the lower or upper half of a shut door.</summary>
    public static bool IsShutDoor(BlockId block) => block is >= BlockId.OakDoor and <= BlockId.OakDoorWestTop;

    // The cell a step along x and z lands in from a cell: level, a block up where the mob has the
    // room to jump, or down as far as three where it drops.
    private static Step? Next(VoxelWorld world, MobKind kind, Step at, int dx, int dz, int tall)
    {
        int x = at.X + dx, z = at.Z + dz;
        if (Stands(world, kind, new Step(x, at.Y, z))) return new Step(x, at.Y, z);
        if (Clear(world, kind, at.X, at.Y + tall, at.Z, 1) && Stands(world, kind, new Step(x, at.Y + 1, z))) return new Step(x, at.Y + 1, z);
        if (!Clear(world, kind, x, at.Y, z, tall)) return null;
        for (int down = 1; down <= 3; down++)
            if (Stands(world, kind, new Step(x, at.Y - down, z))) return new Step(x, at.Y - down, z);
        return null;
    }

    // Whether the cells from a height up through so many stop nothing, a shut door counting as open
    // to a mob that opens it.
    private static bool Clear(VoxelWorld world, MobKind kind, int x, int y, int z, int tall)
    {
        for (int i = 0; i < tall; i++)
        {
            if (world.BoxesAt(x, y + i, z).Count == 0) continue;
            if (!kind.OpensDoors || !IsDoor(world.GetBlock(x, y + i, z))) return false;
        }
        return true;
    }

    // Whether a block's top can be stood on, a block that stops a body with no box higher than its
    // cell's top, so a whole block, a slab or stairs and not a fence, whose top stands above it.
    private static bool Floor(VoxelWorld world, int x, int y, int z)
    {
        var boxes = world.BoxesAt(x, y, z);
        if (boxes.Count == 0 || IsDoor(world.GetBlock(x, y, z))) return false;
        foreach (var box in boxes)
            if (box.Max.Y > 1.001f) return false;
        return true;
    }

    private static bool IsDoor(BlockId block) => block is >= BlockId.OakDoor and <= BlockId.OakDoorWestTopOpen;

    private static float Guess(Step a, Step b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static List<Step> Walk(Dictionary<Step, Step> came, Step from, Step to)
    {
        var path = new List<Step>();
        for (var at = to; at != from; at = came[at]) path.Add(at);
        path.Reverse();
        return path;
    }
}
