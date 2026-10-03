using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Scenes;

/// <summary>
/// Pure-managed tests for <see cref="SceneSpawner"/> and the <see cref="SceneSpawnSystem"/>
/// driver. Builds <see cref="Scene"/> snapshots in code (no USD dependency) and asserts
/// the resulting ECS state.
/// </summary>
[Trait("Category", "Unit")]
public class SceneSpawnerTests
{
    [Fact]
    public void ComputeRootMatrix_YUp_Meters_Is_Identity()
    {
        var scene = new Scene { SourceCoordinateSystem = SceneCoordinateSystem.YUp, SourceMetersPerUnit = 1.0 };

        var m = SceneSpawner.ComputeRootMatrix(scene, SceneSpawnSettings.Default);

        m.Should().Be(Matrix4x4.Identity);
    }

    [Fact]
    public void ComputeRootMatrix_Centimeters_Scales_By_HundredthOfAUnit()
    {
        var scene = new Scene { SourceCoordinateSystem = SceneCoordinateSystem.YUp, SourceMetersPerUnit = 0.01 };

        var m = SceneSpawner.ComputeRootMatrix(scene, SceneSpawnSettings.Default);
        var transformed = Vector3.Transform(new Vector3(100f, 0, 0), m);

        // 100 source units (cm) at 0.01 mpu = 1 meter in target space.
        transformed.X.Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void ComputeRootMatrix_ZUp_To_YUp_Maps_PlusZ_To_PlusY()
    {
        var scene = new Scene { SourceCoordinateSystem = SceneCoordinateSystem.ZUp, SourceMetersPerUnit = 1.0 };

        var m = SceneSpawner.ComputeRootMatrix(scene, SceneSpawnSettings.Default);
        var up = Vector3.Transform(new Vector3(0, 0, 1), m);

        up.X.Should().BeApproximately(0f, 1e-5f);
        up.Y.Should().BeApproximately(1f, 1e-5f);
        up.Z.Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void Spawn_Empty_Scene_Returns_No_Entities()
    {
        var ecs = new EcsWorld();
        var scene = new Scene { Name = "empty" };

        var spawned = SceneSpawner.Spawn(ecs, scene);

        spawned.Should().BeEmpty();
    }

    [Fact]
    public void Spawn_Mesh_Node_Creates_Entity_With_Transform_Mesh_Material_And_SceneInstance()
    {
        var ecs = new EcsWorld();
        var scene = new Scene { Name = "s" };
        scene.Roots.Add(new SceneNode
        {
            Name = "Body",
            SourcePath = "/World/Body",
            LocalTransform = new Transform(new Vector3(2, 0, 0)),
            Components = { MakeTriangle() },
        });

        var spawned = SceneSpawner.Spawn(ecs, scene);

        spawned.Should().HaveCount(1);
        var e = spawned[0];
        ecs.Has<Transform>(e).Should().BeTrue();
        ecs.Has<Mesh>(e).Should().BeTrue();
        ecs.Has<Material>(e).Should().BeTrue();
        ecs.Has<SceneInstance>(e).Should().BeTrue();

        var t = ecs.GetRef<Transform>(e);
        t.Position.X.Should().BeApproximately(2f, 1e-5f);

        // De-indexed: 3 triangle indices -> 3 flat positions in the runtime Mesh.
        var mesh = ecs.GetRef<Mesh>(e);
        mesh.Positions.Should().HaveCount(3);

        var marker = ecs.GetRef<SceneInstance>(e);
        marker.SourcePath.Should().Be("/World/Body");
    }

    [Fact]
    public void Spawn_Skips_Nodes_Whose_Purpose_Is_Filtered_Out()
    {
        var ecs = new EcsWorld();
        var scene = new Scene { Name = "s" };
        scene.Roots.Add(new SceneNode
        {
            Name = "Guide",
            Purpose = ScenePurpose.Guide,
            Components = { MakeTriangle() },
        });
        scene.Roots.Add(new SceneNode
        {
            Name = "Render",
            Purpose = ScenePurpose.Render,
            Components = { MakeTriangle() },
        });

        // Default IncludePurposes is Runtime (Default | Render); guide is excluded.
        var spawned = SceneSpawner.Spawn(ecs, scene);

        spawned.Should().HaveCount(1);
        ecs.GetRef<SceneInstance>(spawned[0]).SourcePath.Should().NotContain("Guide");
    }

    [Fact]
    public void Spawn_Skips_Disabled_Nodes_But_Recurses_Into_Children()
    {
        var ecs = new EcsWorld();
        var parent = new SceneNode
        {
            Name = "P",
            Enabled = false,
            Components = { MakeTriangle() },
        };
        parent.Children.Add(new SceneNode
        {
            Name = "C",
            LocalTransform = new Transform(new Vector3(1, 0, 0)),
            Components = { MakeTriangle() },
        });
        var scene = new Scene();
        scene.Roots.Add(parent);

        var spawned = SceneSpawner.Spawn(ecs, scene);

        spawned.Should().HaveCount(1, "the disabled parent is skipped but its enabled child still spawns");
        ecs.GetRef<SceneInstance>(spawned[0]).SourcePath.Should().Be("/");
        // Child still got its parent's local transform composed with its own (parent
        // identity + child translate(1,0,0)).
        ecs.GetRef<Transform>(spawned[0]).Position.X.Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Spawn_Composes_Parent_World_Transform_For_Children()
    {
        var ecs = new EcsWorld();
        var parent = new SceneNode
        {
            Name = "P",
            LocalTransform = new Transform(new Vector3(10, 0, 0)),
            Components = { MakeTriangle() },
        };
        parent.Children.Add(new SceneNode
        {
            Name = "C",
            LocalTransform = new Transform(new Vector3(1, 0, 0)),
            Components = { MakeTriangle() },
        });
        var scene = new Scene();
        scene.Roots.Add(parent);

        var spawned = SceneSpawner.Spawn(ecs, scene);

        spawned.Should().HaveCount(2);
        var parentT = ecs.GetRef<Transform>(spawned[0]);
        var childT = ecs.GetRef<Transform>(spawned[1]);
        parentT.Position.X.Should().BeApproximately(10f, 1e-5f);
        childT.Position.X.Should().BeApproximately(11f, 1e-5f);
    }

    [Fact]
    public void Spawn_Uses_Material_Payload_When_Present_Else_DefaultAlbedo()
    {
        var ecs = new EcsWorld();
        var scene = new Scene();
        scene.Roots.Add(new SceneNode
        {
            Name = "Plain",
            Components = { MakeTriangle() },
        });
        scene.Roots.Add(new SceneNode
        {
            Name = "Painted",
            Components =
            {
                MakeTriangle(),
                new SceneMaterialPayload
                {
                    SourcePath = "/Looks/Red",
                    BaseColorFactor = new Vector4(1, 0, 0, 1),
                },
            },
        });

        var settings = new SceneSpawnSettings { DefaultAlbedo = new Vector4(0.5f, 0.5f, 0.5f, 1f) };
        var spawned = SceneSpawner.Spawn(ecs, scene, settings);

        spawned.Should().HaveCount(2);
        ecs.GetRef<Material>(spawned[0]).Albedo.Should().Be(new Vector4(0.5f, 0.5f, 0.5f, 1f));
        ecs.GetRef<Material>(spawned[1]).Albedo.Should().Be(new Vector4(1, 0, 0, 1));
    }

    [Fact]
    public void Spawn_Camera_Payload_Becomes_Camera_Component_With_FovY_From_Physical_Inputs()
    {
        var ecs = new EcsWorld();
        var scene = new Scene();
        scene.Roots.Add(new SceneNode
        {
            Name = "Cam",
            Components =
            {
                new SceneCameraPayload
                {
                    Projection = SceneProjection.Perspective,
                    VerticalAperture = 24f,
                    FocalLength = 50f,
                    NearClip = 0.5f,
                    FarClip = 250f,
                },
            },
        });

        var spawned = SceneSpawner.Spawn(ecs, scene);

        spawned.Should().HaveCount(1);
        var cam = ecs.GetRef<Camera>(spawned[0]);
        cam.Near.Should().Be(0.5f);
        cam.Far.Should().Be(250f);
        var expected = 2f * MathF.Atan(24f / (2f * 50f));
        cam.FovY.Should().BeApproximately(expected, 1e-5f);
    }

    [Fact]
    public void Spawn_AttachesSceneInstanceMarker_Carrying_AssetId()
    {
        var ecs = new EcsWorld();
        var scene = new Scene();
        scene.Roots.Add(new SceneNode
        {
            Name = "X",
            SourcePath = "/X",
            Components = { MakeTriangle() },
        });

        var spawned = SceneSpawner.Spawn(ecs, scene, settings: null, sceneAssetId: 1234);

        ecs.GetRef<SceneInstance>(spawned[0]).SceneAssetId.Should().Be(1234UL);
    }

    [Fact]
    public void Spawn_AttachSceneInstanceMarker_False_Skips_Marker()
    {
        var ecs = new EcsWorld();
        var scene = new Scene();
        scene.Roots.Add(new SceneNode
        {
            Name = "X",
            Components = { MakeTriangle() },
        });

        var settings = new SceneSpawnSettings { AttachSceneInstanceMarker = false };
        var spawned = SceneSpawner.Spawn(ecs, scene, settings);

        ecs.Has<SceneInstance>(spawned[0]).Should().BeFalse();
    }

    [Fact]
    public void SpawnSystem_Run_NoOp_When_No_Resources()
    {
        var world = new World();

        // Should not throw even with zero resources registered.
        SceneSpawnSystem.Run(world);
    }

    [Fact]
    public void Spawn_Material_With_Texture_Refs_Maps_BaseColorFactor_To_Albedo()
    {
        // Phase 5 contract: the runtime Material struct is Albedo-only. When a payload
        // carries texture references (BaseColor / MR / Normal / Emissive / Occlusion),
        // they ride along on the SceneMaterialPayload but the spawner still maps only
        // BaseColorFactor to Material.Albedo.
        SceneSpawner.ResetTextureWarningForTest();

        var ecs = new EcsWorld();
        var scene = new Scene();
        scene.Roots.Add(new SceneNode
        {
            Name = "Tex",
            Components =
            {
                MakeTriangle(),
                new SceneMaterialPayload
                {
                    SourcePath = "/Looks/Tex",
                    BaseColorFactor = new Vector4(0.2f, 0.4f, 0.6f, 1f),
                    BaseColorTexture = new SceneTextureRef("foo.png"),
                    NormalTexture = new SceneTextureRef("foo_n.png"),
                },
            },
        });

        var spawned = SceneSpawner.Spawn(ecs, scene);

        spawned.Should().HaveCount(1);
        ecs.GetRef<Material>(spawned[0]).Albedo
            .Should().Be(new Vector4(0.2f, 0.4f, 0.6f, 1f),
                "Albedo == BaseColorFactor; texture refs are ignored at v1");
    }

    [Fact]
    public void Spawn_With_Texture_Refs_Logs_Warning_Exactly_Once_Per_Process()
    {
        SceneSpawner.ResetTextureWarningForTest();
        SceneSpawner.TextureWarningEmittedForTest.Should().BeFalse("latch starts unset");

        var scene = new Scene();
        scene.Roots.Add(new SceneNode
        {
            Name = "A",
            Components =
            {
                MakeTriangle(),
                new SceneMaterialPayload { SourcePath = "/Looks/A", EmissiveTexture = new SceneTextureRef("a.png") },
            },
        });
        scene.Roots.Add(new SceneNode
        {
            Name = "B",
            Components =
            {
                MakeTriangle(),
                new SceneMaterialPayload { SourcePath = "/Looks/B", BaseColorTexture = new SceneTextureRef("b.png") },
            },
        });

        var ecs = new EcsWorld();
        SceneSpawner.Spawn(ecs, scene);

        SceneSpawner.TextureWarningEmittedForTest.Should().BeTrue("the first textured material trips the latch");

        // Spawn again with a fresh world; the latch must stay set (deduped per process).
        var ecs2 = new EcsWorld();
        SceneSpawner.Spawn(ecs2, scene);
        SceneSpawner.TextureWarningEmittedForTest.Should().BeTrue();
    }

    [Fact]
    public void Spawn_Material_Without_Texture_Refs_Does_Not_Trip_Warning()
    {
        SceneSpawner.ResetTextureWarningForTest();

        var ecs = new EcsWorld();
        var scene = new Scene();
        scene.Roots.Add(new SceneNode
        {
            Name = "Plain",
            Components =
            {
                MakeTriangle(),
                new SceneMaterialPayload { SourcePath = "/Looks/Plain", BaseColorFactor = new Vector4(1, 1, 1, 1) },
            },
        });

        SceneSpawner.Spawn(ecs, scene);

        SceneSpawner.TextureWarningEmittedForTest.Should().BeFalse(
            "factor-only materials do not trip the textures-ignored warning");
    }

    private static SceneMeshPayload MakeTriangle() => new()
    {
        Positions = new[] { new Vector3(0, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0) },
        Indices = new[] { 0, 1, 2 },
    };
}