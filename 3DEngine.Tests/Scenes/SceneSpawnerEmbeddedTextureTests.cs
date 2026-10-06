using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// A texture a model file carries inside itself reaches the asset server through the in-memory
/// source, so a spawned material can load it like a file.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SceneSpawnerEmbeddedTextureTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-embedded-");

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void An_Embedded_Png_Is_Published_And_Loads_As_The_Base_Color_Texture()
    {
        var png = _folder.File("red.png");
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
        var taken = new List<AssetId>();
        var spawned = SceneSpawner.SpawnTaking(ecs, scene, null, 0, server, "models/crate.glb", null, taken);

        ecs.TryGet(spawned[0], out Material material).Should().BeTrue();
        material.BaseColorTexture.Path.Path.Should().StartWith("__embedded__/models/crate.glb/0.png");
        var texture = server.LoadSync<TextureAsset>(material.BaseColorTexture.Path.ToString());
        (texture.Width, texture.Height).Should().Be((2, 1));
        texture.Pixels.Take(4).Should().Equal(255, 0, 0, 255);
        taken.Should().Equal([material.BaseColorTexture.Id], "the load is handed back for the caller to give back");
    }
}
