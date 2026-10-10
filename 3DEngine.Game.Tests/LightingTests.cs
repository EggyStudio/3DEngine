using Xunit;

namespace Engine.Game.Tests;

public class LightingTests
{
    // Solid stone from the bottom to 20, the open sky above it.
    private static VoxelWorld Stone() => Laid.World(c => Laid.Fill(c, 0, 20, BlockId.Stone));

    [Fact]
    public void The_Open_Sky_Falls_Down_A_Shaft_Without_Losing_A_Level()
    {
        var world = Stone();
        for (int y = 10; y <= 20; y++) world.SetBlock(8, y, 8, BlockId.Air);

        Assert.Equal(15, world.GetLight(8, 10, 8).Sky);
    }

    [Fact]
    public void A_Roofed_Shaft_With_No_Opening_Goes_Dark()
    {
        var world = Stone();
        for (int y = 10; y <= 20; y++) world.SetBlock(8, y, 8, BlockId.Air);
        world.SetBlock(8, 20, 8, BlockId.Stone);

        for (int y = 10; y < 20; y++) Assert.Equal(0, world.GetLight(8, y, 8).Sky);
    }

    [Fact]
    public void A_Lamp_Lights_A_Level_Less_A_Block_Away_And_Takes_It_Back_When_Broken()
    {
        var world = Stone();
        for (int y = 10; y <= 19; y++) world.SetBlock(8, y, 8, BlockId.Air);
        world.SetBlock(8, 10, 8, BlockId.Glowstone);

        Assert.Equal(15, world.GetLight(8, 10, 8).Block);
        Assert.Equal(14, world.GetLight(8, 11, 8).Block);
        Assert.Equal(10, world.GetLight(8, 15, 8).Block);

        world.SetBlock(8, 10, 8, BlockId.Air);
        for (int y = 10; y <= 19; y++) Assert.Equal(0, world.GetLight(8, y, 8).Block);
    }

    [Fact]
    public void The_Sky_Fades_Two_Levels_For_Each_Block_Of_Water()
    {
        var world = Laid.World(c =>
        {
            Laid.Fill(c, 0, 10, BlockId.Stone);
            Laid.Fill(c, 11, 14, BlockId.Water);
        });

        Assert.Equal(15, world.GetLight(3, 15, 3).Sky);
        Assert.Equal(13, world.GetLight(3, 14, 3).Sky);
        Assert.Equal(11, world.GetLight(3, 13, 3).Sky);
        Assert.Equal(7, world.GetLight(3, 11, 3).Sky);
    }

    [Fact]
    public void Glass_Lets_The_Sky_Through_As_Air_Does()
    {
        var world = Stone();
        for (int y = 10; y <= 20; y++) world.SetBlock(8, y, 8, BlockId.Air);
        world.SetBlock(8, 20, 8, BlockId.Glass);

        Assert.Equal(15, world.GetLight(8, 10, 8).Sky);
    }

    [Fact]
    public void A_Lamp_On_A_Columns_Edge_Lights_The_Column_Beside_It()
    {
        // A lamp at x 15 of every column, so the column at x 1 is lit across its edge at x 16.
        var world = Laid.World(c =>
        {
            Laid.Fill(c, 0, 10, BlockId.Stone);
            c.Set(15, 11, 8, BlockId.Glowstone);
        });

        Assert.Equal(14, world.GetLight(16, 11, 8).Block);
    }
}
