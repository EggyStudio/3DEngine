using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Ecs;

[Trait("Category", "Unit")]
public class ChangeTrackingTests
{
    private static World NewWorld()
    {
        var world = new World();
        world.InsertResource(new EcsWorld());
        return world;
    }

    [Fact]
    public void A_Write_Through_GetRef_Is_Seen_By_A_Changed_Query_And_A_Read_Is_Not()
    {
        var ecs = new EcsWorld();
        var moved = ecs.Spawn();
        var read = ecs.Spawn();
        ecs.Add(moved, new Transform(Vector3.Zero));
        ecs.Add(read, new Transform(Vector3.Zero));
        ecs.BeginFrame();

        ecs.GetRef<Transform>(moved).Position = Vector3.One;
        _ = ecs.GetReadOnly<Transform>(read).Position;

        ecs.Query<Transform>().Changed<Transform>().Select(r => r.Entity).Should().Equal(moved);
        ecs.AnyChanged<Transform>().Should().BeTrue();
        ecs.BeginFrame();
        ecs.AnyChanged<Transform>().Should().BeFalse("a frame begins with nothing changed");
    }

    // A root at x 10 with a child a unit along x, propagated once and then a frame begun.
    private static (World World, EcsWorld Ecs, int Root, int Child) Hierarchy()
    {
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var root = ecs.Spawn();
        var child = ecs.Spawn();
        ecs.Add(root, new Transform(new Vector3(10, 0, 0)));
        ecs.Add(child, new Transform(new Vector3(1, 0, 0)));
        ecs.SetParent(child, root);
        TransformPropagation.Run(world);
        ecs.GetReadOnly<GlobalTransform>(child).Matrix.Translation.Should().Be(new Vector3(11, 0, 0));
        ecs.BeginFrame();
        return (world, ecs, root, child);
    }

    [Fact]
    public void A_Static_Hierarchy_Costs_No_Propagation()
    {
        var (world, ecs, _, _) = Hierarchy();

        TransformPropagation.Run(world);
        ecs.AnyChanged<GlobalTransform>().Should().BeFalse("nothing moved, so no global transform was written");
    }

    [Fact]
    public void Moving_A_Root_Through_GetRef_Moves_Its_Child()
    {
        var (world, ecs, root, child) = Hierarchy();

        ecs.GetRef<Transform>(root).Position = new Vector3(20, 0, 0);
        TransformPropagation.Run(world);

        ecs.GetReadOnly<GlobalTransform>(child).Matrix.Translation.Should().Be(new Vector3(21, 0, 0));
    }

    [Fact]
    public void A_Write_After_Propagation_Reaches_The_Child_The_Next_Frame()
    {
        var (world, ecs, root, child) = Hierarchy();

        TransformPropagation.Run(world);
        ecs.GetRef<Transform>(root).Position = new Vector3(30, 0, 0);
        TransformPropagation.Remember(world);
        ecs.BeginFrame();
        TransformPropagation.Run(world);

        ecs.GetReadOnly<GlobalTransform>(child).Matrix.Translation.Should().Be(new Vector3(31, 0, 0));
    }

    [Fact]
    public void A_Read_Only_Query_Reads_By_Reference_And_Marks_Nothing()
    {
        var ecs = new EcsWorld();
        var a = ecs.Spawn();
        ecs.Add(a, new Transform(new Vector3(1, 2, 3)));
        ecs.Add(a, new Parent(default));
        ecs.BeginFrame();

        var sum = Vector3.Zero;
        foreach (var row in ecs.QueryReadOnly<Transform>()) sum += row.Component.Position;
        foreach (var row in ecs.QueryReadOnly<Transform, Parent>()) sum += row.C1.Position;

        sum.Should().Be(new Vector3(2, 4, 6));
        ecs.AnyChanged<Transform>().Should().BeFalse("reading marks nothing, where QueryRef would have");
        foreach (var _ in ecs.QueryRef<Transform>()) { }
        ecs.AnyChanged<Transform>().Should().BeTrue();
    }

    [Fact]
    public void A_Body_At_Rest_Leaves_Its_Transform_Unmarked()
    {
        using var physics = new PhysicsWorld(new PhysicsSettings { Gravity = Vector3.Zero });
        var ecs = new EcsWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Transform(Vector3.Zero));
        physics.CreateSphere(new Vector3(0, 1, 0), 0.5f, entityId: entity);

        physics.SyncTransforms(ecs);
        ecs.Changed<Transform>(entity).Should().BeTrue("the first sync moves it to the body");
        ecs.BeginFrame();
        physics.StepOnce(1 / 60f);
        physics.SyncTransforms(ecs);
        ecs.Changed<Transform>(entity).Should().BeFalse("a body that did not move writes nothing");
    }
}
