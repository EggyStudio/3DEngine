using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The edit mesh's primitives, its selection and what moves it, on exact counts of vertices, edges
/// and faces, the volume a closed mesh holds, and whether every edge is shared by two faces running
/// along it in opposite directions.
/// </summary>
/// <remarks>
/// A closed mesh whose faces all face out holds a positive volume, so the volume tells a primitive
/// turned inside out from one that is right, and the open edges tell one with a hole or a face
/// turned the wrong way. A solid pushed out from an outline of n corners has 2n vertices, 3n edges
/// and n + 2 faces, which the primitives built that way are held to.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class EditMeshTests : IDisposable
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

    public static TheoryData<string, int, int, int, float> Primitives => new()
    {
        // name, vertices, edges, faces, volume
        { "box", 8, 12, 6, 24f },
        { "cylinder", 16, 24, 10, 8f / 2f * MathF.Sin(MathF.Tau / 8f) * 2f },
        { "square cylinder", 8, 12, 6, 4f },
        { "cone", 7, 12, 7, 6f / 2f * MathF.Sin(MathF.Tau / 6f) * 3f / 3f },
        { "wedge", 6, 9, 5, 12f },
        { "stairs", 20, 30, 12, 2f * 0.25f * 0.5f * (1 + 2 + 3 + 4) },
        { "arch", 36, 54, 20, 8f / 2f * MathF.Sin(MathF.PI / 8f) * (4f - 2.25f) * 1f },
    };

    private EditMesh Make(string name)
    {
        var mesh = New();
        switch (name)
        {
            case "box": EditMeshBox(mesh, new Vector3(2, 3, 4)); break;
            case "cylinder": EditMeshCylinder(mesh, 1, 2, 8); break;
            case "square cylinder": EditMeshCylinder(mesh, 1, 2, 4); break;
            case "cone": EditMeshCone(mesh, 1, 3, 6); break;
            case "wedge": EditMeshWedge(mesh, new Vector3(2, 3, 4)); break;
            case "stairs": EditMeshStairs(mesh, 4, 0.25f, 0.5f, 2); break;
            case "arch": EditMeshArch(mesh, 2, 0.5f, 1, 8); break;
            default: throw new ArgumentOutOfRangeException(nameof(name));
        }
        return mesh;
    }

    // The faces of a mesh whose normal is exactly one direction.
    private static int[] Facing(EditMesh mesh, Vector3 normal) =>
        Enumerable.Range(0, GetEditMeshInfo(mesh).FaceCount).Where(face => GetEditMeshFaceNormal(mesh, face) == normal).ToArray();

    [Theory]
    [MemberData(nameof(Primitives))]
    public void Each_Solid_Primitive_Has_Its_Counts_Is_Closed_Faces_Out_And_Is_Selected_Whole(string name, int vertices, int edges, int faces, float volume)
    {
        var info = GetEditMeshInfo(Make(name));

        (info.VertexCount, info.EdgeCount, info.FaceCount).Should().Be((vertices, edges, faces));
        info.OpenEdges.Should().Be(0);
        info.IsClosed.Should().BeTrue();
        info.Volume.Should().BeApproximately(volume, Close);

        // Selected whole, so the operation after it acts on it, and placed by its lowest corner at
        // the origin where nothing says otherwise.
        (info.SelectedVertexCount, info.SelectedEdgeCount, info.SelectedFaceCount).Should().Be((vertices, edges, faces));
        info.Bounds.Min.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void A_Plane_Is_One_Face_Facing_Up_With_Four_Open_Edges()
    {
        var mesh = New();
        EditMeshPlane(mesh, 2, 3);
        var info = GetEditMeshInfo(mesh);

        (info.VertexCount, info.EdgeCount, info.FaceCount).Should().Be((4, 4, 1));
        info.OpenEdges.Should().Be(4);
        info.IsClosed.Should().BeFalse();
        GetEditMeshFaceNormal(mesh, 0).Should().Be(Vector3.UnitY);
        info.Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 0, 3)));
    }

    [Fact]
    public void A_Primitive_Is_Placed_By_Its_Lowest_Corner_Or_By_Its_Middle()
    {
        var cornered = New();
        EditMeshBox(cornered, new Vector3(2, 3, 4), new Vector3(1, 0, -2));
        GetEditMeshInfo(cornered).Bounds.Should().Be(new BoundingBox(new Vector3(1, 0, -2), new Vector3(3, 3, 2)));

        var centered = New();
        EditMeshBox(centered, new Vector3(2, 3, 4), new Vector3(0, 1.5f, 0), centered: true);
        GetEditMeshInfo(centered).Bounds.Should().Be(new BoundingBox(new Vector3(-1, 0, -2), new Vector3(1, 3, 2)));

        // A cylinder of eight sides stands its corners half a side round from the axes, so it is as
        // wide along X as along Z, and placed by its corner it starts at the origin all the same.
        var round = New();
        EditMeshCylinder(round, 1, 2, 8);
        var bounds = GetEditMeshInfo(round).Bounds;
        bounds.Min.Should().Be(Vector3.Zero);
        bounds.Max.X.Should().BeApproximately(bounds.Max.Z, Close);
        bounds.Max.Y.Should().Be(2);
    }

    [Fact]
    public void Stairs_Rise_Along_Z_From_Their_First_Step_To_A_Back_As_Tall_As_The_Flight()
    {
        var mesh = New();
        EditMeshStairs(mesh, 4, 0.25f, 0.5f, 2);
        GetEditMeshInfo(mesh).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 1, 2)));

        // Four treads face up, one a step, each a step higher and a run further along.
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(4);
        var treads = Enumerable.Range(0, GetEditMeshInfo(mesh).FaceCount).Where(face => IsEditMeshFaceSelected(mesh, face))
            .Select(face => GetEditMeshFace(mesh, face).Aggregate(Vector3.Zero, (sum, corner) => sum + GetEditMeshVertex(mesh, corner)) / 4)
            .OrderBy(center => center.Z).ToArray();
        for (int step = 0; step < 4; step++)
        {
            treads[step].Y.Should().BeApproximately((step + 1) * 0.25f, Close);
            treads[step].Z.Should().BeApproximately((step + 0.5f) * 0.5f, Close);
        }

        // Four risers face toward the start, and the back faces the other way, as tall as the flight.
        EditMeshSelectFacing(mesh, -Vector3.UnitZ);
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(4);
        EditMeshSelectFacing(mesh, Vector3.UnitZ);
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(1);
    }

    [Fact]
    public void A_Wedge_Rises_Along_Z_And_Its_Slope_At_Forty_Five_Degrees_Is_A_Floor()
    {
        var mesh = New();
        EditMeshWedge(mesh, new Vector3(2, 3, 4));
        foreach (var vertex in Enumerable.Range(0, GetEditMeshInfo(mesh).VertexCount).Where(vertex => GetEditMeshVertex(mesh, vertex).Y > 0))
            GetEditMeshVertex(mesh, vertex).Z.Should().Be(4);

        // A slope that rises as far as it runs is taken as facing up, where a character walks it.
        var even = New();
        EditMeshWedge(even, new Vector3(2, 2, 2));
        EditMeshSelectFacing(even, Vector3.UnitY);
        GetEditMeshInfo(even).SelectedFaceCount.Should().Be(1);
        EditMeshSelectFacing(even, -Vector3.UnitZ);
        GetEditMeshInfo(even).SelectedFaceCount.Should().Be(0);
    }

    [Fact]
    public void Two_Primitives_In_One_Mesh_Are_Counted_Together_And_The_Second_Is_What_Is_Selected()
    {
        var mesh = New();
        EditMeshBox(mesh, Vector3.One);
        EditMeshBox(mesh, Vector3.One, new Vector3(3, 0, 0));
        var info = GetEditMeshInfo(mesh);

        (info.VertexCount, info.EdgeCount, info.FaceCount).Should().Be((16, 24, 12));
        info.Volume.Should().BeApproximately(2, Close);
        info.IsClosed.Should().BeTrue();
        (info.SelectedVertexCount, info.SelectedFaceCount).Should().Be((8, 6));
        Enumerable.Range(0, 8).Should().NotContain(vertex => IsEditMeshVertexSelected(mesh, vertex));
    }

    [Fact]
    public void The_Top_Of_A_Box_Selected_And_Moved_Up_Makes_It_Taller()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        var info = GetEditMeshInfo(mesh);
        (info.SelectedVertexCount, info.SelectedEdgeCount, info.SelectedFaceCount).Should().Be((4, 4, 1));

        EditMeshMove(mesh, new Vector3(0, 1, 0));

        info = GetEditMeshInfo(mesh);
        info.Volume.Should().BeApproximately(32, Close);
        info.Bounds.Max.Should().Be(new Vector3(2, 4, 4));
        info.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Inverting_The_Top_Selects_The_Other_Five_Faces_And_Every_Corner()
    {
        var mesh = New();
        EditMeshBox(mesh, Vector3.One);
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshSelectInvert(mesh);
        var info = GetEditMeshInfo(mesh);

        (info.SelectedVertexCount, info.SelectedEdgeCount, info.SelectedFaceCount).Should().Be((8, 12, 5));
        IsEditMeshFaceSelected(mesh, Facing(mesh, Vector3.UnitY).Single()).Should().BeFalse();

        EditMeshSelectAll(mesh);
        EditMeshSelectInvert(mesh);
        (GetEditMeshInfo(mesh).SelectedVertexCount, GetEditMeshInfo(mesh).SelectedFaceCount).Should().Be((0, 0));
    }

    [Fact]
    public void Faces_Are_Added_And_Taken_Away_With_The_Corners_No_Other_Selected_Face_Has()
    {
        var mesh = New();
        EditMeshBox(mesh, Vector3.One);
        var top = Facing(mesh, Vector3.UnitY).Single();
        var front = Facing(mesh, Vector3.UnitZ).Single();

        EditMeshSelectFaces(mesh, [top]);
        EditMeshSelectFaces(mesh, [front], SelectionMode.Add);
        (GetEditMeshInfo(mesh).SelectedVertexCount, GetEditMeshInfo(mesh).SelectedFaceCount).Should().Be((6, 2));

        // The two corners the top and the front share stay with the front.
        EditMeshSelectFaces(mesh, [top], SelectionMode.Remove);
        (GetEditMeshInfo(mesh).SelectedVertexCount, GetEditMeshInfo(mesh).SelectedFaceCount).Should().Be((4, 1));
        IsEditMeshFaceSelected(mesh, front).Should().BeTrue();

        var past = () => EditMeshSelectFaces(mesh, [6]);
        past.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_Box_Drawn_Round_Half_A_Mesh_Picks_Its_Vertices_And_The_Faces_Wholly_Inside()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 1, 1));
        EditMeshSelectInside(mesh, new Vector3(1, -1, -1), new Vector3(3, 2, 2));

        // The far half's four corners, on the box's side, and the one face all of whose corners are there.
        var info = GetEditMeshInfo(mesh);
        (info.SelectedVertexCount, info.SelectedEdgeCount, info.SelectedFaceCount).Should().Be((4, 4, 1));
        GetEditMeshFaceNormal(mesh, Enumerable.Range(0, 6).Single(face => IsEditMeshFaceSelected(mesh, face))).Should().Be(Vector3.UnitX);

        // Taking away the corners at the top takes the face with them.
        EditMeshSelectInside(mesh, new Vector3(-1, 0.5f, -1), new Vector3(3, 2, 2), SelectionMode.Remove);
        (GetEditMeshInfo(mesh).SelectedVertexCount, GetEditMeshInfo(mesh).SelectedFaceCount).Should().Be((2, 0));
    }

    [Fact]
    public void Faces_Put_In_A_Slot_Are_Selected_By_It()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(4, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMaterial(mesh, "ceiling");
        EditMeshSelectFacing(mesh, -Vector3.UnitY);
        EditMeshMaterial(mesh, "floor");

        GetEditMeshSlots(mesh).Should().Equal("default", "ceiling", "floor");
        EditMeshSelectSlot(mesh, "floor");
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(1);
        GetEditMeshFaceSlot(mesh, Facing(mesh, -Vector3.UnitY).Single()).Should().Be("floor");
        EditMeshSelectSlot(mesh, "default");
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(4);
        EditMeshSelectSlot(mesh, "ceiling", SelectionMode.Add);
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(5);
        EditMeshSelectSlot(mesh, "roof");
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(0);

        var twoWords = () => EditMeshMaterial(mesh, "two words");
        twoWords.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_Quarter_Turn_About_Y_Turns_A_Box_In_Place()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshRotate(mesh, Vector3.UnitY, 90);
        var info = GetEditMeshInfo(mesh);

        // The middle of the box holding it stays, at (1, 1.5, 2), and its width and depth trade.
        (info.Bounds.Max.X - info.Bounds.Min.X).Should().BeApproximately(4, Close);
        (info.Bounds.Max.Z - info.Bounds.Min.Z).Should().BeApproximately(2, Close);
        ((info.Bounds.Min.X + info.Bounds.Max.X) / 2).Should().BeApproximately(1, Close);
        ((info.Bounds.Min.Z + info.Bounds.Max.Z) / 2).Should().BeApproximately(2, Close);
        info.Volume.Should().BeApproximately(24, Close);
        info.IsClosed.Should().BeTrue();

        // About a point given, the corner at the origin, a quarter turn about Y takes +X to -Z.
        var pivoted = New();
        EditMeshBox(pivoted, new Vector3(2, 1, 1));
        EditMeshRotate(pivoted, Vector3.UnitY, 90, Vector3.Zero);
        GetEditMeshInfo(pivoted).Bounds.Min.Z.Should().BeApproximately(-2, Close);
        GetEditMeshInfo(pivoted).Bounds.Max.X.Should().BeApproximately(1, Close);
    }

    [Fact]
    public void Scaling_Stretches_About_The_Middle_And_A_Mirror_Still_Faces_Out()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(2));
        EditMeshScale(mesh, new Vector3(2, 1, 1));
        GetEditMeshInfo(mesh).Bounds.Should().Be(new BoundingBox(new Vector3(-1, 0, 0), new Vector3(3, 2, 2)));
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(16, Close);

        EditMeshScale(mesh, new Vector3(-1, 1, 1));
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(16, Close);
        GetEditMeshInfo(mesh).IsClosed.Should().BeTrue();

        // About a point given, which stays where it is.
        var pivoted = New();
        EditMeshBox(pivoted, Vector3.One);
        EditMeshScale(pivoted, new Vector3(3), Vector3.Zero);
        GetEditMeshInfo(pivoted).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(3)));
    }

    [Fact]
    public void The_Grid_Is_A_Power_Of_Two_Of_A_Meter_And_Snap_Puts_The_Selection_On_It()
    {
        var mesh = New();
        EditMeshBox(mesh, Vector3.One, new Vector3(0.3f, 0.6f, -0.2f));
        GetEditMeshInfo(mesh).GridStep.Should().Be(1);

        foreach (var step in new[] { 0.3f, 1f / 32, 128f })
        {
            var refused = () => EditMeshGrid(mesh, step);
            refused.Should().Throw<ArgumentOutOfRangeException>();
        }

        EditMeshGrid(mesh, 0.5f);
        EditMeshSnap(mesh);
        GetEditMeshInfo(mesh).GridStep.Should().Be(0.5f);
        GetEditMeshInfo(mesh).Bounds.Should().Be(new BoundingBox(new Vector3(0.5f, 0.5f, 0), new Vector3(1.5f, 1.5f, 1)));

        // A typed measure is taken as given, off the grid.
        EditMeshMove(mesh, new Vector3(0.2f, 0, 0));
        GetEditMeshInfo(mesh).Bounds.Min.X.Should().BeApproximately(0.7f, Close);
    }

    [Fact]
    public void What_Is_Not_Selected_Stays_Where_It_Is()
    {
        var mesh = New();
        EditMeshBox(mesh, Vector3.One);
        EditMeshBox(mesh, Vector3.One, new Vector3(3, 0, 0));
        EditMeshMove(mesh, new Vector3(0, 5, 0));

        GetEditMeshInfo(mesh).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(4, 6, 1)));
        Enumerable.Range(0, 8).Max(vertex => GetEditMeshVertex(mesh, vertex).Y).Should().BeApproximately(1, Close);

        EditMeshSelectNone(mesh);
        EditMeshMove(mesh, Vector3.One);
        GetEditMeshInfo(mesh).SelectedVertexCount.Should().Be(0);
        GetEditMeshInfo(mesh).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(4, 6, 1)));
    }

    [Fact]
    public void A_Measure_Of_A_Primitive_Is_Above_Zero_And_A_Refused_One_Adds_Nothing()
    {
        var mesh = New();

        var flat = () => EditMeshBox(mesh, new Vector3(1, 0, 1));
        var twoSides = () => EditMeshCylinder(mesh, 1, 1, 2);
        var solidArch = () => EditMeshArch(mesh, 1, 1, 1, 4);
        var noSteps = () => EditMeshStairs(mesh, 0, 1, 1, 1);
        var notANumber = () => EditMeshPlane(mesh, float.NaN, 1);
        foreach (var refused in new[] { flat, twoSides, solidArch, noSteps, notANumber })
            refused.Should().Throw<ArgumentOutOfRangeException>();

        GetEditMeshInfo(mesh).VertexCount.Should().Be(0);
    }

    [Fact]
    public void Each_Operation_Writes_Its_Line_In_The_Recipes_Words()
    {
        var mesh = New();
        EditMeshBox(mesh, new Vector3(8, 3, 6));
        EditMeshBox(mesh, Vector3.One, new Vector3(0, 5, 0), centered: true);
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshSelectFaces(mesh, [0, 3], SelectionMode.Add);
        EditMeshSelectInside(mesh, Vector3.Zero, new Vector3(4, 3, 4), SelectionMode.Remove);
        EditMeshMove(mesh, new Vector3(0, 0.5f, 0));
        EditMeshRotate(mesh, Vector3.UnitY, 90, new Vector3(1, 0, 1));
        EditMeshScale(mesh, new Vector3(2));
        EditMeshScale(mesh, new Vector3(1, 2, 1));
        EditMeshGrid(mesh, 0.25f);
        EditMeshMaterial(mesh, "wall");
        EditMeshShade(mesh, true);

        EditMeshStore.TryGet(mesh, out var data).Should().BeTrue();
        data.Recipe.Should().Equal(
            "box 8 3 6",
            "box 1 1 1 at 0 5 0 center",
            "select +y",
            "select add face 0 3",
            "select remove box 0 0 0 4 3 4",
            "move 0 0.5 0",
            "rotate y 90 at 1 0 1",
            "scale 2",
            "scale 1 2 1",
            "grid 0.25",
            "material wall",
            "shade smooth 30");
    }

    [Fact]
    public void An_Edit_Mesh_That_Is_Not_Loaded_Takes_No_Operation_And_Reads_As_Empty()
    {
        var mesh = New();
        UnloadEditMesh(mesh);

        IsEditMeshValid(mesh).Should().BeFalse();
        IsEditMeshValid(default).Should().BeFalse();
        EditMeshBox(mesh, Vector3.One);
        GetEditMeshInfo(mesh).Should().Be(default(EditMeshInfo));
        GetEditMeshSlots(mesh).Should().BeEmpty();
    }
}
