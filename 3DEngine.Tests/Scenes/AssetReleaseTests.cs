using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// A level's models and textures let go once no entity uses them, as a level streamed in near the
/// player and despawned behind needs, and kept while anything still does.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class AssetReleaseTests : IDisposable
{
    // A folder of the program's source folder, where the asset server reads from.
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "source", "release-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly App _app = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());

    public AssetReleaseTests()
    {
        Directory.CreateDirectory(_folder);
        foreach (var file in new[] { "torus.obj", "torus.mtl", "checker.png" })
            File.Copy(Path.Combine(CheatsheetTestsRoot(), "3DEngine.Examples", "resources", file), Path.Combine(_folder, file));
        _app.World.Resource<AssetRelease>().Grace = 0;
    }

    public void Dispose()
    {
        _app.Shutdown();
        Directory.Delete(_folder, recursive: true);
    }

    private static string CheatsheetTestsRoot() => Api.CheatsheetTests.RepoRoot();

    private EcsWorld Ecs => _app.World.Resource<EcsWorld>();

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _app.BeginFrame();
            _app.EndFrame();
            Thread.Sleep(2);
        }
    }

    private int Place()
    {
        var entity = Ecs.Spawn();
        Ecs.Add(entity, new Transform(Vector3.Zero));
        Ecs.Add(entity, new ModelRef { Path = Path.GetFileName(_folder) + "/torus.obj" });
        return entity;
    }

    // The textures the spawned materials name, once every one of them has loaded.
    private bool Textured(out AssetId texture)
    {
        texture = default;
        _app.World.TryGetResource<Assets<TextureAsset>>(out var textures);
        foreach (var (_, material) in Ecs.Query<Material>())
            if (material.BaseColorTexture.IsValid && textures is not null && textures.Contains(material.BaseColorTexture.Id))
            {
                texture = material.BaseColorTexture.Id;
                return true;
            }
        return false;
    }

    private bool Loaded<T>(AssetId id) => _app.World.TryGetResource<Assets<T>>(out var assets) && assets.Contains(id);

    [Fact]
    public void A_Model_And_Its_Texture_Are_Let_Go_When_The_Last_Entity_Using_Them_Is_Despawned()
    {
        var first = Place();
        var second = Place();
        for (int i = 0; i < 300 && !Textured(out _); i++) Frames(1);
        Textured(out var texture).Should().BeTrue("the torus loads with its checker texture");
        var model = _app.World.Resource<Assets<SceneAsset>>().Ids.Single();
        var release = _app.World.Resource<AssetRelease>();

        // One copy gone, the other still uses both.
        Ecs.DespawnRecursive(first);
        Frames(3);
        Loaded<SceneAsset>(model).Should().BeTrue("the second copy still names the model");
        Loaded<TextureAsset>(texture).Should().BeTrue("and its material the texture");

        // The last gone, both are let go.
        Ecs.DespawnRecursive(second);
        Frames(3);
        Loaded<SceneAsset>(model).Should().BeFalse("no entity names the model");
        Loaded<TextureAsset>(texture).Should().BeFalse("no material names the texture");
        release.Released.Should().Be(2);
        _app.World.Resource<AssetServer>().GetLoadState(model).Should().Be(LoadState.NotLoaded, "the server forgot it, and reads it again when asked");

        // Placed again, it is read again and drawn with its texture.
        Place();
        for (int i = 0; i < 300 && !Textured(out _); i++) Frames(1);
        Textured(out var again).Should().BeTrue();
        again.Should().NotBe(texture, "the texture was read again under a new id");
    }

    [Fact]
    public void A_Texture_The_Program_Loaded_Itself_Stays_When_The_Level_Lets_It_Go()
    {
        var entity = Place();
        for (int i = 0; i < 300 && !Textured(out _); i++) Frames(1);
        Textured(out var texture).Should().BeTrue();

        // The program asks for the same texture the level's material did, and keeps it.
        var server = _app.World.Resource<AssetServer>();
        var own = server.LoadTextureSrgb(Path.GetFileName(_folder) + "/checker.png", generateMips: true);
        own.Id.Should().Be(texture, "one file is one asset");

        Ecs.DespawnRecursive(entity);
        Frames(3);
        Loaded<TextureAsset>(texture).Should().BeTrue("the program's own load holds it");
    }

    [Fact]
    public void A_Prefab_Files_Parsed_Copy_Is_Dropped_When_No_Copy_Of_It_Is_Left()
    {
        var file = Path.Combine(_folder, "lamp.json");
        File.WriteAllText(file, """
            { "format": "3dengine-scene", "version": 1, "entities": [
              { "id": "a1", "name": "Lamp", "components": { "Transform": { "Position": [0, 1, 0] } } } ] }
            """);
        var copies = Enumerable.Range(0, 3).Select(_ =>
        {
            var entity = Ecs.Spawn();
            Ecs.Add(entity, new Transform(Vector3.Zero));
            Ecs.Add(entity, new SceneRef { Path = file });
            return entity;
        }).ToList();
        Frames(2);
        SceneRefSystem.IsParsed(file).Should().BeTrue("the copies share one parse");

        Ecs.DespawnRecursive(copies[0]);
        Ecs.DespawnRecursive(copies[1]);
        Frames(2);
        SceneRefSystem.IsParsed(file).Should().BeTrue("one copy is left");

        Ecs.DespawnRecursive(copies[2]);
        Frames(2);
        SceneRefSystem.IsParsed(file).Should().BeFalse("no copy is left");
    }

    [Fact]
    public void A_Probe_Placed_With_A_Model_Captures_Again_Once_The_Model_Has_Spawned()
    {
        // A room's prefab as a level places it, its model and its probe under one entity.
        var room = Ecs.Spawn();
        Ecs.Add(room, new Transform(Vector3.Zero));
        var model = Place();
        Ecs.SetParent(model, room);
        var probe = Ecs.Spawn();
        Ecs.Add(probe, new Transform(Vector3.Zero));
        Ecs.Add(probe, new ReflectionProbe(new Vector3(8)));
        Ecs.SetParent(probe, room);
        // Another model elsewhere, whose arrival is no reason for this probe to capture again.
        Place();

        for (int i = 0; i < 300 && !Textured(out _); i++) Frames(1);
        Textured(out _).Should().BeTrue("the models spawned");
        Frames(2);
        Ecs.GetReadOnly<ReflectionProbe>(probe).Capture.Should().Be(1, "the probe captures again once, now its room is there");
    }
}
