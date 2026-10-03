using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Models;

/// <summary>
/// Tests for <see cref="ModelLoadExtensions"/> and the <see cref="ModelSpawn"/>
/// factory: model-flavoured aliases over <see cref="SceneSpawnExtensions"/> /
/// <see cref="SceneSpawn"/>. Verifies the surface compiles and forwards correctly
/// without spinning up the full asset pipeline.
/// </summary>
[Trait("Category", "Unit")]
public class ModelLoadExtensionsTests
{
    private static SceneNode MakeMeshNode(string path = "/n")
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var n = new SceneNode { Name = "n", SourcePath = path };
        n.Components.Add(new SceneMeshPayload
        {
            Name = "n",
            Positions = positions,
            Indices = new[] { 0, 1, 2 },
        });
        return n;
    }

    [Fact]
    public void EcsCommands_SpawnModel_Aliases_SpawnScene()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();
        var handle = default(Handle<SceneAsset>);
        var settings = ModelSpawn.At(new Vector3(1, 2, 3));

        cmd.SpawnModel(handle, settings);
        cmd.Apply(ecs);

        var (_, req) = ecs.Query<SpawnSceneRequest>().Single();
        req.Handle.Should().Be(handle);
        req.Settings.Should().BeSameAs(settings);
    }

    [Fact]
    public void EcsWorld_SpawnModel_Synchronously_Spawns_Scene()
    {
        var ecs = new EcsWorld();
        var scene = new Scene { Name = "inline" };
        scene.Roots.Add(MakeMeshNode());

        var spawned = ecs.SpawnModel(scene);

        spawned.Should().HaveCount(1);
        ecs.Query<Mesh>().Should().HaveCount(1);
    }

    [Fact]
    public void ModelSpawn_At_Vector_Builds_Translation_Placement()
    {
        var s = ModelSpawn.At(new Vector3(4, 5, 6));
        Vector3.Transform(Vector3.Zero, s.Placement).Should().Be(new Vector3(4, 5, 6));
    }

    [Fact]
    public void ModelSpawn_At_Vector_And_Rotation_Composes_Placement()
    {
        var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        var s = ModelSpawn.At(Vector3.Zero, rot);
        var x = Vector3.Transform(Vector3.UnitX, s.Placement);

        x.X.Should().BeApproximately(0f, 1e-5f);
        x.Z.Should().BeApproximately(-1f, 1e-5f);
    }

    [Fact]
    public void ModelSpawn_With_Echoes_The_Supplied_Matrix()
    {
        var m = Matrix4x4.CreateScale(2f);
        ModelSpawn.With(m).Placement.Should().Be(m);
    }

    [Fact]
    public void ModelSpawn_WithPurposes_Sets_Include_Mask()
    {
        ModelSpawn.WithPurposes(ScenePurposeMask.Render).IncludePurposes
            .Should().Be(ScenePurposeMask.Render);
    }
}