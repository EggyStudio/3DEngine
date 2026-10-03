using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

[Trait("Category", "Unit")]
public class EcsPluginTests
{
    [Fact]
    public void EcsPlugin_Inserts_EcsWorld_And_EcsCommands_Resources()
    {
        using var app = new App();
        app.AddPlugin(new EcsPlugin());

        app.World.ContainsResource<EcsWorld>().Should().BeTrue();
        app.World.ContainsResource<EcsCommands>().Should().BeTrue();
    }

    [Fact]
    public void EcsPlugin_Registers_PostUpdate_Flush_System()
    {
        using var app = new App();
        int beforeCount = app.Schedule.SystemCount(Stage.PostUpdate);

        app.AddPlugin(new EcsPlugin());

        app.Schedule.SystemCount(Stage.PostUpdate).Should().Be(beforeCount + 1);
    }

    [Fact]
    public void PostUpdate_System_Flushes_Commands()
    {
        using var app = new App();
        app.AddPlugin(new EcsPlugin());

        var ecs = app.World.Resource<EcsWorld>();
        var cmd = app.World.Resource<EcsCommands>();

        // Queue a spawn command
        cmd.Spawn((id, world) => world.Add(id, new TestComp { A = 42 }));

        // Commands are not yet applied
        ecs.Query<TestComp>().ToArray().Should().BeEmpty();

        // Run PostUpdate to flush
        app.Schedule.RunStage(Stage.PostUpdate, app.World);

        // Now the entity should exist
        ecs.Query<TestComp>().ToArray().Should().HaveCount(1);
        ecs.Query<TestComp>().First().Component.A.Should().Be(42);
    }
}
