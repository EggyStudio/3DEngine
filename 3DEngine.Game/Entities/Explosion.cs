using System.Numerics;

namespace Engine.Game;

/// <summary>What an explosion breaks and the harm it does, as Minecraft's explosions are worked out.</summary>
/// <remarks>
/// Rays go out from the middle toward the 1,352 points on the faces of a cube of 16 a side, each
/// with a strength of the power times 0.7 to 1.3, and every step of 0.3 blocks along one takes 0.225
/// from it and, in a block, that block's resistance and 0.3, times 0.3. A block a ray reaches with
/// strength left is broken, so a creeper's power of 3 takes a hollow of dirt about three blocks
/// across and little stone, and water, whose resistance is 100, shelters what lies under it.
/// </remarks>
public static class Explosion
{
    /// <summary>The cells an explosion of a power at a place breaks.</summary>
    public static HashSet<(int X, int Y, int Z)> Breaks(VoxelWorld world, Vector3 at, float power, Random random)
    {
        var broken = new HashSet<(int, int, int)>();
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 16; j++)
                for (int k = 0; k < 16; k++)
                {
                    if (i is > 0 and < 15 && j is > 0 and < 15 && k is > 0 and < 15) continue;
                    var way = Vector3.Normalize(new Vector3(i / 15f * 2 - 1, j / 15f * 2 - 1, k / 15f * 2 - 1)) * 0.3f;
                    var strength = power * (0.7f + (float)random.NextDouble() * 0.6f);
                    for (var p = at; strength > 0; p += way)
                    {
                        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y), z = (int)MathF.Floor(p.Z);
                        var block = world.GetBlock(x, y, z);
                        if (block != BlockId.Air) strength -= (Loot.Resistance(block) + 0.3f) * 0.3f;
                        if (strength > 0 && block != BlockId.Air && !Blocks.IsWater(block) && Blocks.Get(block).Breakable) broken.Add((x, y, z));
                        strength -= 0.225f;
                    }
                }
        return broken;
    }

    /// <summary>How much of an explosion reaches a box, the share of the points of a grid of three a side over it that the middle sees.</summary>
    public static float Exposure(VoxelWorld world, Vector3 at, Vector3 min, Vector3 max)
    {
        int seen = 0;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                for (int k = 0; k < 3; k++)
                    if (Mob.Sees(world, at, min + (max - min) * new Vector3(i / 2f, j / 2f, k / 2f))) seen++;
        return seen / 27f;
    }

    /// <summary>
    /// The damage at a distance from an explosion of a power with so much of it reaching, none at
    /// twice the power or more, Minecraft's (impact squared plus impact) halved, times seven times
    /// twice the power, and one, where the impact falls from 1 at the middle to 0 at twice the power.
    /// </summary>
    public static int Damage(float distance, float power, float exposure)
    {
        if (distance >= power * 2) return 0;
        var impact = (1 - distance / (power * 2)) * exposure;
        return (int)((impact * impact + impact) / 2 * 7 * power * 2 + 1);
    }

    /// <summary>How hard an explosion pushes at a distance, the impact of <see cref="Damage"/>.</summary>
    public static float Impact(float distance, float power, float exposure) => distance >= power * 2 ? 0 : (1 - distance / (power * 2)) * exposure;
}
