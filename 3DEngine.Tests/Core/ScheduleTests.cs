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
    public void A_Seed_Shuffles_Systems_Whose_Declarations_Do_Not_Conflict_And_Keeps_Those_That_Do_In_The_Order_Added()
    {
        static string Shuffled(int seed)
        {
            var schedule = new Schedule();
            // B reads what A writes, so B stays after A. M runs on the main thread and N declares
            // nothing, so every system stays on its side of each. C, D, E, F and G read what nothing
            // here writes, so they may run in any order between those.
            schedule.AddSystem(Stage.Update, Adds("A").Write<First>());
            schedule.AddSystem(Stage.Update, Adds("C").Read<Second>());
            schedule.AddSystem(Stage.Update, Adds("B").Read<First>());
            schedule.AddSystem(Stage.Update, Adds("D").Read<Second>());
            schedule.AddSystem(Stage.Update, Adds("M").MainThreadOnly());
            schedule.AddSystem(Stage.Update, Adds("E").Read<Third>());
            schedule.AddSystem(Stage.Update, Adds("F").Read<Second>());
            schedule.AddSystem(Stage.Update, Adds("N"));
            schedule.AddSystem(Stage.Update, Adds("G").Read<Third>());
            schedule.Shuffle(seed);
            return string.Concat(schedule.SystemNames(Stage.Update));
        }

        var orders = new HashSet<string>();
        for (var seed = 0; seed < 40; seed++)
        {
            var order = Shuffled(seed);
            orders.Add(order);
            order.IndexOf('B').Should().BeGreaterThan(order.IndexOf('A'), $"B reads what A writes, and seed {seed} gave {order}");
            order[..4].Order().Should().Equal("ABCD", $"what was added before M runs before it, and seed {seed} gave {order}");
            order[4..].Should().Match("M??NG", $"what was added between M and N stays there, and seed {seed} gave {order}");
            Shuffled(seed).Should().Be(order, "a seed gives the same order each time");
        }
        orders.Should().HaveCountGreaterThan(5, "systems whose declarations do not conflict move with the seed");
    }

    [Fact]
    public void An_Asked_Order_Holds_In_The_Batches_And_Under_Every_Seed_s_Shuffle()
    {
        // None of these conflict, so only the order asked keeps any of them in place. Score runs
        // after Move, Input before it, and X, Y and Z, added backward, each after the one before.
        static Schedule Made()
        {
            var schedule = new Schedule();
            schedule.AddSystem(Stage.Update, Adds("Score").Read<First>().After("Move"));
            schedule.AddSystem(Stage.Update, Adds("Draw").Read<Second>());
            schedule.AddSystem(Stage.Update, Adds("Move").Read<Third>());
            schedule.AddSystem(Stage.Update, Adds("Input").Read<Second>().Before("Move"));
            schedule.AddSystem(Stage.Update, Adds("Z").Read<First>().After("Y"));
            schedule.AddSystem(Stage.Update, Adds("Y").Read<First>().After("X"));
            schedule.AddSystem(Stage.Update, Adds("X").Read<First>());
            schedule.Order();
            return schedule;
        }

        static void Holds(IList<string> order, string because)
        {
            order.IndexOf("Input").Should().BeLessThan(order.IndexOf("Move"), because);
            order.IndexOf("Move").Should().BeLessThan(order.IndexOf("Score"), because);
            order.IndexOf("X").Should().BeLessThan(order.IndexOf("Y"), because);
            order.IndexOf("Y").Should().BeLessThan(order.IndexOf("Z"), because);
        }

        var world = new World();
        world.InsertResource(new ConcurrentQueue<string>());
        var ordered = Made();
        Holds([.. ordered.SystemNames(Stage.Update)], "the stage is sorted as the app starts");
        Holds(Run(ordered, world), "each runs in a batch after those it asked to follow");

        for (var seed = 0; seed < 40; seed++)
        {
            var schedule = Made();
            schedule.Shuffle(seed);
            Holds([.. schedule.SystemNames(Stage.Update)], $"seed {seed} gave {string.Join(", ", schedule.SystemNames(Stage.Update))}");
        }
    }

    [Fact]
    public void A_Chain_Added_To_An_App_Runs_In_Its_Order_From_The_App_s_Start()
    {
        var app = new App();
        app.World.InsertResource(new ConcurrentQueue<string>());
        app.AddSystem(Stage.Update, Adds("Late").Read<Second>().After("Third"));
        app.Chain(Stage.Update, Adds("First").Read<First>(), Adds("Second").Read<Second>(), Adds("Third").Read<Third>());

        app.Startup();
        app.Schedule.RunStage(Stage.Update, app.World);

        app.World.Resource<ConcurrentQueue<string>>().Should().Equal("First", "Second", "Third", "Late");
        app.Dispose();
    }

    [Fact]
    public void An_Order_That_Goes_Round_Or_Names_No_System_Of_The_Stage_Is_Refused_By_Name()
    {
        var round = new Schedule();
        round.AddSystem(Stage.Update, Adds("A").After("C"));
        round.AddSystem(Stage.Update, Adds("B").After("A"));
        round.AddSystem(Stage.Update, Adds("C").After("B"));
        round.Invoking(s => s.Order()).Should().Throw<InvalidOperationException>()
            .WithMessage("The order asked in stage Update goes round: 'A' after 'C' after 'B' after 'A'.");

        var unknown = new Schedule();
        unknown.AddSystem(Stage.Update, Adds("A").After("Nobody"));
        unknown.Invoking(s => s.Order()).Should().Throw<InvalidOperationException>()
            .WithMessage("System 'A' in stage Update asks to run next to 'Nobody', and no other system of the stage is called that.");
    }

    [Fact]
    public void A_Stage_Where_No_System_Asks_For_An_Order_Is_Left_As_It_Was()
    {
        var schedule = new Schedule();
        foreach (var name in new[] { "A", "B", "C", "D" }) schedule.AddSystem(Stage.Update, Adds(name).Read<First>());
        var before = schedule.SystemNames(Stage.Update);

        schedule.Order();

        schedule.SystemNames(Stage.Update).Should().Equal(before);
        schedule.Systems().Should().OnlyContain(system => system.Predecessors == null, "no system carries an order the batches or a shuffle read");
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

    // Hears what one category logs, from any thread, for as long as it is added. An exception is
    // kept by its type's name, since the category is the process's, and the object of one a
    // script's system threw in another test's app would keep the script's load context alive while
    // this test ran, ScriptGenerationTests' generation among them, which runs in another collection
    // beside this one. It is the one holder of a system's exception that kept the object when that
    // test found its generation alive on 5a2d7df3's macOS run (REVIEW.md, Verdict 70).
    private sealed class Heard : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Message, string? Exception)> Lines { get; } = new();

        public void Log(LogLevel level, string category, string message, Exception? exception = null) =>
            Lines.Enqueue((level, message, exception?.GetType().Name));
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
        lines.Where(line => line.Exception is not null).Select(line => line.Exception)
            .Should().Equal([nameof(InvalidOperationException), nameof(ArgumentException)], "each type's first is logged whole, once");
        lines.Select(line => line.Message[(line.Message.IndexOf(name, StringComparison.Ordinal) + name.Length)..]).Should().Equal(
            "' from Engine.Tests threw in stage Update",
            "' has thrown InvalidOperationException in stage Update 10 times. The last says frame 10 failed",
            "' has thrown InvalidOperationException in stage Update 100 times. The last says frame 100 failed",
            "' from Engine.Tests threw in stage Update",
            "' threw InvalidOperationException in stage Update 996 times in all",
            "' threw ArgumentException in stage Update 4 times in all");
        lines.Should().OnlyContain(line => line.Level == LogLevel.Error);
    }
}
