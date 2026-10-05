using System.Numerics;
using System.Text.Json.Nodes;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering.Models;

/// <summary>
/// Entities playing <see cref="AnimatedModel"/> clips on <c>arm.gltf</c>, an arm standing from y 0
/// to y 2 whose clip "bend" turns its elbow at y 1 a quarter turn about Z over a second, posed on
/// the CPU in an app with no renderer.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class AnimatedModelDrawsTests : IDisposable
{
    private static readonly string Arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
    private readonly App _app = new();
    private readonly EcsWorld _ecs = new();
    private readonly Time _time = new() { MaxDeltaSeconds = 10 };
    private double _elapsed;

    public AnimatedModelDrawsTests()
    {
        _app.World.InitResource<TextureStore>();
        _app.World.InitResource<MeshStore>();
        _app.World.InitResource<ModelDrawList>();
        _app.World.InitResource<DrawList>();
        _app.World.InsertResource(_ecs);
        _app.World.InsertResource(_time);
        UseApp(_app);
        var camera = _ecs.Spawn();
        _ecs.Add(camera, new Camera(60f));
        _ecs.Add(camera, new Transform(new Vector3(0, 1, 6)));
    }

    public void Dispose() => UseApp(null);

    private void Frame(double seconds = 0)
    {
        _elapsed += seconds;
        _time.Update(_elapsed, seconds);
        _app.World.Resource<ModelDrawList>().Clear();
        AnimatedModelDraws.Run(_app.World);
    }

    private int Spawn(AnimatedModel animated, Vector3 at)
    {
        var entity = _ecs.Spawn();
        _ecs.Add(entity, new Transform(at));
        _ecs.Add(entity, animated);
        return entity;
    }

    // The arm's tip as the frame drew the entity placed at x, in the model's own space, the vertex
    // farthest from the shoulder however the elbow is turned.
    private Vector3 Tip(float x)
    {
        var draw = _app.World.Resource<ModelDrawList>().Draws.Single(d => d.World.Translation.X == x);
        _app.World.Resource<MeshStore>().TryGetData(draw.Mesh, out var vertices, out _).Should().BeTrue();
        return vertices.Select(v => v.Position).MaxBy(p => p.LengthSquared());
    }

    [Fact]
    public void An_Entity_Plays_Its_Clip_Where_Its_Transform_Places_It()
    {
        var entity = Spawn(new AnimatedModel(Arm), new Vector3(5, 0, 0));

        Frame();
        Tip(5).Y.Should().BeApproximately(2, 0.01f, "at the start of the clip the arm stands straight");
        Frame(0.5);
        Frame(0.5);

        _ecs.GetReadOnly<AnimatedModel>(entity).Time.Should().BeApproximately(1, 1e-5f, "a second has played");
        Tip(5).X.Should().BeApproximately(-1, 0.02f, "at the clip's end the elbow has turned a quarter, the tip toward -X");
        _app.World.Resource<ModelDrawList>().Draws.Should().OnlyContain(d => d.World.Translation == new Vector3(5, 0, 0));
    }

    [Fact]
    public void Two_Entities_Of_One_File_Are_Posed_Apart_And_A_Despawned_One_Lets_Its_Model_Go()
    {
        var still = Spawn(new AnimatedModel(Arm, speed: 0), new Vector3(-2, 0, 0));
        var bent = Spawn(new AnimatedModel(Arm, speed: 0) { Time = 1 }, new Vector3(2, 0, 0));

        Frame(0.1);

        Tip(-2).Y.Should().BeApproximately(2, 0.01f, "held at the start");
        Tip(2).X.Should().BeApproximately(-1, 0.02f, "held at the end, on a model of its own");
        _app.World.Resource<AnimatedModelDraws>().ModelCount.Should().Be(2);
        _app.World.Resource<AnimatedModelDraws>().FileCount.Should().Be(1, "the two share one load of the file");

        var mesh = _app.World.Resource<ModelDrawList>().Draws.First(d => d.World.Translation.X == 2).Mesh;
        _ecs.Despawn(bent);
        Frame(0.1);
        _app.World.Resource<AnimatedModelDraws>().ModelCount.Should().Be(1);
        _app.World.Resource<MeshStore>().Contains(mesh).Should().BeFalse("the despawned entity's skinned mesh is unloaded");
        Tip(-2).Y.Should().BeApproximately(2, 0.01f, "the other still draws, from the file they shared");
        _ecs.Has<AnimatedModel>(still).Should().BeTrue();

        var meshes = _app.World.Resource<MeshStore>().Count;
        _ecs.Despawn(still);
        Frame(0.1);
        _app.World.Resource<AnimatedModelDraws>().FileCount.Should().Be(0, "the last entity lets the file go");
        _app.World.Resource<MeshStore>().Count.Should().BeLessThan(meshes);
    }

    [Fact]
    public void A_Camera_Drawing_Into_A_Render_Texture_Draws_The_Posed_Model_There_Too()
    {
        var screen = _ecs.Spawn();
        _ecs.Add(screen, new Camera(60f) { Target = new RenderTexture2D(new Texture2D(7, 64, 64)) });
        _ecs.Add(screen, new Transform(new Vector3(0, 1, -6)));
        Spawn(new AnimatedModel(Arm), new Vector3(1, 0, 0));

        Frame();

        var draws = _app.World.Resource<ModelDrawList>().Draws;
        draws.Should().Contain(d => d.Target == 0, "the window's camera draws it");
        draws.Should().Contain(d => d.Target == 7, "and so does the camera drawing into the render texture");
    }

    [Fact]
    public void A_Change_Of_Clip_Blends_From_The_One_Before_Over_Its_Blend_Time()
    {
        var file = TwoClips();
        try
        {
            var entity = Spawn(new AnimatedModel(file, "bend", speed: 0, blendSeconds: 1) { Time = 1 }, Vector3.Zero);
            Frame(0.1);
            Tip(0).X.Should().BeApproximately(-1, 0.02f, "bent");

            _ecs.GetRef<AnimatedModel>(entity).Clip = "rest";
            Frame(0.5);
            Tip(0).X.Should().BeInRange(-0.9f, -0.5f, "halfway from bent to straight, the tip has swung about half back");

            Frame(0.6);
            Tip(0).Y.Should().BeApproximately(2, 0.01f, "the blend is over and the arm stands straight");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
        }
    }

    [Fact]
    public void A_Scene_File_Keeps_What_An_Entity_Plays()
    {
        var ecs = new EcsWorld();
        var entity = ecs.Spawn();
        ecs.Add(entity, new AnimatedModel("models/hero.gltf", "run", speed: 1.5f) { Time = 0.75f });

        var loaded = new World();
        loaded.InsertResource(new EcsWorld());
        var spawned = SceneFile.Read(loaded, SceneFile.Write(ecs));

        var back = loaded.Resource<EcsWorld>().GetReadOnly<AnimatedModel>(spawned.Should().ContainSingle().Subject);
        (back.Path, back.Clip, back.Speed, back.Time, back.BlendSeconds).Should().Be(("models/hero.gltf", "run", 1.5f, 0.75f, 0.2f));
    }

    [Fact]
    public void A_File_That_Cannot_Be_Loaded_Draws_Nothing()
    {
        Spawn(new AnimatedModel("no/such/model.gltf"), Vector3.Zero);

        Frame(0.1);
        Frame(0.1);

        _app.World.Resource<ModelDrawList>().Draws.Should().BeEmpty();
        _app.World.Resource<AnimatedModelDraws>().ModelCount.Should().Be(0);
    }

    // The arm with a second clip, "rest", which holds the elbow straight for a second, in a
    // buffer of its own beside the file's.
    private static string TwoClips()
    {
        var gltf = JsonNode.Parse(File.ReadAllText(Arm))!.AsObject();
        var bytes = new byte[40];
        var floats = new float[] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        var buffers = gltf["buffers"]!.AsArray();
        buffers.Add(new JsonObject { ["byteLength"] = 40, ["uri"] = "data:application/octet-stream;base64," + Convert.ToBase64String(bytes) });
        var views = gltf["bufferViews"]!.AsArray();
        views.Add(new JsonObject { ["buffer"] = buffers.Count - 1, ["byteOffset"] = 0, ["byteLength"] = 8 });
        views.Add(new JsonObject { ["buffer"] = buffers.Count - 1, ["byteOffset"] = 8, ["byteLength"] = 32 });
        var accessors = gltf["accessors"]!.AsArray();
        accessors.Add(new JsonObject { ["bufferView"] = views.Count - 2, ["componentType"] = 5126, ["count"] = 2, ["type"] = "SCALAR", ["min"] = new JsonArray(0.0), ["max"] = new JsonArray(1.0) });
        accessors.Add(new JsonObject { ["bufferView"] = views.Count - 1, ["componentType"] = 5126, ["count"] = 2, ["type"] = "VEC4" });
        var target = gltf["animations"]![0]!["channels"]![0]!["target"]!["node"]!.GetValue<int>();
        gltf["animations"]!.AsArray().Add(new JsonObject
        {
            ["name"] = "rest",
            ["samplers"] = new JsonArray(new JsonObject { ["input"] = accessors.Count - 2, ["output"] = accessors.Count - 1, ["interpolation"] = "LINEAR" }),
            ["channels"] = new JsonArray(new JsonObject { ["sampler"] = 0, ["target"] = new JsonObject { ["node"] = target, ["path"] = "rotation" } }),
        });
        var directory = Directory.CreateTempSubdirectory("engine-animated-").FullName;
        var file = Path.Combine(directory, "arm2.gltf");
        File.WriteAllText(file, gltf.ToJsonString());
        return file;
    }

    [Fact]
    public void A_Level_That_Places_An_Animated_File_With_A_ModelRef_Plays_It_And_Saves_Only_The_Reference()
    {
        var placed = _ecs.Spawn();
        _ecs.Add(placed, new Transform(new Vector3(4, 0, 0)));
        _ecs.Add(placed, new ModelRef { Path = Arm });

        ModelRefSystem.Run(_app.World);
        // The model as the asset server hands it over, read on its workers, which is when its clips
        // are found and the child that plays them is made.
        var id = AssetId.Next();
        var assets = _app.World.GetOrInsertResource(() => new Assets<SceneAsset>());
        assets.Set(id, new SceneAsset { Scene = new AssimpModelReader().ReadFile(Arm, new SceneImportSettings()), SourcePath = Arm });
        _ecs.Add(placed, new SpawnSceneRequest { Handle = new Handle<SceneAsset>(id, AssetPath.Parse(Arm), strong: false) });
        SceneSpawnSystem.Run(_app.World);

        _ecs.Has<SpawnSceneRequest>(placed).Should().BeFalse("the request is answered");
        var child = _ecs.Query<AnimatedModel>().Should().ContainSingle().Subject.Entity;
        _ecs.GetRef<AnimatedModel>(child).Path.Should().Be(Arm);
        _ecs.ParentOf(child).Should().Be(placed, "the child is placed by the reference's entity");
        // As an app does in Stage.Render, before the drawing.
        TransformPropagation.Run(_app.World);
        Frame(0.5);
        _app.World.Resource<ModelDrawList>().Draws.Should().Contain(d => d.World.Translation.X == 4, "the arm is drawn where the level placed it");
        SceneFile.Write(_ecs).Should().NotContain("AnimatedModel", "the child comes back from the file, as spawned meshes do");
    }
}
