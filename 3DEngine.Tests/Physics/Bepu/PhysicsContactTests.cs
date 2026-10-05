using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

[Trait("Category", "Integration")]
public class PhysicsContactTests
{
    private const float Step = 1f / 60f;

    private static PhysicsWorld NewWorld(Vector3? gravity = null) =>
        new(new PhysicsSettings { UseFixedTimestep = true, FixedTimeStep = Step, Gravity = gravity ?? new Vector3(0, -9.81f, 0) });

    private static (List<PhysicsContact> Started, List<PhysicsContact> Ended) Run(PhysicsWorld world, int steps)
    {
        var started = new List<PhysicsContact>();
        var ended = new List<PhysicsContact>();
        for (int i = 0; i < steps; i++)
        {
            world.StepOnce(Step);
            world.TakeContacts(started, ended);
        }
        return (started, ended);
    }

    [Fact]
    public void Two_Bodies_Meeting_Start_A_Contact_Once_And_End_It_When_They_Part()
    {
        using var world = NewWorld(Vector3.Zero);
        var ecs = new EcsWorld();
        world.EntityHandle = ecs.Handle;
        int left = ecs.Spawn(), right = ecs.Spawn();
        var a = world.CreateSphere(new Vector3(-2, 0, 0), 0.5f, entityId: left);
        var b = world.CreateSphere(new Vector3(2, 0, 0), 0.5f, entityId: right);
        a.SetLinearVelocity(new Vector3(3, 0, 0));
        b.SetLinearVelocity(new Vector3(-3, 0, 0));

        var (started, ended) = Run(world, 40);
        started.Should().ContainSingle("the pair touches over several steps but starts touching once");
        new[] { started[0].A, started[0].B }.Should().BeEquivalentTo([ecs.Handle(left), ecs.Handle(right)]);

        a.SetLinearVelocity(new Vector3(-3, 0, 0));
        b.SetLinearVelocity(new Vector3(3, 0, 0));
        var (again, parted) = Run(world, 30);
        again.Should().BeEmpty();
        ended.Concat(parted).Should().ContainSingle("the pair stops touching once, whether the solver's push or the throw parts it")
            .Which.BodyA.Should().BeOneOf(a, b);
    }

    [Fact]
    public void A_Contact_Says_Where_The_Bodies_Met_And_Which_Way()
    {
        using var world = NewWorld();
        var floor = world.CreateStaticBox(new Vector3(2, -0.5f, 3), new Vector3(5, 0.5f, 5));
        var ball = world.CreateSphere(new Vector3(2, 1, 3), 0.5f);

        var (started, _) = Run(world, 60);

        var contact = started.Should().ContainSingle().Which;
        contact.Point.X.Should().BeApproximately(2, 0.01f, "the ball lands straight down");
        contact.Point.Z.Should().BeApproximately(3, 0.01f);
        contact.Point.Y.Should().BeApproximately(0, 0.05f, "it meets the floor's top");
        var upOnBall = contact.BodyA == ball ? contact.Normal : -contact.Normal;
        upOnBall.Y.Should().BeGreaterThan(0.99f, "the normal points from B toward A, so up toward the ball");
        (contact.BodyA == floor || contact.BodyB == floor).Should().BeTrue();
    }

