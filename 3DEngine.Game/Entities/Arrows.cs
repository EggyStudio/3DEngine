using System.Numerics;

namespace Engine.Game;

/// <summary>An arrow in flight or stuck in a block.</summary>
public sealed class Arrow(Vector3 at, Vector3 velocity)
{
    public Vector3 Position = at;

    public Vector3 Velocity = velocity;

    /// <summary>Whether it has struck a block and stays where it struck.</summary>
    public bool Stuck { get; set; }

    /// <summary>Seconds since it was loosed.</summary>
    public float Age { get; set; }
}

/// <summary>
/// The arrows skeletons loose, falling under Minecraft's arrow gravity and slowed a little by the
/// air, which strike the player's box for damage by their speed or stick in the first block they
/// meet, and are gone a minute after they were loosed.
/// </summary>
public sealed class Arrows
{
    /// <summary>The pull on an arrow in blocks a second each second, Minecraft's 0.05 a tick.</summary>
    public const float Gravity = 20;

    private const float Life = 60, Step = 0.1f;

    private readonly List<Arrow> _arrows = [];
    private readonly Random _random = new();

    public IReadOnlyList<Arrow> All => _arrows;

    public void Shoot(Vector3 from, Vector3 velocity) => _arrows.Add(new Arrow(from, velocity));

    /// <summary>Moves every arrow in flight, a step of a tenth of a block at a time, and gives the damage of each that struck the player and where it came from.</summary>
    public List<(int Damage, Vector3 From)> Update(VoxelWorld world, Body player, float seconds)
    {
        var hits = new List<(int, Vector3)>();
        _arrows.RemoveAll(arrow =>
        {
            arrow.Age += seconds;
            if (arrow.Age > Life) return true;
            if (arrow.Stuck) return false;
            arrow.Velocity.Y -= Gravity * seconds;
            arrow.Velocity *= MathF.Exp(-0.2f * seconds);
            var move = arrow.Velocity * seconds;
            var steps = Math.Max(1, (int)MathF.Ceiling(move.Length() / Step));
            for (int i = 0; i < steps; i++)
            {
                arrow.Position += move / steps;
                if (Inside(player.Min - new Vector3(0.05f), player.Max + new Vector3(0.05f), arrow.Position))
                {
                    // As Minecraft's, two points of damage for each block a tick it flies, rounded up, with a chance of one more.
                    var damage = (int)MathF.Ceiling(arrow.Velocity.Length() / 20 * 2) + (_random.Next(4) == 0 ? 1 : 0);
                    hits.Add((damage, arrow.Position - arrow.Velocity * 0.05f));
                    return true;
                }
                int x = (int)MathF.Floor(arrow.Position.X), y = (int)MathF.Floor(arrow.Position.Y), z = (int)MathF.Floor(arrow.Position.Z);
                foreach (var box in world.BoxesAt(x, y, z))
                    if (Inside(new Vector3(x, y, z) + box.Min, new Vector3(x, y, z) + box.Max, arrow.Position))
                    {
                        arrow.Stuck = true;
                        return false;
                    }
            }
            return false;
        });
        return hits;
    }

    private static bool Inside(Vector3 min, Vector3 max, Vector3 p) =>
        p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y && p.Z >= min.Z && p.Z <= max.Z;

    public void Clear() => _arrows.Clear();
}
