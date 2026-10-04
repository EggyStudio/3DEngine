using System.Diagnostics;
using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>Physics through the flat API, in a headless app whose frames run the fixed steps.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class Engine3DPhysicsTests : IDisposable
{
    public Engine3DPhysicsTests()
    {
        var config = Config.Default with { Headless = true, HeadlessFps = 240 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // Runs frames until the condition holds or two seconds of real time pass.
    private static bool RunUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(2))
        {
            BeginDrawing();
            var done = condition();
            EndDrawing();
            if (done) return true;
        }
        return false;
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
    public void A_Ray_Finds_The_Body_In_Its_Way()
    {
        var box = CreatePhysicsStaticBox(new Vector3(0, 0, -5), Vector3.One);
        BeginDrawing();
        EndDrawing();

        GetRayCollisionPhysics(new Ray(Vector3.Zero, -Vector3.UnitZ), 100, out var hit).Should().BeTrue();
        hit.Body.Should().Be(box);
        hit.Distance.Should().BeApproximately(4.5f, 0.01f, "the box's near face is half a unit before its center");
        GetRayCollisionPhysics(new Ray(Vector3.Zero, Vector3.UnitZ), 100, out _).Should().BeFalse("nothing is behind");
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
}
