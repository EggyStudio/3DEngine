namespace Engine.Game;

/// <summary>
/// A column seen through world positions, which takes the blocks written inside it and lets go of
/// those outside, so a tree or a structure is placed whole by every column it crosses, each keeping
/// its own part.
/// </summary>
/// <remarks>
/// A column is generated alone on a worker, and the columns around it may be generating at the same
/// moment or not at all. A feature or a structure that crosses an edge is therefore placed by each
/// column it reaches from where it starts, which the seed and the ground there decide, and each
/// column keeps the blocks that fall inside it, as Minecraft places a structure's pieces chunk by
/// chunk from where it starts. A block outside reads as air.
/// </remarks>
public readonly struct ColumnClip(ChunkColumn column)
{
    public int MinX => column.X * Section.Size;

    public int MinZ => column.Z * Section.Size;

    public bool Inside(int x, int y, int z) =>
        (uint)(x - MinX) < Section.Size && (uint)(z - MinZ) < Section.Size && (uint)y < ChunkColumn.Height;

    public BlockId Get(int x, int y, int z) => Inside(x, y, z) ? column.Get(x - MinX, y, z - MinZ) : BlockId.Air;

    public void Set(int x, int y, int z, BlockId block)
    {
        if (Inside(x, y, z)) column.Set(x - MinX, y, z - MinZ, block);
    }

    /// <summary>Places a block only where there is air, as leaves grow around what stands there.</summary>
    public void Grow(int x, int y, int z, BlockId block)
    {
        if (Inside(x, y, z) && column.Get(x - MinX, y, z - MinZ) == BlockId.Air) column.Set(x - MinX, y, z - MinZ, block);
    }

    /// <summary>Fills a box between two corners, both included, with a block.</summary>
    public void Fill(int x0, int y0, int z0, int x1, int y1, int z1, BlockId block)
    {
        for (int y = Math.Max(y0, 0); y <= Math.Min(y1, ChunkColumn.Height - 1); y++)
            for (int z = Math.Max(z0, MinZ); z <= Math.Min(z1, MinZ + Section.Mask); z++)
                for (int x = Math.Max(x0, MinX); x <= Math.Min(x1, MinX + Section.Mask); x++)
                    column.Set(x - MinX, y, z - MinZ, block);
    }

    /// <summary>Whether a box between two corners reaches into the column.</summary>
    public bool Touches(int x0, int z0, int x1, int z1) =>
        x1 >= MinX && x0 <= MinX + Section.Mask && z1 >= MinZ && z0 <= MinZ + Section.Mask;
}

/// <summary>Something a generator places on the ground once the terrain is made, a tree or a cactus.</summary>
public interface IFeature
{
    /// <summary>How far the feature reaches from where it stands on each side, at most a column's width, so only the columns it can reach place it.</summary>
    int Reach { get; }

    /// <summary>Whether the feature grows on a block of ground.</summary>
    bool GrowsOn(BlockId ground);

    /// <summary>Places the feature standing on the ground block at a world position, varied by a hash of that place.</summary>
    void Place(ColumnClip clip, int x, int y, int z, uint hash);
}

/// <summary>An oak of four to six logs under a round crown of leaves.</summary>
public sealed class OakTree : IFeature
{
    public int Reach => 2;

    public bool GrowsOn(BlockId ground) => ground is BlockId.Grass or BlockId.Snow;

    public void Place(ColumnClip clip, int x, int y, int z, uint hash)
    {
        var height = 4 + (int)(hash % 3);
        var top = y + height;
        if (top + 2 >= ChunkColumn.Height) return;

        clip.Set(x, y, z, BlockId.Dirt);
        for (int i = 1; i <= height; i++) clip.Set(x, y + i, z, BlockId.OakLog);
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
                    clip.Grow(x + dx, top + dy, z + dz, BlockId.OakLeaves);
                }
        }
    }
}

/// <summary>A birch of five to seven pale logs under a narrow crown.</summary>
public sealed class BirchTree : IFeature
{
    public int Reach => 2;

    public bool GrowsOn(BlockId ground) => ground is BlockId.Grass or BlockId.Snow;

    public void Place(ColumnClip clip, int x, int y, int z, uint hash)
    {
        var height = 5 + (int)(hash % 3);
        var top = y + height;
        if (top + 2 >= ChunkColumn.Height) return;

        clip.Set(x, y, z, BlockId.Dirt);
        for (int i = 1; i <= height; i++) clip.Set(x, y + i, z, BlockId.BirchLog);
        for (int dy = -2; dy <= 1; dy++)
        {
            // Two wide layers, then a cross, then the single block on top, as Minecraft's birch has.
            var radius = dy < 0 ? 2 : 1;
            for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var corner = Math.Abs(dx) == radius && Math.Abs(dz) == radius;
                    if (corner && (dy >= 0 || ((hash >> ((dx + 2 + (dz + 2) * 5) & 31)) & 1) == 0)) continue;
                    if (dy == 1 && (dx != 0 || dz != 0)) continue;
                    clip.Grow(x + dx, top + dy, z + dz, BlockId.BirchLeaves);
                }
        }
    }
}

/// <summary>A spruce of six to nine dark logs under a crown that narrows in rings to a point.</summary>
public sealed class SpruceTree : IFeature
{
    public int Reach => 2;

    public bool GrowsOn(BlockId ground) => ground is BlockId.Grass or BlockId.Snow or BlockId.Stone;

    public void Place(ColumnClip clip, int x, int y, int z, uint hash)
    {
        var height = 6 + (int)(hash % 4);
        var top = y + height;
        if (top + 2 >= ChunkColumn.Height) return;

        clip.Set(x, y, z, BlockId.Dirt);
        for (int i = 1; i <= height; i++) clip.Set(x, y + i, z, BlockId.SpruceLog);
        // Rings from the top down: a point, then alternately one and two blocks out, the leaves
        // starting two blocks above the ground.
        for (int dy = 1, ring = 0; top + dy > y + 2; dy--, ring++)
        {
            var radius = ring == 0 ? 0 : ring % 2 == 1 ? 1 : 2;
            for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (radius > 0 && Math.Abs(dx) == radius && Math.Abs(dz) == radius) continue;
                    clip.Grow(x + dx, top + dy, z + dz, BlockId.SpruceLeaves);
                }
        }
    }
}

/// <summary>A cactus one to three blocks tall, on sand.</summary>
public sealed class CactusFeature : IFeature
{
    public int Reach => 0;

    public bool GrowsOn(BlockId ground) => ground == BlockId.Sand;

    public void Place(ColumnClip clip, int x, int y, int z, uint hash)
    {
        var height = 1 + (int)(hash % 3);
        for (int i = 1; i <= height && y + i < ChunkColumn.Height; i++) clip.Set(x, y + i, z, BlockId.Cactus);
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
