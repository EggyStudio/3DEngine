using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class QueryFilterTests
{
    private struct Velocity { public float Y; }
    private struct Falls;
    private struct Grounded;
    private struct Never;
    private struct Mass { public float Kg; }

    private static (EcsWorld Ecs, int Falling, int Landed, int Floating) World()
    {
        var ecs = new EcsWorld();
        var falling = ecs.Spawn();
        ecs.Add(falling, new Velocity());
        ecs.Add(falling, new Falls());
        var landed = ecs.Spawn();
        ecs.Add(landed, new Velocity());
        ecs.Add(landed, new Falls());
        ecs.Add(landed, new Grounded());
        var floating = ecs.Spawn();
        ecs.Add(floating, new Velocity());
        return (ecs, falling, landed, floating);
    }

    private static List<int> Entities(EcsWorld.RefEnumerable<Velocity> query)
    {
        var found = new List<int>();
        foreach (var row in query) found.Add(row.Entity);
        return found;
    }

    [Fact]
    public void With_And_Without_Narrow_The_Query()
    {
        var (ecs, falling, _, _) = World();

        Entities(ecs.QueryRef<Velocity>().With<Falls>().Without<Grounded>()).Should().Equal(falling);
    }

    [Fact]
    public void A_Required_Type_No_Entity_Has_Matches_Nothing_And_A_Forbidden_One_Forbids_Nothing()
    {
        var (ecs, _, _, _) = World();

        Entities(ecs.QueryRef<Velocity>().With<Never>()).Should().BeEmpty();
        Entities(ecs.QueryRef<Velocity>().Without<Never>()).Should().HaveCount(3);
    }

    [Fact]
    public void Changed_Keeps_Only_What_Changed_This_Frame()
    {
        var (ecs, _, landed, _) = World();
        ecs.BeginFrame();
        ecs.Update(landed, new Grounded());

        Entities(ecs.QueryRef<Velocity>().Changed<Grounded>()).Should().Equal(landed);
    }

    [Fact]
    public void A_Filtered_Row_Is_Written_By_Reference()
    {
        var (ecs, falling, _, _) = World();

        foreach (var row in ecs.QueryRef<Velocity>().With<Falls>().Without<Grounded>())
            row.Component.Y = -1;

        ecs.TryGet<Velocity>(falling, out var v).Should().BeTrue();
        v.Y.Should().Be(-1);
    }

    [Fact]
    public void A_Two_Component_Query_Takes_The_Same_Filters()
    {
        var (ecs, falling, landed, floating) = World();
        foreach (var entity in new[] { falling, landed, floating })
            ecs.Add(entity, new Mass { Kg = 2 });

        var found = new List<int>();
        foreach (var row in ecs.QueryRef<Velocity, Mass>().With<Falls>().Without<Grounded>())
        {
            row.C1.Y -= row.C2.Kg;
            found.Add(row.Entity);
        }

        found.Should().Equal(falling);
        ecs.TryGet<Velocity>(falling, out var v).Should().BeTrue();
        v.Y.Should().Be(-2);

        ecs.BeginFrame();
        ecs.Update(landed, new Grounded());
        var changed = new List<int>();
        foreach (var row in ecs.QueryRef<Velocity, Mass>().Changed<Grounded>()) changed.Add(row.Entity);
        changed.Should().Equal(landed);

        var none = 0;
        foreach (var _ in ecs.QueryRef<Velocity, Mass>().With<Never>()) none++;
        none.Should().Be(0);
    }

    [Fact]
    public void A_Three_Component_Query_Yields_Entities_With_All_Three_By_Reference()
    {
        var (ecs, falling, landed, floating) = World();
        ecs.Add(falling, new Mass { Kg = 2 });
        ecs.Add(landed, new Mass { Kg = 3 });
        ecs.Add(floating, new Mass { Kg = 4 });

        var found = new List<int>();
        foreach (var row in ecs.QueryRef<Velocity, Falls, Mass>())
        {
            row.C1.Y -= row.C3.Kg;
            found.Add(row.Entity);
        }

        found.Should().BeEquivalentTo([falling, landed], "the floating entity does not fall");
        ecs.GetRef<Velocity>(falling).Y.Should().Be(-2, "the first component is changed in place");
        ecs.GetRef<Velocity>(floating).Y.Should().Be(0);
    }

    [Fact]
    public void A_Three_Component_Query_Takes_Filters_And_Is_Empty_Without_A_Store()
    {
        var (ecs, falling, landed, _) = World();
        ecs.Add(falling, new Mass());
        ecs.Add(landed, new Mass());

        var found = new List<int>();
        foreach (var row in ecs.QueryRef<Velocity, Falls, Mass>().Without<Grounded>()) found.Add(row.Entity);
        found.Should().Equal(falling);

        foreach (var _ in ecs.QueryRef<Velocity, Falls, Never>()) throw new InvalidOperationException("no entity has a Never");
    }

    [Fact]
    public void A_Three_Component_Query_Marks_What_It_Hands_Out_As_Changed()
    {
        var (ecs, falling, _, _) = World();
        ecs.Add(falling, new Mass());
        ecs.BeginFrame();

        foreach (var _ in ecs.QueryRef<Velocity, Falls, Mass>()) { }

        var changed = new List<int>();
        foreach (var row in ecs.QueryRef<Velocity>().Changed<Mass>()) changed.Add(row.Entity);
        changed.Should().Equal(falling);
    }
}
