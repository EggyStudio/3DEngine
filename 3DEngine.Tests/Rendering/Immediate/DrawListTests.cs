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
    public void A_Vertex_Is_Sixteen_Bytes_With_Color_Last()
    {
        System.Runtime.InteropServices.Marshal.SizeOf<ImmediateVertex>().Should().Be(16);
        System.Runtime.InteropServices.Marshal.OffsetOf<ImmediateVertex>("<Color>k__BackingField").ToInt32().Should().Be(12);
    }
}
