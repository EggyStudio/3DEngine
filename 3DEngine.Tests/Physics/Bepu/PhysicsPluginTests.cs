using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Physics;

[Trait("Category", "Integration")]
public class PhysicsPluginTests
{
    [Fact]
    public void Plugin_Inserts_PhysicsWorld_Resource()
    {
        using var app = new App();
        app.AddPlugin(new PhysicsPlugin());

        app.World.ContainsResource<PhysicsWorld>().Should().BeTrue();
    }

    [Fact]
    public void Plugin_Honours_PreInserted_PhysicsSettings()
    {
        using var app = new App();
        app.World.InsertResource(new PhysicsSettings { Gravity = new Vector3(0, -42f, 0) });

        app.AddPlugin(new PhysicsPlugin());

        app.World.Resource<PhysicsWorld>().Gravity.Y.Should().Be(-42f);
    }

    [Fact]
    public void Plugin_Registers_Step_System_In_PreUpdate()
    {
        using var app = new App();
        int before = app.Schedule.SystemCount(Stage.PreUpdate);

        app.AddPlugin(new PhysicsPlugin());

        app.Schedule.SystemCount(Stage.PreUpdate).Should().BeGreaterThan(before);
    }

    [Fact]
    public void Plugin_Registers_SyncTransforms_System_In_PostUpdate()
    {
        using var app = new App();
        int before = app.Schedule.SystemCount(Stage.PostUpdate);

        app.AddPlugin(new PhysicsPlugin());

        app.Schedule.SystemCount(Stage.PostUpdate).Should().BeGreaterThan(before);
    }

    [Fact]
    public void End_To_End_Falling_Body_Updates_Transform_Through_Schedule()
    {
        using var app = new App();
        app.World.InsertResource(new EcsWorld());
        var time = new Time();
        time.Update(0.0, 1.0 / 60.0);
        app.World.InsertResource(time);
        app.AddPlugin(new PhysicsPlugin());

        var ecs = app.World.Resource<EcsWorld>();
        var phys = app.World.Resource<PhysicsWorld>();

        int e = ecs.Spawn();
        ecs.Add(e, new Transform(new Vector3(0, 10, 0)));
        phys.CreateSphere(new Vector3(0, 10, 0), radius: 0.5f, entityId: e);

        for (int i = 0; i < 30; i++)
        {
            app.Schedule.RunStage(Stage.PreUpdate, app.World);
            app.Schedule.RunStage(Stage.PostUpdate, app.World);
        }

        ecs.GetRef<Transform>(e).Position.Y.Should().BeLessThan(10f);
    }
}

