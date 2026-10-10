namespace Engine.Game;

/// <summary>Something a generator places on the ground once the terrain is made, a tree now and a structure later.</summary>
/// <remarks>
/// A feature writes into the column it starts in and no other, so a column is generated alone on a
/// worker. One that would cross into a neighbor is placed only where it fits, which keeps trees two
/// blocks from a column's edge. Features and structures that span columns need a pass after the
/// neighbors exist, as Minecraft decorates a chunk once those around it are generated, and that pass
/// is not written yet.
/// </remarks>
public interface IFeature
{
    /// <summary>How far the feature reaches from where it stands on each side, so it is placed only where it fits.</summary>
    int Reach { get; }

    /// <summary>Places the feature standing on the block at a place in the column, varied by a hash of that place.</summary>
    void Place(ChunkColumn column, int x, int y, int z, uint hash);
}

/// <summary>An oak of four to six logs under a round crown of leaves.</summary>
public sealed class OakTree : IFeature
{
    public int Reach => 2;

    public void Place(ChunkColumn column, int x, int y, int z, uint hash)
    {
        var height = 4 + (int)(hash % 3);
        var top = y + height;
        if (top + 2 >= ChunkColumn.Height) return;

        column.Set(x, y, z, BlockId.Dirt);
        for (int dy = -2; dy <= 1; dy++)
        {
            var radius = dy < 0 ? 2 : 1;
            for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // Corners are left out at random below and always on top, so the crown is round.
                    var corner = Math.Abs(dx) == radius && Math.Abs(dz) == radius;
                    var bit = (dx + 2 + (dz + 2) * 5 + (dy + 2) * 25) & 31;
                    if (corner && (dy == 1 || ((hash >> bit) & 1) == 0)) continue;
                    if (column.Get(x + dx, top + dy, z + dz) == BlockId.Air)
                        column.Set(x + dx, top + dy, z + dz, BlockId.OakLeaves);
                }
        }
        for (int i = 1; i <= height; i++) column.Set(x, y + i, z, BlockId.OakLog);
    }
}

/// <summary>A hash of a place and a seed, the same on every run, so the features a seed gives repeat.</summary>
public static class PlaceHash
{
    public static uint Of(int x, int z, int seed)
    {
        unchecked
        {
            var h = (uint)x * 0x27D4EB2Du ^ (uint)z * 0x165667B1u ^ (uint)seed * 0x9E3779B9u;
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            h ^= h >> 13;
            h *= 0xC2B2AE3Du;
            h ^= h >> 16;
            return h;
        }
    }
}
