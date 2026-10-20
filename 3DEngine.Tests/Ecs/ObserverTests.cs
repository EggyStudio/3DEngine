using FluentAssertions;

namespace Engine.Tests.Ecs;

/// <summary>Observers of a component added and removed, and of an event of the game's own.</summary>
[Trait("Category", "Unit")]
public class ObserverTests
{
    private struct Health { public int Value; }
    private struct Armor { public int Value; }

    private readonly record struct Hit(Entity Entity, int Damage) : IPropagatingEvent;
    private readonly record struct Shout(Entity Entity) : IEntityEvent;
    private readonly record struct Dawn(int Day);

    [Fact]
    public void An_Observer_Is_Told_Once_Of_A_Component_Added_And_Once_Of_It_Removed()
    {
        using var ecs = new EcsWorld();
        var told = new List<string>();
        ecs.Observe<Add<Health>>(on => told.Add($"add {on.Event.Entity.Index} {on.Event.Value.Value} has {on.Ecs.Has<Health>(on.Event.Entity.Index)}"));
        ecs.Observe<Remove<Health>>(on => told.Add($"remove {on.Event.Entity.Index} {on.Event.Value.Value} has {on.Ecs.Has<Health>(on.Event.Entity.Index)}"));

        var e = ecs.Spawn();
        ecs.Add(e, new Health { Value = 10 });
        ecs.Add(e, new Health { Value = 20 });
        ecs.Update(e, new Health { Value = 30 });
        ecs.Remove<Health>(e);
        ecs.Remove<Health>(e);
        ecs.Update(e, new Health { Value = 40 });

        told.Should().Equal($"add {e} 10 has True", $"remove {e} 30 has False", $"add {e} 40 has True");
    }

    [Fact]
    public void An_Observer_Of_One_Entity_Hears_Its_Changes_Alone_And_Stops_When_Disposed()
    {
        using var ecs = new EcsWorld();
        var a = ecs.Spawn();
        var b = ecs.Spawn();
        var told = new List<string>();
        var watching = ecs.Observe<Add<Armor>>(ecs.Handle(a), on => told.Add($"armor {on.Entity.Index}"));

        ecs.Add(b, new Armor());
        ecs.Add(a, new Armor());
        ecs.Remove<Armor>(a);
        watching.Dispose();
        ecs.Add(a, new Armor());

        told.Should().Equal($"armor {a}");
    }

    [Fact]
    public void A_Despawn_Tells_The_Observers_Of_Every_Component_It_Took_And_Lets_The_Entity_s_Own_Go()
    {
        using var ecs = new EcsWorld();
        var e = ecs.Spawn();
        ecs.Add(e, new Health { Value = 5 });
        ecs.Add(e, new Armor { Value = 2 });
        var handle = ecs.Handle(e);
        var told = new List<string>();
        ecs.Observe<Remove<Health>>(on => told.Add($"health {on.Event.Value.Value} alive {ecs.TryResolve(on.Event.Entity, out _)}"));
        ecs.Observe<Remove<Armor>>(handle, on => told.Add($"armor {on.Event.Value.Value}"));

        ecs.Despawn(e);

        told.Should().BeEquivalentTo("health 5 alive False", "armor 2");
        ecs.GetStorePublic<Armor>().RemoveObservers.Should().Be(0, "the despawned entity's own observer is let go");
        ecs.Despawn(e);
        told.Should().HaveCount(2, "a second despawn takes nothing");
    }

    [Fact]
    public void A_Change_Queued_On_The_Commands_Tells_The_Observers_As_They_Are_Applied()
    {
        using var ecs = new EcsWorld();
        var commands = new EcsCommands();
        var e = ecs.Spawn();
        var told = 0;
        ecs.Observe<Add<Health>>(_ => told++);

        commands.Add(e, new Health());
        told.Should().Be(0);
        commands.Apply(ecs);
        told.Should().Be(1);
    }

