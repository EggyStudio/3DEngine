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
    public void A_Stream_Of_The_Programs_Own_That_Is_Not_One_Of_Its_Format_A_Vertex_Is_Refused()
    {
        var store = new MeshStore();

        var fewer = () => store.Add(Triangle, [0, 1, 2], custom: MeshStream.From(MeshStreamFormat.UInt, new uint[] { 1, 2 }));
        var ragged = () => store.Add(Triangle, [0, 1, 2], custom: new MeshStream(MeshStreamFormat.UInt2, new byte[20]));
        var ragged2 = () => MeshStream.From(MeshStreamFormat.Float4, new float[] { 1, 2, 3 });

        fewer.Should().Throw<ArgumentException>("eight bytes do not give three vertices four each");
        ragged.Should().Throw<ArgumentException>("20 bytes are not a whole number of eight-byte vertices");
        ragged2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_Stream_Written_In_Place_Is_Queued_Alone_And_Beside_Vertices_Written_In_Place()
    {
        var store = new MeshStore();
        var id = store.Add(Triangle, [0, 1, 2], custom: MeshStream.From(MeshStreamFormat.UInt, new uint[] { 1, 2, 3 }));
        store.Take().Uploads.Should().ContainSingle().Which.Custom!.Format.Should().Be(MeshStreamFormat.UInt);

        store.UpdateCustom(id, MeshStream.From(MeshStreamFormat.UInt, new uint[] { 4, 5, 6 })).Should().BeTrue();
        var alone = store.Take().Uploads.Should().ContainSingle().Which;
        (alone.CustomOnly, alone.VerticesOnly).Should().Be((true, false), "the stream alone is written, and nothing else of the mesh");
        alone.Custom!.Data.Should().Equal(System.Runtime.InteropServices.MemoryMarshal.AsBytes<uint>([4, 5, 6]).ToArray());

        store.UpdateCustom(id, MeshStream.From(MeshStreamFormat.UInt, new uint[] { 7, 8, 9 }));
        store.UpdateVertices(id, Triangle);
        var both = store.Take().Uploads.Should().ContainSingle().Which;
        (both.CustomOnly, both.VerticesOnly).Should().Be((true, true), "the vertices written after it in the frame keep the stream's write beside them");
        store.CustomOf(id)!.Data.Should().Equal(System.Runtime.InteropServices.MemoryMarshal.AsBytes<uint>([7, 8, 9]).ToArray());

        store.Remove(id);
        store.CustomOf(id).Should().BeNull();
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

    [Fact]
    public void New_Vertices_For_An_Uploaded_Mesh_Are_Queued_Alone_And_The_Last_Of_A_Frame_Wins()
    {
        var store = new MeshStore();
        var id = store.Add(Triangle, [0, 1, 2]);
        store.Take();

        var moved = Triangle.Select(v => v with { Position = v.Position + Vector3.UnitZ }).ToArray();
        store.UpdateVertices(id, Triangle).Should().BeTrue();
        store.UpdateVertices(id, moved).Should().BeTrue();

        var upload = store.Take().Uploads.Should().ContainSingle().Subject;
        upload.VerticesOnly.Should().BeTrue("the triangles did not change");
        upload.Vertices.Should().BeSameAs(moved);
        store.TryGetData(id, out var vertices, out _);
        vertices.Should().BeSameAs(moved, "reading the mesh back gives what it was last given");
    }

    [Fact]
    public void New_Vertices_Before_The_First_Upload_Are_Uploaded_With_The_Triangles()
    {
        var store = new MeshStore();
        var id = store.Add(Triangle, [0, 1, 2]);
        var moved = Triangle.Select(v => v with { Position = -v.Position }).ToArray();
        store.UpdateVertices(id, moved);

        var upload = store.Take().Uploads.Should().ContainSingle().Subject;
        upload.VerticesOnly.Should().BeFalse("the GPU has no buffers for it yet");
        upload.Vertices.Should().BeSameAs(moved);
    }

    [Fact]
    public void New_Vertices_Must_Match_The_Count_And_A_Gone_Mesh_Takes_None()
    {
        var store = new MeshStore();
        var id = store.Add(Triangle, [0, 1, 2]);

        store.Invoking(s => s.UpdateVertices(id, Triangle[..2])).Should().Throw<ArgumentException>();
        store.Remove(id);
        store.UpdateVertices(id, Triangle).Should().BeFalse();
    }
}
