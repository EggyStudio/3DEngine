using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Scenes;

/// <summary>
/// The events a scene file's load tells once the whole file is in, each component's on each entity
/// it spawned and the file's own after them, and nothing done for a load no one observes.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SceneLoadedTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-scene-loaded-");

    private readonly record struct Ping;

    public void Dispose() => _folder.Dispose();

    // Three entities, b a child of a, and c a camera, each with a transform.
    private const string Level = """
        { "format": "3dengine-scene", "version": 1, "entities": [
          { "id": "a", "name": "A", "components": { "Transform": { "Position": [1, 0, 0] } } },
          { "id": "b", "name": "B", "parent": "a", "components": { "Transform": { "Position": [2, 0, 0] } } },
          { "id": "c", "name": "C", "components": { "Transform": { "Position": [3, 0, 0] }, "Camera": { "FovY": 1 } } }
        ] }
        """;

    private static (World World, EcsWorld Ecs) NewWorld()
    {
        var world = new World();
        var ecs = new EcsWorld();
        world.InsertResource(ecs);
        return (world, ecs);
    }

    private static string NameOf(EcsWorld ecs, Entity entity) =>
        ecs.TryResolve(entity, out var id) && ecs.TryGet<Name>(id, out var name) ? name.Value : "?";

    [Fact]
    public void Each_Component_Is_Told_On_Every_Entity_Once_The_Last_Is_In_And_The_File_After_Them()
    {
        var (world, ecs) = NewWorld();
        var told = new List<string>();
        ecs.Observe<Loaded<Transform>>(on =>
        {
            var e = on.Event.Entity.Index;
            told.Add($"transform {NameOf(ecs, on.Event.Entity)} at {on.Event.Value.Position.X}, of {ecs.Count<Name>()} named, parent {ecs.ParentOf(e) != 0}, camera there {ecs.Count<Camera>()}");
        });
        ecs.Observe<Loaded<Camera>>(on => told.Add($"camera {NameOf(ecs, on.Event.Entity)} {on.Event.Value.FovY}"));
        ecs.Observe<SceneLoaded>(on => told.Add($"scene '{on.Event.Path}' of {string.Join(", ", on.Event.Entities.Select(e => NameOf(ecs, e)))}"));

        SceneFile.Read(world, Level);

        // Entity by entity in the file's order, each entity's components in the order they are registered.
        told.Should().Equal(
            "transform A at 1, of 3 named, parent False, camera there 1",
            "transform B at 2, of 3 named, parent True, camera there 1",
            "camera C 1",
            "transform C at 3, of 3 named, parent False, camera there 1",
            "scene '' of A, B, C");
    }

    [Fact]
    public void A_File_Loaded_By_Its_Path_Names_It_And_A_Save_Read_Back_Is_Told_As_A_Scene()
    {
        var (world, ecs) = NewWorld();
        var hero = ecs.Spawn();
        ecs.SetName(hero, "Hero");
        ecs.Add(hero, new Transform(new Vector3(5, 0, 0)));
        var save = _folder.File("slot.json");
        SceneFile.Save(ecs, save);

        var (loadedWorld, loaded) = NewWorld();
        var told = new List<string>();
        loaded.Observe<Loaded<Transform>>(on => told.Add($"{NameOf(loaded, on.Event.Entity)} at {on.Event.Value.Position.X}"));
        loaded.Observe<SceneLoaded>(on => told.Add($"{Path.GetFileName(on.Event.Path)} of {on.Event.Entities.Count}"));

        SceneFile.Load(loadedWorld, save);

        told.Should().Equal("Hero at 5", "slot.json of 1");
    }

    [Fact]
    public void A_Load_No_One_Observes_Does_Nothing_More_And_An_Observer_Of_Something_Else_Changes_That_Not()
    {
        var (world, ecs) = NewWorld();
        // A read and the despawn of what it spawned, measured once the frames after have emptied the
        // log of removals the despawn fills, so each read finds it as large as the one before.
        long Read()
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            foreach (var entity in SceneFile.Read(world, Level)) ecs.Despawn(entity);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            for (int i = 0; i <= EcsWorld.RemovalFrames; i++) ecs.BeginFrame();
            return allocated;
        }

        // The least of several reads, since a test beside this one may register a codec, which the
        // registry's next lookup builds its index again for, in the middle of one.
        long Least() => Enumerable.Range(0, 5).Min(_ => Read());

        Read();
        var none = Least();
        using var other = ecs.Observe<Ping>(_ => { });
        Read();
        var unrelated = Least();
        var told = 0;
        using var transforms = ecs.Observe<Loaded<Transform>>(_ => told++);
        Read();

        unrelated.Should().Be(none, "an observer of something else makes the load do no more than with none");
        told.Should().Be(3, "an observer of a load is told");
    }
}
