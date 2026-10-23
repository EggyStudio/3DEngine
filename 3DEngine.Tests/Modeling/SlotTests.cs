using System.Numerics;
using System.Text.Json;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Modeling;

/// <summary>
/// An edit mesh's material slots added, renamed and taken away, and given a look, a color, a
/// texture and the maps of the engine's material, which a model built from the mesh is drawn with,
/// a glTF file written from it holds, and the recipe and the model commands write and read.
/// </summary>
/// <remarks>
/// The textures are 512 pixels wide, written beside the test program, so a look names them by a
/// path from there as a game's recipe names its textures.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class SlotTests : IDisposable
{
    private readonly List<EditMesh> _made = [];
    private readonly string _name = $"slot-textures-{Guid.NewGuid():N}";
    private readonly TestFolder _folder;

    public SlotTests()
    {
        var config = Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        _folder = TestFolder.At(Path.Combine(AppContext.BaseDirectory, _name));
        ExportImage(GenImageChecked(512, 512, 8, 4, new Color(160, 60, 40, 255), new Color(200, 190, 180, 255)), _folder.File("bricks.png")).Should().BeTrue();
        ExportImage(GenImageColor(512, 256, new Color(128, 128, 255, 255)), _folder.File("bricks_n.png")).Should().BeTrue();
    }

    public void Dispose()
    {
        foreach (var mesh in _made) UnloadEditMesh(mesh);
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private string Bricks => $"{_name}/bricks.png";

    private EditMesh Room()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshBox(mesh, new Vector3(6, 3, 5));
        EditMeshMaterial(mesh, "wall");
        EditMeshSelectFacing(mesh, -Vector3.UnitY);
        EditMeshMaterial(mesh, "floor");
        return mesh;
    }

    private static string[] Lines(EditMesh mesh) => [.. GetEditMeshRecipe(mesh).TrimEnd('\n').Split('\n').Skip(1)];

    private static int Count(EditMesh mesh, string slot) =>
        Enumerable.Range(0, GetEditMeshInfo(mesh).FaceCount).Count(face => GetEditMeshFaceSlot(mesh, face) == slot);

    [Fact]
    public void A_Slot_Is_Added_Renamed_And_Taken_Away_Its_Faces_Going_To_The_First()
    {
        var mesh = Room();
        EditMeshSlotAdd(mesh, "trim");
        GetEditMeshSlots(mesh).Should().Equal("default", "wall", "floor", "trim");

        EditMeshSlotRename(mesh, "floor", "planks");
        GetEditMeshSlots(mesh).Should().Equal("default", "wall", "planks", "trim");
        Count(mesh, "planks").Should().Be(1);

        EditMeshSlotRemove(mesh, "wall");
        GetEditMeshSlots(mesh).Should().Equal("default", "planks", "trim");
        Count(mesh, "default").Should().Be(5, "the walls and the ceiling were the wall slot's");
        Count(mesh, "planks").Should().Be(1);
        Lines(mesh)[^3..].Should().Equal("slot add trim", "slot rename floor planks", "slot remove wall");

        var first = () => EditMeshSlotRemove(mesh, "default");
        first.Should().Throw<InvalidOperationException>();
        var taken = () => EditMeshSlotRename(mesh, "trim", "planks");
        taken.Should().Throw<ArgumentException>();
        var missing = () => EditMeshSlotAdd(mesh, "trim");
        missing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void The_First_Slot_Renamed_Is_Named_Default_Again_When_The_Rename_Is_Undone()
    {
        var mesh = Room();
        EditMeshSlotRename(mesh, "default", "ceiling");
        GetEditMeshSlots(mesh)[0].Should().Be("ceiling");

        EditMeshUndo(mesh).Should().BeTrue();
        GetEditMeshSlots(mesh).Should().Equal("default", "wall", "floor");
    }

    [Fact]
    public void A_Look_Is_Lines_Of_The_Recipe_That_Make_It_Again()
    {
        var mesh = Room();
        EditMeshSlotColor(mesh, "wall", new Color(204, 191, 179, 255));
        EditMeshSlotMap(mesh, "wall", MaterialMapIndex.Albedo, Bricks);
        EditMeshSlotMap(mesh, "wall", MaterialMapIndex.Normal, $"{_name}/bricks_n.png");
        EditMeshSlotEmissive(mesh, "lamp", new Vector3(4, 3.5f, 3));
        EditMeshSlotMetallic(mesh, "floor", 0.5f);
        EditMeshSlotRoughness(mesh, "floor", 0.3f);
        EditMeshSlotMap(mesh, "wall", MaterialMapIndex.Normal, null);

        Lines(mesh)[^7..].Should().Equal(
            "slot color wall 0.8 0.749 0.702",
            $"slot texture wall {Bricks}",
            $"slot normal-map wall {_name}/bricks_n.png",
            "slot emissive lamp 4 3.5 3",
            "slot metallic floor 0.5",
            "slot roughness floor 0.3",
            "slot normal-map wall none");

        var again = LoadEditMeshFromMemory(GetEditMeshRecipe(mesh));
        _made.Add(again);
        LoadMaterialFromEditMeshSlot(again, "wall").Color.Should().Be(new Color(204, 191, 179, 255), "three places bring a byte back whole");
        GetEditMeshSlots(again).Should().Equal("default", "wall", "floor", "lamp");

        var height = () => EditMeshSlotMap(mesh, "wall", MaterialMapIndex.Height, Bricks);
        height.Should().Throw<ArgumentException>();
        var bright = () => EditMeshSlotMetallic(mesh, "wall", 1.5f);
        bright.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_Model_Built_From_The_Mesh_Is_Drawn_With_Each_Slots_Look_And_Its_Prototype_Where_It_Has_None()
    {
        var mesh = Room();
        EditMeshSlotMap(mesh, "wall", MaterialMapIndex.Albedo, Bricks);
        EditMeshSlotColor(mesh, "floor", new Color(120, 90, 60, 255));
        EditMeshSlotEmissive(mesh, "lamp", new Vector3(4, 2, 1));
        var model = LoadModelFromEditMesh(mesh);
        ModelMaterial Of(int slot) => model.Materials[model.MeshMaterial[slot]];

        Of(0).Should().Be(LoadMaterialPrototype("default"));
        (Of(1).Texture.Width, Of(1).Texture.Height, Of(1).Color).Should().Be((512, 512, Color.White), "a texture takes the grid's place");
        (Of(2).Color, Of(2).Texture).Should().Be((new Color(120, 90, 60, 255), LoadMaterialPrototype("floor").Texture), "a color alone tints the grid");
        (Of(3).Emissive, Of(3).EmissiveIntensity).Should().Be((new Color(255, 188, 137, 255), 4f), "a light past one is its color at its brightest part's strength");

        // A material the program gave a slot stays while that slot's look stays, and a slot given
        // another look takes it.
        model.Materials[model.MeshMaterial[1]] = Of(1) with { Roughness = 0.2f };
        EditMeshSlotColor(mesh, "floor", new Color(60, 90, 120, 255));
        UpdateModelFromEditMesh(model, mesh);
        Of(1).Roughness.Should().Be(0.2f);
        Of(2).Color.Should().Be(new Color(60, 90, 120, 255));
        foreach (var built in model.Meshes) UnloadMesh(built);
    }

    [Fact]
    public void A_Missing_Texture_Is_Said_And_The_Slot_Drawn_On_The_Grid_Until_The_File_Comes()
    {
        var mesh = Room();
        EditMeshSlotMap(mesh, "wall", MaterialMapIndex.Albedo, $"{_name}/late.png");

        var material = LoadMaterialFromEditMeshSlot(mesh, "wall");
        material.Texture.Should().Be(LoadMaterialPrototype("wall").Texture);
        material.Color.Should().Be(GetPrototypeColor("wall"), "with no texture and no color, the slot's name gives its color");

        ExportImage(GenImageColor(64, 64, Color.Red), _folder.File("late.png")).Should().BeTrue();
        LoadMaterialFromEditMeshSlot(mesh, "wall").Texture.Width.Should().Be(64, "a missing file is looked for again at the next build");
    }

    [Fact]
    public void A_Density_Given_Alone_Reads_The_Width_Of_The_Slots_Texture()
    {
        var mesh = LoadEditMesh();
        _made.Add(mesh);
        EditMeshBox(mesh, new Vector3(2));
        EditMeshSlotMap(mesh, "default", MaterialMapIndex.Albedo, Bricks);
        EditMeshSelectFacing(mesh, Vector3.UnitY);

        EditMeshUvDensity(mesh, 256).Should().BeTrue();
        Lines(mesh)[^1].Should().Be("uv density 256 512");

        EditMeshSlotMap(mesh, "default", MaterialMapIndex.Albedo, null);
        EditMeshUvDensity(mesh, 256).Should().BeFalse("the slot has no texture to read the width of");
    }

    [Fact]
    public void A_glTF_File_Holds_Each_Slots_Color_Texture_And_Light()
    {
        var mesh = Room();
        EditMeshSlotMap(mesh, "wall", MaterialMapIndex.Albedo, Bricks);
        EditMeshSlotColor(mesh, "floor", new Color(255, 128, 0, 255));
        EditMeshSlotRoughness(mesh, "floor", 0.4f);
        EditMeshSelectFacing(mesh, Vector3.UnitY);
        EditMeshMaterial(mesh, "lamp");
        EditMeshSlotEmissive(mesh, "lamp", new Vector3(4, 2, 1));
        var file = _folder.File("level/room.gltf");

        ExportEditMesh(mesh, file).Should().BeTrue();

        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var root = json.RootElement;
        var materials = root.GetProperty("materials").EnumerateArray().ToDictionary(material => material.GetProperty("name").GetString()!);
        root.GetProperty("images")[0].GetProperty("uri").GetString().Should().Be("../bricks.png", "a picture is named by its path from the file's folder");
        materials["wall"].GetProperty("pbrMetallicRoughness").GetProperty("baseColorTexture").GetProperty("index").GetInt32().Should().Be(0);

        var floor = materials["floor"].GetProperty("pbrMetallicRoughness");
        floor.GetProperty("baseColorFactor").EnumerateArray().Select(part => part.GetSingle()).Should()
            .Equal([1f, 0.215897f, 0f, 1f], (a, b) => MathF.Abs(a - b) < 1e-5f, "glTF's factor is linear, the recipe's 0.502 for 128 being a fifth");
        floor.GetProperty("roughnessFactor").GetSingle().Should().Be(0.4f);

        var lamp = materials["lamp"];
        lamp.GetProperty("emissiveFactor").EnumerateArray().Select(part => part.GetSingle()).Should().Equal(1f, 0.5f, 0.25f);
        lamp.GetProperty("extensions").GetProperty("KHR_materials_emissive_strength").GetProperty("emissiveStrength").GetSingle().Should().Be(4);
        root.GetProperty("extensionsUsed")[0].GetString().Should().Be("KHR_materials_emissive_strength");
    }

    [Fact]
    public void The_Model_Commands_Give_A_Slot_Its_Look_And_Shape_The_Coordinates()
    {
        string Run(string line, out string? code)
        {
            using (ConsoleHost.Lend(GetApp().World, GetApp()))
            {
                var answer = ConsoleCommands.Run(line) ?? "";
                code = ConsoleHost.Failure?.Code;
                return answer;
            }
        }

        Run("model.box hall 2 2 2", out _);
        Run($"model.slot hall texture default {Bricks}", out var code).Should().StartWith("hall: ");
        code.Should().BeNull();
        Run("model.select hall +y", out _);
        Run("model.uv hall density 256", out code);
        code.Should().BeNull();
        Run("model.uv hall rotate 90 each", out _);
        Run("model.recipe hall", out _).Split('\n')[^2..].Should().Equal("uv density 256 512", "uv rotate 90 each");
        Run("model.info hall", out _).Split('\n')[3].Should().Be($"slots default (texture {Bricks})");

        Run("model.uv hall spin 3", out code);
        code.Should().Be("BAD_ARGUMENT");
        Run("model.slot hall remove default", out code);
        code.Should().Be("BAD_ARGUMENT", "the first slot stays");
        Run("model.free hall", out _);
    }
}
