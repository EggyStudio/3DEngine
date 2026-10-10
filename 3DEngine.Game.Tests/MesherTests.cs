using Xunit;

namespace Engine.Game.Tests;

public class MesherTests
{
    // A floor of bedrock at 0, so the section above it is meshed with the world around it loaded.
    private static VoxelWorld Floor() => Laid.World(c => c.Set(0, 0, 0, BlockId.Bedrock));

    private static SectionMesher Meshed(VoxelWorld world)
    {
        var mesher = new SectionMesher();
        Assert.True(mesher.Mesh(world, new SectionKey(0, 1, 0)));
        return mesher;
    }

    [Fact]
    public void A_Block_In_The_Air_Shows_Its_Six_Faces()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Stone);

        var mesher = Meshed(world);
        Assert.Equal(24, mesher.Solid.Vertices.Count);
        Assert.Equal(36, mesher.Solid.Indices.Count);
        Assert.Empty(mesher.SeeThrough.Vertices);
    }

    [Fact]
    public void Two_Blocks_Side_By_Side_Hide_The_Faces_Between_Them()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Stone);
        world.SetBlock(9, 20, 8, BlockId.Dirt);

        Assert.Equal(10 * 4, Meshed(world).Solid.Vertices.Count);
    }

    [Fact]
    public void Glass_Beside_Glass_Shows_No_Face_Between_And_Glass_Beside_Stone_Shows_The_Stone()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Glass);
        world.SetBlock(9, 20, 8, BlockId.Glass);
        world.SetBlock(7, 20, 8, BlockId.Stone);

        var mesher = Meshed(world);
        // The panes' ten faces less the one toward the stone, and the stone's six with its face toward the glass.
        Assert.Equal(9 * 4, mesher.SeeThrough.Vertices.Count);
        Assert.Equal(6 * 4, mesher.Solid.Vertices.Count);
    }

    [Fact]
    public void Water_Open_To_The_Air_Stands_An_Eighth_Low()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Water);

        var tops = Meshed(world).SeeThrough.Vertices.Select(v => v.Position.Y).Distinct().Order().ToArray();
        Assert.Equal(new[] { 4f, 4.875f }, tops);
    }

    [Fact]
    public void A_Block_That_Gives_Off_Light_Is_Left_Out_Of_The_Faces_For_Its_Own_Cube()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Glowstone);

        var mesher = Meshed(world);
        Assert.Empty(mesher.Solid.Vertices);
        Assert.Equal(BlockId.Glowstone, Assert.Single(mesher.Emitters).Block);
    }

    [Fact]
    public void A_Corner_Where_Two_Blocks_Meet_Is_Darker_Than_An_Open_One()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Stone);
        // Walls on two sides of the top face's corner at +x, +z.
        world.SetBlock(9, 21, 8, BlockId.Stone);
        world.SetBlock(8, 21, 9, BlockId.Stone);

        var mesher = Meshed(world);
        var top = mesher.Solid.Vertices.Select((v, i) => (v, i)).Where(p => p.v.Normal.Y > 0.5f && p.v.Position.Y == 5).ToArray();
        var closed = top.Single(p => p.v.Position.X == 9 && p.v.Position.Z == 9);
        var open = top.Single(p => p.v.Position.X == 8 && p.v.Position.Z == 8);
        Assert.True(mesher.Solid.Colors[closed.i].R < mesher.Solid.Colors[open.i].R);
    }
}
