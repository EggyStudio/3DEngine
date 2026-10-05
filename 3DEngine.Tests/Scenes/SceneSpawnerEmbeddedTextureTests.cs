using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// A texture a model file carries inside itself reaches the asset server through the in-memory
/// source, so a spawned material can load it like a file.
/// </summary>
[Trait("Category", "Unit")]
public class SceneSpawnerEmbeddedTextureTests
{
    [Fact]
    public void An_Embedded_Png_Is_Published_And_Loads_As_The_Base_Color_Texture()
    {
        var png = Path.Combine(Directory.CreateTempSubdirectory("engine-embedded-").FullName, "red.png");
        PngWriter.Write(png, [255, 0, 0, 255, 255, 0, 0, 255], 2, 1);

        var registry = new TextureDecoderRegistry();
        registry.RegisterDecoder(new StbTextureDecoder());
        using var server = new AssetServer(1);
        server.AddSource(new InMemoryAssetReader(), "InMemory");
        server.RegisterLoader(new TextureAssetLoader(registry));

        var scene = new Scene { Name = "crate" };
        scene.EmbeddedTextures.Add(new SceneEmbeddedTexture(null, File.ReadAllBytes(png), "png", 0, 0, null));
        scene.Roots.Add(new SceneNode
        {
            Name = "Crate",
            SourcePath = "/Crate",
            Components =
            {
                new SceneMeshPayload { Positions = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY], Indices = [0, 1, 2] },
                new SceneMaterialPayload { Name = "Wood", SourcePath = "/Looks/Wood", BaseColorTexture = new SceneTextureRef("*0") },
            },
        });

        var ecs = new EcsWorld();
        var spawned = SceneSpawner.Spawn(ecs, scene, assetServer: server, sceneSourcePath: "models/crate.glb");

        ecs.TryGet(spawned[0], out Material material).Should().BeTrue();
        material.BaseColorTexture.Path.Path.Should().StartWith("__embedded__/models/crate.glb/0.png");
        var texture = server.LoadSync<TextureAsset>(material.BaseColorTexture.Path.ToString());
        (texture.Width, texture.Height).Should().Be((2, 1));
        texture.Pixels.Take(4).Should().Equal(255, 0, 0, 255);
    }
}
