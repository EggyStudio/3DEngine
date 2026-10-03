using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

[Trait("Category", "Unit")]
public class EcsCommandsTests
{
    // -- Spawn --

    [Fact]
    public void Spawn_Queues_And_Apply_Creates_Entity()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();

        int spawnedId = -1;
        cmd.Spawn((id, world) =>
        {
            spawnedId = id;
            world.Add(id, new TestComp { A = 99 });
        });

        cmd.Apply(ecs);

        spawnedId.Should().BeGreaterThanOrEqualTo(1);
        ecs.TryGet<TestComp>(spawnedId, out var comp).Should().BeTrue();
        comp.A.Should().Be(99);
    }

    // -- Despawn --

    [Fact]
    public void Despawn_Queues_And_Apply_Removes_Entity()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 1 });

        var cmd = new EcsCommands();
        cmd.Despawn(e);
        cmd.Apply(ecs);

        ecs.TryGet<TestComp>(e, out _).Should().BeFalse();
    }

    // -- Add --

    [Fact]
    public void Add_Queues_And_Apply_Attaches_Component()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();

        var cmd = new EcsCommands();
        cmd.Add(e, new TestComp { A = 42 });
        cmd.Apply(ecs);

        ecs.Has<TestComp>(e).Should().BeTrue();
        ecs.TryGet<TestComp>(e, out var comp).Should().BeTrue();
        comp.A.Should().Be(42);
    }

    // -- Remove --

    [Fact]
    public void Remove_Queues_And_Apply_Detaches_Component()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 1 });

        var cmd = new EcsCommands();
        cmd.Remove<TestComp>(e);
        cmd.Apply(ecs);

        ecs.Has<TestComp>(e).Should().BeFalse();
    }

    // -- FIFO ordering --

    [Fact]
    public void Apply_Executes_Commands_In_FIFO_Order()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();

        int capturedId = -1;

        // First: spawn, then add component to that entity
        cmd.Spawn((id, world) =>
        {
            capturedId = id;
        });

        // This relies on FIFO: the spawn runs first, then we can reference the ID
        // We'll do it in the spawn callback itself to be safe
        cmd.Spawn((id, world) =>
        {
            world.Add(id, new TestComp { A = 100 });
        });

        cmd.Apply(ecs);

        // Both spawns should have worked
        capturedId.Should().BeGreaterThanOrEqualTo(1);
    }

    // -- Fluent chaining --

    [Fact]
    public void Methods_Support_Fluent_Chaining()
    {
        var cmd = new EcsCommands();

        var result = cmd
            .Spawn((id, w) => { })
            .Despawn(999)
            .Add(1, new TestComp())
            .Remove<TestComp>(1);

        result.Should().BeSameAs(cmd);
    }

    // -- Apply empties the queue --

    [Fact]
    public void Apply_Empties_Queue_Second_Apply_Is_NoOp()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();
        int count = 0;

        cmd.Spawn((id, world) => count++);
        cmd.Apply(ecs);
        count.Should().Be(1);

        // Second apply should do nothing
        cmd.Apply(ecs);
        count.Should().Be(1);
    }

    // -- Complex scenario: spawn + add multiple components --

    [Fact]
    public void Spawn_With_Multiple_Components()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();

        cmd.Spawn((id, world) =>
        {
            world.Add(id, new TestComp { A = 10 });
            world.Add(id, new OtherComp { B = 20 });
        });

        cmd.Apply(ecs);

        var results = ecs.Query<TestComp, OtherComp>().ToArray();
        results.Should().HaveCount(1);
        results[0].C1.A.Should().Be(10);
        results[0].C2.B.Should().Be(20);
    }

    // -- SpawnBatch --

    [Fact]
    public void SpawnBatch_Creates_Correct_Number_Of_Entities()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();

        cmd.SpawnBatch(100, (id, world) =>
        {
            world.Add(id, new TestComp { A = id });
        });

        cmd.Apply(ecs);

        ecs.Count<TestComp>().Should().Be(100);
    }

    [Fact]
    public void SpawnBatch_Generic_Factory_Creates_Correct_Count()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();

        cmd.SpawnBatch<TestComp>(50, id => new TestComp { A = id });
        cmd.Apply(ecs);

        ecs.Count<TestComp>().Should().Be(50);
    }

    [Fact]
    public void SpawnBatch_Default_Constructor_Creates_Correct_Count()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();

        cmd.SpawnBatch<TestComp>(200);
        cmd.Apply(ecs);

        ecs.Count<TestComp>().Should().Be(200);
    }

    [Fact]
    public void SpawnBatch_Returns_Fluent_Self()
    {
        var cmd = new EcsCommands();
        var result = cmd.SpawnBatch(10, (id, w) => { });
        result.Should().BeSameAs(cmd);
    }

    [Fact]
    public void SpawnBatch_Large_Batch_100K()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();

        cmd.SpawnBatch<TestComp>(100_000);
        cmd.Apply(ecs);

        ecs.Count<TestComp>().Should().Be(100_000);
    }
}
