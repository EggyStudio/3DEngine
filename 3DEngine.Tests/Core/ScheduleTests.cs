using System.Collections.Concurrent;
using FluentAssertions;

namespace Engine.Tests.Core;

/// <summary>Systems of a parallel stage, batched by what they read and write.</summary>
[Trait("Category", "Unit")]
public class ScheduleTests
{
    private sealed class First;
    private sealed class Second;
    private sealed class Third;

    // Runs a stage's systems, each named by the letter it adds to the order, until the order has
    // settled, since systems of one batch may run in either order.
    private static List<string> Run(Schedule schedule, World world)
    {
        schedule.RunStage(Stage.Update, world);
        return [.. world.Resource<ConcurrentQueue<string>>()];
    }

    private static SystemDescriptor Adds(string name) =>
        new(world => world.Resource<ConcurrentQueue<string>>().Enqueue(name), name);

    [Fact]
    public void A_System_Reading_What_An_Earlier_System_Writes_Runs_After_It_Though_A_Batch_Before_Has_Room()
    {
        var world = new World();
        world.InsertResource(new ConcurrentQueue<string>());
        var schedule = new Schedule();
        // A writes First, B writes First and Second, so B waits for A. C reads Second, which B
        // writes, and was added after B, so it runs after B, though nothing in A's batch is in its way.
        schedule.AddSystem(Stage.Update, Adds("A").Write<First>().Write<ConcurrentQueue<string>>());
        schedule.AddSystem(Stage.Update, Adds("B").Write<First>().Write<Second>().Write<ConcurrentQueue<string>>());
        schedule.AddSystem(Stage.Update, Adds("C").Read<Second>().Read<Third>());

        var order = Run(schedule, world);

        order.IndexOf("C").Should().BeGreaterThan(order.IndexOf("B"), $"C reads what B writes, and ran in the order {string.Join(", ", order)}");
    }

    [Fact]
    public void A_System_Added_After_One_On_The_Main_Thread_Runs_After_It()
    {
        var world = new World();
        world.InsertResource(new ConcurrentQueue<string>());
        var schedule = new Schedule();
        schedule.AddSystem(Stage.Update, Adds("A").Read<First>());
        schedule.AddSystem(Stage.Update, Adds("M").MainThreadOnly());
        schedule.AddSystem(Stage.Update, Adds("B").Read<Third>());

        var order = Run(schedule, world);

        order.IndexOf("B").Should().BeGreaterThan(order.IndexOf("M"), $"the main thread's system is where B's turn comes after, and they ran {string.Join(", ", order)}");
    }
}
