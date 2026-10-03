using FluentAssertions;
using Xunit;

namespace Engine.Tests.Entities;

[Trait("Category", "Unit")]
public class EcsWorldTests
{
    // -- Spawn / Add / Query --

    [Fact]
    public void Spawn_Add_Query_Work()
    {
        var ecs = new EcsWorld();
        var e1 = ecs.Spawn();
        var e2 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 1 });
        ecs.Add(e2, new TestComp { A = 2 });

        var result = ecs.Query<TestComp>().OrderBy(t => t.Entity).ToArray();

        result.Should().HaveCount(2);
        result[0].Entity.Should().Be(e1);
        result[0].Component.A.Should().Be(1);
        result[1].Entity.Should().Be(e2);
        result[1].Component.A.Should().Be(2);
    }

    [Fact]
    public void Spawn_Assigns_FirstGeneration()
    {
        var ecs = new EcsWorld();

        var id = ecs.Spawn();

        ecs.GetGeneration(id).Should().BeGreaterThanOrEqualTo(1);
    }

    // -- Update / Changed --

    [Fact]
    public void Update_Marks_Changed_For_Current_Frame()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 0 });

        ecs.BeginFrame();
        ecs.Update(e, new TestComp { A = 1 });

        ecs.Changed<TestComp>(e).Should().BeTrue();
        ecs.Query<TestComp>().Single().Component.A.Should().Be(1);

        // Next frame: changed must reset
        ecs.BeginFrame();
        ecs.Changed<TestComp>(e).Should().BeFalse();
    }

    [Fact]
    public void Changed_Reset_After_Frame_When_ModifiedViaUpdate()
    {
        var ecs = new EcsWorld();
        int e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 7 });

        ecs.BeginFrame();
        ecs.Update(e, new TestComp { A = 8 });
        ecs.Changed<TestComp>(e).Should().BeTrue();

        ecs.BeginFrame();
        ecs.Changed<TestComp>(e).Should().BeFalse();
    }

    [Fact]
    public void Changed_Reset_After_Remove_And_ReAdd()
    {
        var ecs = new EcsWorld();
        int e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 1 });

        ecs.BeginFrame();
        ecs.Update(e, new TestComp { A = 2 });
        ecs.Changed<TestComp>(e).Should().BeTrue();

        ecs.BeginFrame();
        ecs.Changed<TestComp>(e).Should().BeFalse();

        ecs.Remove<TestComp>(e);
        ecs.Add(e, new TestComp { A = 3 });

        ecs.BeginFrame();
        ecs.Update(e, new TestComp { A = 4 });
        ecs.Changed<TestComp>(e).Should().BeTrue();
    }

    // -- Mutate --

    [Fact]
    public void Mutate_Transforms_And_Marks_Changed()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 5 });

        ecs.BeginFrame();
        ecs.Mutate<TestComp>(e, c => { c.A += 3; return c; });

        ecs.Changed<TestComp>(e).Should().BeTrue();
        ecs.Query<TestComp>().Single().Component.A.Should().Be(8);
    }

    [Fact]
    public void Mutate_NoOp_On_Missing_Component_Does_Not_Throw()
    {
        var ecs = new EcsWorld();
        int e = ecs.Spawn();

        // Should silently do nothing
        var act = () => ecs.Mutate<TestComp>(e, c => { c.A++; return c; });

        act.Should().NotThrow();
        ecs.TryGet<TestComp>(e, out _).Should().BeFalse();
    }

    // -- TransformEach --

    [Fact]
    public void TransformEach_Updates_All_And_Marks_Changed()
    {
        var ecs = new EcsWorld();
        var e1 = ecs.Spawn();
        var e2 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 1 });
        ecs.Add(e2, new TestComp { A = 2 });

        ecs.BeginFrame();
        ecs.TransformEach<TestComp>((id, c) => { c.A += id == e1 ? 10 : 20; return c; });

        var dict = ecs.Query<TestComp>().ToDictionary(t => t.Entity, t => t.Component.A);
        dict[e1].Should().Be(11);
        dict[e2].Should().Be(22);
        ecs.Changed<TestComp>(e1).Should().BeTrue();
        ecs.Changed<TestComp>(e2).Should().BeTrue();
    }

    // -- ParallelTransformEach --

    [Fact]
    public void ParallelTransformEach_MarksChanged()
    {
        var ecs = new EcsWorld();
        for (int i = 0; i < 1000; i++)
        {
            int e = ecs.Spawn();
            ecs.Add(e, new TestComp { A = i });
        }

        ecs.BeginFrame();
        ecs.ParallelTransformEach<TestComp>((_, c) => { c.A += 1; return c; });

        ecs.Changed<TestComp>(1).Should().BeTrue();
        ecs.Changed<TestComp>(500).Should().BeTrue();
        ecs.Query<TestComp>().First(t => t.Component.A == 1).Component.A.Should().Be(1);
    }

    // -- TryGet --

    [Fact]
    public void TryGet_Returns_Existing_Value()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 42 });

        ecs.TryGet<TestComp>(e, out var comp).Should().BeTrue();
        comp.A.Should().Be(42);
    }

    // -- Multi-component queries --

    [Fact]
    public void Query_Two_And_Three_Components()
    {
        var ecs = new EcsWorld();
        var e1 = ecs.Spawn();
        var e2 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 1 });
        ecs.Add(e2, new TestComp { A = 2 });
        ecs.Add(e1, new OtherComp { B = 3 });
        ecs.Add(e2, new OtherComp { B = 4 });
        ecs.Add(e2, new ThirdComp { C = 9 });

        var q2 = ecs.Query<TestComp, OtherComp>().OrderBy(t => t.Entity).ToArray();
        q2.Should().HaveCount(2);
        q2[0].C1.A.Should().Be(1);
        q2[0].C2.B.Should().Be(3);

        var q3 = ecs.Query<TestComp, OtherComp, ThirdComp>().ToArray();
        q3.Should().ContainSingle();
        q3[0].Entity.Should().Be(e2);
        q3[0].C1.A.Should().Be(2);
        q3[0].C2.B.Should().Be(4);
        q3[0].C3.C.Should().Be(9);
    }

    [Fact]
    public void QueryWhere_Filters_Correctly()
    {
        var ecs = new EcsWorld();
        int e1 = ecs.Spawn();
        int e2 = ecs.Spawn();
        int e3 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 1 });
        ecs.Add(e2, new TestComp { A = 5 });
        ecs.Add(e3, new TestComp { A = 10 });

        var filtered = ecs.QueryWhere<TestComp>(c => c.A >= 5).OrderBy(t => t.Entity).ToArray();

        filtered.Should().HaveCount(2);
        filtered[0].Entity.Should().Be(e2);
        filtered[1].Entity.Should().Be(e3);
    }

    // -- GetRef --

    [Fact]
    public void GetRef_ModifyComponent_ReflectsInQuery()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 10 });

        ref var comp = ref ecs.GetRef<TestComp>(e);
        comp.A += 5;

        ecs.Query<TestComp>().Single().Component.A.Should().Be(15);
        ecs.TryGet<TestComp>(e, out var c2).Should().BeTrue();
        c2.A.Should().Be(15);
    }

    [Fact]
    public void GetRef_Throws_For_Missing_Component()
    {
        var ecs = new EcsWorld();
        var e = ecs.Spawn();

        var act = () => { ref var _ = ref ecs.GetRef<TestComp>(e); };

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetRef_Works_After_Spawn_But_Not_After_Despawn()
    {
        var ecs = new EcsWorld();
        var id = ecs.Spawn();
        ecs.Add(id, new TestComp { A = 1 });

        ref var c = ref ecs.GetRef<TestComp>(id);
        c.A = 5;

        ecs.TryGet<TestComp>(id, out var v).Should().BeTrue();
        v.A.Should().Be(5);

        ecs.Despawn(id);

        var act = () => { ref var _ = ref ecs.GetRef<TestComp>(id); };
        act.Should().Throw<KeyNotFoundException>();
    }

    // -- GetSpan --

    [Fact]
    public void GetSpan_MutateAll_MarksChanged()
    {
        var ecs = new EcsWorld();
        int e1 = ecs.Spawn();
        int e2 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 2 });
        ecs.Add(e2, new TestComp { A = 3 });

        ecs.BeginFrame();
        var span = ecs.GetSpan<TestComp>();
        span.IsValid.Should().BeTrue();

        for (int i = 0; i < span.Entities.Length; i++)
            span.Components[i].A *= 2;

        // Direct span mutation does not auto mark changed - explicit mark needed
        ecs.TransformEach<TestComp>((_, c) => c);

        ecs.Changed<TestComp>(e1).Should().BeTrue();
        ecs.Changed<TestComp>(e2).Should().BeTrue();
        var arr = ecs.Query<TestComp>().OrderBy(t => t.Entity).ToArray();
        arr[0].Component.A.Should().Be(4);
        arr[1].Component.A.Should().Be(6);
    }

    [Fact]
    public void SpanMutation_Without_Marking_Does_Not_Set_Changed()
    {
        var ecs = new EcsWorld();
        int e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 10 });

        ecs.BeginFrame();
        var span = ecs.GetSpan<TestComp>();
        span.Components[0].A = 99;

        // Changed should still be false until explicit marking
        ecs.Changed<TestComp>(e).Should().BeFalse();

        ecs.TransformEach<TestComp>((_, c) => c);
        ecs.Changed<TestComp>(e).Should().BeTrue();
    }

    // -- QueryRef --

    [Fact]
    public void QueryRef_ModifiesComponents()
    {
        var ecs = new EcsWorld();
        int e1 = ecs.Spawn();
        int e2 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 1 });
        ecs.Add(e2, new TestComp { A = 2 });

        ecs.BeginFrame();
        foreach (var rc in ecs.QueryRef<TestComp>())
            rc.Component.A += 5;

        ecs.Changed<TestComp>(e1).Should().BeTrue();
        ecs.Changed<TestComp>(e2).Should().BeTrue();
        var values = ecs.Query<TestComp>().OrderBy(x => x.Entity).Select(x => x.Component.A).ToArray();
        values.Should().Equal(6, 7);
    }

    [Fact]
    public void QueryRef_TwoComponents_ModifiesBoth()
    {
        var ecs = new EcsWorld();
        int e1 = ecs.Spawn();
        int e2 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 10 });
        ecs.Add(e1, new OtherComp { B = 1 });
        ecs.Add(e2, new TestComp { A = 20 });
        ecs.Add(e2, new OtherComp { B = 2 });

        ecs.BeginFrame();
        foreach (var rc in ecs.QueryRef<TestComp, OtherComp>())
        {
            rc.C1.A += rc.C2.B * 10;
            rc.C2.B += 3;
        }

        var q = ecs.Query<TestComp, OtherComp>().OrderBy(t => t.Entity).ToArray();
        q.Should().HaveCount(2);
        q[0].C1.A.Should().Be(20);  // 10 + 1*10
        q[0].C2.B.Should().Be(4);   // 1 + 3
        q[1].C1.A.Should().Be(40);  // 20 + 2*10
        q[1].C2.B.Should().Be(5);   // 2 + 3
        ecs.Changed<TestComp>(e1).Should().BeTrue();
        ecs.Changed<TestComp>(e2).Should().BeTrue();
        ecs.Changed<OtherComp>(e1).Should().BeTrue();
        ecs.Changed<OtherComp>(e2).Should().BeTrue();
    }

    // -- Despawn --

    [Fact]
    public void Despawn_Disposes_Disposable_Components_And_Reuses_EntityId()
    {
        var ecs = new EcsWorld();
        int e1 = ecs.Spawn();
        var disp = new DisposableComp();
        ecs.Add(e1, disp);

        ecs.Despawn(e1);

        disp.IsDisposed.Should().BeTrue();
        var e2 = ecs.Spawn();
        e2.Should().Be(e1); // free list reuse
    }

    [Fact]
    public void Despawn_Increments_Generation()
    {
        var ecs = new EcsWorld();
        var id = ecs.Spawn();
        var g1 = ecs.GetGeneration(id);

        ecs.Despawn(id);

        var g2 = ecs.GetGeneration(id);
        g2.Should().NotBe(g1);

        // ID is reused with new generation
        var id2 = ecs.Spawn();
        id2.Should().Be(id);
        ecs.GetGeneration(id2).Should().Be(g2);
    }

    // -- Remove --

    [Fact]
    public void Remove_Component_RemovesOnlyThatComponent()
    {
        var ecs = new EcsWorld();
        int e = ecs.Spawn();
        ecs.Add(e, new TestComp { A = 5 });
        ecs.Add(e, new OtherComp { B = 9 });

        ecs.Remove<OtherComp>(e).Should().BeTrue();

        ecs.Has<TestComp>(e).Should().BeTrue();
        ecs.Has<OtherComp>(e).Should().BeFalse();
    }

    // -- EntitiesWith --

    [Fact]
    public void EntitiesWith_Returns_All_Component_Entities()
    {
        var ecs = new EcsWorld();
        int e1 = ecs.Spawn();
        int e2 = ecs.Spawn();
        int e3 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 1 });
        ecs.Add(e3, new TestComp { A = 3 });

        var entities = ecs.EntitiesWith<TestComp>().ToArray();
        Array.Sort(entities);

        entities.Should().Equal(e1, e3);
    }

    // -- SwapBackRemoval --

    [Fact]
    public void SwapBackRemoval_Preserves_ChangedFlag_ForMovedComponent()
    {
        var ecs = new EcsWorld();
        int e1 = ecs.Spawn();
        int e2 = ecs.Spawn();
        ecs.Add(e1, new TestComp { A = 1 });
        ecs.Add(e2, new TestComp { A = 2 });

        ecs.BeginFrame();
        ecs.Update(e2, new TestComp { A = 3 });
        ecs.Changed<TestComp>(e2).Should().BeTrue();

        ecs.BeginFrame();
        ecs.Changed<TestComp>(e2).Should().BeFalse();

        ecs.Update(e2, new TestComp { A = 4 });
        ecs.Changed<TestComp>(e2).Should().BeTrue();

        // Remove e1 causing swap-back of e2 to index 0
        ecs.Remove<TestComp>(e1);

        // e2 should still have its changed bit
        ecs.Changed<TestComp>(e2).Should().BeTrue();
    }

    // -- SpawnBatch --

    [Fact]
    public void SpawnBatch_With_Builder_Creates_Correct_Count()
    {
        var ecs = new EcsWorld();
        ecs.SpawnBatch(1000, (id, world) => world.Add(id, new TestComp { A = id }));

        ecs.Count<TestComp>().Should().Be(1000);
    }

    [Fact]
    public void SpawnBatch_With_Factory_Creates_Correct_Count()
    {
        var ecs = new EcsWorld();
        ecs.SpawnBatch<TestComp>(500, id => new TestComp { A = id * 2 });

        ecs.Count<TestComp>().Should().Be(500);
        // Verify values are correct
        foreach (var (entity, comp) in ecs.Query<TestComp>())
            comp.A.Should().Be(entity * 2);
    }

    [Fact]
    public void SpawnBatch_Default_Creates_Correct_Count()
    {
        var ecs = new EcsWorld();
        ecs.SpawnBatch<TestComp>(200);

        ecs.Count<TestComp>().Should().Be(200);
    }

    [Fact]
    public void SpawnBatch_Large_Batch_Works()
    {
        var ecs = new EcsWorld();
        const int count = 100_000;
        ecs.SpawnBatch<TestComp>(count);

        ecs.Count<TestComp>().Should().Be(count);
    }

    [Fact]
    public void SpawnBatch_Zero_Count_Is_NoOp()
    {
        var ecs = new EcsWorld();
        ecs.SpawnBatch<TestComp>(0);
        ecs.Count<TestComp>().Should().Be(0);
    }
}

// -- Test helper types --

public struct TestComp
{
    public int A;
}

public struct OtherComp
{
    public int B;
}

public struct ThirdComp
{
    public int C;
}

public sealed class DisposableComp : IDisposable
{
    public int DisposedCount;
    public bool IsDisposed => DisposedCount > 0;
    public void Dispose() => DisposedCount++;
}
