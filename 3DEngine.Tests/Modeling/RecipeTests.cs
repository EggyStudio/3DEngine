using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// An edit mesh's recipe, the lines its operations write, the mesh made again from them, undo and
/// redo by making it again, the file it is saved as and loaded from, a model joined in by its file,
/// and the errors a line that is not an operation is named by.
/// </summary>
/// <remarks>
/// The lines are the words BevyCSharp's edit mesh writes too, so a recipe written in one engine
/// builds the same mesh in the other.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class RecipeTests : IDisposable
{
    private const float Close = 1e-4f;
    private readonly TestFolder _folder = new("engine-recipe-");
    private readonly List<EditMesh> _made = [];

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
        _folder.Dispose();
    }

    private EditMesh Kept(EditMesh mesh)
    {
        _made.Add(mesh);
        return mesh;
    }

    private static string[] Lines(EditMesh mesh) => GetEditMeshRecipe(mesh).Split('\n', StringSplitOptions.RemoveEmptyEntries)[1..];

    private EditMesh Room()
    {
        var mesh = Kept(LoadEditMesh());
        EditMeshBox(mesh, new Vector3(8, 3, 6));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMaterial(mesh, "ceiling");
        EditMeshInset(mesh, 1);
        EditMeshExtrude(mesh, 0.5f);
        EditMeshSelectAll(mesh);
        EditMeshRotate(mesh, Vector3.UnitY, 90, Vector3.Zero);
        EditMeshGrid(mesh, 0.5f);
        EditMeshSnap(mesh);
        return mesh;
    }

    [Fact]
    public void Each_Operation_Writes_Its_Line_In_The_Words_A_Recipe_Takes()
    {
        Lines(Room()).Should().Equal("box 8 3 6", "select +y", "material ceiling", "inset 1", "extrude 0.5", "select all",
            "rotate y 90 at 0 0 0", "grid 0.5", "snap");

        var mesh = Kept(LoadEditMesh());
        EditMeshStairs(mesh, 4, 0.25f, 0.3f, 2, new Vector3(1, 0, -2));
        EditMeshCylinder(mesh, 0.4f, 3, 8, new Vector3(0, 1.5f, 0), centered: true);
        EditMeshSelectFaces(mesh, [0, 2], SelectionMode.Add);
        EditMeshSelectFacing(mesh, -Vector3.UnitX, SelectionMode.Remove);
        EditMeshSelectInside(mesh, Vector3.Zero, Vector3.One);
        EditMeshSelectSlot(mesh, "default", SelectionMode.Add);
        EditMeshScale(mesh, new Vector3(1, 2, 1));
        EditMeshRotate(mesh, new Vector3(1, 0, 1), 45);
        EditMeshShade(mesh, true);
        EditMeshDelete(mesh, fill: true);
        EditMeshSelectAll(mesh);
        EditMeshMerge(mesh, 0.01f);
        EditMeshFlip(mesh);
        EditMeshTriangulate(mesh);
        EditMeshDuplicate(mesh);
        EditMeshExtrude(mesh, new Vector3(0, 1, 0));
        EditMeshClear(mesh);

        Lines(mesh).Should().Equal(
            "stairs 4 0.25 0.3 2 at 1 0 -2", "cylinder 0.4 3 8 at 0 1.5 0 center", "select add face 0 2", "select remove -x",
            "select box 0 0 0 1 1 1", "select add slot default", "scale 1 2 1", "rotate 1 0 1 45", "shade smooth 30", "delete fill",
            "select all", "merge 0.01", "flip", "triangulate", "duplicate", "extrude 0 1 0", "clear");
    }

    [Fact]
    public void A_Recipe_Run_From_Nothing_Makes_The_Same_Mesh()
    {
        var made = Room();
        var again = Kept(LoadEditMeshFromMemory(GetEditMeshRecipe(made)));

        Lines(again).Should().Equal(Lines(made));
        var (a, b) = (GetEditMeshInfo(made), GetEditMeshInfo(again));
        (b.VertexCount, b.EdgeCount, b.FaceCount).Should().Be((a.VertexCount, a.EdgeCount, a.FaceCount));
        GetEditMeshSlots(again).Should().Equal(GetEditMeshSlots(made));
        b.Volume.Should().BeApproximately(a.Volume, Close);
        Enumerable.Range(0, a.VertexCount).Should().OnlyContain(vertex => GetEditMeshVertex(made, vertex) == GetEditMeshVertex(again, vertex));
        b.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Undo_Makes_The_Mesh_Again_Without_The_Last_Line_And_Redo_Puts_It_Back()
    {
        var mesh = Kept(LoadEditMesh());
        EditMeshBox(mesh, new Vector3(2, 3, 4));
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMove(mesh, new Vector3(0, 1, 0));
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(32, Close);

        EditMeshUndo(mesh).Should().BeTrue();
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(24, Close);
        Lines(mesh).Should().Equal("box 2 3 4", "select +y");
        // The selection is the one the lines before left, the top.
        GetEditMeshInfo(mesh).SelectedFaceCount.Should().Be(1);

        EditMeshRedo(mesh).Should().BeTrue();
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(32, Close);
        EditMeshRedo(mesh).Should().BeFalse();

        // An operation after an undo forgets what was taken back.
        EditMeshUndo(mesh);
        EditMeshMove(mesh, new Vector3(0, 2, 0));
        EditMeshRedo(mesh).Should().BeFalse();
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(40, Close);

        while (EditMeshUndo(mesh)) { }
        GetEditMeshInfo(mesh).VertexCount.Should().Be(0);
        Lines(mesh).Should().BeEmpty();
    }

    [Fact]
    public void A_Saved_Recipe_Loads_Back_As_The_Same_Mesh()
    {
        var made = Room();
        var path = _folder.File("models/room.model");
        SaveEditMesh(made, path).Should().BeTrue();

        File.ReadAllText(path).Should().StartWith("model 1\n");
        File.Exists(path + ".part").Should().BeFalse();

        var loaded = Kept(LoadEditMesh(path));
        IsEditMeshValid(loaded).Should().BeTrue();
        Lines(loaded).Should().Equal(Lines(made));
        (GetEditMeshInfo(loaded).VertexCount, GetEditMeshInfo(loaded).FaceCount).Should().Be((GetEditMeshInfo(made).VertexCount, GetEditMeshInfo(made).FaceCount));
        EditMeshUndo(loaded).Should().BeTrue("a loaded model's undo goes back through its recipe");
    }

    [Fact]
    public void A_Joined_Model_Is_Added_By_Its_File_With_Its_Slots_By_Name()
    {
        var pillar = Kept(LoadEditMesh());
        EditMeshCylinder(pillar, 0.5f, 3, 8);
        EditMeshMaterial(pillar, "stone");
        var pillarPath = _folder.File("pillar.model");
        SaveEditMesh(pillar, pillarPath);

        var hall = Kept(LoadEditMesh());
        EditMeshBox(hall, new Vector3(10, 3, 10));
        EditMeshJoin(hall, pillarPath);
        EditMeshMove(hall, new Vector3(5, 0, 5));

        var info = GetEditMeshInfo(hall);
        (info.VertexCount, info.FaceCount).Should().Be((8 + 16, 6 + 10));
        GetEditMeshSlots(hall).Should().Equal("default", "stone");
        (info.SelectedVertexCount, info.SelectedFaceCount).Should().Be((16, 10));
        Lines(hall)[1].Should().Be($"join {pillarPath}");

        // A model that joins itself is refused rather than read until the stack runs out.
        var loop = _folder.File("loop.model");
        File.WriteAllText(loop, $"model 1\nbox 1 1 1\njoin {loop}\n");
        var joinsItself = () => EditMeshJoin(hall, loop);
        joinsItself.Should().Throw<FormatException>();
        IsEditMeshValid(LoadEditMesh(loop)).Should().BeFalse("its load is refused with a warning");
    }

    [Fact]
    public void A_Read_Recipe_Skips_Comments_And_Blank_Lines_And_Says_Which_Line_Is_Wrong()
    {
        var mesh = Kept(LoadEditMeshFromMemory("model 1\n# a room\n\nbox 8 3 6   # the walls\nselect +y\nextrude 1\n"));
        Lines(mesh).Should().Equal("box 8 3 6", "select +y", "extrude 1");
        GetEditMeshInfo(mesh).Volume.Should().BeApproximately(8 * 4 * 6, Close);

        string Refusal(string recipe)
        {
            var read = () => LoadEditMeshFromMemory(recipe);
            return read.Should().Throw<FormatException>().Which.Message;
        }

        Refusal("model 1\nbox 1 1 1\nboxx 1 1 1\n").Should().StartWith("Line 3 of the recipe, boxx 1 1 1, boxx is not an operation");
        Refusal("box 1 1").Should().Contain("needs another number");
        Refusal("move 1 up 0").Should().Contain("is not a number");
        Refusal("flip twice").Should().Contain("words after");
        Refusal("model 2\nbox 1 1 1").Should().Contain("this engine reads model 1");
        Refusal("box 1 0 1").Should().Contain("above zero");
    }

    [Fact]
    public void A_File_That_Is_Not_A_Models_Recipe_Gives_An_Edit_Mesh_That_Is_Not_Loaded()
    {
        var noHeader = _folder.File("plain.model");
        File.WriteAllText(noHeader, "box 1 1 1\n");
        IsEditMeshValid(LoadEditMesh(noHeader)).Should().BeFalse("a recipe's file starts with model 1");
        IsEditMeshValid(LoadEditMesh(_folder.File("none.model"))).Should().BeFalse();
    }

    [Theory]
    [InlineData("rotate y 90 at 1 0 1")]
    [InlineData("rotate 0 -1 0 45")]
    [InlineData("scale 2")]
    [InlineData("scale 1 2 1 at 0 0 0")]
    [InlineData("select add face 0 1")]
    [InlineData("select remove +y")]
    [InlineData("select box 0 0 0 1 1 1")]
    [InlineData("select slot default")]
    [InlineData("extrude 1")]
    [InlineData("extrude 0 1 0")]
    [InlineData("inset 0.25")]
    [InlineData("delete fill")]
    [InlineData("shade smooth 45")]
    [InlineData("shade flat")]
    [InlineData("wedge 1 2 3 center")]
    [InlineData("arch 2 0.5 1 8 at 0 0 4")]
    public void A_Line_Run_Is_Written_Back_As_It_Was_Typed(string line)
    {
        var mesh = Kept(LoadEditMesh());
        EditMeshBox(mesh, new Vector3(2));
        EditMeshSelectAll(mesh);
        EditMeshRun(mesh, line);

        Lines(mesh)[^1].Should().Be(line);
    }
}
