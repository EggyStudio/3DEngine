using System.Numerics;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// A saved game written as what play changed over the scene it started from, and read back by
/// loading the scene again and laying the save over it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SaveGameTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-save-game-");

    public void Dispose() => _folder.Dispose();

    // Three entities, a and b with a transform each and c with a transform and a camera.
    private static string Level(float a = 1, float b = 2, string aExtra = "") => $$"""
        { "format": "3dengine-scene", "version": 1, "entities": [
          { "id": "a", "name": "A", "components": { "Transform": { "Position": [{{a}}, 0, 0] }{{aExtra}} } },
          { "id": "b", "name": "B", "components": { "Transform": { "Position": [{{b}}, 0, 0] } } },
          { "id": "c", "name": "C", "components": { "Transform": { "Position": [3, 0, 0] }, "Camera": { "FovY": 1 } } }
        ] }
        """;

    private static (World World, EcsWorld Ecs) NewWorld()
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        return (world, ecs);
    }

    private static int Named(EcsWorld ecs, string name) =>
        ecs.Query<Name>().Where(row => row.Item2.Value == name).Select(row => row.Item1).DefaultIfEmpty(0).Single();

    private static float X(EcsWorld ecs, string name) => ecs.GetReadOnly<Transform>(Named(ecs, name)).Position.X;

    private string WriteLevel(string text)
    {
        var path = _folder.File("level.json");
        File.WriteAllText(path, text);
        return path;
    }

    // Starts a game from the level, moves A, despawns B, takes C's camera off, and spawns D to be saved.
    private (World World, EcsWorld Ecs, string Save) Played(string level)
    {
        var (world, ecs) = NewWorld();
        SaveGame.Start(world, level);
        ecs.GetRef<Transform>(Named(ecs, "A")).Position = new Vector3(10, 0, 0);
        ecs.Despawn(Named(ecs, "B"));
        ecs.Remove<Camera>(Named(ecs, "C"));
        var d = ecs.Spawn();
        ecs.SetName(d, "D");
        ecs.Add(d, new Transform(new Vector3(4, 0, 0)));
        ecs.Add(d, default(Saved));
        var save = _folder.File("slot.json");
        SaveGame.Save(world, save).Should().Be(4, "A changed, B removed, C changed and D spawned");
        return (world, ecs, save);
    }

    [Fact]
    public void A_Save_Holds_What_Play_Changed_And_Reads_Back_Over_The_Scene()
    {
        var level = WriteLevel(Level());
        var (_, _, save) = Played(level);

        using (var written = JsonDocument.Parse(File.ReadAllText(save)))
        {
            var entities = written.RootElement.GetProperty("entities").EnumerateArray().ToArray();
            written.RootElement.GetProperty("scenes")[0].GetString().Should().Be(level);
            entities.Single(e => e.GetProperty("id").GetString() == "a").GetProperty("components").EnumerateObject().Select(c => c.Name)
                .Should().Equal(["Transform"], "only what play changed is written");
            entities.Single(e => e.GetProperty("id").GetString() == "b").GetProperty("removed").GetBoolean().Should().BeTrue();
            entities.Single(e => e.GetProperty("id").GetString() == "c").GetProperty("components").GetProperty("Camera").ValueKind
                .Should().Be(JsonValueKind.Null, "a component play took off is written as null");
            entities.Single(e => e.TryGetProperty("name", out var n) && n.GetString() == "D").TryGetProperty("scene", out _)
                .Should().BeFalse("what the game spawned is written whole, belonging to no scene");
        }

        var (world, ecs) = NewWorld();
        SaveGame.Load(world, save);

        X(ecs, "A").Should().Be(10);
        Named(ecs, "B").Should().Be(0, "removed in play, so gone once laid over the scene");
        X(ecs, "C").Should().Be(3);
        ecs.Has<Camera>(Named(ecs, "C")).Should().BeFalse();
        X(ecs, "D").Should().Be(4);
        ecs.Has<Saved>(Named(ecs, "D")).Should().BeTrue("and saved again by the next save");
        SaveGame.ScenesOf(world).Should().Equal(level);
    }

    [Fact]
    public void A_Save_Read_Into_The_Game_It_Was_Made_In_Ends_That_Game_First()
    {
        var level = WriteLevel(Level());
        var (world, ecs, save) = Played(level);
        var stray = ecs.Spawn();
        ecs.SetName(stray, "Camera of the game");

        SaveGame.Load(world, save);

        ecs.Query<Name>().Select(row => row.Item2.Value).Order().Should().Equal("A", "C", "Camera of the game", "D");
    }

    [Fact]
    public void A_Level_Edited_After_The_Save_Shows_Its_Edits_Where_The_Save_Did_Not_Touch_Them()
    {
        var level = WriteLevel(Level());
        var (_, _, save) = Played(level);
        WriteLevel(Level(a: 5, b: 2, aExtra: ", \"Camera\": { \"FovY\": 2 }"));
        File.WriteAllText(level, File.ReadAllText(level).Replace("\"id\": \"c\", \"name\": \"C\", \"components\": { \"Transform\": { \"Position\": [3, 0, 0] }",
            "\"id\": \"c\", \"name\": \"C\", \"components\": { \"Transform\": { \"Position\": [30, 0, 0] }"));

        var (world, ecs) = NewWorld();
        SaveGame.Load(world, save);

        X(ecs, "A").Should().Be(10, "play moved A, so the save's place stands over the level's new one");
        ecs.GetReadOnly<Camera>(Named(ecs, "A")).FovY.Should().Be(2, "the level gave A a camera the save never touched");
        X(ecs, "C").Should().Be(30, "play never moved C, so the level's edit shows");
        ecs.Has<Camera>(Named(ecs, "C")).Should().BeFalse("play took C's camera off");
    }

    [Fact]
    public void A_Save_Read_Back_Is_Told_Once_Over_The_Whole()
    {
        var level = WriteLevel(Level());
        var (_, _, save) = Played(level);
        var (world, ecs) = NewWorld();
        var told = new List<string>();
        ecs.Observe<Loaded<Transform>>(on => told.Add($"{ecs.GetReadOnly<Name>(on.Event.Entity.Index).Value} at {on.Event.Value.Position.X}"));
        ecs.Observe<SceneLoaded>(on => told.Add($"{Path.GetFileName(on.Event.Path)} of {on.Event.Entities.Count}"));

        SaveGame.Load(world, save);

        told.Should().Equal("A at 10", "C at 3", "D at 4", "slot.json of 3");
    }

    [Fact]
    public void A_Save_Of_One_Change_In_A_Level_Of_Two_Hundred_Is_A_Line_Where_The_Whole_Scene_Is_Every_Entity()
    {
        var text = new StringBuilder("{ \"format\": \"3dengine-scene\", \"version\": 1, \"entities\": [");
        for (int i = 0; i < 200; i++)
            text.Append(i == 0 ? "" : ",").Append($"{{ \"id\": \"e{i}\", \"name\": \"E{i}\", \"components\": {{ \"Transform\": {{ \"Position\": [{i}, 0, 0] }} }} }}");
        var level = WriteLevel(text.Append("] }").ToString());
        var (world, ecs) = NewWorld();
        SaveGame.Start(world, level);
        ecs.GetRef<Transform>(Named(ecs, "E7")).Position = new Vector3(70, 0, 0);

        var save = _folder.File("slot.json");
        SaveGame.Save(world, save).Should().Be(1);
        var whole = SceneFile.Write(ecs);

        new FileInfo(save).Length.Should().BeLessThan(whole.Length / 20, "the save holds one entity's one component, the scene every entity's every one");
    }

    [Fact]
    public void A_Save_Needs_A_Started_Game_And_A_File_That_Is_No_Save_Leaves_The_Game_As_It_Was()
    {
        var (world, ecs) = NewWorld();
        var save = () => SaveGame.Save(world, _folder.File("slot.json"));
        save.Should().Throw<InvalidOperationException>();

        var level = WriteLevel(Level());
        SaveGame.Start(world, level);
        var load = () => SaveGame.Load(world, level);
        load.Should().Throw<InvalidDataException>().WithMessage("Not a saved game*");
        ecs.Query<Name>().Should().HaveCount(3, "the level still stands");
    }
}
