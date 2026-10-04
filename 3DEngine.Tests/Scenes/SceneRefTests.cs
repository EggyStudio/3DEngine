using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>Scene files placed inside others through <see cref="SceneRef"/>, as prefabs.</summary>
[Trait("Category", "Unit")]
public sealed class SceneRefTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-sceneref-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static World NewWorld()
    {
        var world = new World();
        world.InsertResource(new EcsWorld());
        return world;
    }

    // A lamp: a post with a light on top, saved as a scene file of its own.
    private string Lamp()
    {
        var ecs = new EcsWorld();
        var post = ecs.Spawn();
        ecs.SetName(post, "Post");
        ecs.Add(post, new Transform(new Vector3(0, 1, 0)));
        var bulb = ecs.Spawn();
        ecs.SetName(bulb, "Bulb");
        ecs.Add(bulb, new Transform(new Vector3(0, 1, 0)));
        ecs.SetParent(bulb, post);
        var path = Path.Combine(_directory, "lamp.json");
        SceneFile.Save(ecs, path);
        return path;
    }

    private static int Place(EcsWorld ecs, string path, Vector3 at)
    {
        var entity = ecs.Spawn();
        ecs.Add(entity, new Transform(at));
        ecs.Add(entity, new SceneRef { Path = path });
        return entity;
    }

    [Fact]
    public void A_Placed_File_Spawns_Under_Its_Entity_Twice_Over_Without_Sharing_Ids()
    {
        var lamp = Lamp();
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var left = Place(ecs, lamp, new Vector3(-5, 0, 0));
        var right = Place(ecs, lamp, new Vector3(5, 0, 0));

        SceneRefSystem.Run(world);
        TransformPropagation.Run(world);

        foreach (var (placed, x) in new[] { (left, -5f), (right, 5f) })
        {
            var post = ecs.ChildrenOf(placed).Should().ContainSingle().Subject;
            ecs.GetReadOnly<Name>(post).Value.Should().Be("Post");
            var bulb = ecs.ChildrenOf(post).Should().ContainSingle().Subject;
            TransformPropagation.WorldMatrix(ecs, bulb).Translation.Should().Be(new Vector3(x, 2, 0), "the copy sits where its entity places it");
            ecs.Has<SceneId>(post).Should().BeFalse("a copy drops the file's ids, which the other copy has too");
        }

        SceneRefSystem.Run(world);
        ecs.ChildrenOf(left).Should().ContainSingle("a file is spawned once");
    }

    [Fact]
    public void A_Level_Saved_With_A_Placed_File_Keeps_The_Reference_And_Brings_The_Copy_Back()
    {
        var lamp = Lamp();
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        Place(ecs, lamp, new Vector3(3, 0, 0));
        SceneRefSystem.Run(world);

        var json = SceneFile.Write(ecs);
        json.Should().NotContain("Bulb", "the copy comes back from its file, so the level does not hold it");

        var loaded = NewWorld();
        var spawned = SceneFile.Read(loaded, json);
        SceneRefSystem.Run(loaded);
        var placed = spawned.Should().ContainSingle().Subject;
        loaded.Resource<EcsWorld>().ChildrenOf(placed).Should().ContainSingle("the copy is spawned again from its file");
    }

    [Fact]
    public void A_File_That_Places_Itself_Stops_At_The_Deepest_Reference()
    {
        var path = Path.Combine(_directory, "itself.json");
        var ecs = new EcsWorld();
        var inner = ecs.Spawn();
        ecs.Add(inner, new SceneRef { Path = path });
        SceneFile.Save(ecs, path);

        var world = NewWorld();
        Place(world.Resource<EcsWorld>(), path, Vector3.Zero);
        SceneRefSystem.Run(world);

        world.Resource<EcsWorld>().Query<SceneRef>().Count().Should().Be(SceneRefSystem.MaxDepth + 1, "the placed one and each copy down to the limit");
    }

    [Fact]
    public void A_Placed_File_Written_Since_It_Was_Spawned_Is_Spawned_Again_In_Its_Place()
    {
        var lamp = Lamp();
        var world = NewWorld();
        var ecs = world.Resource<EcsWorld>();
        var placed = Place(ecs, lamp, Vector3.Zero);
        SceneRefSystem.Run(world);
        var before = ecs.ChildrenOf(placed).Single();
        var beforeHandle = ecs.Handle(before);

        SceneRefSystem.ReloadChanged(ecs);
        ecs.ChildrenOf(placed).Should().Equal([before], "an unwritten file is left as it was spawned");

        // The lamp saved again with its post renamed, as an edit in another tool would.
        var edited = new EcsWorld();
        var post = edited.Spawn();
        edited.SetName(post, "Tall post");
        edited.Add(post, new Transform(new Vector3(0, 2, 0)));
        SceneFile.Save(edited, lamp);
        File.SetLastWriteTimeUtc(lamp, DateTime.UtcNow.AddSeconds(5));

        SceneRefSystem.ReloadChanged(ecs);
        SceneRefSystem.Run(world);

        ecs.IsAlive(beforeHandle).Should().BeFalse("the old copy goes");
        var after = ecs.ChildrenOf(placed).Should().ContainSingle().Subject;
        ecs.GetReadOnly<Name>(after).Value.Should().Be("Tall post");
    }
}
