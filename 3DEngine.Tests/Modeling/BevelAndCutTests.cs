using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The bevel, the loop cut and the subdivision, on exact counts of vertices, edges and faces, the
/// volume a closed mesh holds and whether every edge is shared by two faces running along it in
/// opposite directions.
/// </summary>
/// <remarks>
/// A closed mesh of one piece with no hole through it has two more vertices and faces together than
/// edges, which every count here keeps. A bevel takes from a box what lies outside its round
/// bands, so a round bevel holds less than the box and more than the flat chamfer of the same width,
/// whose bands are the round ones' chords.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class BevelAndCutTests : IDisposable
{
    private const float Close = 1e-4f;
    private readonly List<EditMesh> _made = [];

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
    }

    private EditMesh Box(Vector3 size)
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshBox(mesh, size);
        return mesh;
    }

    private static EditMeshInfo Info(EditMesh mesh) => GetEditMeshInfo(mesh);

    private static (int, int, int) Counts(EditMesh mesh) => (Info(mesh).VertexCount, Info(mesh).EdgeCount, Info(mesh).FaceCount);

    [Fact]
    public void A_Boxs_Twelve_Edges_Beveled_Round_In_Two_Segments_Are_Closed_And_Hold_Between_The_Box_And_Its_Chamfer()
    {
        var round = Box(new Vector3(2));
        EditMeshBevel(round, 0.25f, 2);
        var chamfer = Box(new Vector3(2));
        EditMeshBevel(chamfer, 0.25f);

        // Each face keeps a corner at each of its four, each edge a band of two faces with a vertex
        // between them at each end, and each corner of the box a face of six corners.
        Counts(round).Should().Be((48, 84, 38));
        Info(round).IsClosed.Should().BeTrue();
        Counts(chamfer).Should().Be((24, 48, 26));
        Info(chamfer).IsClosed.Should().BeTrue();

        Info(round).Volume.Should().BeLessThan(8).And.BeGreaterThan(Info(chamfer).Volume);
        // The chamfer takes a prism of half a square of the width's side along each edge, less where
        // the edges meet, and the corners.
        Info(chamfer).Volume.Should().BeLessThan(8 - 12 * (0.25f * 0.25f / 2) * (2 - 2 * 0.25f) + Close);
        // The bands and the corners are selected after.
        Info(round).SelectedFaceCount.Should().Be(12 * 2 + 8);
        // The box keeps its size, the bands within it.
        Info(round).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2)));
    }

    [Fact]
    public void One_Edge_Beveled_Takes_A_Prism_Along_It_And_Leaves_The_Rest()
    {
        var mesh = Box(new Vector3(2));
        // The edge along X at the top of the front, from (0, 2, 0) to (2, 2, 0).
        EditMeshSelectInside(mesh, new Vector3(0, 2, 0), new Vector3(2, 2, 0));
        EditMeshBevel(mesh, 0.25f);

        Counts(mesh).Should().Be((10, 15, 7));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(8 - 0.25f * 0.25f / 2 * 2, Close);

        // Rounded in three segments, the band's end at each side of the box closed by a face beside it.
        var round = Box(new Vector3(2));
        EditMeshSelectInside(round, new Vector3(0, 2, 0), new Vector3(2, 2, 0));
        EditMeshBevel(round, 0.25f, 3);
        Counts(round).Should().Be((14, 23, 11));
        Info(round).IsClosed.Should().BeTrue();
        Info(round).Volume.Should().BeGreaterThan(Info(mesh).Volume).And.BeLessThan(8);

        // The band is a quarter circle of the width's radius about the line the width in from both
        // faces, so every vertex where the edge was is that far from it.
        var band = Enumerable.Range(0, Info(round).VertexCount).Select(vertex => GetEditMeshVertex(round, vertex))
            .Where(point => point.Y > 1.75f - Close && point.Z < 0.25f + Close).ToArray();
        band.Should().HaveCount(8);
        band.Should().OnlyContain(point => MathF.Abs(new Vector2(point.Y - 1.75f, point.Z - 0.25f).Length() - 0.25f) < Close);
    }

    [Fact]
    public void A_Boxs_Top_Edges_Beveled_Leave_Its_Sides_Edges_Standing()
    {
        var mesh = Box(new Vector3(2, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshBevel(mesh, 0.5f, 2);

        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeLessThan(24).And.BeGreaterThan(24 - 2 * (2 + 4) * 0.5f * 0.5f);
        // The top, smaller by the width all round, and the bottom keep their places.
        Info(mesh).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 3, 4)));
        var info = Info(mesh);
        (info.VertexCount + info.FaceCount - info.EdgeCount).Should().Be(2);
    }

    [Fact]
    public void A_Loop_Cut_Across_A_Boxs_Upright_Edge_Rings_Its_Four_Sides()
    {
        var mesh = Box(new Vector3(2, 3, 4));
        EditMeshSelectInside(mesh, Vector3.Zero, new Vector3(0, 3, 0));
        EditMeshLoopCut(mesh, 0.25f);

        Counts(mesh).Should().Be((12, 20, 10));
        Info(mesh).IsClosed.Should().BeTrue();
        Info(mesh).Volume.Should().BeApproximately(24, Close);

        // The loop's four vertices are a quarter up from the bottom, measured from the edge's lower
        // end, and selected with the loop's edges.
        (Info(mesh).SelectedVertexCount, Info(mesh).SelectedEdgeCount).Should().Be((4, 4));
        Enumerable.Range(0, Info(mesh).VertexCount).Where(vertex => IsEditMeshVertexSelected(mesh, vertex))
            .Should().OnlyContain(vertex => MathF.Abs(GetEditMeshVertex(mesh, vertex).Y - 0.75f) < Close);
    }

    [Fact]
    public void A_Loop_Cut_Round_A_Cylinder_Splits_Each_Side_And_Leaves_Its_Ends_Whole()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshCylinder(mesh, 1, 2, 8);
        // An upright edge, its two ends the first corner of the bottom and of the top.
        EditMeshSelectInside(mesh, GetEditMeshVertex(mesh, 0), GetEditMeshVertex(mesh, 8));
        EditMeshLoopCut(mesh, 0.5f);

        Counts(mesh).Should().Be((24, 40, 18));
        Info(mesh).IsClosed.Should().BeTrue();
    }

    [Fact]
    public void A_Box_Subdivided_Once_Is_Twenty_Six_Vertices_And_Twice_Fifty_Six()
    {
        var once = Box(new Vector3(2, 3, 4));
        EditMeshSelectAll(once);
        EditMeshSubdivide(once, 1);
        Counts(once).Should().Be((26, 48, 24));
        Info(once).IsClosed.Should().BeTrue();
        Info(once).Volume.Should().BeApproximately(24, Close);
        Info(once).SelectedFaceCount.Should().Be(24);

        var twice = Box(new Vector3(2, 3, 4));
        EditMeshSelectAll(twice);
        EditMeshSubdivide(twice, 2);
        Counts(twice).Should().Be((56, 108, 54));
        Info(twice).IsClosed.Should().BeTrue();
    }

    [Fact]
    public void A_Subdivided_Top_Puts_Its_New_Vertices_Into_The_Sides_Beside_It()
    {
        var mesh = Box(new Vector3(2, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshSubdivide(mesh);

        // Four faces on the top, and each side a face of five corners with the top's new vertex.
        Counts(mesh).Should().Be((13, 20, 9));
        Info(mesh).IsClosed.Should().BeTrue();
        Enumerable.Range(0, 9).Count(face => GetEditMeshFace(mesh, face).Length == 5).Should().Be(4);
    }

    [Fact]
    public void Triangles_And_Faces_Of_More_Corners_Subdivide_Into_Triangles()
    {
        // A cone's base of four is a grid of four, and each triangle of its sides a grid of four.
        var cone = LoadEditMesh();
        _made.Add(cone);
        EditMeshCone(cone, 1, 2, 4);
        EditMeshSelectAll(cone);
        EditMeshSubdivide(cone);
        Counts(cone).Should().Be((14, 32, 20));
        Info(cone).IsClosed.Should().BeTrue();

        // A cylinder's end of six corners is a fan of twelve triangles from its middle.
        var cylinder = LoadEditMesh();
        _made.Add(cylinder);
        EditMeshCylinder(cylinder, 1, 2, 6);
        EditMeshSelectFacing(cylinder, Vector3.UnitY);
        EditMeshSubdivide(cylinder);
        Counts(cylinder).Should().Be((19, 36, 19));
        Info(cylinder).IsClosed.Should().BeTrue();
    }

    [Theory]
    [InlineData("bevel 0.25 2")]
    [InlineData("bevel 0.1 1")]
    [InlineData("loopcut 0.5")]
    [InlineData("subdivide 2")]
    public void A_Cuts_Line_Run_Is_Written_Back_As_It_Was_Typed(string line)
    {
        var mesh = Box(new Vector3(2));
        EditMeshSelectAll(mesh);
        EditMeshRun(mesh, line);

        GetEditMeshRecipe(mesh).Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1].Should().Be(line);
        Info(mesh).IsClosed.Should().BeTrue();
    }

    [Fact]
    public void A_Measure_Out_Of_Range_Is_Refused()
    {
        var mesh = Box(Vector3.One);
        var refused = new Action[]
        {
            () => EditMeshBevel(mesh, 0),
            () => EditMeshBevel(mesh, 0.1f, 0),
            () => EditMeshLoopCut(mesh, 1),
            () => EditMeshSubdivide(mesh, 0),
        };
        foreach (var call in refused) call.Should().Throw<ArgumentOutOfRangeException>();
        Counts(mesh).Should().Be((8, 12, 6));
    }
}
