using Xunit;

namespace Engine.Game.Tests;

public class FluidTests
{
    // A floor of stone with its top at 11.
    private static VoxelWorld Floor() => Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Stone));

    [Fact]
    public void A_Source_On_A_Floor_Spreads_Seven_Blocks_Along_It_And_No_Further()
    {
        var world = Floor();
        world.SetBlock(8, 11, 8, BlockId.Water);

        world.Fluids.Update(5);

        for (int d = 1; d <= 7; d++) Assert.Equal(Blocks.FlowingWater(d), world.GetBlock(8 + d, 11, 8));
        Assert.Equal(BlockId.Air, world.GetBlock(16, 11, 8));
        Assert.Equal(BlockId.Air, world.GetBlock(8, 12, 8));
    }

    [Fact]
    public void Water_Off_A_Ledge_Falls_To_The_Floor_And_Spreads_There()
    {
        var world = Floor();
        for (int y = 11; y <= 14; y++) world.SetBlock(8, y, 8, BlockId.Stone);
        world.SetBlock(8, 15, 8, BlockId.Water);

        world.Fluids.Update(5);

        Assert.Equal(BlockId.Water1, world.GetBlock(9, 15, 8));
        for (int y = 11; y <= 14; y++) Assert.Equal(BlockId.FallingWater, world.GetBlock(9, y, 8));
        Assert.Equal(BlockId.Water1, world.GetBlock(10, 11, 8));
    }

    [Fact]
    public void Flowing_Water_Dries_When_Its_Source_Is_Taken_Away()
    {
        var world = Floor();
        world.SetBlock(8, 11, 8, BlockId.Water);
        world.Fluids.Update(5);

        world.SetBlock(8, 11, 8, BlockId.Air);
        world.Fluids.Update(20);

        for (int d = 0; d <= 7; d++) Assert.Equal(BlockId.Air, world.GetBlock(8 + d, 11, 8));
    }

    [Fact]
    public void Two_Sources_With_A_Gap_Between_Them_Make_A_Third_In_It()
    {
        var world = Floor();
        world.SetBlock(8, 11, 8, BlockId.Water);
        world.SetBlock(10, 11, 8, BlockId.Water);

        world.Fluids.Update(5);

        Assert.Equal(BlockId.Water, world.GetBlock(9, 11, 8));
    }

    [Fact]
    public void Flowing_Water_Stands_Lower_The_Further_It_Has_Flowed()
    {
        var world = Floor();
        world.SetBlock(8, 11, 8, BlockId.Water);
        world.Fluids.Update(5);

        var mesher = new SectionMesher();
        Assert.True(mesher.Mesh(world, new SectionKey(0, 0, 0)));
        // The tops of the source and of the water one and two blocks from it along x.
        var vertices = mesher.SeeThrough.Vertices;
        var tops = Enumerable.Range(0, vertices.Count / 4).Select(q => vertices.Skip(q * 4).Take(4).ToArray())
            .Where(f => f[0].Normal.Y > 0.5f).Select(f => (Middle: (f[0].Position + f[2].Position) / 2, Height: f[0].Position.Y)).ToArray();
        float TopAt(int x) => tops.Where(t => (int)MathF.Floor(t.Middle.X) == x && (int)MathF.Floor(t.Middle.Z) == 8).Select(t => t.Height).DefaultIfEmpty(-1).Max();
        Assert.True(TopAt(8) > TopAt(9) && TopAt(9) > TopAt(10), $"the tops stand at {TopAt(8)}, {TopAt(9)} and {TopAt(10)}");
    }
}
