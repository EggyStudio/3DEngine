using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Diagnostics;

/// <summary>
/// The model commands run through the console host as <c>./e3d command</c> runs them, with the
/// guide's house blocked out line by line and the counts each line answers, its recipe saved and
/// loaded again, the refusals a script reads, a program's mesh reached by its name, and a model
/// shown following its edits.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class EditMeshCommandsTests : IDisposable
{
    private readonly TestFolder _folder = new("e3d-model-commands-");

    public EditMeshCommandsTests()
    {
        var config = Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        foreach (var (name, _) in Names.All) Run($"model.free {name}");
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static EditMeshNames Names => EditMeshNames.Of(GetApp().World);

    private static string Run(string line) => Run(line, out _);

    private static string Run(string line, out string? code)
    {
        using (ConsoleHost.Lend(GetApp().World, GetApp()))
        {
            var answer = ConsoleCommands.Run(line) ?? "";
            code = ConsoleHost.Failure?.Code;
            return answer;
        }
    }

    // The guide's house, a room with a floor of its own, a window and a door in its front wall.
    private static readonly string[] House =
    [
        "model.room house 6 3 5 0.2",
        "model.material house wall",
        "model.select house box 0 0.1 0 6 0.3 5",
        "model.material house floor",
        "model.select house +z",
        "model.cut house window 1.2 1 4.5 1",
        "model.select house +z",
        "model.cut house door 1 2.1 1.5",
    ];

    [Fact]
    public void The_Guides_House_Is_Blocked_Out_Line_By_Line_Each_Answering_Its_Counts()
    {
        var answers = House.Select(line => Run(line)).ToArray();

        answers[0].Should().Be("house: 16 vertices, 24 edges, 12 faces, 12 faces and 16 vertices selected");
        answers[3].Should().Be("house: 16 vertices, 24 edges, 12 faces, 1 faces and 4 vertices selected");
        answers.Should().OnlyContain(answer => answer.StartsWith("house: ", StringComparison.Ordinal));

        var mesh = GetEditMeshByName("house");
        var info = GetEditMeshInfo(mesh);
        info.IsClosed.Should().BeTrue("the openings are cut through a closed solid and leave one");
        info.Volume.Should().BeApproximately(6 * 3 * 5 - 5.6f * 2.6f * 4.6f - (1.2f * 1 + 1 * 2.1f) * 0.2f, 1e-4f,
            "the room's walls less the window and the door, each as deep as the wall");
        GetEditMeshSlots(mesh).Should().Equal("default", "wall", "floor");
        answers[^1].Should().Be($"house: {info.VertexCount} vertices, {info.EdgeCount} edges, {info.FaceCount} faces, "
            + $"{info.SelectedFaceCount} faces and {info.SelectedVertexCount} vertices selected");

        Run("model.info house").Split('\n').Should().Equal(
            answers[^1],
            "bounds (0, 0, 0) to (6, 3, 5)",
            $"volume {info.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture)} cubic meters, 0 open edges, closed",
            "slots default, wall, floor",
            "grid 1, epsilon 1E-05, 8 lines of recipe");
        Run("model.recipe house").Split('\n').Should().Equal(["model 1", .. House.Select(line => line["model.".Length..].Replace("house ", ""))]);
        Run("model.list").Should().Be(answers[^1]);
    }

    [Fact]
    public void A_Model_Saved_And_Loaded_Again_Has_The_Same_Counts_And_Recipe()
    {
        foreach (var line in House) Run(line);
        var path = _folder.File("house.model");

        Run($"model.save house {path}").Should().Be($"wrote house to {path}");
        Run($"model.load again {path}").Should().Be(Run("model.info house").Split('\n')[0].Replace("house:", "again:"));
        Run("model.recipe again").Should().Be(Run("model.recipe house"));
        Run("model.info again").Split('\n')[1..].Should().Equal(Run("model.info house").Split('\n')[1..]);

        // An undo after the load goes back through the file's lines.
        Run("model.undo again").Should().StartWith("again: ");
        GetEditMeshInfo(GetEditMeshByName("again")).IsClosed.Should().BeTrue("the door is taken back and the window stays");
        Run("model.redo again");
        Run("model.info again").Should().Be(Run("model.info house").Replace("house:", "again:"));
    }

    [Fact]
    public void A_Script_Reads_Why_A_Line_Was_Refused_By_Its_Code()
    {
        Run("model.extrude nothing 1", out var code).Should().Contain("There is no model called nothing");
        code.Should().Be("NO_SUCH_MODEL", "an operation on a mistyped name makes no model");
        Names.All.Should().BeEmpty();

        Run("model.box crate 1 1", out code).Should().StartWith("box on crate: ");
        code.Should().Be("BAD_ARGUMENT");
        Run("model.box crate 1 1 1", out code).Should().Be("crate: 8 vertices, 12 edges, 6 faces, 6 faces and 8 vertices selected");
        code.Should().BeNull();

        Run("model.undo crate");
        Run("model.undo crate", out code);
        code.Should().Be("NOTHING_TO_UNDO");
        Run("model.redo crate", out code).Should().StartWith("crate: 8 vertices");
        Run("model.redo crate", out code);
        code.Should().Be("NOTHING_TO_REDO");

        Run($"model.load crate {_folder.File("missing.model")}", out code);
        code.Should().Be("NO_SUCH_FILE");
        var notes = _folder.File("notes.model");
        File.WriteAllText(notes, "box 1 1 1\n");
        Run($"model.load crate {notes}", out code).Should().Contain("does not start with the line model 1");
        code.Should().Be("BAD_FILE");
        Run("model.show crate 1 2", out code);
        code.Should().Be("BAD_ARGUMENT", "a point is three numbers");

        Run("model.free crate").Should().Be("forgot crate");
        Run("model.free crate", out code);
        code.Should().Be("NO_SUCH_MODEL");
    }

    [Fact]
    public void A_Programs_Mesh_Answers_To_Its_Name_And_Stays_The_Programs()
    {
        var level = LoadEditMesh();
        EditMeshBox(level, new Vector3(8, 3, 6));
        SetEditMeshName(level, "level");

        Run("model.select level +y");
        Run("model.extrude level 1").Should().Be("level: 12 vertices, 20 edges, 10 faces, 1 faces and 4 vertices selected");
        GetEditMeshInfo(level).Bounds.Max.Y.Should().Be(4, "the commands changed the mesh the program holds");
        GetEditMeshByName("level").Should().Be(level);

        Run("model.free level");
        IsEditMeshValid(level).Should().BeTrue("a mesh the program named is the program's to free");
        GetEditMeshByName("level").IsValid.Should().BeFalse();
        UnloadEditMesh(level);

        var bad = () => SetEditMeshName(level, "two words");
        bad.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_Model_Made_By_The_Commands_Is_Freed_With_Its_Name()
    {
        Run("model.box crate 1 1 1");
        var crate = GetEditMeshByName("crate");
        Run("model.new crate").Should().Be("crate is an empty model");
        IsEditMeshValid(crate).Should().BeFalse("the name went to another mesh and the commands made the first");

        var empty = GetEditMeshByName("crate");
        Run("model.free crate");
        IsEditMeshValid(empty).Should().BeFalse();
    }

    [Fact]
    public void A_Model_Shown_Is_An_Entity_At_Its_Point_Drawing_The_Model_Built_Again_After_Each_Edit()
    {
        Run("model.box crate 1 1 1");
        Run("model.show crate 2 0 -3").Should().Be("showing crate at (2, 0, -3)");
        var ecs = GetApp().World.Resource<EcsWorld>();
        var named = Names.Find("crate")!;
        ecs.TryResolve(named.Shown, out var entity).Should().BeTrue();
        ecs.GetReadOnly<Transform>(entity).Position.Should().Be(new Vector3(2, 0, -3));
        ecs.GetReadOnly<Name>(entity).Value.Should().Be("Model crate");

        BeginDrawing();
        EndDrawing();
        named.Model!.Meshes.Should().HaveCount(1);
        var first = named.Model.Meshes[0].Id;

        Run("model.select crate +y");
        Run("model.material crate floor");
        BeginDrawing();
        EndDrawing();
        named.Model.Meshes.Should().HaveCount(2, "the slot made since has a mesh of its own");
        named.Model.Meshes[0].Id.Should().Be(first, "the model is written again in place");
        named.Model.Materials[named.Model.MeshMaterial[1]].Color.Should().Be(GetPrototypeColor("floor"));

        // Shown again, the entity moves rather than another being made.
        Run("model.show crate");
        ecs.Count<Name>().Should().Be(1);
        ecs.GetReadOnly<Transform>(entity).Position.Should().Be(Vector3.Zero);

        Run("model.free crate");
        ecs.TryResolve(named.Shown, out _).Should().BeFalse("what drew the model is taken away with its name");
    }
}
