using System.Numerics;

namespace Engine.Game;

/// <summary>A stack of an item lying in the world, which falls, slides to a stop and is picked up.</summary>
public sealed class ItemDrop(Item item, int count, Vector3 at, Vector3 velocity, float delay, int wear = 0)
{
    public Item Item { get; } = item;

    public int Count { get; set; } = count;

    /// <summary>How worn a tool dropped is, which it keeps when picked up again.</summary>
    public int Wear { get; } = wear;

    /// <summary>Its box, a quarter of a block a side.</summary>
    public Body Body { get; } = new(0.125f, 0.25f) { Position = at, Velocity = velocity };

    /// <summary>Seconds since it was dropped.</summary>
    public float Age { get; set; }

    /// <summary>Seconds from its drop before it can be picked up.</summary>
    public float Delay { get; } = delay;
}

/// <summary>
/// The stacks of items lying in the loaded columns: dropped by a block broken in survival, by a mob
/// as it dies and by the player, picked up when the player comes near, and gone after five minutes,
/// as Minecraft's are.
/// </summary>
/// <remarks>
/// A stack can be picked up half a second after it is dropped, two seconds after the player throws
/// it, so a stack thrown is not taken back at once, and two stacks of one item within half a block
/// of each other join. A stack in a column not loaded waits there, its five minutes held, until the
/// column loads again, and no stack is saved with the world.
/// </remarks>
public sealed class ItemDrops
{
    /// <summary>Seconds before a stack a block or a mob dropped can be picked up, and one the player threw.</summary>
    public const float Delay = 0.5f, ThrownDelay = 2;

    private const float Lifetime = 300, Gravity = 16, Join = 0.5f;

    private readonly List<ItemDrop> _drops = [];
    private readonly Random _random = new();

    public IReadOnlyList<ItemDrop> All => _drops;

    /// <summary>Drops a stack at a place with a velocity, which can be picked up after a delay.</summary>
    public ItemDrop Spawn(Item item, int count, Vector3 at, Vector3 velocity, float delay = Delay, int wear = 0)
    {
        var drop = new ItemDrop(item, count, at, velocity, delay, wear);
        _drops.Add(drop);
        return drop;
    }

    /// <summary>Drops a stack from a block's middle, hopping a little in a direction of its own, as a broken block's does.</summary>
    public void Pop(Item item, int count, int x, int y, int z)
    {
        var angle = (float)_random.NextDouble() * MathF.Tau;
        var velocity = new Vector3(MathF.Cos(angle) * 1.2f, 3, MathF.Sin(angle) * 1.2f);
        Spawn(item, count, new Vector3(x + 0.5f, y + 0.375f, z + 0.5f), velocity);
    }

    /// <summary>
    /// Moves every stack whose column is loaded, joins stacks of one item lying together, puts into
    /// the inventory the stacks the player's box reaches within a block across and half a block up
    /// and down unless <paramref name="pickUp"/> is clear, as while the player lies dead, and takes
    /// away those five minutes old.
    /// </summary>
    /// <returns>How many items the player picked up.</returns>
    public int Update(VoxelWorld world, Body player, Inventory inventory, float seconds, bool pickUp = true)
    {
        var picked = 0;
        Vector3 reachMin = player.Min - new Vector3(1, 0.5f, 1), reachMax = player.Max + new Vector3(1, 0.5f, 1);
        foreach (var drop in _drops)
        {
            if (!world.HasColumn(VoxelWorld.ColumnOf((int)MathF.Floor(drop.Body.Position.X)), VoxelWorld.ColumnOf((int)MathF.Floor(drop.Body.Position.Z)))) continue;
            drop.Age += seconds;
            Move(world, drop, seconds);
            if (!pickUp || drop.Age < drop.Delay || drop.Count == 0) continue;
            Vector3 min = drop.Body.Min, max = drop.Body.Max;
            if (min.X > reachMax.X || max.X < reachMin.X || min.Y > reachMax.Y || max.Y < reachMin.Y || min.Z > reachMax.Z || max.Z < reachMin.Z) continue;
            var left = inventory.Add(drop.Item, drop.Count, drop.Wear);
            picked += drop.Count - left;
            drop.Count = left;
        }
        JoinStacks();
        _drops.RemoveAll(d => d.Count == 0 || d.Age > Lifetime);
        return picked;
    }

    private static void Move(VoxelWorld world, ItemDrop drop, float seconds)
    {
        var body = drop.Body;
        var middle = body.Position + new Vector3(0, 0.125f, 0);
        if (Blocks.IsWater(world.GetBlock((int)MathF.Floor(middle.X), (int)MathF.Floor(middle.Y), (int)MathF.Floor(middle.Z))))
        {
            // A stack floats up through water, slowed by it.
            body.Velocity.Y = MathF.Min(body.Velocity.Y + 10 * seconds, 1);
            body.Velocity.X *= MathF.Exp(-3 * seconds);
            body.Velocity.Z *= MathF.Exp(-3 * seconds);
        }
        else
        {
            body.Velocity.Y = MathF.Max(body.Velocity.Y - Gravity * seconds, -40);
            // It slides to a stop on the ground and drifts a little in the air.
            var drag = MathF.Exp(-(body.OnGround ? 10 : 0.5f) * seconds);
            body.Velocity.X *= drag;
            body.Velocity.Z *= drag;
        }
        body.Move(world, body.Velocity * seconds, keepToEdges: false);
    }

    // Each stack takes into itself the stacks of its item lying within half a block, as much as it holds.
    private void JoinStacks()
    {
        for (int i = 0; i < _drops.Count; i++)
        {
            var a = _drops[i];
            if (a.Count == 0 || a.Count >= a.Item.MostInStack) continue;
            for (int j = i + 1; j < _drops.Count; j++)
            {
                var b = _drops[j];
                if (b.Count == 0 || b.Item != a.Item || Vector3.DistanceSquared(a.Body.Position, b.Body.Position) > Join * Join) continue;
                var moved = Math.Min(b.Count, a.Item.MostInStack - a.Count);
                (a.Count, b.Count) = (a.Count + moved, b.Count - moved);
                if (a.Count >= a.Item.MostInStack) break;
            }
        }
    }

    public void Clear() => _drops.Clear();
}
