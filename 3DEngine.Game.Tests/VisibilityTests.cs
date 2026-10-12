using System.Numerics;
using Xunit;

namespace Engine.Game.Tests;

public class VisibilityTests
{
    [Fact]
    public void A_Section_Of_Stone_Joins_No_Faces_And_One_Of_Air_Joins_Them_All()
    {
        var stone = new Section();
        for (int i = 0; i < Section.Volume; i++) stone.Set(i & 15, i >> 8, (i >> 4) & 15, BlockId.Stone);
        Assert.Equal(0UL, Visibility.Joins(stone));
        Assert.Equal((1UL << 36) - 1, Visibility.Joins(new Section()));

        // A tunnel along x through the stone joins its two ends and nothing else.
        for (int x = 0; x < 16; x++) stone.Set(x, 8, 8, BlockId.Air);
        var joins = Visibility.Joins(stone);
        Assert.NotEqual(0UL, joins & 1UL << (0 * 6 + 1));
        Assert.Equal(0UL, joins & 1UL << (0 * 6 + 2));
    }

    [Fact]
    public void From_A_Closed_Room_Under_The_Ground_The_Flood_Reaches_The_Room_Alone()
    {
        // Stone from the bottom to 60, the sky above it.
        var world = Laid.World(c => Laid.Fill(c, 0, 60, BlockId.Stone));
        for (int y = 20; y <= 26; y++)
            for (int z = 4; z <= 10; z++)
                for (int x = 4; x <= 10; x++) world.SetBlock(x, y, z, BlockId.Air);
        var visibility = new Visibility();

        visibility.Flood(world, new Vector3(7.5f, 22, 7.5f), 2);

        Assert.Equal(new HashSet<SectionKey> { new(0, 1, 0) }, visibility.Seen.Where(k => world.HasColumn(k.X, k.Z)).ToHashSet());
        Assert.False(visibility.Sees(new SectionKey(0, 4, 0)));

        // From the sky the flood reaches every column's top sections.
        visibility.Flood(world, new Vector3(7.5f, 100, 7.5f), 2);
        Assert.True(visibility.Sees(new SectionKey(1, 6, 1)) && visibility.Sees(new SectionKey(-1, 3, 0)));
        Assert.False(visibility.Sees(new SectionKey(0, 1, 0)));
    }
}
