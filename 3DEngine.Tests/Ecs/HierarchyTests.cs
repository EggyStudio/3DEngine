using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class HierarchyTests
{
    [Fact]
    public void An_Entity_Is_Found_By_Its_Name()
    {
        var ecs = new EcsWorld();
        var a = ecs.Spawn();
        ecs.SetName(a, "Player");
        ecs.SetName(a, "Hero");

        ecs.NameOf(a).Should().Be("Hero");
        ecs.FindByName("Hero").Should().Be(a);
        ecs.FindByName("Player").Should().Be(0);
    }

    [Fact]
    public void Children_Are_Listed_And_Despawned_With_Their_Parent()
    {
        var ecs = new EcsWorld();
        var root = ecs.Spawn();
        var child = ecs.Spawn();
        var grandchild = ecs.Spawn();
        ecs.SetParent(child, root);
        ecs.SetParent(grandchild, child);

        ecs.ChildrenOf(root).Should().Equal(child);
        ecs.ParentOf(grandchild).Should().Be(child);

        ecs.DespawnRecursive(root);

        ecs.EntityCount.Should().Be(0);
    }

    [Fact]
    public void A_Parent_Despawned_And_Replaced_Is_Not_The_New_Entity()
    {
        var ecs = new EcsWorld();
        var parent = ecs.Spawn();
        var child = ecs.Spawn();
        ecs.SetParent(child, parent);

        ecs.Despawn(parent);
        var reuse = ecs.Spawn();

        reuse.Should().Be(parent);
        ecs.ParentOf(child).Should().Be(0);
    }

    [Fact]
    public void A_Cycle_Is_Refused()
    {
        var ecs = new EcsWorld();
        var a = ecs.Spawn();
        var b = ecs.Spawn();
        ecs.SetParent(b, a);

        var act = () => ecs.SetParent(a, b);

        act.Should().Throw<InvalidOperationException>();
    }
}
