using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class KeyIndexTests
{
    // A fire in a cell, found by the cell, as the voxel game's fires are.
    private struct Fire : IKeyed<(int X, int Y, int Z)>
    {
        public (int X, int Y, int Z) Cell;
        public readonly (int X, int Y, int Z) Key => Cell;
    }

    [Fact]
    public void An_Entity_Is_Found_By_Its_Components_Key_Among_A_Thousand_And_Not_After_It_Is_Despawned()
    {
        var ecs = new EcsWorld();
        var cells = new Dictionary<(int, int, int), int>();
        for (int i = 0; i < 1000; i++)
        {
            var entity = ecs.Spawn();
            var cell = (i % 10, i / 100, i / 10 % 10);
            ecs.Add(entity, new Fire { Cell = cell });
            cells[cell] = entity;
        }
        // Made after the fires, so it takes those already there, and kept from then on.
        var fires = ecs.Index<Fire, (int, int, int)>();
        fires.Count.Should().Be(1000);
        var burning = cells[(3, 4, 5)];

        fires.TryGet((3, 4, 5), out var found).Should().BeTrue();
        found.Should().Be(burning);
        fires.Contains((10, 0, 0)).Should().BeFalse("no fire burns there");
        ecs.Index<Fire, (int, int, int)>().Should().BeSameAs(fires, "a type keeps one index");

        // Warmed once, so the JIT's own allocations are behind it.
        fires.TryGet((3, 4, 5), out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) fires.TryGet((3, 4, 5), out _);
        GC.GetAllocatedBytesForCurrentThread().Should().Be(before, "a lookup allocates nothing");

        ecs.Despawn(burning);
        fires.TryGet((3, 4, 5), out _).Should().BeFalse("the fire's entity is gone");
        fires.Count.Should().Be(999);

        var spread = ecs.Spawn();
        ecs.Add(spread, new Fire { Cell = (3, 4, 5) });
        fires.TryGet((3, 4, 5), out found).Should().BeTrue("a fire added after the index was made is kept");
        found.Should().Be(spread);

        ecs.Update(spread, new Fire { Cell = (20, 0, 0) });
        fires.Contains((3, 4, 5)).Should().BeFalse("its fire was replaced by one in another cell");
        fires.TryGet((20, 0, 0), out found).Should().BeTrue();
        found.Should().Be(spread);

        ecs.Remove<Fire>(spread);
        fires.Contains((20, 0, 0)).Should().BeFalse("its fire was taken off it");

        var moved = cells[(1, 2, 3)];
        ecs.GetRef<Fire>(moved).Cell = (30, 0, 0);
        fires.Contains((1, 2, 3)).Should().BeFalse("a key changed in place is found by its old key no more");
    }
}
