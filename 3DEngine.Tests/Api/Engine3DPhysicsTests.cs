using System.Diagnostics;
using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Physics through the flat API, in a headless app whose frames run the fixed steps, each frame a
/// sixtieth of a second by the clock it is set to, so a test counts frames and not the machine's time.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed partial class Engine3DPhysicsTests : IDisposable
{
    public Engine3DPhysicsTests()
    {
        var config = Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // Runs frames, a sixtieth of a second each, until the condition holds or two seconds of them pass.
    private static bool RunUntil(Func<bool> condition, int frames = 120)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            BeginDrawing();
            var done = condition();
            EndDrawing();
            if (done) return true;
        }
        return false;
    }

    // Runs frames, a sixtieth of a second each.
    private static void Frames(int count) => RunUntil(() => false, count);

    [Fact]
    public void A_Push_At_A_Corner_Turns_A_Body_And_A_Ray_Can_Look_Past_It()
    {
        SetPhysicsGravity(Vector3.Zero);
        var box = CreatePhysicsBox(new Vector3(0, 5, 0), Vector3.One, mass: 1);

        // Pushed sideways at its top, it moves and turns about the axis across the push.
        ApplyPhysicsImpulseAt(box, new Vector3(1, 0, 0), new Vector3(0, 5.5f, 0));
        GetPhysicsBodyVelocity(box).X.Should().BeApproximately(1, 1e-4f, "the whole impulse moves it");
        GetPhysicsBodyAngularVelocity(box).Z.Should().BeLessThan(-0.1f, "and pushed above its middle it turns, top first");
        var top = GetPhysicsBodyPointVelocity(box, new Vector3(0, 5.5f, 0));
        var bottom = GetPhysicsBodyPointVelocity(box, new Vector3(0, 4.5f, 0));
        top.X.Should().BeGreaterThan(bottom.X, "its top moves faster than its bottom as it turns");

        SetPhysicsBodyAngularVelocity(box, Vector3.Zero);
        var turned = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1);
        SetPhysicsBodyRotation(box, turned);
        Quaternion.Dot(GetPhysicsBodyRotation(box), turned).Should().BeGreaterThan(0.9999f);

        // A ray from inside the box meets the box, and looking past it meets the floor below.
        var floor = CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
        var down = new Ray(new Vector3(0, 5, 0), -Vector3.UnitY);
        var hit = GetRayCollisionPhysicsEx(down, 20, box);
        hit.Hit.Should().BeTrue();
        hit.Body.Should().Be(floor, "the box the ray starts in is looked past");
        hit.Point.Y.Should().BeApproximately(0, 1e-3f);

        // A trigger over the floor stops nothing, a ray included, from inside it or from above.
        CreatePhysicsTrigger(new Vector3(0, 2, 0), new Vector3(6, 4, 6));
        hit = GetRayCollisionPhysicsEx(new Ray(new Vector3(0, 1, 0), -Vector3.UnitY), 5, box);
        hit.Hit.Should().BeTrue();
        hit.Body.Should().Be(floor, "a wheel's ray inside a gate's sensor reaches the ground");
        hit = GetRayCollisionPhysics(new Ray(new Vector3(3, 10, 3), -Vector3.UnitY), 20);
        hit.Hit.Should().BeTrue();
        hit.Body.Should().Be(floor, "and from above, the ray meets the floor");
    }

    [Fact]
    public void A_Box_Dropped_On_A_Floor_Lands_And_Reports_The_Contact()
    {
        var floor = CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10));
        var box = CreatePhysicsBox(new Vector3(0, 1.5f, 0), Vector3.One);

        RunUntil(() => IsPhysicsBodyHit(box)).Should().BeTrue("the box falls a unit onto the floor");
        GetPhysicsContacts().Should().Contain(c => (c.BodyA == box && c.BodyB == floor) || (c.BodyA == floor && c.BodyB == box));

        RunUntil(() => GetPhysicsBodyVelocity(box).Length() < 0.01f).Should().BeTrue("and comes to rest");
        GetPhysicsBodyPosition(box).Y.Should().BeApproximately(0.5f, 0.05f, "resting on the floor, half its size above it");
        GetPhysicsBodyTransform(box).Translation.Y.Should().BeApproximately(0.5f, 0.05f);
    }

    [Fact]
    public void A_Capsule_Lands_Upright_And_A_Trigger_It_Falls_Through_Reports_It()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10));
        var gate = CreatePhysicsTrigger(new Vector3(0, 2, 0), new Vector3(3, 0.5f, 3));
        var capsule = CreatePhysicsCapsule(new Vector3(0, 4, 0), 0.4f, 2);

        var entered = false;
        RunUntil(() =>
        {
            foreach (var contact in GetPhysicsContacts())
                if (contact.BodyA == gate || contact.BodyB == gate)
                {
                    entered = true;
                    var toward = contact.BodyA == capsule ? contact.Normal : -contact.Normal;
                    toward.Y.Should().BeGreaterThan(0.5f, "the trigger is under the falling capsule");
                }
            return entered && GetPhysicsBodyVelocity(capsule).Length() < 0.01f && GetPhysicsBodyPosition(capsule).Y < 1.5f;
        }).Should().BeTrue("the capsule falls through the trigger and comes to rest on the floor");
        GetPhysicsBodyPosition(capsule).Y.Should().BeApproximately(1, 0.05f, "two units tall, it stands with its middle a unit up");

        // Its top still reaches into the trigger, and moved aside it leaves it.
        var left = false;
        SetPhysicsBodyPosition(capsule, new Vector3(4, 1, 0));
        RunUntil(() =>
        {
            foreach (var contact in GetPhysicsContactsEnded())
                if ((contact.BodyA == gate && contact.BodyB == capsule) || (contact.BodyA == capsule && contact.BodyB == gate)) left = true;
            return left;
        }).Should().BeTrue("the capsule left the trigger");
    }

    [Fact]
    public void A_Ball_Lands_On_A_Model_Made_Level_Geometry_And_A_Rope_Holds_Another()
    {
        var ground = LoadModelFromMesh(GenMeshPlane(10, 10, 1, 1));
        CreatePhysicsStaticModel(ground, new Vector3(0, 1, 0), 2).IsValid.Should().BeTrue();
        var ball = CreatePhysicsSphere(new Vector3(0, 4, 0), 0.5f);
        var hook = CreatePhysicsKinematicBox(new Vector3(5, 6, 0), new Vector3(0.2f, 0.2f, 0.2f));
        var weight = CreatePhysicsSphere(new Vector3(5, 5, 0), 0.25f);
        var rope = CreatePhysicsDistanceJoint(hook, weight, new Vector3(5, 6, 0), new Vector3(5, 5, 0), 0, 2);

        var rested = RunUntil(() => GetPhysicsBodyVelocity(ball).Length() < 0.01f && GetPhysicsBodyPosition(ball).Y < 2);
        rested.Should().BeTrue("the ball comes to rest on the model");
        GetPhysicsBodyPosition(ball).Y.Should().BeApproximately(1.5f, 0.05f, "on the plane a unit up, its radius above it");
        GetPhysicsBodyPosition(weight).Y.Should().BeApproximately(4, 0.05f, "the rope holds the weight two units under the hook");
        IsPhysicsJointValid(rope).Should().BeTrue();
        DestroyPhysicsJoint(rope);
        IsPhysicsJointValid(rope).Should().BeFalse();
        UnloadModel(ground);
    }

    // A crate on a platform whose parent the program moves at 2 units a second, its pace measured
    // over a second of frames of each length, and over frames of two lengths taking turns.
    [Theory]
    [InlineData(144.0, 144.0)]
    [InlineData(75.0, 75.0)]
    [InlineData(60.0, 60.0)]
    [InlineData(50.0, 50.0)]
    [InlineData(40.0, 40.0)]
    [InlineData(30.0, 30.0)]
    [InlineData(20.0, 20.0)]
    [InlineData(100.0, 25.0)]
    [InlineData(144.0, 35.0)]
    public void A_Crate_On_A_Platform_Keeps_Its_Parents_Pace_At_Every_Frame_Rate(double fps, double otherFps)
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var time = GetApp().World.Resource<Time>();
        var carrier = ecs.Spawn();
        ecs.Add(carrier, new Transform(Vector3.Zero));
        var platform = ecs.Spawn();
        ecs.Add(platform, new Transform(new Vector3(0, 0.25f, 0)));
        ecs.Add(platform, Collider.Box(new Vector3(20, 0.5f, 6)));
        ecs.Add(platform, RigidBody.Kinematic);
        ecs.SetParent(platform, carrier);
        var crate = ecs.Spawn();
        ecs.Add(crate, new Transform(new Vector3(0, 1, 0)));
        ecs.Add(crate, Collider.Box(Vector3.One));
        ecs.Add(crate, RigidBody.Dynamic());

        var frame = 0;
        void Frame(bool carry)
        {
            time.FrameSeconds = 1 / (frame++ % 2 == 0 ? fps : otherFps);
            BeginDrawing();
            if (carry) ecs.GetRef<Transform>(carrier).Position.X += 2 * GetFrameTime();
            EndDrawing();
        }
        double Simulated(double seconds, bool carry)
        {
            var spent = 0.0;
            while (spent < seconds)
            {
                Frame(carry);
                spent += time.DeltaSeconds;
            }
            return spent;
        }
        float CrateX() => GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(crate)).X;

        Simulated(0.5, carry: false);
        Simulated(1, carry: true);
        var from = CrateX();
        var spent = Simulated(2, carry: true);
        var pace = (CrateX() - from) / spent;
        pace.Should().BeApproximately(2, 0.06f, $"the crate rides at its platform's pace, 2, at {fps} and {otherFps} frames a second");
    }

    [Theory]
    [InlineData(144.0)]
    [InlineData(30.0)]
    public void A_Parent_Moved_In_The_Fixed_Steps_Is_Followed_A_Steps_Distance_A_Step(double fps)
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        GetApp().World.Resource<Time>().FrameSeconds = 1 / fps;
        var carrier = ecs.Spawn();
        ecs.Add(carrier, new Transform(Vector3.Zero));
        var platform = ecs.Spawn();
        ecs.Add(platform, new Transform(Vector3.Zero));
        ecs.Add(platform, Collider.Box(new Vector3(4, 0.5f, 4)));
        ecs.Add(platform, RigidBody.Kinematic);
        ecs.SetParent(platform, carrier);
        // Moved 2 units a second a step at a time, as an [OnFixedUpdate] behavior moves it.
        GetApp().AddSystem(Stage.FixedUpdate, new SystemDescriptor(w =>
        {
            var step = (float)w.Resource<FixedTime>().StepSeconds;
            w.Resource<EcsWorld>().GetRef<Transform>(carrier).Position.X += 2 * step;
        }, "MoveCarrierInSteps").Write<EcsWorld>());

        var gap = 0f;
        RunUntil(() =>
        {
            var behind = ecs.GetReadOnly<Transform>(carrier).Position.X - GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(platform)).X;
            gap = MathF.Max(gap, MathF.Abs(behind));
            return false;
        }, (int)fps);
        gap.Should().BeLessThan(2f / 60 + 1e-3f, "the platform is never more than a step's travel from its parent");
    }

    [Theory]
    [InlineData(5f, true)]
    [InlineData(150f, false)]
    public void A_Parent_Said_To_Be_Placed_Or_Put_Past_The_Setting_Places_Its_Platform_And_Flings_Nothing(float away, bool said)
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var carrier = ecs.Spawn();
        ecs.Add(carrier, new Transform(Vector3.Zero));
        var platform = ecs.Spawn();
        ecs.Add(platform, new Transform(Vector3.Zero));
        ecs.Add(platform, Collider.Box(new Vector3(4, 0.5f, 4)));
        ecs.Add(platform, RigidBody.Kinematic);
        ecs.SetParent(platform, carrier);
        var crate = ecs.Spawn();
        ecs.Add(crate, new Transform(new Vector3(0, 0.75f, 0)));
        ecs.Add(crate, Collider.Box(Vector3.One));
        ecs.Add(crate, RigidBody.Dynamic());
        Frames(30);

        // A level started again: the carrier put back a few units away, which the program says, or
        // farther than PhysicsSettings.PlaceBeyond, which says it alone.
        RunUntil(() =>
        {
            ecs.GetRef<Transform>(carrier).Position = new Vector3(away, 0, 0);
            if (said) GetApp().World.Resource<PhysicsWorld>().MarkPlaced(ecs.Handle(carrier));
            return true;
        });
        Frames(3);
        var body = ecs.GetReadOnly<PhysicsBody>(platform);
        GetPhysicsBodyPosition(body).X.Should().BeApproximately(away, 0.01f, "the platform is put at its parent's new place");
        GetPhysicsBodyVelocity(body).Length().Should().BeLessThan(0.01f, "and is at rest there");
        GetPhysicsBodyVelocity(ecs.GetReadOnly<PhysicsBody>(crate)).Length().Should().BeLessThan(1, "the crate that stood on it is not flung after it");
    }

    [Fact]
    public void A_Fast_Platform_Carries_Its_Crate_Through_A_Frame_Of_A_Quarter_Second()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var time = GetApp().World.Resource<Time>();
        var carrier = ecs.Spawn();
        ecs.Add(carrier, new Transform(Vector3.Zero));
        var platform = ecs.Spawn();
        ecs.Add(platform, new Transform(new Vector3(0, 0.25f, 0)));
        ecs.Add(platform, Collider.Box(new Vector3(8, 0.5f, 8)));
        ecs.Add(platform, RigidBody.Kinematic);
        ecs.SetParent(platform, carrier);
        var crate = ecs.Spawn();
        ecs.Add(crate, new Transform(new Vector3(0, 1, 0)));
        ecs.Add(crate, Collider.Box(Vector3.One));
        ecs.Add(crate, RigidBody.Dynamic());
        Frames(30);
        var crateBody = ecs.GetReadOnly<PhysicsBody>(crate);
        float Ahead() => GetPhysicsBodyPosition(crateBody).X - ecs.GetReadOnly<Transform>(carrier).Position.X;

        // Brought up to 60 units a second slowly enough that the crate's friction keeps it with
        // the platform, then held there.
        var speed = 0f;
        void Frame()
        {
            BeginDrawing();
            speed = MathF.Min(60, speed + 4 * GetFrameTime());
            ecs.GetRef<Transform>(carrier).Position.X += speed * GetFrameTime();
            EndDrawing();
        }
        while (speed < 60) Frame();
        for (int i = 0; i < 30; i++) Frame();
        var before = Ahead();

        // One frame of a quarter second, the most a frame counts for, in which the platform goes
        // fifteen units.
        time.FrameSeconds = 0.25;
        Frame();
        time.FrameSeconds = 1.0 / 60;
        // Placed rather than followed, as at ten units, the platform stood still for the next
        // step and then caught up at twice its speed.
        var slowest = float.MaxValue;
        var platformBody = ecs.GetReadOnly<PhysicsBody>(platform);
        for (int i = 0; i < 30; i++)
        {
            slowest = MathF.Min(slowest, GetPhysicsBodyVelocity(platformBody).X);
            Frame();
        }

        slowest.Should().BeApproximately(60, 0.5f, "the platform kept its parent's pace through the long frame and after it");

        GetPhysicsBodyPosition(crateBody).Y.Should().BeGreaterThan(0.9f, "the crate is still on the platform");
        Ahead().Should().BeApproximately(before, 0.25f, "the crate rode the platform through the long frame and was not left behind");
    }

    [Fact]
    public void A_Platform_Under_A_Moving_Parent_Carries_A_Crate_And_A_Character()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        // A carrier entity moved along +X by the program, with a kinematic platform under it.
        var carrier = ecs.Spawn();
        ecs.Add(carrier, new Transform(Vector3.Zero));
        var platform = ecs.Spawn();
        ecs.Add(platform, new Transform(new Vector3(0, 0.25f, 0)));
        ecs.Add(platform, Collider.Box(new Vector3(6, 0.5f, 6)));
        ecs.Add(platform, RigidBody.Kinematic);
        ecs.SetParent(platform, carrier);
        var crate = ecs.Spawn();
        ecs.Add(crate, new Transform(new Vector3(-1.5f, 1, 0)));
        ecs.Add(crate, Collider.Box(Vector3.One));
        ecs.Add(crate, RigidBody.Dynamic());
        var walker = ecs.Spawn();
        ecs.Add(walker, new Transform(new Vector3(1.5f, 1.4f, 0)));
        ecs.Add(walker, Collider.Capsule(0.3f, 1.8f));
        ecs.Add(walker, RigidBody.Dynamic(80));
        ecs.Add(walker, CharacterController.Default);

        // Settled first, then carried along X at two units a second for a second and a half.
        var clock = 0f;
        RunUntil(() => (clock += GetFrameTime()) > 0.5f);
        var crateFrom = ecs.GetReadOnly<Transform>(crate).Position.X;
        var walkerFrom = ecs.GetReadOnly<Transform>(walker).Position.X;
        clock = 0;
        // The crate's pace over the last half second, an average, since a frame slow under load
        // moves the carrier in one step and the crate's speed at any one instant with it.
        float? crateAtSecond = null;
        var carried = 0f;
        RunUntil(() =>
        {
            ecs.GetRef<Transform>(carrier).Position.X += 2 * GetFrameTime();
            clock += GetFrameTime();
            if (clock >= 1 && crateAtSecond is null) (crateAtSecond, carried) = (ecs.GetReadOnly<Transform>(crate).Position.X, clock);
            return clock > 1.5f;
        });
        var pace = (ecs.GetReadOnly<Transform>(crate).Position.X - crateAtSecond!.Value) / (clock - carried);

        var moved = ecs.GetReadOnly<Transform>(carrier).Position.X;
        moved.Should().BeGreaterThan(2.5f);
        var platformAt = GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(platform));
        platformAt.X.Should().BeApproximately(moved, 0.1f, "the platform followed its parent");
        // The crate is carried by friction, so it takes a moment to catch up with a platform that
        // starts at once, and then keeps its pace. The character walks relative to what it stands
        // on, so it keeps the pace from the start.
        pace.Should().BeApproximately(2, 0.1f, "the crate moves with the platform");
        (ecs.GetReadOnly<Transform>(crate).Position.X - crateFrom).Should().BeGreaterThan(moved * 0.6f, "it rode most of the way");
        (ecs.GetReadOnly<Transform>(walker).Position.X - walkerFrom).Should().BeApproximately(moved, 0.15f, "the character rode the whole way");
        ecs.GetReadOnly<Transform>(platform).Position.Should().Be(new Vector3(0, 0.25f, 0), "the platform's own transform is its place under the parent");
    }
}
