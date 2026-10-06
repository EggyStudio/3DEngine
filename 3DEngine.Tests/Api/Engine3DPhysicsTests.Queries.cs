using System.Diagnostics;
using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

public sealed partial class Engine3DPhysicsTests
{
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
    [InlineData("the crate's layer", false)]
    [InlineData("the floor's layer", false)]
    [InlineData("which layers collide", false)]
    [InlineData("the floor a trigger", false)]
    [InlineData("the crate a trigger", false)]
    // A kinematic floor at rest, which Bepu puts to sleep in the set of what rests on it, as a
    // second crate coming to rest beside the first was found to join.
    [InlineData("the crate's layer", true)]
    [InlineData("the floor's layer", true)]
    [InlineData("which layers collide", true)]
    [InlineData("the floor a trigger", true)]
    [InlineData("the crate a trigger", true)]
    public void A_Crate_Asleep_On_A_Floor_Falls_Once_A_Change_Lets_It_Through(string change, bool kinematic)
    {
        var floor = kinematic
            ? CreatePhysicsKinematicBox(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10))
            : CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 1, 10));
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
        if (kinematic) GetPhysicsBodyPosition(floor).Y.Should().BeApproximately(-0.5f, 1e-3f, "a kinematic floor stays where it is");
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
}
