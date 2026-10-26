using System.Numerics;
using System.Text.Json;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// Data in files of its own, saved with an id and referred to by it from a scene, found again after
/// a rename, read as none where the file is missing, and costing a scene with no reference nothing.
/// </summary>
/// <remarks>
/// The weapon's codec and the component referring to it are written here as the generator writes
/// them for a <see cref="DataAssetAttribute"/> type and a <see cref="SceneComponentAttribute"/> one,
/// since the generator does not run over the tests. The machine's reference is the engine's own,
/// written by the generator.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DataAssetTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-data-");

    public void Dispose() => _folder.Dispose();

    public struct Weapon
    {
        public int Damage;
        public string Title;
        public Vector3 Reach;

        public static Weapon Default => new() { Damage = 1, Title = "Fist", Reach = Vector3.One };
    }

    public struct Armed
    {
        public DataRef<Weapon> Weapon;
    }

    static DataAssetTests()
    {
        DataAssets.Add(new SceneCodec<Weapon>("Weapon",
            static (System.Text.Json.Utf8JsonWriter w, in Weapon v, SceneWriteContext ctx) =>
            {
                w.WriteNumber("Damage", v.Damage);
                w.WriteString("Title", v.Title);
                SceneJson.Write(w, "Reach", v.Reach);
            },
            static (e, ctx) =>
            {
                var value = Weapon.Default;
                if (e.TryGetProperty("Damage", out var p)) value.Damage = p.GetInt32();
                if (e.TryGetProperty("Title", out p)) value.Title = p.GetString()!;
                if (e.TryGetProperty("Reach", out p)) value.Reach = SceneJson.ReadVector3(p);
                return value;
            }));
        SceneComponents.Add(new SceneCodec<Armed>("DataTestArmed",
            static (System.Text.Json.Utf8JsonWriter w, in Armed v, SceneWriteContext ctx) => w.WriteString("Weapon", v.Weapon.Id),
            static (e, ctx) => new Armed { Weapon = e.TryGetProperty("Weapon", out var p) ? ctx.Data<Weapon>(p.GetString()) : default }));
    }

    private static (World World, EcsWorld Ecs) NewWorld()
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        return (world, ecs);
    }

    // Whether a warning logged since the count was taken says the text.
    private static bool Warned(int before, string text) =>
        ConsoleLog.All().Skip(Math.Max(0, ConsoleLog.All().Length - (ConsoleLog.Written - before)))
            .Any(line => line.Level == LogLevel.Warning && line.Text.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void A_Data_File_Is_Saved_With_An_Id_Read_Back_By_Its_Name_And_Saved_Again_Under_The_Same_Id()
    {
        var file = _folder.File("sword.json");
        var sword = new Weapon { Damage = 12, Title = "Sword", Reach = new Vector3(0, 0, 2) };

        var saved = DataAssets.Save(file, sword);

        saved.Id.Should().MatchRegex("^[0-9a-f]{12}$", "an id is given at the first save, as a scene file's entities are");
        saved.Path.Should().Be(file);
        var text = File.ReadAllText(file);
        using (var json = JsonDocument.Parse(text))
        {
            var root = json.RootElement;
            root.GetProperty("format").GetString().Should().Be(DataAssets.Format);
            root.GetProperty("version").GetInt32().Should().Be(DataAssets.Version);
            root.GetProperty("id").GetString().Should().Be(saved.Id);
            root.GetProperty("type").GetString().Should().Be(typeof(Weapon).FullName);
            root.GetProperty("fields").GetProperty("Damage").GetInt32().Should().Be(12);
        }
        text.Should().Contain("\"Reach\": [0, 0, 2]", "a vector is folded onto one line, as a scene file's is");

        // As a program begun again reads it.
        DataAssets.Forget(saved.Id!);
        var read = DataAssets.Load<Weapon>(file);
        read.Id.Should().Be(saved.Id);
        read.Value.Should().Be(sword);
        read.IsNone.Should().BeFalse();

        DataAssets.Save(file, sword with { Damage = 15 });
        DataAssets.Load<Weapon>(file).Id.Should().Be(saved.Id, "a later save keeps the id");
        read.Value.Damage.Should().Be(15, "every reference to the file shares the value a save writes");
    }

    [Fact]
    public void A_File_Written_By_Hand_Keeps_The_Default_For_A_Field_It_Lacks_And_Is_Given_An_Id_When_First_Read()
    {
        var file = _folder.File("club.json");
        File.WriteAllText(file, $$"""{ "format": "3dengine-data", "version": 1, "type": "{{typeof(Weapon).FullName}}", "fields": { "Damage": 3 } }""");

        var club = DataAssets.Load<Weapon>(file);

        club.Value.Damage.Should().Be(3);
        club.Value.Title.Should().Be("Fist", "a field the file lacks keeps the type's default");
        club.Id.Should().MatchRegex("^[0-9a-f]{12}$");
        var text = File.ReadAllText(file);
        text.IndexOf("\"id\"", StringComparison.Ordinal).Should().BeGreaterThan(text.IndexOf("\"version\"", StringComparison.Ordinal), "written after the format and version")
            .And.BeLessThan(text.IndexOf("\"type\"", StringComparison.Ordinal));
        DataAssets.Find<Weapon>(club.Id!).Value.Damage.Should().Be(3, "found by the id it was given");
    }

    [Fact]
    public void A_Machine_Referred_To_By_Its_Id_Is_Found_After_Its_File_Is_Renamed()
    {
        var file = _folder.File("hero.machine.json");
        new AnimationStateMachine { States = [new() { Name = "Idle", Clip = "idle" }, new() { Name = "Walk", Clip = "walk" }] }.Save(file);
        var reference = DataAssets.Load<AnimationStateMachine>(file);
        reference.Id.Should().NotBeNull("the machine's save gave its file an id");

        var (_, ecs) = NewWorld();
        var hero = ecs.Spawn();
        ecs.Add(hero, new AnimationMachine(reference));
        var level = SceneFile.Write(ecs);
        level.Should().Contain($"\"File\": \"{reference.Id}\"", "a scene file holds the machine by its file's id");

        // Moved into a folder of its own and renamed, as a person tidying the project does, and
        // read by a program begun again, which does not know where it went.
        var renamed = _folder.File(Path.Combine("machines", "hero-idle.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(renamed)!);
        File.Move(file, renamed);
        DataAssets.Forget(reference.Id!);

        var (loadedWorld, loaded) = NewWorld();
        var machine = loaded.GetReadOnly<AnimationMachine>(SceneFile.Read(loadedWorld, level)[0]);

        machine.File.IsNone.Should().BeFalse(machine.File.Problem);
        machine.File.Path.Should().Be(renamed);
        machine.File.Value.States.Select(state => state.Name).Should().Equal("Idle", "Walk");
        DataAssets.Find<Weapon>(reference.Id!).Problem.Should().Contain(nameof(AnimationStateMachine), "the file holds a machine, which a weapon's reference reads as none");
    }

    [Fact]
    public void A_Reference_Whose_File_Is_Missing_Reads_As_None_With_A_Warning_Naming_Its_Id_And_Keeps_The_Id()
    {
        var file = _folder.File("axe.json");
        var axe = DataAssets.Save(file, new Weapon { Damage = 30, Title = "Axe" });
        var (_, ecs) = NewWorld();
        var guard = ecs.Spawn();
        ecs.Add(guard, new Armed { Weapon = axe });
        var level = SceneFile.Write(ecs);
        File.Delete(file);
        DataAssets.Forget(axe.Id!);

        var before = ConsoleLog.Written;
        var (loadedWorld, loaded) = NewWorld();
        var weapon = loaded.GetReadOnly<Armed>(SceneFile.Read(loadedWorld, level)[0]).Weapon;

        weapon.IsNone.Should().BeTrue();
        weapon.Id.Should().Be(axe.Id);
        weapon.Problem.Should().Contain(axe.Id);
        weapon.Path.Should().BeNull();
        weapon.Value.Should().Be(Weapon.Default, "a reference that reads none gives the type's default");
        Warned(before, axe.Id!).Should().BeTrue("the warning names the id");
        SceneFile.Write(loaded).Should().Contain($"\"Weapon\": \"{axe.Id}\"", "a scene saved again still names the file");
    }

    [Fact]
    public void A_Scene_With_No_Reference_Asks_Nothing_Of_The_Data_Files_And_Costs_No_More_Once_Some_Are_Known()
    {
        // Transforms, and a machine named by its path alone, whose reference by id is null.
        const string level = """
            { "format": "3dengine-scene", "version": 1, "entities": [
              { "id": "a", "components": { "Transform": { "Position": [1, 0, 0] } } },
              { "id": "b", "components": { "Transform": { "Position": [2, 0, 0] }, "AnimationMachine": { "Path": "robot.machine.json", "File": null, "State": "" } } }
            ] }
            """;
        var (world, ecs) = NewWorld();
        long Read()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var entity in SceneFile.Read(world, level)) ecs.Despawn(entity);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            for (int i = 0; i <= EcsWorld.RemovalFrames; i++) ecs.BeginFrame();
            return allocated;
        }

        // The least of several reads, as the scene load tests take it, since a test beside this one
        // may register a codec, which the registry's next lookup builds its index again for.
        long Least() => Enumerable.Range(0, 5).Min(_ => Read());

        // The log's sinks the process holds, which the read's debug line reaches on this thread,
        // are the file the process's first app opens, kept from then on, and a test's listener. One
        // begun by a test beside this one between the two counts adds its cost to the second alone,
        // 160 bytes on the build run of ec87f64b, so the pair is taken again until the sinks stood
        // still through both.
        static (bool File, bool Heard) Sinks() => (FileLoggerProvider.Instance is not null, Logger.Heard is not null);

        Read();
        DataRef<Weapon> shield = default;
        var (unknown, known) = (0L, 0L);
        for (int pair = 0; pair < 3; pair++)
        {
            var sinks = Sinks();
            var asked = DataAssets.Asked;
            unknown = Least();
            DataAssets.Asked.Should().Be(asked, "a scene with no reference asks nothing of the data files");

            if (pair == 0) shield = DataAssets.Save(_folder.File("shield.json"), new Weapon { Damage = 0, Title = "Shield" });
            DataAssets.Forget(shield.Id!);
            DataAssets.Find<Weapon>(shield.Id!).IsNone.Should().BeFalse();
            asked = DataAssets.Asked;
            known = Least();
            DataAssets.Asked.Should().Be(asked, "nor once the table holds files");
            if (Sinks() == sinks) break;
            // Forgotten, so the next pair starts as this one did, from a table without it.
            DataAssets.Forget(shield.Id!);
        }
        known.Should().Be(unknown, "a scene with no reference costs the same whatever the table holds");
    }
}
