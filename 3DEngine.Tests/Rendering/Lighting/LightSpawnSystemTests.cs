using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Lighting;

[Trait("Category", "Unit")]
public class LightSpawnSystemTests
{
    private static (World world, EcsWorld ecs) NewWorld()
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        return (world, ecs);
    }

    [Fact]
    public void Spawn_Translates_Sphere_Payload_To_Light_Component_And_Removes_Payload()
    {
        var (world, ecs) = NewWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new SceneLightPayload
        {
            Name = "Key",
            Type = SceneLightType.Sphere,
            Color = new Vector3(1f, 0.5f, 0.25f),
            Intensity = 2f,
            Exposure = 1f,
            Radius = 0.5f,
        });

        LightSpawnSystem.Run(world);

        ecs.Has<SceneLightPayload>(entity).Should().BeFalse("payload is the idempotency marker");
        ecs.TryGet<Light>(entity, out var light).Should().BeTrue();
        light.Type.Should().Be(LightType.Sphere);
        light.Color.Should().Be(new Vector3(1f, 0.5f, 0.25f));
        light.Intensity.Should().Be(2f);
        light.Exposure.Should().Be(1f);
        light.Radius.Should().Be(0.5f);
        ecs.Has<LightShadow>(entity).Should().BeFalse();
        ecs.Has<LightShaping>(entity).Should().BeFalse();
    }

    [Fact]
    public void Spawn_Attaches_LightShadow_When_Payload_Authors_Shadow_Inputs()
    {
        var (world, ecs) = NewWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new SceneLightPayload
        {
            Type = SceneLightType.Distant,
            Shadow = new SceneLightShadow(Enable: true, Color: new Vector3(0.1f, 0.1f, 0.2f), Distance: 50f),
        });

        LightSpawnSystem.Run(world);

        ecs.TryGet<LightShadow>(entity, out var shadow).Should().BeTrue();
        shadow.Enable.Should().Be(true);
        shadow.Color.Should().Be(new Vector3(0.1f, 0.1f, 0.2f));
        shadow.Distance.Should().Be(50f);
    }

    [Fact]
    public void Spawn_Attaches_LightShaping_From_Convenience_Shortcuts()
    {
        var (world, ecs) = NewWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new SceneLightPayload
        {
            Type = SceneLightType.Sphere,
            ConeAngle = 45f,
            ConeSoftness = 0.2f,
        });

        LightSpawnSystem.Run(world);

        ecs.TryGet<LightShaping>(entity, out var shaping).Should().BeTrue();
        shaping.ConeAngle.Should().Be(45f);
        shaping.ConeSoftness.Should().Be(0.2f);
    }

    [Fact]
    public void Spawn_Empty_Shadow_Record_Does_Not_Attach_LightShadow()
    {
        // SceneLightShadow with all-null fields = "API applied but nothing authored";
        // mirror that by leaving the companion off so renderers see "default" semantics.
        var (world, ecs) = NewWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new SceneLightPayload
        {
            Type = SceneLightType.Sphere,
            Shadow = new SceneLightShadow(),
        });

        LightSpawnSystem.Run(world);

        ecs.Has<LightShadow>(entity).Should().BeFalse();
    }

    [Fact]
    public void Spawn_Maps_Dome_Texture_Format_Token_To_Enum()
    {
        var (world, ecs) = NewWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new SceneLightPayload
        {
            Type = SceneLightType.Dome,
            DomeTextureFormat = "latlong",
            DomeGuideRadius = 1000f,
        });

        LightSpawnSystem.Run(world);

        ecs.TryGet<Light>(entity, out var light).Should().BeTrue();
        light.DomeTextureFormat.Should().Be(DomeTextureFormat.LatLong);
        light.DomeGuideRadius.Should().Be(1000f);
    }

    [Fact]
    public void Spawn_Carries_Geometry_And_Portal_Paths_Across()
    {
        var (world, ecs) = NewWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new SceneLightPayload
        {
            Type = SceneLightType.Geometry,
            GeometryPaths = new[] { "/World/Mesh/Sphere" },
            PortalPaths = new[] { "/World/Lights/Portal1" },
            FilterPaths = new[] { "/World/Lights/Filter1" },
        });

        LightSpawnSystem.Run(world);

        ecs.TryGet<Light>(entity, out var light).Should().BeTrue();
        light.GeometryPaths.Should().BeEquivalentTo(new[] { "/World/Mesh/Sphere" });
        light.PortalPaths.Should().BeEquivalentTo(new[] { "/World/Lights/Portal1" });
        light.FilterPaths.Should().BeEquivalentTo(new[] { "/World/Lights/Filter1" });
    }

    [Fact]
    public void Spawn_Is_NoOp_When_World_Has_No_EcsWorld()
    {
        var world = new World();
        // Should not throw even with no EcsWorld resource.
        var act = () => LightSpawnSystem.Run(world);
        act.Should().NotThrow();
    }
}