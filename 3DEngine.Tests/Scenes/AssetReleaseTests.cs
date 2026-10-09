using System.Diagnostics;
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
    private readonly TestFolder _folder = TestFolder.At(Path.Combine(AppContext.BaseDirectory, "source", "release-" + Guid.NewGuid().ToString("N")[..8]));
    private readonly App _app = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());

    public AssetReleaseTests()
    {
        foreach (var file in new[] { "torus.obj", "torus.mtl", "checker.png" })
            File.Copy(Path.Combine(CheatsheetTestsRoot(), "3DEngine.Examples", "resources", file), _folder.File(file));
        _app.World.Resource<AssetRelease>().Grace = 0;
    }

    public void Dispose()
    {
        _app.Shutdown();
        _folder.Dispose();
    }

    private static string CheatsheetTestsRoot() => Api.CheatsheetTests.RepoRoot();

    private EcsWorld Ecs => _app.World.Resource<EcsWorld>();

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _app.BeginFrame();
            _app.EndFrame();
            // Models and textures load on the asset server's worker, outside the frame loop.
            Thread.Sleep(2);
        }
    }

    private int Place()
    {
        var entity = Ecs.Spawn();
        Ecs.Add(entity, new Transform(Vector3.Zero));
        Ecs.Add(entity, new ModelRef { Path = Path.GetFileName(_folder.Path) + "/torus.obj" });
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

    // How long the torus and its texture are waited on. They are read on the asset server's worker,
    // outside the frame loop, so the wait is bound by the machine's clock and not by a count of
    // frames, whose sleep is some fifteen milliseconds on Windows and longer on a runner busy with
    // other tests.
    private static readonly TimeSpan LoadBound = TimeSpan.FromSeconds(30);

    // Runs frames until the spawned materials' texture has loaded, and says so, with how long it
    // waited where it did not.
    private AssetId LoadTextured(string because)
    {
        var clock = Stopwatch.StartNew();
        var frames = 0;
        for (; !Textured(out _) && clock.Elapsed < LoadBound; frames++) Frames(1);
        Textured(out var texture).Should().BeTrue($"{because}, waited on for {clock.Elapsed.TotalSeconds:0.0} seconds over {frames} frames");
        return texture;
    }

    private bool Loaded<T>(AssetId id) => _app.World.TryGetResource<Assets<T>>(out var assets) && assets.Contains(id);

    [Fact]
    public void A_Model_And_Its_Texture_Are_Let_Go_When_The_Last_Entity_Using_Them_Is_Despawned()
    {
        var first = Place();
        var second = Place();
        var texture = LoadTextured("the torus loads with its checker texture");
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
        var again = LoadTextured("the torus placed again is read again with its texture");
        again.Should().NotBe(texture, "the texture was read again under a new id");
    }

    [Fact]
    public void A_Model_Spawned_Again_By_Hot_Reload_Lets_Its_Texture_Go_With_It()
    {
        var entity = Place();
        var texture = LoadTextured("the torus loads with its checker texture");
        var materials = Ecs.Query<Material>().Count();

        // The model reported written while it runs, as the server's watcher reports a file, which
        // spawns it again in place of the first copy.
        var spawnedBefore = Ecs.Query<Material>().Select(m => Ecs.Handle(m.Entity)).ToHashSet();
        var model = _app.World.Resource<Assets<SceneAsset>>().Ids.Single();
        _app.World.Resource<Events<AssetEvent<SceneAsset>>>().Send(AssetEvent<SceneAsset>.Modified(new Handle<SceneAsset>(model, default, strong: false)));
        Frames(2);
        Ecs.Query<Material>().Select(m => Ecs.Handle(m.Entity)).ToHashSet().SetEquals(spawnedBefore).Should().BeFalse("the model is spawned again");
        Ecs.Query<Material>().Count().Should().Be(materials);
        Frames(3);
        Loaded<TextureAsset>(texture).Should().BeTrue("the new copy holds the texture the old one gave back");
        Ecs.Query<Material>().All(m => Ecs.ParentOf(m.Entity) != 0).Should().BeTrue("the new copy hangs under the entity that placed the model, as the first did");

        Ecs.DespawnRecursive(entity);
        Frames(3);
        Loaded<TextureAsset>(texture).Should().BeFalse("the copy hot reload spawned gives its texture back too");
    }

    [Fact]
    public void A_Texture_The_Program_Loaded_Itself_Stays_When_The_Level_Lets_It_Go()
    {
        var entity = Place();
        var texture = LoadTextured("the torus loads with its checker texture");

        // The program asks for the same texture the level's material did, and keeps it.
        var server = _app.World.Resource<AssetServer>();
        var own = server.LoadTextureSrgb(Path.GetFileName(_folder.Path) + "/checker.png", generateMips: true);
        own.Id.Should().Be(texture, "one file is one asset");

        Ecs.DespawnRecursive(entity);
        Frames(3);
        Loaded<TextureAsset>(texture).Should().BeTrue("the program's own load holds it");
    }

    [Fact]
    public void A_Prefab_Files_Parsed_Copy_Is_Dropped_When_No_Copy_Of_It_Is_Left()
    {
        var file = _folder.File("lamp.json");
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

        LoadTextured("the models spawned");
        Frames(2);
        Ecs.GetReadOnly<ReflectionProbe>(probe).Capture.Should().Be(1, "the probe captures again once, now its room is there");
    }
}
