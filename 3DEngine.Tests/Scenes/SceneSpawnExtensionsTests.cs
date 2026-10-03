using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Scenes;

/// <summary>
/// Tests for <see cref="SceneSpawnExtensions"/> and the <see cref="SceneSpawn"/> factory:
/// the convenience layer that collapses
/// <c>server.Load + Cmd.Spawn(... new SpawnSceneRequest)</c> into a single call.
/// </summary>
[Trait("Category", "Unit")]
public class SceneSpawnExtensionsTests
{
    private static SceneNode MakeMeshNode(string path = "/n", Vector3? p = null)
    {
        var positions = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0) };
        var indices   = new[] { 0, 1, 2 };
        var n = new SceneNode { Name = "n", SourcePath = path };
        n.Components.Add(new SceneMeshPayload
        {
            Name = "n",
            Positions = positions,
            Indices = indices,
        });
        return n;
    }

    [Fact]
    public void EcsCommands_SpawnScene_Queues_Entity_With_SpawnSceneRequest()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();
        var handle = default(Handle<SceneAsset>);
        var settings = SceneSpawn.At(new Vector3(1, 2, 3));

        cmd.SpawnScene(handle, settings);
        cmd.Apply(ecs);

        var rows = ecs.Query<SpawnSceneRequest>().ToList();
        rows.Should().HaveCount(1);
        var (_, req) = rows[0];
        req.Handle.Should().Be(handle);
        req.Settings.Should().BeSameAs(settings);
    }

    [Fact]
    public void EcsCommands_SpawnScene_Without_Settings_Defaults_To_Null()
    {
        var ecs = new EcsWorld();
        var cmd = new EcsCommands();
        var handle = default(Handle<SceneAsset>);

        cmd.SpawnScene(handle);
        cmd.Apply(ecs);

        var (_, req) = ecs.Query<SpawnSceneRequest>().Single();
        req.Settings.Should().BeNull();
    }

    [Fact]
    public void EcsWorld_SpawnScene_Synchronously_Spawns_From_InMemory_Scene()
    {
        var ecs = new EcsWorld();
        var scene = new Scene { Name = "inline" };
        scene.Roots.Add(MakeMeshNode("/n"));

        var spawned = ecs.SpawnScene(scene);

        spawned.Should().HaveCount(1);
        ecs.Query<Mesh>().Should().HaveCount(1);
    }

    [Fact]
    public void SceneSpawn_At_Vector_Builds_Translation_Placement()
    {
        var s = SceneSpawn.At(new Vector3(1, 2, 3));

        var origin = Vector3.Transform(Vector3.Zero, s.Placement);
        origin.Should().Be(new Vector3(1, 2, 3));
    }

    [Fact]
    public void SceneSpawn_At_VectorAndRotation_Composes_Translation_After_Rotation()
    {
        var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        var s = SceneSpawn.At(new Vector3(0, 0, 0), rot);

        // Rotating +X by 90deg around Y maps it to -Z (right-handed).
        var x = Vector3.Transform(Vector3.UnitX, s.Placement);
        x.Z.Should().BeApproximately(-1f, 1e-5f);
        x.X.Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void SceneSpawn_With_Echoes_The_Supplied_Matrix()
    {
        var m = Matrix4x4.CreateScale(2f);

        SceneSpawn.With(m).Placement.Should().Be(m);
    }

    [Fact]
    public void SceneSpawn_WithPurposes_Sets_Include_Mask()
    {
        var s = SceneSpawn.WithPurposes(ScenePurposeMask.Render);

        s.IncludePurposes.Should().Be(ScenePurposeMask.Render);
    }
}