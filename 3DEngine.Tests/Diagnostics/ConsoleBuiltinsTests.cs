using FluentAssertions;

namespace Engine.Tests.Diagnostics;

[Collection("Console")]
[Trait("Category", "Unit")]
public class ConsoleBuiltinsTests
{
    private struct Health
    {
        public int Value;
        // Written only by entity.set, through the console's field setter, which the compiler cannot see.
#pragma warning disable CS0649
        public System.Numerics.Vector3 Spot;
        public System.Numerics.Vector3[] Path;
#pragma warning restore CS0649
    }
    private struct Tag;

    private static (World World, App App) Setup()
    {
        var app = new App();
        var ecs = new EcsWorld();
        app.World.InsertResource(ecs);
        app.World.InsertResource(new Time());
        var a = ecs.Spawn();
        ecs.Add(a, new Health { Value = 7 });
        ecs.Add(a, new Tag());
        var b = ecs.Spawn();
        ecs.Add(b, new Health { Value = 3 });
        return (app.World, app);
    }

    private static string Run(World world, App app, string line)
    {
        using (ConsoleHost.Lend(world, app)) return ConsoleCommands.Run(line) ?? "";
    }

    [Fact]
    public void The_Generator_Registers_The_Built_In_Commands()
    {
        ConsoleCommands.Find("entity.count").Should().NotBeNull();
        ConsoleCommands.Find("frames.wait")!.Parameters.Should().ContainSingle().Which.Kind.Should().Be("whole");
    }

    [Fact]
    public void Memory_Reads_What_The_Program_Holds_As_Name_And_Number_Pairs()
    {
        var (world, app) = Setup();
        var ecs = world.Resource<EcsWorld>();
        var gone = ecs.Spawn();
        ecs.Despawn(gone);

        var words = Run(world, app, "memory.collect").Split(' ');
        var values = Enumerable.Range(0, words.Length / 2).ToDictionary(i => words[2 * i], i => long.Parse(words[2 * i + 1]));

        values["entities"].Should().Be(2);
        values["entityIds"].Should().Be(4, "three ids were given out, the despawned one free to be given again");
        values["heap"].Should().BePositive();
        values.Should().NotContainKey("buffers", "with no renderer there is no device to read");
    }

    [Fact]
    public void A_Parameter_With_A_Default_May_Be_Left_Off()
    {
        var (world, app) = Setup();
        world.InitResource<Input>();

        ConsoleCommands.Find("input.drag")!.Usage.Should().Be("<button> <dx> <dy> <frames> [rest]");
        Run(world, app, "input.drag Left 10 0").Should().Be("needs 4 arguments: <button> <dx> <dy> <frames> [rest]");
        Run(world, app, "input.drag Left 10 0 3").Should().StartWith("dragged");
        Run(world, app, "input.drag Left 10 0 3 5").Should().StartWith("dragged");
        Run(world, app, "input.drag Left 10 0 3 soon").Should().Be("not a whole: soon");
    }

    [Fact]
    public void Entities_Are_Counted_Listed_And_Read()
    {
        var (world, app) = Setup();

        Run(world, app, "entity.count").Should().Be("2");
        Run(world, app, "component.list").Should().Contain("Health 2").And.Contain("Tag 1");
        Run(world, app, "entity.list 10").Should().Contain("1: Health, Tag").And.Contain("2: Health");
        Run(world, app, "entity.get 1").Should().Contain("Health { Value=7");
    }

    [Fact]
    public void A_Word_That_Does_Not_Parse_Is_Answered_With_A_Sentence()
    {
        var (world, app) = Setup();

        Run(world, app, "entity.get seven").Should().Be("not a whole: seven");
        Run(world, app, "entity.get").Should().StartWith("needs 1 argument");
    }

    [Fact]
    public void The_Schedule_Lists_Systems_By_Stage()
    {
        var (world, app) = Setup();
        app.AddSystem(Stage.Update, new SystemDescriptor(_ => { }, "My.System"));

        Run(world, app, "schedule.list").Should().Contain("Update\n  My.System");
    }

    [Fact]
    public void A_Field_Is_Set_From_Its_Words_And_Marked_Changed()
    {
        var (world, app) = Setup();
        var ecs = world.Resource<EcsWorld>();
        ecs.BeginFrame();

        Run(world, app, "entity.set 1 Health.Value 42").Should().Contain("Value=42");
        Run(world, app, "entity.set 1 health.spot 1,2.5,-3").Should().Contain("Spot=<1, 2.5, -3>");

        ecs.TryGet<Health>(1, out var health).Should().BeTrue();
        health.Value.Should().Be(42);
        health.Spot.Should().Be(new System.Numerics.Vector3(1, 2.5f, -3));
        ecs.Changed<Health>(1).Should().BeTrue();
    }

    [Fact]
    public void An_Array_Field_Is_Set_From_Its_Items_Split_By_Semicolons()
    {
        var (world, app) = Setup();
        var ecs = world.Resource<EcsWorld>();

        Run(world, app, "entity.set 1 Health.Path 0,1,0;-1,-1,0;1,-1,0").Should().NotContain("not a");
        ecs.TryGet<Health>(1, out var health).Should().BeTrue();
        health.Path.Should().Equal(new System.Numerics.Vector3(0, 1, 0), new System.Numerics.Vector3(-1, -1, 0), new System.Numerics.Vector3(1, -1, 0));

        Run(world, app, "entity.set 1 Health.Path 0,1,0;oops").Should().Contain("not a Vector3[]");
    }

    [Fact]
    public void A_Field_That_Does_Not_Exist_Or_Parse_Is_Refused()
    {
        var (world, app) = Setup();

        Run(world, app, "entity.set 1 Health.Missing 1").Should().Contain("no field");
        Run(world, app, "entity.set 1 Health.Value lots").Should().Contain("not a Int32");
        Run(world, app, "entity.set 1 Armor.Value 1").Should().Contain("no Armor");
    }

    [Fact]
    public void Entities_Are_Spawned_Given_Components_With_Sensible_Defaults_And_Despawned()
    {
        var (world, app) = Setup();
        var ecs = world.Resource<EcsWorld>();

        var id = int.Parse(Run(world, app, "entity.spawn crate"));
        ecs.FindByName("crate").Should().Be(id);

        Run(world, app, $"entity.add {id} Transform").Should().Contain("Scale=<1, 1, 1>");
        Run(world, app, $"entity.add {id} material").Should().StartWith("Material");
        ecs.TryGet<Material>(id, out var material).Should().BeTrue();
        material.Albedo.Should().Be(System.Numerics.Vector4.One);
        Run(world, app, $"entity.add {id} Camera");
        ecs.TryGet<Camera>(id, out var camera).Should().BeTrue();
        camera.FovY.Should().BeApproximately(float.DegreesToRadians(60), 1e-5f, "the optional constructor's defaults are used");

        Run(world, app, $"entity.add {id} Transform").Should().Contain("already has a Transform");
        Run(world, app, $"entity.add {id} NoSuchThing").Should().Contain("No component type");

        Run(world, app, $"entity.despawn {id}").Should().Be($"despawned {id}");
        ecs.IsAlive(ecs.Handle(id)).Should().BeFalse();
        Run(world, app, $"entity.despawn {id}").Should().Be($"no entity {id}");
    }
}
