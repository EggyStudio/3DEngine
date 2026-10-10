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
}