    [Fact]
    public void A_Game_s_Event_Reaches_Every_Observer_Then_The_Entity_s_Then_Goes_Up_Until_Stopped()
    {
        using var ecs = new EcsWorld();
        var body = ecs.Spawn();
        var arm = ecs.Spawn();
        var hand = ecs.Spawn();
        ecs.SetParent(arm, body);
        ecs.SetParent(hand, arm);
        var told = new List<string>();
        ecs.Observe<Hit>(on => told.Add($"every at {Name(on.Entity)} {on.Event.Damage}"));
        ecs.Observe<Hit>(ecs.Handle(hand), on => told.Add($"hand {on.Event.Damage}"));
        // The arm's armor takes half and stops a hit of less than 4, the arm's second observer still told.
        ecs.Observe<Hit>(ecs.Handle(arm), on =>
        {
            on.Event = on.Event with { Damage = on.Event.Damage / 2 };
            if (on.Event.Damage < 4) on.Propagate(false);
        });
        ecs.Observe<Hit>(ecs.Handle(arm), on => told.Add($"arm {on.Event.Damage}"));
        ecs.Observe<Hit>(ecs.Handle(body), on => told.Add($"body {on.Event.Damage}"));

        ecs.Trigger(new Hit(ecs.Handle(hand), 10));
        told.Should().Equal("every at hand 10", "hand 10", "every at arm 10", "arm 5", "every at body 5", "body 5");

        told.Clear();
        ecs.Trigger(new Hit(ecs.Handle(hand), 6));
        told.Should().Equal("every at hand 6", "hand 6", "every at arm 6", "arm 3");

        string Name(Entity entity) => entity.Index == hand ? "hand" : entity.Index == arm ? "arm" : "body";
    }

    [Fact]
    public void An_Event_That_Does_Not_Propagate_Stays_At_Its_Entity_And_One_Of_No_Entity_Reaches_Every_Observer()
    {
        using var ecs = new EcsWorld();
        var parent = ecs.Spawn();
        var child = ecs.Spawn();
        ecs.SetParent(child, parent);
        var told = new List<string>();
        ecs.Observe<Shout>(ecs.Handle(parent), _ => told.Add("parent"));
        ecs.Observe<Shout>(ecs.Handle(child), _ => told.Add("child"));
        ecs.Observe<Dawn>(on => told.Add($"day {on.Event.Day} at {on.Entity}"));

        ecs.Trigger(new Shout(ecs.Handle(child)));
        ecs.Trigger(new Dawn(3));

        told.Should().Equal("child", "day 3 at Entity.None");
    }

    [Fact]
    [ExpectsError("Engine.ECS", "An observer of Add<Health> at")]
    public void An_Observer_That_Throws_Is_Logged_And_The_Change_And_The_Observers_After_It_Stand()
    {
        using var ecs = new EcsWorld();
        var e = ecs.Spawn();
        var told = 0;
        ecs.Observe<Add<Health>>(_ => throw new InvalidOperationException("an observer's fault"));
        ecs.Observe<Add<Health>>(_ => told++);

        ecs.Add(e, new Health { Value = 1 });

        told.Should().Be(1);
        ecs.Has<Health>(e).Should().BeTrue();
    }

    [Fact]
    public void With_No_Observer_Of_A_Component_Its_Adds_Removals_And_Despawns_Allocate_Nothing()
    {
        using var ecs = new EcsWorld();
        var ids = new int[1000];
        for (int i = 0; i < ids.Length; i++) ids[i] = ecs.Spawn();
        ecs.Add(ids[0], new Health());
        ecs.Add(ids[0], new Armor());
        ecs.Remove<Health>(ids[0]);
        ecs.Remove<Armor>(ids[0]);

        long Allocated(Action act)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            act();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        void Churn()
        {
            foreach (var id in ids) ecs.Add(id, new Health { Value = id });
            foreach (var id in ids) ecs.Remove<Health>(id);
            foreach (var id in ids) ecs.Add(id, new Armor());
            for (int i = 1; i < ids.Length; i++) ecs.Despawn(ids[i]);
            for (int i = 1; i < ids.Length; i++) ids[i] = ecs.Spawn();
        }

        // Removals are logged for a second of frames, so a pass over the frames after the first
        // empties the log the measured pass then fills to the size it had, allocating nothing.
        void Settle()
        {
            Churn();
            for (int i = 0; i <= EcsWorld.RemovalFrames; i++) ecs.BeginFrame();
        }

        Settle();
        Allocated(Churn).Should().Be(0, "a world with no observer tells none");

        // An observer of another component leaves these as they were.
        using var other = ecs.Observe<Remove<Hit>>(_ => { });
        Settle();
        Allocated(Churn).Should().Be(0, "only the component observed pays for its observers");
    }

    [Fact]
    public void A_World_Not_Disposed_With_Observers_Lets_Its_Stores_Be_Collected()
    {
        var store = StoreOfAWorldWithObservers();
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        store.IsAlive.Should().BeFalse("the stores count their observers and hold no reference to them");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference StoreOfAWorldWithObservers()
    {
        var ecs = new EcsWorld();
        ecs.Observe<Add<Health>>(on => on.Ecs.Add(on.Event.Entity.Index, new Armor()));
        ecs.Add(ecs.Spawn(), new Health());
        return new WeakReference(ecs.GetStorePublic<Health>());
    }
}
