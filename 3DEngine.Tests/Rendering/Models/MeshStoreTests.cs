using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Rendering.Models;

[Trait("Category", "Unit")]
public class MeshStoreTests
{
    private static readonly ModelVertex[] Triangle =
    [
        new(Vector3.Zero, Vector3.UnitZ, Vector2.Zero),
        new(Vector3.UnitX, Vector3.UnitZ, Vector2.UnitX),
        new(Vector3.UnitY, Vector3.UnitZ, Vector2.UnitY),
    ];

    [Fact]
    public void A_Mesh_Is_Queued_Once_And_Its_Removal_Once()
    {
        var store = new MeshStore();
        var id = store.Add(Triangle, [0, 1, 2]);

        store.Take().Uploads.Should().ContainSingle().Which.Id.Should().Be(id);
        store.Remove(id).Should().BeTrue();
        store.Take().Removals.Should().Equal(id);
        store.Count.Should().Be(0);
    }

    [Fact]
    public void A_Mesh_Removed_Before_Upload_Is_Never_Uploaded()
    {
        var store = new MeshStore();
        var id = store.Add(Triangle, [0, 1, 2]);
        store.Remove(id);

        store.Take().Uploads.Should().BeEmpty();
    }

    [Theory]
    [InlineData(new uint[] { 0, 1 })]
    [InlineData(new uint[] { 0, 1, 3 })]
    [InlineData(new uint[0])]
    public void Indices_That_Are_Not_Whole_Triangles_In_Range_Are_Refused(uint[] indices)
    {
        var act = () => new MeshStore().Add(Triangle, indices);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_Vertex_Is_32_Bytes_In_The_Order_The_Pipeline_Reads()
    {
        System.Runtime.InteropServices.Marshal.SizeOf<ModelVertex>().Should().Be(32);
        System.Runtime.InteropServices.Marshal.OffsetOf<ModelVertex>("<Normal>k__BackingField").ToInt32().Should().Be(12);
        System.Runtime.InteropServices.Marshal.OffsetOf<ModelVertex>("<Uv>k__BackingField").ToInt32().Should().Be(24);
    }

    [Fact]
    public void A_Bounding_Box_Holds_Every_Point()
    {
        var box = BoundingBox.Around([new Vector3(1, -2, 3), new Vector3(-1, 4, 0), Vector3.Zero]);

        box.Should().Be(new BoundingBox(new Vector3(-1, -2, 0), new Vector3(1, 4, 3)));
    }
}
