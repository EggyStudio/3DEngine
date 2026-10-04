using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Immediate;

[Trait("Category", "Unit")]
public class DrawListTests
{
    [Fact]
    public void Consecutive_Shapes_With_The_Same_State_Share_A_Batch()
    {
        var list = new DrawList();
        list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);
        list.Line(Vector3.Zero, Vector3.UnitY, Color.Red);

        list.Batches.Should().ContainSingle()
            .Which.Should().Be(new DrawBatch(PrimitiveTopology.LineList, Matrix4x4.Identity, false, 0, 4));
    }

    [Fact]
    public void A_Change_Of_Topology_Transform_Or_Depth_Opens_A_Batch()
    {
        var list = new DrawList();
        list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);
        list.SetTransform(Matrix4x4.CreateScale(2), depthTest: true);
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);

        list.Batches.Select(b => (b.Topology, b.DepthTest, b.FirstVertex, b.VertexCount)).Should().Equal(
            (PrimitiveTopology.LineList, false, 0, 2),
            (PrimitiveTopology.TriangleList, false, 2, 3),
            (PrimitiveTopology.TriangleList, true, 5, 3));
    }

    [Fact]
    public void A_Quad_Is_Two_Triangles()
    {
        var list = new DrawList();
        list.Quad(Vector3.Zero, Vector3.UnitX, Vector3.One, Vector3.UnitY, Color.Blue);

        list.Vertices.Length.Should().Be(6);
        list.Batches.Should().ContainSingle().Which.Topology.Should().Be(PrimitiveTopology.TriangleList);
    }

    [Fact]
    public void Clear_Forgets_Shapes_And_Returns_To_Screen_Space()
    {
        var list = new DrawList();
        list.SetTransform(Matrix4x4.CreateScale(2), depthTest: true);
        list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);

        list.Clear();

        list.Vertices.Length.Should().Be(0);
        list.Batches.Should().BeEmpty();
        list.Transform.Should().Be(Matrix4x4.Identity);
        list.DepthTest.Should().BeFalse();
    }

    [Fact]
    public void The_List_Grows_Past_Its_First_Capacity()
    {
        var list = new DrawList();
        for (int i = 0; i < 5000; i++)
            list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);

        list.Vertices.Length.Should().Be(10000);
        list.Batches.Should().ContainSingle().Which.VertexCount.Should().Be(10000);
    }

    [Fact]
    public void A_Vertex_Is_24_Bytes_In_The_Order_The_Pipeline_Reads()
    {
        System.Runtime.InteropServices.Marshal.SizeOf<ImmediateVertex>().Should().Be(24);
        System.Runtime.InteropServices.Marshal.OffsetOf<ImmediateVertex>("<Uv>k__BackingField").ToInt32().Should().Be(12);
        System.Runtime.InteropServices.Marshal.OffsetOf<ImmediateVertex>("<Color>k__BackingField").ToInt32().Should().Be(20);
    }

    [Fact]
    public void A_Change_Of_Texture_Opens_A_Batch()
    {
        var list = new DrawList();
        var uv = Vector2.Zero;
        list.TexturedQuad(Vector3.Zero, Vector3.UnitX, Vector3.One, Vector3.UnitY, uv, uv, uv, uv, Color.White, 3);
        list.TexturedQuad(Vector3.Zero, Vector3.UnitX, Vector3.One, Vector3.UnitY, uv, uv, uv, uv, Color.White, 3);
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);

        list.Batches.Select(b => (b.Texture, b.VertexCount)).Should().Equal((3, 12), (0, 3));
    }

    [Fact]
    public void A_Blend_Mode_Or_Scissor_Opens_A_Batch_And_Clear_Resets_Both()
    {
        var list = new DrawList();
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);
        list.SetBlend(BlendMode.Additive);
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);
        list.SetScissor(new ScissorRect(1, 2, 3, 4));
        list.Triangle(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Color.Red);

        list.Batches.Select(b => (b.Blend, b.Scissor)).Should().Equal(
            (BlendMode.Alpha, null), (BlendMode.Additive, null), (BlendMode.Additive, new ScissorRect(1, 2, 3, 4)));

        list.Clear();
        (list.Blend, list.Scissor).Should().Be((BlendMode.Alpha, (ScissorRect?)null));
    }

    [Fact]
    public void Shapes_For_A_Render_Target_Are_Batched_Apart_And_Its_Clear_Is_Kept()
    {
        var list = new DrawList();
        list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);
        list.SetTarget(5);
        list.SetTargetClear(Color.Blue);
        list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);
        list.SetTarget(0);
        list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);

        list.Batches.Select(b => b.Target).Should().Equal(0, 5, 0);
        list.TargetClears.Should().ContainSingle().Which.Should().Be(new KeyValuePair<int, Color>(5, Color.Blue));

        list.Clear();
        list.TargetClears.Should().BeEmpty();
        list.Target.Should().Be(0);
    }

    [Fact]
    public void A_Batch_Read_Mid_Frame_Keeps_Growing_And_A_Transform_Set_Back_Extends_It()
    {
        var list = new DrawList();
        list.Line(Vector3.Zero, Vector3.UnitX, Color.Red);
        list.Batches.Should().ContainSingle().Which.VertexCount.Should().Be(2);

        list.Line(Vector3.Zero, Vector3.UnitY, Color.Red);
        list.SetTransform(Matrix4x4.Identity, depthTest: false);
        list.Line(Vector3.Zero, Vector3.UnitZ, Color.Red);

        list.Batches.Should().ContainSingle("the transform was set to what it was").Which.VertexCount.Should().Be(6);
    }
}
