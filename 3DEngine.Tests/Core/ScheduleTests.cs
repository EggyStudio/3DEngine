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

    [Fact]
    public void A_Batch_Of_Systems_That_Took_Next_To_Nothing_Runs_On_The_Calling_Thread_And_A_Heavy_One_Is_Shared()
    {
        var world = new World();
        var parallel = new ConcurrentDictionary<string, bool>();
        var threads = new ConcurrentDictionary<string, int>();
        // Each says the thread it ran on, and whether a batch was handed to other threads while it
        // ran, which its own batch does whichever thread the pool gives each system.
        SystemDescriptor Recording(string name) => new SystemDescriptor(_ =>
        {
            parallel[name] = Schedule.RunningInParallel;
            threads[name] = Environment.CurrentManagedThreadId;
        }, name).Read<First>();
        var light = new[] { "a", "b", "c", "d" }.Select(Recording).ToArray();
        var heavy = new[] { "w", "x", "y", "z" }.Select(Recording).ToArray();
        var lightSchedule = new Schedule();
        var heavySchedule = new Schedule();
        foreach (var desc in light) lightSchedule.AddSystem(Stage.Update, desc);
        foreach (var desc in heavy) heavySchedule.AddSystem(Stage.Update, desc);

        // What each took when it last ran, set rather than measured, so the test does not hang on
        // how busy the machine is.
        foreach (var desc in light) desc.LastMilliseconds = 0.01;
        foreach (var desc in heavy) desc.LastMilliseconds = 5;
        lightSchedule.RunStage(Stage.Update, world);
        heavySchedule.RunStage(Stage.Update, world);

        light.Select(d => threads[d.Name]).Should().AllBeEquivalentTo(Environment.CurrentManagedThreadId, "four systems of microseconds are not worth the thread pool");
        heavy.Select(d => parallel[d.Name]).Should().AllBeEquivalentTo(true, "systems of milliseconds each are shared among threads");
    }

    // Hears what one category logs, from any thread, for as long as it is added.
    private sealed class Heard : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message, Exception? Exception)> Lines { get; } = new();

        public void Log(LogLevel level, string category, string message, Exception? exception = null) =>
            Lines.Enqueue((level, message, exception));
    }

    [Fact]
    [ExpectsError("Engine.Schedule", "System 'Throws.")]
    public void A_System_That_Throws_In_Every_Frame_Is_Logged_Whole_Once_And_Then_Counted()
    {
        // A name of its own, since the schedule's log is the process's and other tests' apps log there too.
        var name = $"Throws.{Guid.NewGuid():N}";
        var heard = new Heard();
        var schedule = Log.Factory.CreateLogger("Engine.Schedule").UseProvider(heard);
        try
        {
            var app = new App();
            int frames = 0;
            app.AddSystem(Stage.Update, new SystemDescriptor(_ =>
            {
                frames++;
                if (frames % 250 == 0) throw new ArgumentException("another kind, now and then");
                throw new InvalidOperationException($"frame {frames} failed");
            }, name));
            for (int i = 0; i < 1000; i++) app.Schedule.RunStage(Stage.Update, app.World);
            app.Shutdown();
        }
        finally
        {
            schedule.RemoveProvider(heard);
        }

        var lines = heard.Lines.Where(line => line.Message.Contains(name, StringComparison.Ordinal)).ToList();
        lines.Where(line => line.Exception is not null).Select(line => line.Exception!.GetType())
            .Should().Equal([typeof(InvalidOperationException), typeof(ArgumentException)], "each type's first is logged whole, once");
        lines.Select(line => line.Message[(line.Message.IndexOf(name, StringComparison.Ordinal) + name.Length)..]).Should().Equal(
            "' threw in stage Update",
            "' has thrown InvalidOperationException in stage Update 10 times, the last: frame 10 failed",
            "' has thrown InvalidOperationException in stage Update 100 times, the last: frame 100 failed",
            "' threw in stage Update",
            "' threw InvalidOperationException in stage Update 996 times in all",
            "' threw ArgumentException in stage Update 4 times in all");
        lines.Should().OnlyContain(line => line.Level == LogLevel.Error);
    }
}
