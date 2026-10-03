using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class EcsStoreRaceTests
{
    private struct Raced { public int Value; }

    [Fact]
    public void Systems_Touching_A_New_Type_Together_Share_One_Store()
    {
        // Repeated, because the race needs the threads to meet in the slow path, which one try may miss.
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var ecs = new EcsWorld();
            using var start = new Barrier(8);
            var stores = new EcsWorld.ComponentStore<Raced>[8];

            Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                start.SignalAndWait();
                stores[i] = ecs.GetStorePublic<Raced>();
            });

            stores.Distinct().Should().ContainSingle("every system must see the same store");
        }
    }
}
