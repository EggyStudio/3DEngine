using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The bridge, the fill, the dissolves, the knife, the mirror, the spin and the smooth, on exact
/// counts of vertices, edges and faces, the volume a closed mesh holds and whether every edge is
/// shared by two faces running along it in opposite directions.
/// </summary>
/// <remarks>
/// A closed mesh of one piece has two more vertices and faces together than edges, and a ring, with
/// its hole through it, as many, which the counts here keep.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class JoinAndSweepTests : IDisposable
{
    private const float Close = 1e-4f;
    private readonly List<EditMesh> _made = [];

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
    }

    private EditMesh New()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        return mesh;
    }

    private static EditMeshInfo Info(EditMesh mesh) => GetEditMeshInfo(mesh);

    private static (int, int, int) Counts(EditMesh mesh) => (Info(mesh).VertexCount, Info(mesh).EdgeCount, Info(mesh).FaceCount);

    private static Vector3 Center(EditMesh mesh, int face) =>
        GetEditMeshFace(mesh, face).Aggregate(Vector3.Zero, (sum, corner) => sum + GetEditMeshVertex(mesh, corner)) / GetEditMeshFace(mesh, face).Length;

    // The face whose middle is at a point.
    private static int FaceAt(EditMesh mesh, Vector3 middle) =>
        Enumerable.Range(0, Info(mesh).FaceCount).Single(face => (Center(mesh, face) - middle).Length() < Close);

    // A face of a meter standing upright from x 1 to 2 and y -1 to 0, facing +z, beside the Y axis.
    private EditMesh Upright()
    {
        var mesh = New();
        EditMeshPlane(mesh, 1, 1);
        EditMeshRotate(mesh, Vector3.UnitX, 90, Vector3.Zero);
        EditMeshMove(mesh, new Vector3(1, 0, 0));
        return mesh;
    }

    [Fact]
    public void Two_Boxes_Facing_Faces_Bridged_Are_One_Solid_Holding_The_Gap()
    {
        var mesh = New();
        EditMeshBox(mesh, Vector3.One);
        EditMeshBox(mesh, Vector3.One, new Vector3(2, 0, 0));
        EditMeshSelectFaces(mesh, [FaceAt(mesh, new Vector3(1, 0.5f, 0.5f)), FaceAt(mesh, new Vector3(2, 0.5f, 0.5f))]);
        EditMeshBridge(mesh);

        Counts(mesh).Should().Be((16, 28, 14));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(3, Close);
        Info(mesh).SelectedFaceCount.Should().Be(4);

        // One face alone has nothing to bridge to.
        EditMeshSelectFaces(mesh, [0]);
        var refused = () => EditMeshBridge(mesh);
        refused.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_Hole_Whose_Edges_Are_Selected_Is_Filled_With_One_Face()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshDelete(mesh);
        EditMeshSelectAll(mesh);
        EditMeshFill(mesh);

        Counts(mesh).Should().Be((8, 12, 6));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(24, Close);
        GetEditMeshFaceNormal(mesh, Enumerable.Range(0, 6).Single(face => IsEditMeshFaceSelected(mesh, face))).Should().Be(Vector3.UnitY);
    }

    [Fact]
    public void A_Subdivided_Top_Dissolved_Is_One_Face_Of_Eight_Corners()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectAll(mesh);
        EditMeshSubdivide(mesh);
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshDissolve(mesh);

        // The top's middle vertex goes with the edges inside it, and its edges' middles stay on its outline.
        Counts(mesh).Should().Be((25, 44, 21));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(24, Close);
        GetEditMeshFace(mesh, Enumerable.Range(0, 21).Single(face => IsEditMeshFaceSelected(mesh, face))).Should().HaveCount(8);
    }

    [Fact]
    public void A_Loop_Cuts_Edges_Dissolved_Join_The_Faces_It_Split()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectInside(mesh, Vector3.Zero, new Vector3(0, 3, 0));
        EditMeshLoopCut(mesh, 0.5f);
        EditMeshDissolveEdges(mesh);

        // Each side one face again, keeping the loop's vertex on each upright edge.
        Counts(mesh).Should().Be((12, 16, 6));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(24, Close);
    }

    [Fact]
    public void A_Knife_Through_A_Whole_Box_Cuts_A_Loop_And_Through_Its_Top_Alone_Splits_The_Top()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectAll(mesh);
        EditMeshKnife(mesh, Vector3.UnitY, new Vector3(0, 1.5f, 0));

        Counts(mesh).Should().Be((12, 20, 10));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(24, Close);
        Info(mesh).SelectedVertexCount.Should().Be(4);

        var top = New();
        EditMeshBox(top, new Vector3(2, 3, 4));
        EditMeshSelectFacing(top, Vector3.UnitY);
        EditMeshKnife(top, Vector3.UnitX, new Vector3(1, 0, 0));
        Counts(top).Should().Be((10, 15, 7));
        Info(top).IsClosed.Should().BeTrue();
    }

    [Fact]
    public void A_Half_Mirrored_Across_Its_Open_Side_Is_A_Whole_With_No_Vertex_Twice()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(1, 2, 2));
        EditMeshSelectFacing(mesh, -Vector3.UnitX);
        EditMeshDelete(mesh);
        EditMeshSelectAll(mesh);
        EditMeshMirror(mesh, Vector3.UnitX);

        Counts(mesh).Should().Be((12, 20, 10));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(8, Close);
        Info(mesh).Bounds.Should().Be(new BoundingBox(new Vector3(-1, 0, 0), new Vector3(1, 2, 2)));

        // A face in the plane is not copied, so a half left closed there keeps that face inside.
        var closed = New();
        EditMeshBox(closed, new Vector3(1, 2, 2));
        EditMeshMirror(closed, Vector3.UnitX);
        Info(closed).FaceCount.Should().Be(11);
        Info(closed).IsClosed.Should().BeFalse();
    }

    [Fact]
    public void A_Face_Spun_A_Whole_Turn_Round_An_Axis_Beside_It_Is_A_Closed_Ring()
    {
        var mesh = Upright();
        EditMeshSpin(mesh, Vector3.UnitY, 360, 8);

        // Four corners on each of eight steps, and a face for each edge each step, a ring through
        // which the axis runs.
        Counts(mesh).Should().Be((32, 64, 32));
        Info(mesh).IsClosed.Should().BeTrue();
        // An octagon's ring between radii one and two, a meter high.
        MathF.Abs(Info(mesh).Volume).Should().BeApproximately(8 / 2f * MathF.Sin(MathF.Tau / 8) * (4 - 1), Close);

        // A quarter turn in four steps keeps the face, turned, and is open where it started.
        var bent = Upright();
        EditMeshSpin(bent, Vector3.UnitY, 90, 4);
        (Info(bent).VertexCount, Info(bent).FaceCount, Info(bent).OpenEdges).Should().Be((20, 17, 4));
        GetEditMeshFaceNormal(bent, Enumerable.Range(0, 17).Single(face => IsEditMeshFaceSelected(bent, face))).X.Should().BeApproximately(1, Close);
    }

    [Fact]
    public void An_Edge_Spun_Alone_Sweeps_A_Band_Of_Its_Own()
    {
        var mesh = Upright();
        EditMeshSelectInside(mesh, new Vector3(2, -1, 0), new Vector3(2, 0, 0));
        EditMeshSpin(mesh, Vector3.UnitY, 360, 8);

        // The face, and a band of eight faces round the axis, its last step joined to its first.
        Counts(mesh).Should().Be((18, 27, 9));
        Info(mesh).SelectedVertexCount.Should().Be(2);
    }

    [Fact]
    public void A_Box_Smoothed_By_A_Half_Draws_In_To_A_Smaller_Box()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2), Vector3.Zero, centered: true);
        EditMeshSelectAll(mesh);
        EditMeshSmooth(mesh, 0.5f);

        // Each corner moves half way to the mean of its three neighbors, a third of the way in.
        Info(mesh).Bounds.Should().Be(new BoundingBox(new Vector3(-2f / 3), new Vector3(2f / 3)));
        Info(mesh).Volume.Should().BeApproximately(64f / 27, Close);
        Counts(mesh).Should().Be((8, 12, 6));
    }

    [Theory]
    [InlineData("fill")]
    [InlineData("dissolve")]
    [InlineData("dissolve edges")]
    [InlineData("knife y at 0 1 0")]
    [InlineData("knife 0 -1 0")]
    [InlineData("mirror x at 3 0 0")]
    [InlineData("spin y 90 4 at 5 0 0")]
    [InlineData("spin z -45 2")]
    [InlineData("smooth 0.25")]
    public void A_Line_Run_Is_Written_Back_As_It_Was_Typed(string line)
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2));
        EditMeshSelectAll(mesh);
        EditMeshRun(mesh, line);

        GetEditMeshRecipe(mesh).Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1].Should().Be(line);
    }

    [Fact]
    public void A_Measure_Out_Of_Range_Is_Refused()
    {
        var mesh = New();
        EditMeshBox(mesh, Vector3.One);
        var refused = new Action[]
        {
            () => EditMeshKnife(mesh, Vector3.Zero),
            () => EditMeshMirror(mesh, Vector3.Zero),
            () => EditMeshSpin(mesh, Vector3.Zero, 90, 4),
            () => EditMeshSpin(mesh, Vector3.UnitY, 0, 4),
            () => EditMeshSpin(mesh, Vector3.UnitY, 400, 4),
            () => EditMeshSpin(mesh, Vector3.UnitY, 90, 0),
            () => EditMeshSmooth(mesh, 2),
        };
        foreach (var call in refused) call.Should().Throw<ArgumentException>();
        Counts(mesh).Should().Be((8, 12, 6));
    }
}
