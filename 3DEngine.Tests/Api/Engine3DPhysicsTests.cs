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
public sealed class Engine3DPhysicsTests : IDisposable
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
        hit.Body.Should().Be(floor, "and from above, the floor is what the ray meets");
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

    [Fact]
    public void A_Ray_Finds_The_Body_In_Its_Way()
    {
        var box = CreatePhysicsStaticBox(new Vector3(0, 0, -5), Vector3.One);
        BeginDrawing();
        EndDrawing();

        var hit = GetRayCollisionPhysics(new Ray(Vector3.Zero, -Vector3.UnitZ), 100);
        hit.Hit.Should().BeTrue();
        hit.Body.Should().Be(box);
        hit.Distance.Should().BeApproximately(4.5f, 0.01f, "the box's near face is half a unit before its center");
        GetRayCollisionPhysics(new Ray(Vector3.Zero, Vector3.UnitZ), 100).Hit.Should().BeFalse("nothing is behind");
    }

    [Fact]
    public void A_Ball_Cast_Meets_What_A_Ray_Beside_It_Would_Miss()
    {
        // A wall a unit high whose near face is 4.5 ahead, a trigger before it, and a box to look past.
        var wall = CreatePhysicsStaticBox(new Vector3(0, 0.5f, -5), new Vector3(10, 1, 1));
        CreatePhysicsTrigger(new Vector3(0, 0.5f, -2), new Vector3(10, 1, 1));
        var near = CreatePhysicsStaticBox(new Vector3(3, 0.5f, -1), Vector3.One);
        BeginDrawing();
        EndDrawing();

        // Just over the wall's top a ray passes, and a ball of half a unit does not.
        var over = new Ray(new Vector3(0, 1.3f, 0), -Vector3.UnitZ);
        GetRayCollisionPhysics(over, 100).Hit.Should().BeFalse("the ray clears the wall");
        var hit = GetSphereCastPhysics(over, 0.5f, 100);
        hit.Hit.Should().BeTrue("the ball is too thick to clear it");
        hit.Body.Should().Be(wall, "and goes through the trigger, as a ray does");
        hit.Point.Y.Should().BeApproximately(1, 0.01f, "it touches the wall's top edge");

        // Straight at the face it stops with its middle half a unit short, facing back.
        var level = GetSphereCastPhysics(new Ray(new Vector3(0, 0.5f, 0), -Vector3.UnitZ), 0.5f, 100);
        level.Distance.Should().BeApproximately(4, 0.01f, "the face is 4.5 ahead and the ball half a unit wide");
        level.Normal.Z.Should().BeApproximately(1, 0.01f, "the face looks back at it");
        GetSphereCastPhysics(new Ray(new Vector3(0, 0.5f, 0), -Vector3.UnitZ), 0.5f, 3).Hit.Should().BeFalse("short of the wall it meets nothing");

        // Starting inside a body it meets that at 0, and past it what is beyond.
        var start = new Ray(new Vector3(3, 0.5f, -1), Vector3.UnitZ * -1);
        GetSphereCastPhysics(start, 0.2f, 100).Distance.Should().Be(0, "it starts inside the box");
        GetSphereCastPhysicsEx(start, 0.2f, 100, near).Body.Should().Be(wall, "looking past the box it starts in");
    }

    [Fact]
    public void The_Bodies_In_A_Sphere_Are_Those_It_Reaches_Past_Triggers()
    {
        SetPhysicsGravity(Vector3.Zero);
        var close = CreatePhysicsSphere(new Vector3(1, 0, 0), 0.5f);
        var touching = CreatePhysicsBox(new Vector3(0, 0, -2.4f), Vector3.One);
        var far = CreatePhysicsSphere(new Vector3(5, 0, 0), 0.5f);
        var trigger = CreatePhysicsTrigger(new Vector3(0, 0, 1), Vector3.One);
        // Its bounds reach into the sphere's corner and its shape, 2.2 from the middle, does not.
        var beside = CreatePhysicsSphere(new Vector3(1.9f, 1.9f, 0), 0.5f);
        BeginDrawing();
        EndDrawing();

        var found = GetPhysicsBodiesInSphere(Vector3.Zero, 2);
        found.Should().Contain(close).And.Contain(touching, "a box whose face is within the sphere");
        found.Should().NotContain(far).And.NotContain(trigger, "triggers are left out, as rays pass through them");
        found.Should().NotContain(beside, "a body is found by its shape, not its bounds");
        found.Should().OnlyHaveUniqueItems();
        GetPhysicsBodiesInSphere(new Vector3(20, 0, 0), 1).Should().BeEmpty("nothing is there");
    }

    [Fact]
    public void Bodies_On_Layers_That_Do_Not_Collide_Pass_Through_Each_Other_And_Report_Nothing()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
        // A ball dropped onto another on a layer it does not collide with, and a box on a layer the
        // floor's does not collide with.
        SetPhysicsLayersCollide(1, 2, false);
        SetPhysicsLayersCollide(3, 0, false);
        var under = CreatePhysicsSphere(new Vector3(0, 0.5f, 0), 0.5f, mass: 50);
        var over = CreatePhysicsSphere(new Vector3(0, 3, 0), 0.5f);
        SetPhysicsBodyLayer(under, 1);
        SetPhysicsBodyLayer(over, 2);
        GetPhysicsBodyLayer(over).Should().Be(2);
        var sinking = CreatePhysicsBox(new Vector3(5, 1, 0), Vector3.One);
        SetPhysicsBodyLayer(sinking, 3);

        RunUntil(() => GetPhysicsBodyPosition(sinking).Y < -3).Should().BeTrue("the box falls through a floor its layer does not collide with");
        GetPhysicsBodyPosition(over).Y.Should().BeLessThan(0.7f, "the ball falls through the other to the floor");
        GetPhysicsContacts().Any(c => (c.BodyA == over && c.BodyB == under) || (c.BodyA == under && c.BodyB == over)).Should().BeFalse();

        // A ray sees every layer, and one cast past a body only what that body's layer collides with.
        var down = new Ray(new Vector3(0, 5, 0), -Vector3.UnitY);
        var past = CreatePhysicsSphere(new Vector3(0, 5, 0), 0.2f);
        SetPhysicsBodyLayer(past, 2);
        SetPhysicsBodyVelocity(past, Vector3.Zero);
        GetRayCollisionPhysicsEx(down, 20, past).Body.Should().NotBe(under, "a ray past a body on layer 2 does not see layer 1");
        var below = GetRayCollisionPhysics(new Ray(new Vector3(0, 4, 0), -Vector3.UnitY), 20).Body;
        (below == under || below == over).Should().BeTrue("a ray past no body sees a ball of either layer");
    }

    [Fact]
    public void A_Trigger_On_A_Layer_Reports_Only_The_Layers_It_Collides_With()
    {
        SetPhysicsGravity(Vector3.Zero);
        SetPhysicsLayersCollide(5, 0, false);
        var sensor = CreatePhysicsTrigger(Vector3.Zero, new Vector3(4, 4, 4));
        SetPhysicsBodyLayer(sensor, 5);
        SetPhysicsLayersCollide(5, 6, true);
        // Each in a lane of its own through the trigger, so they do not meet each other.
        var crate = CreatePhysicsBox(new Vector3(-3, 0, 1.2f), Vector3.One);
        var player = CreatePhysicsBox(new Vector3(3, 0, -1.2f), Vector3.One);
        SetPhysicsBodyLayer(player, 6);
        SetPhysicsBodyVelocity(crate, new Vector3(4, 0, 0));
        SetPhysicsBodyVelocity(player, new Vector3(-4, 0, 0));

        var seen = new HashSet<PhysicsBody>();
        RunUntil(() =>
        {
            foreach (var contact in GetPhysicsContacts())
                if (contact.BodyA == sensor || contact.BodyB == sensor) seen.Add(contact.BodyA == sensor ? contact.BodyB : contact.BodyA);
            return GetPhysicsBodyPosition(player).X < -2;
        }).Should().BeTrue();
        seen.Should().Contain(player, "the player's layer collides with the trigger's").And.NotContain(crate, "the crate's does not");
    }

    [Theory]
    [InlineData("the crate's layer")]
    [InlineData("the floor's layer")]
    [InlineData("which layers collide")]
    [InlineData("the floor a trigger")]
    [InlineData("the crate a trigger")]
    public void A_Crate_Asleep_On_A_Floor_Falls_Once_A_Change_Lets_It_Through(string change)
    {
        var floor = CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10));
        var crate = CreatePhysicsBox(new Vector3(0, 0.5f, 0), Vector3.One);
        SetPhysicsLayersCollide(1, 2, false);
        RunUntil(() => !crate.IsAwake, 600).Should().BeTrue("a crate at rest on a floor goes to sleep");

        // A pair that sleeps is tested again only once something wakes it, which the change does.
        switch (change)
        {
            case "the crate's layer":
                SetPhysicsBodyLayer(floor, 1);
                RunUntil(() => !crate.IsAwake, 600).Should().BeTrue("the floor moving to a layer the crate still collides with leaves it resting");
                SetPhysicsBodyLayer(crate, 2);
                break;
            case "the floor's layer":
                SetPhysicsBodyLayer(crate, 1);
                RunUntil(() => !crate.IsAwake, 600).Should().BeTrue();
                SetPhysicsBodyLayer(floor, 2);
                break;
            case "which layers collide":
                SetPhysicsBodyLayer(crate, 3);
                RunUntil(() => !crate.IsAwake, 600).Should().BeTrue();
                SetPhysicsLayersCollide(3, 0, false);
                break;
            case "the floor a trigger":
                SetPhysicsBodyTrigger(floor, true);
                break;
            case "the crate a trigger":
                SetPhysicsBodyTrigger(crate, true);
                break;
        }

        RunUntil(() => GetPhysicsBodyPosition(crate).Y < -1).Should().BeTrue("the crate falls through a floor it no longer meets when {0} changes", change);
    }

    [Fact]
    public void A_Character_Falls_Through_A_Platform_Its_Layer_Does_Not_Collide_With()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
        var platform = CreatePhysicsStaticBox(new Vector3(0, 2.5f, 0), new Vector3(4, 1, 4));
        SetPhysicsBodyLayer(platform, 8);
        SetPhysicsLayersCollide(8, 9, false);
        var character = CreatePhysicsCharacter(new Vector3(0, 3.1f, 0), 0.4f, 1.8f);
        SetPhysicsBodyLayer(character, 9);

        RunUntil(() => GetPhysicsBodyPosition(character).Y < 1.2f && IsPhysicsCharacterGrounded(character)).Should()
            .BeTrue("it stands on the floor under the platform, whose layer it passes through");
    }

    [Fact]
    public void A_Fast_Ball_Crosses_A_Thin_Wall_Unless_It_Is_Swept()
    {
        SetPhysicsGravity(Vector3.Zero);
        CreatePhysicsStaticBox(new Vector3(0, 0, -5), new Vector3(10, 10, 0.1f));
        var plain = CreatePhysicsSphere(new Vector3(-2, 0, 0), 0.05f, 0.01f);
        var swept = CreatePhysicsSphere(new Vector3(2, 0, 0), 0.05f, 0.01f);
        SetPhysicsBodyContinuous(swept, true);
        SetPhysicsBodyVelocity(plain, new Vector3(0, 0, -40));
        SetPhysicsBodyVelocity(swept, new Vector3(0, 0, -40));

        RunUntil(() => GetPhysicsBodyPosition(plain).Z < -8).Should().BeTrue("at 40 units a second the plain ball crosses the wall within a step");
        GetPhysicsBodyPosition(swept).Z.Should().BeGreaterThan(-5, "the swept one meets it");
    }

    [Fact]
    public void A_Slider_Is_Driven_Along_Its_Axis_To_Its_Limits_And_Keeps_Its_Line_And_Its_Turn()
    {
        // A lift's car on a frame that does not move, sliding up its axis and nothing else.
        var frame = CreatePhysicsKinematicBox(Vector3.Zero, new Vector3(2, 0.2f, 2));
        var car = CreatePhysicsBox(new Vector3(0, 1, 0), Vector3.One, mass: 10);
        var lift = CreatePhysicsSliderJoint(frame, car, Vector3.UnitY);
        SetPhysicsSliderLimits(lift, 0, 3);
        SetPhysicsSliderMotor(lift, 2, 2000);
        ApplyPhysicsImpulseAt(car, new Vector3(30, 0, 10), new Vector3(0.5f, 1.5f, 0.5f));

        RunUntil(() => GetPhysicsSliderPosition(lift) > 2.95f).Should().BeTrue("a positive speed drives it toward the axis's tip, up to its upper limit");
        Frames(30);
        GetPhysicsSliderPosition(lift).Should().BeApproximately(3, 0.05f, "the limit holds it against the motor");
        var at = GetPhysicsBodyPosition(car);
        new Vector2(at.X, at.Z).Length().Should().BeLessThan(0.02f, "pushed sideways it stays on its line");
        Quaternion.Dot(GetPhysicsBodyRotation(car), Quaternion.Identity).Should().BeGreaterThan(0.999f, "and twisted it does not turn");

        SetPhysicsSliderMotor(lift, -2, 2000);
        RunUntil(() => GetPhysicsSliderPosition(lift) < 0.05f).Should().BeTrue("driven back down to its lower limit");
        GetPhysicsBodyPosition(car).Y.Should().BeGreaterThan(0.9f, "and no lower, where it started");
    }

    [Fact]
    public void A_Resting_Box_Presses_Its_Floor_By_Its_Weight_Times_The_Step()
    {
        var floor = CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
        var light = CreatePhysicsBox(new Vector3(-3, 0.5f, 0), Vector3.One, mass: 1);
        var heavy = CreatePhysicsBox(new Vector3(3, 0.5f, 0), Vector3.One, mass: 10);
        var away = CreatePhysicsBox(new Vector3(0, 5, 20), Vector3.One);
        // Asked about every frame while they settle and fall asleep, as a pressure plate is, each
        // pair goes on being answered with what it pressed when it slept.
        var settled = 0;
        RunUntil(() =>
        {
            GetPhysicsContactImpulse(light, floor);
            GetPhysicsContactImpulse(floor, heavy);
            return ++settled > 90;
        });
        var physics = GetApp().World.Resource<PhysicsWorld>();
        physics.Simulation.Bodies[new BepuPhysics.BodyHandle(light.Handle)].Awake.Should().BeFalse("a second and a half at rest puts it to sleep");

        // At rest each is held up by its weight over a step of a sixtieth, 9.81 / 60 a unit of mass.
        GetPhysicsContactImpulse(light, floor).Should().BeApproximately(9.81f / 60, 0.03f);
        GetPhysicsContactImpulse(floor, heavy).Should().BeApproximately(98.1f / 60, 0.3f, "either order, ten times the mass ten times the push");
        GetPhysicsContactImpulse(light, heavy).Should().Be(0, "a pair not touching");
        GetPhysicsContactImpulse(away, floor).Should().Be(0);
    }

    [Fact]
    public void A_Crate_Dragged_And_Turned_Across_A_Plate_Presses_It_By_Its_Weight_Alone()
    {
        var plate = CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
        var crate = CreatePhysicsBox(new Vector3(-5, 0.5f, 0), Vector3.One, mass: 2);
        Frames(30);

        // Dragged at a steady 3 units a second and turned about the plate's normal, so friction
        // along the plate and the twist about its normal both work against it, and neither is a push.
        var pressed = new List<float>();
        RunUntil(() =>
        {
            SetPhysicsBodyVelocity(crate, new Vector3(3, GetPhysicsBodyVelocity(crate).Y, 0));
            SetPhysicsBodyAngularVelocity(crate, new Vector3(0, 2, 0));
            pressed.Add(GetPhysicsContactImpulse(crate, plate));
            return pressed.Count == 60;
        });

        pressed.Skip(10).Average().Should().BeApproximately(2 * 9.81f / 60, 0.03f, "it presses by its weight times the step, as it does at rest");
    }

    [Fact]
    public void An_Impulse_Moves_A_Body_And_Destroying_It_Removes_It()
    {
        SetPhysicsGravity(Vector3.Zero);
        var ball = CreatePhysicsSphere(Vector3.Zero, 0.5f, mass: 2);
        ApplyPhysicsImpulse(ball, new Vector3(4, 0, 0));

        GetPhysicsBodyVelocity(ball).X.Should().BeApproximately(2, 0.01f, "an impulse over the mass");
        DestroyPhysicsBody(ball);
        IsPhysicsBodyValid(ball).Should().BeFalse();
    }

    [Fact]
    public void A_Ray_Through_The_Middle_Of_The_Screen_Points_Where_The_Camera_Looks()
    {
        var camera = new Camera3D(new Vector3(0, 2, 10), new Vector3(0, 2, 0), Vector3.UnitY, 45);
        var ray = GetScreenToWorldRayEx(new Vector2(400, 225), camera, 800, 450);

        Vector3.Distance(ray.Direction, -Vector3.UnitZ).Should().BeLessThan(1e-4f);
        var up = GetScreenToWorldRayEx(new Vector2(400, 0), camera, 800, 450);
        up.Direction.Y.Should().BeGreaterThan(0, "the top of the screen is above the middle");
    }

    [Fact]
    public void The_Default_Plugins_Mark_No_Transform_Of_A_Static_Scene()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 2, 8)));
        var sun = ecs.Spawn();
        ecs.Add(sun, Light.Directional(Vector3.One, 1));
        ecs.Add(sun, new Transform(Vector3.Zero));
        var root = ecs.Spawn();
        ecs.Add(root, new Transform(Vector3.Zero));
        var child = ecs.Spawn();
        ecs.Add(child, new Transform(Vector3.UnitX));
        ecs.Add(child, new Mesh([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)]));
        ecs.Add(child, new Material(Vector4.One));
        ecs.SetParent(child, root);

        // A body with no gravity to fall by, which rests where it was made.
        SetPhysicsGravity(Vector3.Zero);
        var resting = ecs.Spawn();
        ecs.Add(resting, new Transform(new Vector3(0, 3, 0)));
        GetApp().World.Resource<PhysicsWorld>().CreateSphere(new Vector3(0, 3, 0), 0.5f, entityId: resting);

        for (int frame = 0; frame < 3; frame++)
        {
            BeginDrawing();
            EndDrawing();
        }

        ecs.AnyChanged<Transform>().Should().BeFalse("nothing in the scene moved, so no system wrote a transform");
        ecs.AnyChanged<GlobalTransform>().Should().BeFalse("and propagation recomputed no chain");
    }

    [Fact]
    public void A_Paused_Simulation_Holds_Every_Body_Still()
    {
        var box = CreatePhysicsBox(new Vector3(0, 5, 0), Vector3.One);
        SetPhysicsPaused(true);
        for (int frame = 0; frame < 20; frame++)
        {
            BeginDrawing();
            EndDrawing();
        }

        IsPhysicsPaused().Should().BeTrue();
        GetPhysicsBodyPosition(box).Y.Should().Be(5, "no step ran while paused");
        SetPhysicsPaused(false);
        RunUntil(() => GetPhysicsBodyPosition(box).Y < 4.9f).Should().BeTrue("it falls once the simulation runs again");
    }

    [Fact]
    public void A_Character_Walked_Through_The_Flat_API_Stops_At_A_Wall_And_Stands()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
        CreatePhysicsStaticBox(new Vector3(2, 1, 0), new Vector3(0.5f, 2, 10));
        var player = CreatePhysicsCharacter(Vector3.Zero, 0.4f, 1.8f);

        MovePhysicsCharacter(player, new Vector3(5, 0, 0));
        RunUntil(() => false);

        IsPhysicsCharacterGrounded(player).Should().BeTrue();
        GetPhysicsBodyPosition(player).X.Should().BeInRange(1.2f, 1.4f, "the wall's face is at 1.75 and the capsule 0.4 wide");
    }

    [Fact]
    public void A_Character_Controller_Component_Walks_Its_Body_And_Reports_Ground()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var physics = GetApp().World.Resource<PhysicsWorld>();
        physics.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
        var player = ecs.Spawn();
        ecs.Add(player, physics.CreateCharacter(new Vector3(0, 0.5f, 0), 0.4f, 1.8f, entityId: player));
        ecs.Add(player, new Transform(Vector3.Zero));
        ecs.Add(player, CharacterController.Default with { Velocity = new Vector3(0, 0, -2) });

        RunUntil(() => ecs.GetReadOnly<Transform>(player).Position.Z < -1).Should().BeTrue("the controller walks it along -Z");
        ecs.GetReadOnly<CharacterController>(player).Grounded.Should().BeTrue("and it stands on the floor");
    }

    [Fact]
    public void A_Flat_Walk_Of_A_Character_With_A_Controller_Goes_Through_The_Controller()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
        var player = ecs.Spawn();
        ecs.Add(player, new Transform(new Vector3(0, 0.9f, 0)));
        ecs.Add(player, Collider.Capsule(0.4f, 1.8f));
        ecs.Add(player, RigidBody.Dynamic(80));
        ecs.Add(player, CharacterController.Default);
        BeginDrawing();
        EndDrawing();
        var body = ecs.GetReadOnly<PhysicsBody>(player);

        MovePhysicsCharacter(body, new Vector3(3, 0, 0));

        ecs.GetReadOnly<CharacterController>(player).Velocity.Should().Be(new Vector3(3, 0, 0));
        RunUntil(() => GetPhysicsBodyPosition(body).X > 1).Should().BeTrue("the step walks it as the controller says");
    }

    [Fact]
    public void A_Character_Controller_Sets_Its_Step_Height_And_Crouches_And_Stands_By_Its_Height()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var physics = GetApp().World.Resource<PhysicsWorld>();
        physics.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(20, 0.5f, 20));
        // A step 0.6 high, higher than either character's radius of 0.4.
        physics.CreateStaticBox(new Vector3(3, 0.3f, 0), new Vector3(1, 0.3f, 10));
        int Character(float z, float stepHeight)
        {
            var entity = ecs.Spawn();
            ecs.Add(entity, physics.CreateCharacter(new Vector3(0, 0, z), 0.4f, 1.8f, entityId: entity));
            ecs.Add(entity, new Transform(new Vector3(0, 0.9f, z)));
            ecs.Add(entity, CharacterController.Default with { Velocity = new Vector3(2, 0, 0), StepHeight = stepHeight });
            return entity;
        }
        var low = Character(-2, 0);
        var high = Character(2, 0.7f);

        RunUntil(() => ecs.GetReadOnly<Transform>(high).Position.X > 2.5f).Should().BeTrue("the higher step height climbs onto the step");
        ecs.GetReadOnly<Transform>(high).Position.Y.Should().BeGreaterThan(1.4f, "and stands on it");
        ecs.GetReadOnly<Transform>(low).Position.X.Should().BeLessThan(1.7f, "the radius alone does not climb it");

        ecs.GetRef<CharacterController>(low).Velocity = Vector3.Zero;
        ecs.GetRef<CharacterController>(low).Height = 1;
        RunUntil(() => MathF.Abs(GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(low)).Y - 0.5f) < 0.02f)
            .Should().BeTrue("crouched to a unit, its middle is half a unit over its feet");
        ecs.GetRef<CharacterController>(low).Height = 1.8f;
        RunUntil(() => MathF.Abs(GetPhysicsBodyPosition(ecs.GetReadOnly<PhysicsBody>(low)).Y - 0.9f) < 0.02f)
            .Should().BeTrue("standing again, it is as tall as it was made");
    }

    [Fact]
    public void A_Vehicle_Settles_On_Its_Springs_Drives_Forward_Turns_Left_And_Brakes_To_A_Stop()
    {
        CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(400, 1, 400));
        var car = CreatePhysicsVehicle(new Vector3(0, 1.2f, 0), new Vector3(1.8f, 0.6f, 3.8f));
        // Frames of a sixtieth of a second, a step each.
        void Steps(int sixtieths) => Frames(sixtieths);

        // At rest it hangs on its four springs, every wheel on the ground, level.
        Steps(120);
        GetPhysicsBodyVelocity(car).Length().Should().BeLessThan(0.05f, "the springs and dampers settle");
        GetPhysicsVehicleWheels(car).Should().HaveCount(4).And.OnlyContain(w => w.Grounded);
        var restY = GetPhysicsBodyPosition(car).Y;
        restY.Should().BeInRange(0.6f, 1.2f, "held up by its wheels, not lying on the ground");
        Vector3.Transform(Vector3.UnitY, GetPhysicsBodyRotation(car)).Y.Should().BeGreaterThan(0.999f);

        // Throttle drives it the way it faces, -Z.
        SetPhysicsVehicleInput(car, 1, 0);
        Steps(120);
        GetPhysicsBodyPosition(car).Z.Should().BeLessThan(-5, "it drove forward");
        GetPhysicsBodyVelocity(car).Z.Should().BeLessThan(-5);

        // Steering left turns its heading toward -X.
        SetPhysicsVehicleInput(car, 0.5f, 1);
        Steps(60);
        Vector3.Transform(-Vector3.UnitZ, GetPhysicsBodyRotation(car)).X.Should().BeLessThan(-0.2f, "a positive steer turns left");

        // The brakes bring it to a stop, upright.
        SetPhysicsVehicleInput(car, 0, 0, brake: true);
        Steps(240);
        GetPhysicsBodyVelocity(car).Length().Should().BeLessThan(0.3f, "the brakes stop it");
        Vector3.Transform(Vector3.UnitY, GetPhysicsBodyRotation(car)).Y.Should().BeGreaterThan(0.99f, "and it did not roll over");
    }
}