    [Fact]
    public void A_Contact_Says_How_Fast_The_Bodies_Closed_As_They_Met()
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(5, 0.5f, 5));
        // Half a unit above the floor, it lands at the square root of twice gravity times that.
        world.CreateSphere(new Vector3(0, 1, 0), 0.5f);
        var resting = world.CreateSphere(new Vector3(3, 0.5f, 0), 0.5f);

        var (started, _) = Run(world, 60);

        started.Should().HaveCount(2);
        started.Single(c => c.BodyA == resting || c.BodyB == resting).Speed.Should().BeLessThan(0.3f, "a body placed on the floor meets it at rest");
        started.Single(c => c.BodyA != resting && c.BodyB != resting).Speed.Should().BeApproximately(MathF.Sqrt(2 * 9.81f * 0.5f), 0.35f);

        using var space = NewWorld(Vector3.Zero);
        var a = space.CreateSphere(new Vector3(-2, 0, 0), 0.5f);
        var b = space.CreateSphere(new Vector3(2, 0, 0), 0.5f);
        a.SetLinearVelocity(new Vector3(3, 0, 0));
        b.SetLinearVelocity(new Vector3(-3, 0, 0));
        Run(space, 40).Started.Should().ContainSingle().Which.Speed.Should().BeApproximately(6, 0.01f, "each closes at 3 toward the other");
    }

    [Fact]
    public void A_Trigger_Reports_What_Passes_Through_It_And_Stops_Nothing()
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(0, -10.5f, 0), new Vector3(5, 0.5f, 5));
        var gate = world.CreateStaticBox(new Vector3(0, -3, 0), new Vector3(2, 0.5f, 2));
        world.SetTrigger(gate, true);
        var ball = world.CreateSphere(new Vector3(0, 0, 0), 0.5f);

        var (started, ended) = Run(world, 120);

        started.Should().Contain(c => c.BodyA == gate || c.BodyB == gate, "the ball entering the trigger starts a contact");
        ended.Should().Contain(c => c.BodyA == gate || c.BodyB == gate, "and leaving it ends one");
        world.GetPosition(ball).Y.Should().BeLessThan(-9, "the trigger did not hold the ball, which fell to the floor below");
    }

    [Fact]
    public void A_Body_Made_Where_A_Trigger_Was_Destroyed_Is_Solid()
    {
        using var world = NewWorld();
        var gate = world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(5, 0.5f, 5));
        world.SetTrigger(gate, true);
        world.Destroy(gate);
        var floor = world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(5, 0.5f, 5));
        floor.Handle.Should().Be(gate.Handle, "the handle is given out again");
        var ball = world.CreateSphere(new Vector3(0, 1, 0), 0.5f);

        Run(world, 60);

        world.GetPosition(ball).Y.Should().BeApproximately(0.5f, 0.05f, "the new box stops the ball");
    }

    [Fact]
    public void A_Body_Resting_Until_It_Sleeps_Stays_In_Contact_And_Ends_When_Destroyed()
    {
        using var world = NewWorld();
        world.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(5, 0.5f, 5));
        var ball = world.CreateSphere(new Vector3(0, 1, 0), 0.5f);

        var (started, ended) = Run(world, 600);
        started.Should().ContainSingle("the ball lands once");
        world.IsAwake(ball).Should().BeFalse("ten seconds at rest puts a body to sleep");
        ended.Should().BeEmpty("a sleeping pair is not tested but still touches");

        world.Destroy(ball);
        (_, ended) = Run(world, 1);
        ended.Should().ContainSingle("a destroyed body touches nothing");
    }

    [Fact]
    public void Contacts_Reach_Game_Code_As_Events_That_Last_One_Frame()
    {
        using var app = new App();
        app.World.InsertResource(new EcsWorld());
        var time = new Time();
        time.Update(0.0, Step);
        app.World.InsertResource(time);
        app.AddPlugin(new PhysicsPlugin());
        var ecs = app.World.Resource<EcsWorld>();
        var phys = app.World.Resource<PhysicsWorld>();

        int ground = ecs.Spawn(), ball = ecs.Spawn();
        phys.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(5, 0.5f, 5), entityId: ground);
        phys.CreateSphere(new Vector3(0, 0.6f, 0), 0.5f, entityId: ball);

        var seen = new List<ContactStarted>();
        for (int frame = 0; frame < 30; frame++)
        {
            app.Schedule.RunStage(Stage.First, app.World);
            app.Schedule.RunStage(Stage.PreUpdate, app.World);
            seen.AddRange(app.World.ReadEvents<ContactStarted>());
        }

        seen.Should().ContainSingle("an event is cleared when the next frame starts");
        seen[0].Involves(ecs.Handle(ball), out var other).Should().BeTrue();
        other.Should().Be(ecs.Handle(ground));
    }

    [Fact]
    public void A_Body_Under_A_Parent_Stays_Where_The_Simulation_Has_It_As_The_Parent_Moves()
    {
        using var world = NewWorld(Vector3.Zero);
        var ecs = new EcsWorld();
        int parent = ecs.Spawn();
        ecs.Add(parent, new Transform(new Vector3(10, 0, 0)));
        int child = ecs.Spawn();
        ecs.Add(child, new Transform(Vector3.Zero));
        ecs.SetParent(child, parent);
        var body = world.CreateSphere(new Vector3(3, 4, 5), 0.5f, entityId: child);

        world.SyncTransforms(ecs);
        TransformPropagation.ComposedWorldMatrix(ecs, child).Translation.Should().Be(new Vector3(3, 4, 5));
        ecs.GetRef<Transform>(child).Position.Should().Be(new Vector3(-7, 4, 5), "the local transform is the body's pose less the parent's");

        // The parent turns and moves away, and the next sync keeps the child on its body.
        ref var p = ref ecs.GetRef<Transform>(parent);
        p.Position = new Vector3(0, 2, 0);
        p.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        world.SyncTransforms(ecs);

        var composed = TransformPropagation.ComposedWorldMatrix(ecs, child);
        Vector3.Distance(composed.Translation, body.Position).Should().BeLessThan(1e-4f);
        Matrix4x4.Decompose(composed, out _, out var rotation, out _);
        Quaternion.Dot(rotation, body.Rotation).Should().BeApproximately(1f, 1e-4f, "the world rotation is the body's too");
    }
}
