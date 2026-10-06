using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class EntityHandleOperationsTests
{
    private struct Health { public int Value; }

    [Fact]
    public void A_Handle_Reaches_Its_Entity_While_It_Lives()
    {
        var ecs = new EcsWorld();
        var handle = ecs.Handle(ecs.Spawn());

        ecs.Add(handle, new Health { Value = 3 });
        ecs.GetRef<Health>(handle).Value++;
        ecs.Has<Health>(handle).Should().BeTrue();
        ecs.TryGet<Health>(handle, out var health).Should().BeTrue();
        health.Value.Should().Be(4);
        ecs.GetReadOnly<Health>(handle).Value.Should().Be(4);
        ecs.Remove<Health>(handle).Should().BeTrue();
    }

    [Fact]
    public void A_Stale_Handle_Is_Refused_Rather_Than_Reaching_The_Entity_That_Reused_Its_Id()
    {
        var ecs = new EcsWorld();
        var first = ecs.Spawn();
        var stale = ecs.Handle(first);
        ecs.Despawn(first);
        var second = ecs.Spawn();
        ecs.Add(second, new Health { Value = 9 });
        second.Should().Be(first, "the id was reused, which a bare int cannot tell");

        ecs.Has<Health>(stale).Should().BeFalse();
        ecs.TryGet<Health>(stale, out _).Should().BeFalse();
        ecs.Remove<Health>(stale).Should().BeFalse();
        ecs.Invoking(e => e.Add(stale, new Health())).Should().Throw<InvalidOperationException>().WithMessage("*gone*");
        ecs.Invoking(e => e.Update(stale, new Health())).Should().Throw<InvalidOperationException>();
        ecs.GetReadOnly<Health>(second).Value.Should().Be(9, "the entity that took the id is untouched");
    }
}
