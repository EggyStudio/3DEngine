using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class GroupIndexTests
{
    // A mob, many of which stand in one column of a voxel world.
    private struct Mob : IKeyed<(int X, int Z)>
    {
        public (int X, int Z) Column;
        public readonly (int X, int Z) Key => Column;
    }

    [Fact]
    public void A_Thousand_Entities_In_Twenty_Groups_Are_Found_By_Their_Key_And_None_After_It_Leaves()
    {
        var ecs = new EcsWorld();
        var mobs = new int[1000];
        // Half spawned before the index is asked for, which it finds as it is made, and half after.
        for (int i = 0; i < 500; i++) ecs.Add(mobs[i] = ecs.Spawn(), new Mob { Column = (i % 20, 0) });
        var columns = ecs.Groups<Mob, (int X, int Z)>();
        for (int i = 500; i < mobs.Length; i++) ecs.Add(mobs[i] = ecs.Spawn(), new Mob { Column = (i % 20, 0) });

        columns.Count.Should().Be(20);
        for (int g = 0; g < 20; g++)
            columns.Members((g, 0)).ToArray().Should().BeEquivalentTo(mobs.Where((_, i) => i % 20 == g), "column {0} holds every twentieth", g);
        ecs.Groups<Mob, (int X, int Z)>().Should().BeSameAs(columns, "the world keeps one");

        var before = GC.GetAllocatedBytesForCurrentThread();
        var found = 0;
        foreach (var mob in columns.Members((3, 0))) found += mob >= 0 ? 1 : 0;
        GC.GetAllocatedBytesForCurrentThread().Should().Be(before, "a group is walked as a span of the index's own list");
        found.Should().Be(50);

        ecs.Despawn(mobs[3]);
        ecs.Remove<Mob>(mobs[23]);
        ecs.Update(mobs[43], new Mob { Column = (7, 0) });
        columns.Members((3, 0)).ToArray().Should().NotContain(new[] { mobs[3], mobs[23], mobs[43] }, "one despawned, one without its mob and one moved");
        columns.CountOf((3, 0)).Should().Be(47);
        columns.Members((7, 0)).ToArray().Should().Contain(mobs[43]).And.HaveCount(51);

        foreach (var mob in columns.Members((5, 0)).ToArray()) ecs.Despawn(mob);
        columns.Members((5, 0)).IsEmpty.Should().BeTrue();
        columns.Count.Should().Be(19, "a key with no entity left is no longer one");
        ecs.Invoking(e => e.Index<Mob, (int X, int Z)>()).Should().Throw<InvalidOperationException>("a component is kept by one index");
    }
}
