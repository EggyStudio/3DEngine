using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The operations that change which vertices and faces an edit mesh has, extrude, inset, delete,
/// merge, flip, triangulate, duplicate and shade, on exact counts, the volume a closed mesh holds
/// and whether every edge is shared by two faces running along it in opposite directions.
/// </summary>
/// <remarks>
/// A box of two by three by four holds 24 cubic meters and its top is two by four, so pushing the
/// top up a meter adds eight and an inset of the top changes nothing it holds. A mesh left closed
/// with its faces facing out after an operation is one the build and the collider take as a solid.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class EditMeshTopologyTests : IDisposable
{
    private const float Close = 1e-4f;
    private readonly List<EditMesh> _made = [];

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
    }

    private EditMesh Box(Vector3? size = null)
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshBox(mesh, size ?? new Vector3(2, 3, 4));
        return mesh;
    }

    private static EditMeshInfo Info(EditMesh mesh) => GetEditMeshInfo(mesh);

    private static int FaceFacing(EditMesh mesh, Vector3 way) =>
        Enumerable.Range(0, Info(mesh).FaceCount).Single(face => (GetEditMeshFaceNormal(mesh, face) - way).Length() < Close);

    [Fact]
    public void The_Top_Of_A_Box_Extruded_A_Meter_Is_A_Taller_Box_Of_Twelve_Vertices_And_Ten_Faces()
    {
        var mesh = Box();
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshExtrude(mesh, 1);

        (Info(mesh).VertexCount, Info(mesh).EdgeCount, Info(mesh).FaceCount).Should().Be((12, 20, 10));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(32, Close);
        Info(mesh).Bounds.Max.Should().Be(new Vector3(2, 4, 4));

        // The top stays selected, so a second extrude pushes it further.
        (Info(mesh).SelectedVertexCount, Info(mesh).SelectedFaceCount).Should().Be((4, 1));
        EditMeshExtrude(mesh, 1);
        Info(mesh).Volume.Should().BeApproximately(40, Close);

        // By a vector, the same.
        var byVector = Box();
        EditMeshSelectFacing(byVector, Vector3.UnitY);
        EditMeshExtrude(byVector, new Vector3(0, 1, 0));
        Info(byVector).Volume.Should().BeApproximately(32, Close);
    }

    [Fact]
    public void Two_Faces_Extruded_Together_Move_Each_The_Whole_Distance_Along_Its_Own_Normal()
    {
        // The top and the front of a box of two pushed out a meter as one region stand a meter out
        // each, the corner they share moved along both, and the edge between them stays joined.
        var mesh = Box(new Vector3(2));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshSelectFacing(mesh, Vector3.UnitZ, SelectionMode.Add);
        EditMeshExtrude(mesh, 1);

        Info(mesh).Bounds.Max.Should().Be(new Vector3(2, 3, 3));
        Info(mesh).IsClosed.Should().BeTrue();
        // Six corners for the region's six, and a side for each of its six rim edges.
        (Info(mesh).VertexCount, Info(mesh).FaceCount).Should().Be((14, 12));
        Info(mesh).Volume.Should().BeApproximately(8 + 4 + 4 + 2, Close);
    }

    [Fact]
    public void A_Plane_Pushed_Up_Is_A_Box_Open_Underneath()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshPlane(mesh, 2, 2);
        EditMeshExtrude(mesh, 1);

        (Info(mesh).VertexCount, Info(mesh).EdgeCount, Info(mesh).FaceCount).Should().Be((8, 12, 5));
        Info(mesh).OpenEdges.Should().Be(4);
        (Info(mesh).Bounds.Max.Y - Info(mesh).Bounds.Min.Y).Should().BeApproximately(1, Close);
    }

    [Fact]
    public void An_Inset_Top_Is_A_Smaller_Face_Inside_A_Band_And_Holds_As_Much()
    {
        var mesh = Box();
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshInset(mesh, 0.5f);

        (Info(mesh).VertexCount, Info(mesh).EdgeCount, Info(mesh).FaceCount).Should().Be((12, 20, 10));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(24, Close);

        // The smaller face is selected, a meter by three, half a meter in from each edge.
        (Info(mesh).SelectedVertexCount, Info(mesh).SelectedFaceCount).Should().Be((4, 1));
        var inner = Enumerable.Range(0, Info(mesh).FaceCount).Single(face => IsEditMeshFaceSelected(mesh, face));
        var corners = GetEditMeshFace(mesh, inner).Select(corner => GetEditMeshVertex(mesh, corner)).ToArray();
        (corners.Max(point => point.X) - corners.Min(point => point.X)).Should().BeApproximately(1, Close);
        (corners.Max(point => point.Z) - corners.Min(point => point.Z)).Should().BeApproximately(3, Close);

        // Pushed in, it is a recess a meter deep.
        EditMeshExtrude(mesh, -1);
        Info(mesh).Volume.Should().BeApproximately(24 - 3, Close);

        var refused = () => EditMeshInset(mesh, 0);
        refused.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_Top_Deleted_Leaves_A_Hole_And_Filled_Closes_It()
    {
        var open = Box();
        EditMeshSelectFacing(open, Vector3.UnitY);
        EditMeshDelete(open);
        (Info(open).VertexCount, Info(open).EdgeCount, Info(open).FaceCount).Should().Be((8, 12, 5));
        Info(open).OpenEdges.Should().Be(4);
        Info(open).SelectedFaceCount.Should().Be(0);

        var filled = Box();
        EditMeshSelectFacing(filled, Vector3.UnitY);
        EditMeshDelete(filled, fill: true);
        (Info(filled).VertexCount, Info(filled).EdgeCount, Info(filled).FaceCount).Should().Be((8, 12, 6));
        Info(filled).IsClosed.Should().BeTrue();
        Info(filled).Volume.Should().BeApproximately(24, Close);
        GetEditMeshFaceNormal(filled, Enumerable.Range(0, 6).Single(face => IsEditMeshFaceSelected(filled, face))).Should().Be(Vector3.UnitY);
    }

    [Fact]
    public void A_Vertex_Deleted_Takes_The_Faces_Using_It_With_It()
    {
        var mesh = Box();
        EditMeshSelectInside(mesh, new Vector3(-0.1f), new Vector3(0.1f));
        EditMeshDelete(mesh);

        (Info(mesh).VertexCount, Info(mesh).FaceCount).Should().Be((7, 3));
        Info(mesh).OpenEdges.Should().Be(6);
    }

    [Fact]
    public void A_Top_Moved_Onto_The_Bottom_Merges_Into_Two_Faces_Of_Four_Corners()
    {
        var mesh = Box(Vector3.One);
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMove(mesh, new Vector3(0, -1, 0));
        EditMeshSelectAll(mesh);
        EditMeshMerge(mesh, 0.001f);

        // The four sides have two corners each left and are gone, and the top and the bottom face
        // each other on the same four vertices.
        (Info(mesh).VertexCount, Info(mesh).EdgeCount, Info(mesh).FaceCount).Should().Be((4, 4, 2));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(0, Close);
        Info(mesh).SelectedVertexCount.Should().Be(4);

        // Nothing near enough is nothing joined.
        var apart = Box(Vector3.One);
        EditMeshBox(apart, Vector3.One, new Vector3(3, 0, 0));
        EditMeshSelectAll(apart);
        EditMeshMerge(apart, 0.5f);
        Info(apart).VertexCount.Should().Be(16);
    }

    [Fact]
    public void Flipped_Faces_Face_In_And_Hold_A_Negative_Volume()
    {
        var mesh = Box();
        EditMeshSelectAll(mesh);
        EditMeshFlip(mesh);

        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(-24, Close);
        GetEditMeshFaceNormal(mesh, FaceFacing(mesh, -Vector3.UnitY)).Should().Be(-Vector3.UnitY);
    }

    [Fact]
    public void Triangulating_Cuts_Each_Face_Of_N_Corners_Into_N_Less_Two_And_Keeps_The_Solid()
    {
        var box = Box();
        EditMeshSelectAll(box);
        EditMeshTriangulate(box);
        (Info(box).VertexCount, Info(box).EdgeCount, Info(box).FaceCount).Should().Be((8, 18, 12));
        Info(box).IsClosed.Should().BeTrue();
        Info(box).Volume.Should().BeApproximately(24, Close);
        Info(box).SelectedFaceCount.Should().Be(12);

        // A flight's sides are concave, ten corners each for four steps, and are cut inside their
        // outlines, so the solid holds what it did, its ten faces of four corners two triangles each
        // and its sides eight.
        var stairs = LoadEditMesh();
        _made.Add(stairs);
        EditMeshStairs(stairs, 4, 0.25f, 0.5f, 2);
        EditMeshSelectAll(stairs);
        EditMeshTriangulate(stairs);
        Info(stairs).FaceCount.Should().Be(2 * 10 + 2 * 8);
        Info(stairs).IsClosed.Should().BeTrue();
        Info(stairs).Volume.Should().BeApproximately(2.5f, Close);
        Enumerable.Range(0, Info(stairs).FaceCount).Should().OnlyContain(face => GetEditMeshFace(stairs, face).Length == 3);
    }

    [Fact]
    public void A_Duplicate_Shares_No_Vertex_And_Moves_Alone()
    {
        var mesh = Box();
        EditMeshSelectAll(mesh);
        EditMeshDuplicate(mesh);
        (Info(mesh).VertexCount, Info(mesh).FaceCount).Should().Be((16, 12));
        (Info(mesh).SelectedVertexCount, Info(mesh).SelectedFaceCount).Should().Be((8, 6));

        EditMeshMove(mesh, new Vector3(5, 0, 0));
        Info(mesh).Volume.Should().BeApproximately(48, Close);
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(7, 3, 4)));
    }

    [Fact]
    public void Shading_Sets_The_Selected_Faces_Angle_And_Flat_Sets_None()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshCylinder(mesh, 1, 2, 8);
        EditMeshSelectAll(mesh);
        EditMeshShade(mesh, true, 45);
        EditMeshStore.TryGet(mesh, out var data).Should().BeTrue();
        Enumerable.Range(0, data.FaceCount).Should().OnlyContain(face => data.SmoothOf(face) == 45);

        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshShade(mesh, false);
        data.SmoothOf(FaceFacing(mesh, Vector3.UnitY)).Should().Be(0);

        var refused = () => EditMeshShade(mesh, true, 0);
        refused.Should().Throw<ArgumentOutOfRangeException>();
    }
}
