namespace Engine.Game;

/// <summary>
/// A ravine, a long narrow cut in the ground open to the sky as Minecraft's are, up to seven blocks
/// wide at its middle and narrowing to its ends, its floor up to thirty blocks under the ground, a
/// few magma blocks glowing on its floor.
/// </summary>
/// <remarks>
/// Its line runs across its cell along a way the start's hash picks, swaying to either side, so
/// each column it crosses finds the same line from the start alone and cuts its own part, from the
/// ground the generator makes at each place down. It cuts nothing beside ground at the sea's level,
/// so no water stands beside its air, and it clears whatever grows over its cut, so no tree hangs
/// over it.
/// </remarks>
public sealed class Ravine : Structure
{
    public override string Name => "ravine";
    public override int Size => 56;
    protected override int Chance => 20;
    protected override int Salt => 0x4a71;

    // Half its length, and how far it sways to either side of its line.
    private const float Reach = 24, Sway = 3;

    protected override bool Suits(Biome biome, int ground, uint hash, out int y)
    {
        y = ground;
        return ground > Overworld.SeaLevel + 3;
    }

    public override void Build(ColumnClip clip, StructureStart start, Overworld land)
    {
        var angle = start.Hash % 360 * MathF.PI / 180;
        float ax = MathF.Cos(angle), az = MathF.Sin(angle), phase = start.Hash / 360 % 100 / 16f;
        float cx = start.X + Size / 2f, cz = start.Z + Size / 2f;

        for (int z = Math.Max(start.Z, clip.MinZ); z < Math.Min(start.Z + Size, clip.MinZ + Section.Size); z++)
            for (int x = Math.Max(start.X, clip.MinX); x < Math.Min(start.X + Size, clip.MinX + Section.Size); x++)
            {
                // Along the line and across it, from the cell's middle.
                float dx = x + 0.5f - cx, dz = z + 0.5f - cz;
                float along = dx * ax + dz * az, across = -dx * az + dz * ax;
                if (MathF.Abs(along) > Reach) continue;
                var middle = 1 - along * along / (Reach * Reach);
                across -= Sway * MathF.Sin(along / Reach * 2.5f + phase);
                if (MathF.Abs(across) > 1 + 2.5f * middle) continue;

                var ground = land.At(x, z).Height;
                if (NearWater(land, x, z, ground)) continue;
                var floor = Math.Max(2, ground - (int)(8 + 22 * middle));
                clip.Fill(x, floor, z, x, ground + 12, z, BlockId.Air);
                if (PlaceHash.Of(x, z, (int)start.Hash) % 13 == 0) clip.Set(x, floor - 1, z, BlockId.Magma);
            }
    }

    // Whether the ground at a place or beside it lies at the sea's level or under it.
    private static bool NearWater(Overworld land, int x, int z, int ground)
    {
        if (ground <= Overworld.SeaLevel + 1) return true;
        foreach (var (dx, dz) in (ReadOnlySpan<(int, int)>)[(1, 0), (-1, 0), (0, 1), (0, -1)])
            if (land.At(x + dx, z + dz).Height <= Overworld.SeaLevel) return true;
        return false;
    }
}
