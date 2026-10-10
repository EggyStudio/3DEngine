using System.Numerics;
using Xunit;

namespace Engine.Game.Tests;

public class BodyTests
{
    // A floor of stone with its top at 11.
    private static VoxelWorld Floor() => Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Stone));

    [Fact]
    public void A_Falling_Body_Lands_On_The_Floor_And_Stands_On_It()
    {
        var world = Floor();
        var body = new PlayerBody { Position = new Vector3(4.5f, 15, 4.5f) };

        body.Move(world, new Vector3(0, -10, 0), keepToEdges: false);

        Assert.Equal(11, body.Position.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void A_Body_Walking_Into_A_Wall_Stops_With_Its_Side_On_The_Walls_Face()
    {
        var world = Floor();
        world.SetBlock(7, 11, 4, BlockId.Stone);
        world.SetBlock(7, 12, 4, BlockId.Stone);
        var body = new PlayerBody { Position = new Vector3(4.5f, 11, 4.5f) };

        body.Move(world, new Vector3(5, 0, 0), keepToEdges: false);

        Assert.Equal(7 - PlayerBody.HalfWidth, body.Position.X, 4);
    }

    [Fact]
    public void A_Sneaking_Body_Does_Not_Walk_Off_An_Edge()
    {
        var world = Floor();
        // A step of one block down at x 6.
        for (int z = 0; z < 16; z++)
            for (int x = 6; x < 16; x++)
                world.SetBlock(x, 10, z, BlockId.Air);
        var body = new PlayerBody { Position = new Vector3(5.5f, 11, 4.5f) };
        body.Move(world, new Vector3(0, -0.01f, 0), keepToEdges: false);

        body.Move(world, new Vector3(2, 0, 0), keepToEdges: true);

        Assert.True(body.Position.X < 6 + PlayerBody.HalfWidth);
        Assert.Equal(11, body.Position.Y);
    }

    [Fact]
    public void A_Body_Passes_Through_Water_And_Lands_On_The_Floor_Under_It()
    {
        var world = Laid.World(c =>
        {
            Laid.Fill(c, 0, 10, BlockId.Stone);
            Laid.Fill(c, 11, 14, BlockId.Water);
        });
        var body = new PlayerBody { Position = new Vector3(4.5f, 16, 4.5f) };

        body.Move(world, new Vector3(0, -10, 0), keepToEdges: false);

        Assert.Equal(11, body.Position.Y);
    }

    [Fact]
    public void The_Crosshairs_Ray_Passes_Through_Water_To_The_Floor()
    {
        var world = Laid.World(c =>
        {
            Laid.Fill(c, 0, 10, BlockId.Stone);
            Laid.Fill(c, 11, 12, BlockId.Water);
        });

        var hit = VoxelRay.Cast(world, new Vector3(4.5f, 13.5f, 4.5f), -Vector3.UnitY, 5);

        Assert.NotNull(hit);
        Assert.Equal((4, 10, 4), (hit.Value.X, hit.Value.Y, hit.Value.Z));
        Assert.Equal(1, hit.Value.NormalY);
    }

    [Fact]
    public void A_Body_Walking_Into_A_Slab_Climbs_Onto_It_And_Into_A_Block_Does_Not()
    {
        var world = Floor();
        world.SetBlock(6, 11, 4, BlockId.StoneSlab);
        world.SetBlock(6, 11, 8, BlockId.Stone);
        var onSlab = new PlayerBody { Position = new Vector3(4.5f, 11, 4.5f) };
        var atBlock = new PlayerBody { Position = new Vector3(4.5f, 11, 8.5f) };
        onSlab.Move(world, new Vector3(0, -0.01f, 0), keepToEdges: false);
        atBlock.Move(world, new Vector3(0, -0.01f, 0), keepToEdges: false);

        for (int i = 0; i < 10; i++)
        {
            onSlab.Move(world, new Vector3(0.2f, -0.01f, 0), keepToEdges: false);
            atBlock.Move(world, new Vector3(0.2f, -0.01f, 0), keepToEdges: false);
        }

        Assert.Equal(11.5f, onSlab.Position.Y, 4);
        Assert.Equal(6.5f, onSlab.Position.X, 4);
        Assert.Equal(11, atBlock.Position.Y, 4);
        Assert.Equal(6 - PlayerBody.HalfWidth, atBlock.Position.X, 4);
    }

    [Fact]
    public void A_Lantern_Stops_A_Body_And_A_Torch_Does_Not()
    {
        var world = Floor();
        world.SetBlock(6, 11, 4, BlockId.Lantern);
        world.SetBlock(6, 11, 8, BlockId.Torch);
        var atLantern = new PlayerBody { Position = new Vector3(4.5f, 11, 4.5f) };
        var atTorch = new PlayerBody { Position = new Vector3(4.5f, 11, 8.5f) };

        atLantern.Move(world, new Vector3(3, 0, 0), keepToEdges: false);
        atTorch.Move(world, new Vector3(3, 0, 0), keepToEdges: false);

        // The lantern's glass begins 5/16 into its cell, and the body climbs no lantern of half a block.
        Assert.Equal(6 + 5 / 16f - PlayerBody.HalfWidth, atLantern.Position.X, 4);
        Assert.Equal(7.5f, atTorch.Position.X, 4);
    }

    [Fact]
    public void A_Fence_Stands_Half_A_Block_Above_Its_Post_So_A_Jump_Does_Not_Clear_It()
    {
        var world = Floor();
        for (int z = 0; z < 16; z++) world.SetBlock(6, 11, z, BlockId.OakFence);
        // Rising by a block and a quarter, more than the fence's post and less than its box.
        var body = new PlayerBody { Position = new Vector3(4.5f, 12.25f, 4.5f) };

        body.Move(world, new Vector3(3, 0, 0), keepToEdges: false);

        Assert.True(body.Position.X < 6 + PlayerBody.HalfWidth);
    }
}
