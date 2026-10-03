using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Engine.Tests.Ecs;

/// <summary>A world's component stores are not kept alive by the static cache its lookups use.</summary>
[Trait("Category", "Unit")]
public class EcsWorldReleaseTests
{
    private struct Payload { public int Value; }

    // Made in a method of its own, so nothing on this frame's stack keeps the world reachable.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference StoreOfAWorld(bool dispose)
    {
        var ecs = new EcsWorld();
        ecs.Add(ecs.Spawn(), new Payload { Value = 1 });
        var store = new WeakReference(ecs.GetStorePublic<Payload>());
        if (dispose) ecs.Dispose();
        return store;
    }

    private static void Collect()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [Fact]
    public void A_Disposed_World_Lets_Its_Stores_Be_Collected()
    {
        var store = StoreOfAWorld(dispose: true);
        Collect();
        store.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void A_Dropped_World_Lets_Its_Stores_Be_Collected_Without_Being_Disposed()
    {
        var store = StoreOfAWorld(dispose: false);
        Collect();
        store.IsAlive.Should().BeFalse("the world's finalizer releases its slots in the cache");
    }

    [Fact]
    public void Another_World_Keeps_Its_Store_When_One_Is_Disposed()
    {
        var kept = new EcsWorld();
        var entity = kept.Spawn();
        kept.Add(entity, new Payload { Value = 7 });
        var gone = new EcsWorld();
        gone.Add(gone.Spawn(), new Payload { Value = 1 });

        gone.Dispose();

        kept.TryGet<Payload>(entity, out var payload).Should().BeTrue();
        payload.Value.Should().Be(7);
    }

    [Fact]
    public void A_Disposed_World_Refuses_To_Make_Stores_Again()
    {
        var ecs = new EcsWorld();
        var entity = ecs.Spawn();
        ecs.Dispose();

        var add = () => ecs.Add(entity, new Payload { Value = 1 });

        add.Should().Throw<ObjectDisposedException>();
    }
}
