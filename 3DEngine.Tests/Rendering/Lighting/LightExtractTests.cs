using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Lighting;

[Trait("Category", "Unit")]
public class LightExtractTests
{
    private static (World world, EcsWorld ecs, RenderWorld render) NewWorlds()
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        return (world, ecs, new RenderWorld());
    }

    private static RenderLight Only(RenderWorld render) => render.Entities.Query<RenderLight>().Should().ContainSingle().Subject.Item2;

    [Fact]
    public void A_Light_Is_Extracted_With_Its_Color_Times_Intensity_And_Its_Pose()
    {
        var (world, ecs, render) = NewWorlds();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Transform(new Vector3(1f, 2f, 3f)));
        ecs.Add(entity, Light.Point(new Vector3(1f, 0.5f, 0.25f), 4f, range: 10f));

        new LightExtract().Run(world, render);

        var light = Only(render);
        light.MainEntityId.Should().Be(entity);
        light.Kind.Should().Be(LightKind.Point);
        light.Position.Should().Be(new Vector3(1f, 2f, 3f));
        light.EmittedColor.Should().Be(new Vector3(4f, 2f, 1f));
        light.Range.Should().Be(10f);
        render.TryGet<RenderLights>()!.All.Should().ContainSingle();
    }

    [Fact]
    public void A_Spots_Angles_Become_Cosines_With_The_Inner_Kept_Inside_The_Outer()
    {
        var (world, ecs, render) = NewWorlds();
        var entity = ecs.Spawn();
        ecs.Add(entity, Light.Spot(Vector3.One, 1f, innerAngle: 50f, outerAngle: 30f));

        new LightExtract().Run(world, render);

        var light = Only(render);
        light.CosOuter.Should().BeApproximately(MathF.Cos(float.DegreesToRadians(30)), 1e-6f);
        light.CosInner.Should().Be(light.CosOuter, "an inner angle past the outer one is taken as the outer");
    }

    [Fact]
    public void A_Lights_Direction_Is_Its_Entitys_Minus_Z()
    {
        var (world, ecs, render) = NewWorlds();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        ecs.Add(entity, Light.Directional(Vector3.One, 1f));

        new LightExtract().Run(world, render);

        var direction = Only(render).Direction;
        Vector3.Distance(direction, -Vector3.UnitX).Should().BeLessThan(1e-5f, "-Z turned a quarter about Y points along -X");
    }

    [Fact]
    public void ClearEntities_Despawns_The_Frames_Lights()
    {
        var (world, ecs, render) = NewWorlds();
        ecs.Add(ecs.Spawn(), Light.Ambient(Vector3.One, 0.2f));
        new LightExtract().Run(world, render);

        render.ClearEntities();

        render.Entities.Query<RenderLight>().Should().BeEmpty();
    }
}
