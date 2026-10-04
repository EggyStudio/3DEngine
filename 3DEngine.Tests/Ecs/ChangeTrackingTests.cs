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
        ecs.AnyChanged<Transform>().Should().BeFalse("outside a system, a frame begins with nothing changed");
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
        long lastRun = 0;
        // Propagation as the schedule runs it, a system with a change tick of its own.
        void Propagate()
        {
            var outer = ChangeTicks.Enter(lastRun, out var tick);
            try { TransformPropagation.Run(world); }
            finally { ChangeTicks.Leave(outer); lastRun = tick; }
        }

        Propagate();
        ecs.GetRef<Transform>(root).Position = new Vector3(30, 0, 0);
        ecs.BeginFrame();
        Propagate();

        ecs.GetReadOnly<GlobalTransform>(child).Matrix.Translation.Should().Be(new Vector3(31, 0, 0));
    }

    private struct Health { public int Value; }

    // How many fixed steps see one change written in Update on the third frame, over twelve frames
    // of the given length, with a fixed step of a sixtieth of a second.
    private static int FixedStepsSeeingOneChange(double frameSeconds)
    {
        var app = new App();
        var ecs = new EcsWorld();
        app.World.InsertResource(ecs);
        var fixedTime = new FixedTime { Hz = 60, MaxStepsPerFrame = 10 };
        app.World.InsertResource(fixedTime);
        var entity = ecs.Spawn();
        ecs.Add(entity, new Health());

        var frame = 0;
        var seen = 0;
        // Each frame begins as EcsPlugin begins it.
        app.AddSystem(Stage.First, new SystemDescriptor(_ => ecs.BeginFrame(), "Begin").MainThreadOnly());
        app.AddSystem(Stage.Update, new SystemDescriptor(_ =>
        {
            if (frame == 3) ecs.GetRef<Health>(entity).Value++;
        }, "Write").MainThreadOnly());
        app.AddSystem(Stage.FixedUpdate, new SystemDescriptor(_ =>
        {
            foreach (var row in ecs.Query<Health>().Changed<Health>()) seen++;
        }, "Watch").MainThreadOnly());

        for (frame = 1; frame <= 12; frame++)
        {
            fixedTime.Accumulate(frameSeconds);
            app.BeginFrame();
            app.EndFrame();
        }
        return seen;
    }

    [Fact]
    public void A_Fixed_Step_Sees_A_Change_Once_At_A_High_Frame_Rate()
    {
        // Four frames to a step, so the change is written in a frame with no step of its own.
        FixedStepsSeeingOneChange(1.0 / 240).Should().Be(1);
    }

    [Fact]
    public void A_Fixed_Step_Sees_A_Change_Once_At_A_Low_Frame_Rate()
    {
        // Two and a half steps to a frame, so the frame after the change runs two or three.
        FixedStepsSeeingOneChange(2.5 / 60).Should().Be(1);
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

    [Fact]
    public void A_System_Sees_A_Component_Added_Once_And_Not_When_It_Is_Overwritten()
    {
        var ecs = new EcsWorld();
        long lastRun = 0;
        // What a system finds added, run as the schedule runs it.
        List<int> Look()
        {
            var outer = ChangeTicks.Enter(lastRun, out var tick);
            try { return ecs.Query<Health>().Added<Health>().Select(r => r.Entity).ToList(); }
            finally { ChangeTicks.Leave(outer); lastRun = tick; }
        }

        var first = ecs.Spawn();
        ecs.Add(first, new Health { Value = 1 });
        Look().Should().Equal(first);
        Look().Should().BeEmpty("it was seen already");

        ecs.Add(first, new Health { Value = 2 });
        var second = ecs.Spawn();
        ecs.Update(second, new Health { Value = 3 });
        Look().Should().Equal([second], "overwriting a component adds nothing, and Update adds one an entity lacked");
    }

    [Fact]
    public void Every_Query_Kind_Filters_By_Added()
    {
        var ecs = new EcsWorld();
        var old = ecs.Spawn();
        ecs.Add(old, new Health());
        ecs.Add(old, new Transform(Vector3.Zero));
        ecs.BeginFrame();
        var fresh = ecs.Spawn();
        ecs.Add(fresh, new Health());
        ecs.Add(fresh, new Transform(Vector3.Zero));

        ecs.Added<Health>(fresh).Should().BeTrue();
        ecs.Added<Health>(old).Should().BeFalse("outside a system, added counts from the frame's start");
        ecs.Query<Transform>().Added<Health>().Select(r => r.Entity).Should().Equal(fresh);
        var read = new List<int>();
        foreach (var row in ecs.QueryReadOnly<Transform, Health>().Added<Health>()) read.Add(row.Entity);
        read.Should().Equal(fresh);
        var visited = new List<int>();
        foreach (var row in ecs.QueryRef<Transform>().Added<Transform>()) visited.Add(row.Entity);
        visited.Should().Equal(fresh);
    }
}
