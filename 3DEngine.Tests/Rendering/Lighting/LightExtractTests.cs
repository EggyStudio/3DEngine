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

    [Fact]
    public void Extract_Spawns_RenderLight_With_Premultiplied_Emission_And_Pose()
    {
        var (world, ecs, render) = NewWorlds();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Transform
        {
            Position = new Vector3(1f, 2f, 3f),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One,
        });
        ecs.Add(entity, new Light
        {
            Type = LightType.Sphere,
            Color = new Vector3(1f, 1f, 1f),
            Intensity = 4f,
            Exposure = 1f, // 2^1 = 2
            Radius = 0.25f,
        });

        new LightExtract().Run(world, render);

        var rendered = render.Entities.Query<RenderLight>().ToList();
        rendered.Should().HaveCount(1);
        var rl = rendered[0].Item2;
        rl.MainEntityId.Should().Be(entity);
        rl.Type.Should().Be(LightType.Sphere);
        rl.Position.Should().Be(new Vector3(1f, 2f, 3f));
        rl.EmittedColor.Should().Be(new Vector3(8f, 8f, 8f), "color * intensity * 2^exposure = 1 * 4 * 2");
        rl.Radius.Should().Be(0.25f);
        rl.CastsShadows.Should().BeTrue("default when no LightShadow component");
        rl.HasCone.Should().BeFalse();

        render.TryGet<RenderLights>().Should().NotBeNull();
        render.TryGet<RenderLights>()!.All.Should().HaveCount(1);
    }

    [Fact]
    public void Extract_Honors_LightShadow_Disable_And_Shaping_Cone()
    {
        var (world, ecs, render) = NewWorlds();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Light { Type = LightType.Sphere, Color = Vector3.One, Intensity = 1f });
        ecs.Add(entity, new LightShadow { Enable = false });
        ecs.Add(entity, new LightShaping { ConeAngle = 30f, ConeSoftness = 0.1f });

        new LightExtract().Run(world, render);

        var rl = render.Entities.Query<RenderLight>().Single().Item2;
        rl.CastsShadows.Should().BeFalse();
        rl.HasCone.Should().BeTrue();
        rl.ConeAngle.Should().Be(30f);
        rl.ConeSoftness.Should().Be(0.1f);
    }

    [Fact]
    public void ClearEntities_Despawns_RenderLight_Bucket()
    {
        var (world, ecs, render) = NewWorlds();
        var entity = ecs.Spawn();
        ecs.Add(entity, new Light { Type = LightType.Distant, Color = Vector3.One, Intensity = 1f });

        new LightExtract().Run(world, render);
        render.Entities.Count<RenderLight>().Should().Be(1);

        render.ClearEntities();
        render.Entities.Count<RenderLight>().Should().Be(0);
    }

    [Fact]
    public void Extract_Direction_Follows_Rotation_From_NegZ_Convention()
    {
        var (world, ecs, render) = NewWorlds();
        var entity = ecs.Spawn();
        // 90 deg around Y rotates -Z to -X.
        ecs.Add(entity, new Transform
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f),
            Scale = Vector3.One,
        });
        ecs.Add(entity, new Light { Type = LightType.Distant, Color = Vector3.One, Intensity = 1f });

        new LightExtract().Run(world, render);

        var rl = render.Entities.Query<RenderLight>().Single().Item2;
        rl.Direction.X.Should().BeApproximately(-1f, 1e-5f);
        rl.Direction.Y.Should().BeApproximately(0f, 1e-5f);
        rl.Direction.Z.Should().BeApproximately(0f, 1e-5f);
    }
}