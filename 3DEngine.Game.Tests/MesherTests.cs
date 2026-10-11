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
        Assert.Equal(24, mesher.Solid.Count);
        Assert.Equal(36, mesher.Solid.Indices.Count);
        Assert.Equal(2 * 24, mesher.Solid.Packed.Count);
        Assert.Equal(0, mesher.SeeThrough.Count);
    }

    [Fact]
    public void Two_Blocks_Side_By_Side_Hide_The_Faces_Between_Them()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Stone);
        world.SetBlock(9, 20, 8, BlockId.Dirt);

        Assert.Equal(10 * 4, Meshed(world).Solid.Count);
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
        Assert.Equal(9 * 4, mesher.SeeThrough.Count);
        Assert.Equal(6 * 4, mesher.Solid.Count);
    }

    [Fact]
    public void Water_Open_To_The_Air_Stands_An_Eighth_Low()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Water);

        var tops = Meshed(world).SeeThrough.Positions.Select(p => p.Y).Distinct().Order().ToArray();
        Assert.Equal(new[] { 4f, 4.875f }, tops);
    }

    [Fact]
    public void A_Block_That_Gives_Off_Light_Is_Left_Out_Of_The_Faces_For_Its_Own_Cube()
    {
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Glowstone);

        var mesher = Meshed(world);
        Assert.Equal(0, mesher.Solid.Count);
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
        var faces = mesher.Solid;
        var top = Enumerable.Range(0, faces.Count).Where(i => faces.Unpack(i).Face == 2 && faces.Positions[i].Y == 5).ToArray();
        var closed = top.Single(i => faces.Positions[i].X == 9 && faces.Positions[i].Z == 9);
        var open = top.Single(i => faces.Positions[i].X == 8 && faces.Positions[i].Z == 8);
        Assert.True(faces.Unpack(closed).Shade < faces.Unpack(open).Shade);
        Assert.True(faces.Colors[closed].R < faces.Colors[open].R);
    }

    [Fact]
    public void A_Corners_Packed_Bytes_Hold_Its_Face_Corner_Shade_And_Color_As_The_Shader_Reads_Them()
    {
        var (first, second) = SectionMesher.Pack(4, 3, 0.5f, new Color(200, 100, 50, 168));
        var (face, corner, shade, tint) = SectionMesher.Unpack(first, second);

        Assert.Equal((4, 3), (face, corner));
        Assert.Equal(128 / 255f, shade, 4);
        Assert.Equal(new Color(200, 100, 50, 168), tint);
    }

    [Fact]
    public void Each_Corner_Of_A_Face_Lies_Toward_The_Faces_Normal_From_The_Blocks_Middle()
    {
        // The face a corner's packed bytes name is the one its position lies on, so the shader's
        // normal from the face points out of the block the corner belongs to.
        var world = Floor();
        world.SetBlock(8, 20, 8, BlockId.Stone);

        var faces = Meshed(world).Solid;
        var middle = new System.Numerics.Vector3(8.5f, 4.5f, 8.5f);
        for (int i = 0; i < faces.Count; i++)
        {
            var normal = SectionMesher.Normal(faces.Unpack(i).Face);
            Assert.Equal(0.5f, System.Numerics.Vector3.Dot(faces.Positions[i] - middle, normal), 4);
        }
    }

    [Fact]
    public void A_Change_Of_Light_Keeps_The_Positions_And_Changes_The_Packed_Bytes()
    {
        // A closed cavity in stone, dark until a lamp is set in it.
        var world = Laid.World(c => Laid.Fill(c, 0, 40, BlockId.Stone));
        for (int x = 6; x <= 11; x++) world.SetBlock(x, 20, 8, BlockId.Air);
        var mesher = Meshed(world);
        var positions = mesher.Solid.Positions.ToArray();
        var packed = mesher.Solid.Packed.ToArray();

        // The lamp's own cube is drawn apart, so the faces stay where they were, the one it covers
        // left out after the others, and their light changes.
        world.SetBlock(11, 20, 8, BlockId.Glowstone);
        mesher = Meshed(world);
        var lit = Enumerable.Range(0, mesher.Solid.Count).Where(i => positions.Contains(mesher.Solid.Positions[i])).ToArray();

        Assert.NotEmpty(lit);
        Assert.Contains(lit, i => mesher.Solid.Unpack(i).Shade > SectionMesher.Unpack(packed[2 * Array.IndexOf(positions, mesher.Solid.Positions[i])], packed[2 * Array.IndexOf(positions, mesher.Solid.Positions[i]) + 1]).Shade);
    }
}
