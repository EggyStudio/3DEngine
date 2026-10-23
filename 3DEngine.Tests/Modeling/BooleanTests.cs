using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// The edit mesh's booleans, a box cut through a wall, two boxes joined, two kept where they overlap,
/// a flight of stairs joined by its primitive's line, and the room, the window, the door and the ramp
/// made with them, each closed with every edge shared by two faces, holding the volume the operation
/// leaves, and joined back into few faces.
/// </summary>
/// <remarks>
/// The partition leaves convex pieces, which are joined back where they lie in one plane and one
/// slot, so a cut through a wall leaves the wall's two faces in a few pieces round the hole, the
/// hole's four sides, and the wall's other faces as they were.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class BooleanTests : IDisposable
{
    private const float Close = 1e-3f;
    private readonly TestFolder _folder = new("engine-boolean-");
    private readonly List<EditMesh> _made = [];

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
        _folder.Dispose();
    }

    private EditMesh New()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        return mesh;
    }

    private EditMesh Box(Vector3 size, Vector3 at = default)
    {
        var mesh = New();
        EditMeshBox(mesh, size, at);
        return mesh;
    }

    private static EditMeshInfo Info(EditMesh mesh) => GetEditMeshInfo(mesh);

    private static string[] Lines(EditMesh mesh) => GetEditMeshRecipe(mesh).Split('\n', StringSplitOptions.RemoveEmptyEntries)[1..];

    private static Vector3 Center(EditMesh mesh, int face) =>
        GetEditMeshFace(mesh, face).Aggregate(Vector3.Zero, (sum, corner) => sum + GetEditMeshVertex(mesh, corner)) / GetEditMeshFace(mesh, face).Length;

    private string Saved(EditMesh mesh, string name)
    {
        var path = _folder.File(name);
        SaveEditMesh(mesh, path).Should().BeTrue();
        return path;
    }

    [Fact]
    public void A_Box_Cut_Through_A_Wall_Leaves_It_Closed_And_Holding_The_Wall_Less_The_Hole()
    {
        var wall = Box(new Vector3(4, 3, 0.5f));
        EditMeshCut(wall, "box 1 1 1 at 1.5 1 -0.25");

        Info(wall).IsClosed.Should().BeTrue();
        Info(wall).Volume.Should().BeApproximately(6 - 0.5f, Close);

        // The hole's four sides are selected. The wall's front and back are two faces each round the
        // hole, which one loop cannot go round, and its top, bottom and ends one each, twelve faces
        // in all, with a hole through, so vertices less edges plus faces is none.
        Info(wall).SelectedFaceCount.Should().Be(4);
        (Info(wall).VertexCount, Info(wall).EdgeCount, Info(wall).FaceCount).Should().Be((20, 32, 12));
        Lines(wall).Should().Equal("box 4 3 0.5", "cut box 1 1 1 at 1.5 1 -0.25");
    }

    [Fact]
    public void Two_Boxes_Sharing_A_Face_Unite_Into_One_Box()
    {
        var other = Saved(Box(Vector3.One), "cube.model");
        var joined = Box(Vector3.One);
        EditMeshUnion(joined, other, new Vector3(1, 0, 0)).Should().BeTrue();

        (Info(joined).VertexCount, Info(joined).EdgeCount, Info(joined).FaceCount).Should().Be((8, 12, 6));
        Info(joined).IsClosed.Should().BeTrue();
        Info(joined).Volume.Should().BeApproximately(2, Close);
        Info(joined).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 1, 1)));
        Lines(joined).Should().Equal("box 1 1 1", $"union {other} at 1 0 0");
    }

    [Fact]
    public void Two_Overlapping_Boxes_Intersect_In_Their_Overlap_And_Subtract_To_The_Rest()
    {
        var other = Saved(Box(new Vector3(2)), "block.model");

        var both = Box(new Vector3(2));
        EditMeshIntersect(both, other, Vector3.One).Should().BeTrue();
        (Info(both).VertexCount, Info(both).FaceCount).Should().Be((8, 6));
        Info(both).IsClosed.Should().BeTrue();
        Info(both).Volume.Should().BeApproximately(1, Close);

        var rest = Box(new Vector3(2));
        EditMeshSubtract(rest, other, Vector3.One).Should().BeTrue();
        Info(rest).IsClosed.Should().BeTrue();
        Info(rest).Volume.Should().BeApproximately(7, Close);
    }

    [Fact]
    public void A_Room_Is_Two_Closed_Shells_Holding_Its_Walls()
    {
        var room = New();
        EditMeshRoom(room, new Vector3(6, 3, 5), 0.2f);

        (Info(room).VertexCount, Info(room).EdgeCount, Info(room).FaceCount).Should().Be((16, 24, 12));
        Info(room).IsClosed.Should().BeTrue();
        Info(room).Volume.Should().BeApproximately(6 * 3 * 5 - 5.6f * 2.6f * 4.6f, Close);
    }

    [Fact]
    public void A_Window_Goes_Through_The_Wall_Selected_And_Not_The_Wall_Across()
    {
        var room = New();
        EditMeshRoom(room, new Vector3(6, 3, 5), 0.2f);
        var before = Info(room).Volume;

        // The front wall's outside, the first face facing out along +Z, the inside of the back wall
        // facing that way too, after it.
        EditMeshSelectFacing(room, Vector3.UnitZ);
        EditMeshCutWindow(room, 1, 1, 3, 1);

        Info(room).IsClosed.Should().BeTrue();
        Info(room).Volume.Should().BeApproximately(before - 1 * 1 * 0.2f, Close);
        Info(room).SelectedFaceCount.Should().Be(4);
        foreach (var face in Enumerable.Range(0, Info(room).FaceCount).Where(face => IsEditMeshFaceSelected(room, face)))
        {
            Center(room, face).Z.Should().BeInRange(4.8f - Close, 5 + Close);
            Center(room, face).X.Should().BeInRange(2.5f - Close, 3.5f + Close);
        }

        EditMeshSelectFacing(room, Vector3.UnitY);
        var floor = () => EditMeshCutWindow(room, 1, 1, 1, 1);
        floor.Should().Throw<InvalidOperationException>();
        EditMeshSelectNone(room);
        var none = () => EditMeshCutWindow(room, 1, 1, 1, 1);
        none.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_Door_Stands_On_The_Walls_Bottom_And_A_Second_Opening_Is_Measured_On_The_Whole_Wall()
    {
        var wall = Box(new Vector3(6, 3, 0.2f));
        EditMeshSelectFacing(wall, Vector3.UnitZ);
        EditMeshCutWindow(wall, 1, 1, 4, 1);

        // Every piece the window left of the front, so the door is measured from the wall's left.
        EditMeshSelectInside(wall, new Vector3(-1, -1, 0.199f), new Vector3(7, 4, 0.201f));
        EditMeshCutDoor(wall, 1, 2.1f, 1.5f);

        Info(wall).IsClosed.Should().BeTrue();
        Info(wall).Volume.Should().BeApproximately(6 * 3 * 0.2f - 1 * 0.2f - 1 * 2.1f * 0.2f, Close);

        // The door's sides from its foot at the wall's bottom up to its head, between one and two along X.
        var sides = Enumerable.Range(0, Info(wall).FaceCount).Where(face => IsEditMeshFaceSelected(wall, face)).Select(face => Center(wall, face)).ToArray();
        sides.Should().HaveCount(3).And.OnlyContain(center => center.X >= 1 - Close && center.X <= 2 + Close);
        Lines(wall).Should().Equal("box 6 3 0.2", "select +z", "cut window 1 1 4 1", "select box -1 -1 0.199 7 4 0.201", "cut door 1 2.1 1.5");
    }

    [Fact]
    public void A_Door_Cut_Down_Through_A_Rooms_Slab_Leaves_The_Floor_Behind_The_Wall_Whole()
    {
        var room = New();
        EditMeshRoom(room, new Vector3(6, 3, 5), 0.2f);
        var before = Info(room).Volume;
        EditMeshSelectFacing(room, Vector3.UnitZ);
        EditMeshCutDoor(room, 1, 2.1f, 1.5f);

        // The door goes through the wall from the ground, a step up to the floor inside, and the
        // cutter's centimeter past the wall stops above the floor, so no slit goes down through the
        // slab along the threshold.
        Info(room).IsClosed.Should().BeTrue();
        Info(room).Volume.Should().BeApproximately(before - 1 * 2.1f * 0.2f, Close);
        var floor = Enumerable.Range(0, Info(room).FaceCount)
            .Where(face => GetEditMeshFaceNormal(room, face) == Vector3.UnitY && MathF.Abs(Center(room, face).Y - 0.2f) < Close).ToArray();
        floor.Should().ContainSingle("the floor inside is one face, uncut");
        GetEditMeshFace(room, floor[0]).Should().HaveCount(6, "its edge along the front wall takes a corner where each of the door's sides meets it");
    }

    [Fact]
    public void A_Flight_Joined_By_Its_Line_Is_One_Solid_With_The_Floor_And_The_Wall_It_Stands_On()
    {
        // A hall's floor and wall the flight stands on and against.
        EditMesh Hall()
        {
            var mesh = Box(new Vector3(6, 3, 6));
            EditMeshCut(mesh, "box 5.6 2.6 5.6 at 0.2 0.2 0.2");
            return mesh;
        }
        var shell = 6 * 3 * 6 - 5.6f * 2.6f * 5.6f;

        var hall = Hall();
        EditMeshUnion(hall, "stairs 8 0.2 0.3 1 at 0.2 0.2 1").Should().BeTrue();
        Info(hall).IsClosed.Should().BeTrue();
        Lines(hall)[^1].Should().Be("union stairs 8 0.2 0.3 1 at 0.2 0.2 1");
        Info(hall).Volume.Should().BeApproximately(shell + 0.3f * 1 * 0.2f * 36, Close);

        // A window cut after it leaves every edge shared by two faces, where a flight added as a
        // second solid touching the floor and the wall has its edges welded to theirs by the cut,
        // four faces along each, and every one of them counted open.
        static void Window(EditMesh mesh)
        {
            EditMeshSelectInside(mesh, new Vector3(5.8f, 0.2f, 0.2f), new Vector3(5.8f, 2.8f, 5.8f));
            EditMeshCutWindow(mesh, 1, 1, 2.8f, 1);
        }
        Window(hall);
        Info(hall).OpenEdges.Should().Be(0);

        var added = Hall();
        EditMeshStairs(added, 8, 0.2f, 0.3f, 1, new Vector3(0.2f, 0.2f, 1));
        Window(added);
        Info(added).OpenEdges.Should().BePositive();
    }

    [Fact]
    public void A_Primitives_Line_In_A_Boolean_Takes_The_Meshs_First_Slot_And_Reads_Back()
    {
        var mesh = Box(new Vector3(2));
        EditMeshUnion(mesh, "box 1 1 1 at 2 0 0").Should().BeTrue();
        EditMeshSubtract(mesh, "box 0.5 0.5 0.5").Should().BeTrue();
        EditMeshIntersect(mesh, "box 3 3 3 at -0.5 -0.5 -0.5").Should().BeTrue();

        GetEditMeshSlots(mesh).Should().Equal("default");
        Info(mesh).IsClosed.Should().BeTrue();
        Lines(mesh).Should().Equal("box 2 2 2", "union box 1 1 1 at 2 0 0", "subtract box 0.5 0.5 0.5", "intersect box 3 3 3 at -0.5 -0.5 -0.5");
        var again = LoadEditMeshFromMemory(GetEditMeshRecipe(mesh));
        _made.Add(again);
        Lines(again).Should().Equal(Lines(mesh));

        // A primitive's line places it with its own at, which a boolean's move of a file's model is not.
        var moved = () => EditMeshUnion(mesh, "box 1 1 1", Vector3.One);
        moved.Should().Throw<ArgumentException>();
        // A file that is missing is answered false, the mesh as it was.
        EditMeshUnion(mesh, _folder.File("none.model")).Should().BeFalse();
        Info(mesh).IsClosed.Should().BeTrue();
    }

    [Fact]
    public void A_Ramp_Is_A_Wedge_In_The_Stairs_Words_And_The_Lines_Read_Back()
    {
        var ramp = New();
        EditMeshRamp(ramp, 1, 3, 2);
        (Info(ramp).VertexCount, Info(ramp).FaceCount).Should().Be((6, 5));
        Info(ramp).Volume.Should().BeApproximately(3, Close);
        Info(ramp).Bounds.Should().Be(new BoundingBox(Vector3.Zero, new Vector3(2, 1, 3)));

        var made = New();
        EditMeshEpsilon(made, 1e-4f);
        EditMeshRoom(made, new Vector3(6, 3, 5), 0.2f);
        EditMeshSelectFacing(made, Vector3.UnitZ);
        EditMeshCutWindow(made, 1, 1, 3, 1);
        EditMeshCut(made, "cylinder 0.2 4 8 at 1 -0.5 1");
        EditMeshRamp(made, 0.5f, 2, 1, new Vector3(1, 0.2f, 1));
        var again = LoadEditMeshFromMemory(GetEditMeshRecipe(made));
        _made.Add(again);

        Lines(again).Should().Equal(Lines(made));
        (Info(again).VertexCount, Info(again).FaceCount).Should().Be((Info(made).VertexCount, Info(made).FaceCount));
        Info(again).Volume.Should().BeApproximately(Info(made).Volume, Close);

        var plane = () => EditMeshCut(Box(Vector3.One), "plane 1 1");
        plane.Should().Throw<ArgumentException>();
        var solidRoom = () => EditMeshRoom(New(), Vector3.One, 0.5f);
        solidRoom.Should().Throw<ArgumentOutOfRangeException>();
    }
}
