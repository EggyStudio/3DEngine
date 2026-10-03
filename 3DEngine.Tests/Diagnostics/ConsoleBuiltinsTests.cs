using FluentAssertions;

namespace Engine.Tests.Diagnostics;

[Collection("Console")]
[Trait("Category", "Unit")]
public class ConsoleBuiltinsTests
{
    private struct Health { public int Value; }
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
    public void Entities_Are_Counted_Listed_And_Read()
    {
        var (world, app) = Setup();

        Run(world, app, "entity.count").Should().Be("2");
        Run(world, app, "component.list").Should().Contain("Health 2").And.Contain("Tag 1");
        Run(world, app, "entity.list 10").Should().Contain("1: Health, Tag").And.Contain("2: Health");
        Run(world, app, "entity.get 1").Should().Contain("Health { Value=7 }");
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
}
