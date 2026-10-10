using Xunit;

namespace Engine.Game.Tests;

public class GenerationTests
{
    private static ushort[] Blocks(ChunkColumn column) => [.. column.Sections.SelectMany(s => s.Blocks)];

    [Fact]
    public void A_Column_Is_Generated_The_Same_Every_Time()
    {
        var land = new Overworld(1);
        var first = land.Generate(5, -7);
        land.Generate(4, -7);
        var again = new Overworld(1).Generate(5, -7);

        Assert.Equal(Blocks(first), Blocks(again));
        Assert.Equal(first.Biome, again.Biome);
    }

    [Fact]
    public void A_Tree_On_A_Columns_Edge_Grows_Its_Crown_Into_The_Column_Beside_It()
    {
        // The first trunks found standing on the +x edge of a column, and the blocks beside each in
        // the next column, generated alone, about its top.
        var land = new Overworld(1);
        var checkedTrunks = 0;
        for (int cx = -20; cx < 20 && checkedTrunks < 5; cx++)
        {
            var column = land.Generate(cx, 2);
            var next = land.Generate(cx + 1, 2);
            for (int z = 2; z < 14; z++)
            {
                var top = -1;
                for (int y = 0; y < ChunkColumn.Height; y++)
                    if (column.Get(15, y, z) is BlockId.OakLog or BlockId.BirchLog or BlockId.SpruceLog) top = y;
                if (top < 0 || column.Get(14, top, z) is BlockId.OakLog or BlockId.BirchLog or BlockId.SpruceLog) continue;
                var crown = 0;
                for (int y = top - 2; y <= top + 1; y++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (next.Get(0, y, z + dz) != BlockId.Air) crown++;
                Assert.True(crown > 0, $"the trunk at column {cx}, x 15, z {z} up to {top} has no crown across its edge");
                checkedTrunks++;
            }
        }
        Assert.True(checkedTrunks > 0, "no trunk stood on a column's edge in the columns read");
    }

    [Fact]
    public void Water_Stands_To_Sea_Level_Over_Ground_Under_It()
    {
        var land = new Overworld(1);
        // The ground near the origin that lies lowest, which the survey of seed 1 puts under water.
        var (x, z) = Enumerable.Range(-20, 41).SelectMany(i => Enumerable.Range(-20, 41).Select(j => (X: i * 8, Z: j * 8)))
            .MinBy(p => land.At(p.X, p.Z).Height);
        var height = land.At(x, z).Height;
        Assert.True(height < Overworld.SeaLevel, $"the lowest ground found stands at {height}");

        var column = land.Generate(x >> 4, z >> 4);
        var top = column.Get(x & 15, Overworld.SeaLevel, z & 15);
        Assert.True(top is BlockId.Water or BlockId.Ice);
        Assert.Equal(BlockId.Air, column.Get(x & 15, Overworld.SeaLevel + 1, z & 15));
    }
}
