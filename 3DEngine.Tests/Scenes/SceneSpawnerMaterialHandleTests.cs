using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Scenes;

/// <summary>
/// Verifies that <see cref="SceneSpawner"/> registers every <see cref="SceneMaterialPayload"/>
/// it encounters with the supplied <see cref="MaterialLibrary"/> and stamps the resulting
/// <see cref="MaterialHandle"/> on <see cref="Material.Handle"/>, with de-duplication by
/// <see cref="MaterialDescription.SourcePath"/>.
/// </summary>
[Trait("Category", "Unit")]
public class SceneSpawnerMaterialHandleTests
{
    [Fact]
    public void Spawn_Without_MaterialLibrary_Leaves_Handle_Default()
    {
        var ecs = new EcsWorld();
        var scene = MakeSingleMeshScene("/Looks/Red", new Vector4(1, 0, 0, 1));

        var spawned = SceneSpawner.Spawn(ecs, scene);

        ecs.TryGet(spawned[0], out Material mat).Should().BeTrue();
        mat.Handle.IsValid.Should().BeFalse();
        mat.Albedo.Should().Be(new Vector4(1, 0, 0, 1));
    }

    [Fact]
    public void Spawn_With_MaterialLibrary_Stamps_Valid_Handle_On_Material()
    {
        var ecs = new EcsWorld();
        var lib = new MaterialLibrary();
        var scene = MakeSingleMeshScene("/Looks/Red", new Vector4(1, 0, 0, 1));

        var spawned = SceneSpawner.Spawn(ecs, scene, materialLibrary: lib);

        ecs.TryGet(spawned[0], out Material mat).Should().BeTrue();
        mat.Handle.IsValid.Should().BeTrue();
        lib.Count.Should().Be(1);
        var desc = mat.Handle.GetDescription();
        desc.SourcePath.Should().Be("/Looks/Red");
        desc.BaseColorFactor.Should().Be(new Vector4(1, 0, 0, 1));
    }

    [Fact]
    public void Spawn_Two_Meshes_Sharing_SourcePath_Reuse_Same_Handle()
    {
        var ecs = new EcsWorld();
        var lib = new MaterialLibrary();
        var scene = new Scene { Name = "s" };
        scene.Roots.Add(MakeMeshNode("A", "/Looks/Brass"));
        scene.Roots.Add(MakeMeshNode("B", "/Looks/Brass"));

        var spawned = SceneSpawner.Spawn(ecs, scene, materialLibrary: lib);

        spawned.Should().HaveCount(2);
        ecs.TryGet(spawned[0], out Material mA);
        ecs.TryGet(spawned[1], out Material mB);
        mA.Handle.IsValid.Should().BeTrue();
        mA.Handle.Should().Be(mB.Handle, "CreateOrGet de-dupes by SourcePath");
        lib.Count.Should().Be(1);
    }

    [Fact]
    public void Spawn_Two_Meshes_Distinct_SourcePath_Allocate_Distinct_Handles()
    {
        var ecs = new EcsWorld();
        var lib = new MaterialLibrary();
        var scene = new Scene { Name = "s" };
        scene.Roots.Add(MakeMeshNode("A", "/Looks/Red"));
        scene.Roots.Add(MakeMeshNode("B", "/Looks/Blue"));

        SceneSpawner.Spawn(ecs, scene, materialLibrary: lib);

        lib.Count.Should().Be(2);
    }

    private static Scene MakeSingleMeshScene(string materialSourcePath, Vector4 baseColor)
    {
        var s = new Scene { Name = "s" };
        s.Roots.Add(MakeMeshNode("Root", materialSourcePath, baseColor));
        return s;
    }

    private static SceneNode MakeMeshNode(string name, string materialSourcePath, Vector4? baseColor = null)
    {
        return new SceneNode
        {
            Name = name,
            SourcePath = $"/{name}",
            Components =
            {
                new SceneMeshPayload
                {
                    Positions = new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY },
                    Indices = new[] { 0, 1, 2 },
                },
                new SceneMaterialPayload
                {
                    Name = name + "Mat",
                    SourcePath = materialSourcePath,
                    BaseColorFactor = baseColor ?? Vector4.One,
                },
            },
        };
    }
}

