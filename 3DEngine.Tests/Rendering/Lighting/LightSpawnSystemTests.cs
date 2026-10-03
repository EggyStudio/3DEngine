using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Lighting;

[Trait("Category", "Unit")]
public class LightSpawnSystemTests
{
    [Fact]
    public void A_Payload_Becomes_A_Light_And_Is_Removed()
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        var entity = ecs.Spawn();
        ecs.Add(entity, new SceneLightPayload
        {
            Kind = LightKind.Spot, Color = new Vector3(1, 0, 0), Intensity = 3, Range = 8, InnerAngle = 10, OuterAngle = 20, CastsShadows = true,
        });

        LightSpawnSystem.Run(world);

        ecs.Has<SceneLightPayload>(entity).Should().BeFalse();
        ecs.GetRef<Light>(entity).Should().Be(new Light
        {
            Kind = LightKind.Spot, Color = new Vector3(1, 0, 0), Intensity = 3, Range = 8, InnerAngle = 10, OuterAngle = 20, CastsShadows = true,
        });
    }

    [Fact]
    public void Without_An_Ecs_World_Nothing_Happens()
    {
        var act = () => LightSpawnSystem.Run(new World());
        act.Should().NotThrow();
    }
}
