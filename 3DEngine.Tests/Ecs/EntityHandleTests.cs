using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class EntityHandleTests
{
    private struct Marker { public int Value; }

    [Fact]
    public void A_Handle_Kept_Past_A_Despawn_Does_Not_Name_The_Entity_That_Reuses_The_Id()
    {
        var ecs = new EcsWorld();
        var first = ecs.Spawn();
        var handle = ecs.Handle(first);

        ecs.Despawn(first);
        var second = ecs.Spawn();

        second.Should().Be(first, "the id is reused");
        ecs.IsAlive(handle).Should().BeFalse();
        ecs.TryResolve(handle, out _).Should().BeFalse();
        ecs.TryResolve(ecs.Handle(second), out var id).Should().BeTrue();
        id.Should().Be(second);
    }

    [Fact]
    public void Despawning_Twice_Frees_The_Id_Once()
    {
        var ecs = new EcsWorld();
        var a = ecs.Spawn();

        ecs.Despawn(a);
        ecs.Despawn(a);

        var b = ecs.Spawn();
        var c = ecs.Spawn();
        b.Should().NotBe(c, "two spawns must not share an id");
        ecs.EntityCount.Should().Be(2);
    }

    [Fact]
    public void Despawning_By_Handle_Leaves_A_Newer_Entity_Alone()
    {
        var ecs = new EcsWorld();
        var a = ecs.Spawn();
        var stale = ecs.Handle(a);
        ecs.Despawn(a);
        var b = ecs.Spawn();
        ecs.Add(b, new Marker { Value = 1 });

        ecs.Despawn(stale).Should().BeFalse();

        ecs.IsAlive(b).Should().BeTrue();
        ecs.Has<Marker>(b).Should().BeTrue();
    }

    [Fact]
    public void A_Dead_Id_Has_No_Handle()
    {
        var ecs = new EcsWorld();

        ecs.Handle(42).Should().Be(Entity.None);
        Entity.None.IsNone.Should().BeTrue();
    }
}
