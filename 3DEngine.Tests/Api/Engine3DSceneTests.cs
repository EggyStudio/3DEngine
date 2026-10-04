using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>Scene files through the flat API, in a headless app.</summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class Engine3DSceneTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"e3d-scene-{Guid.NewGuid():N}.json");

    public Engine3DSceneTests() => UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        File.Delete(_file);
    }

    [Fact]
    public void A_Loaded_Scene_Hands_Out_Handles_That_Refuse_A_Despawned_Entity()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var kept = ecs.Spawn();
        ecs.Add(kept, new Transform(new Vector3(1, 2, 3)));
        var other = ecs.Spawn();
        ecs.Add(other, new Transform(new Vector3(4, 5, 6)));
        SaveScene(_file, [ecs.Handle(kept), ecs.Handle(other)]);

        var level = LoadScene(_file);

        level.Should().HaveCount(2);
        level.Select(e => ecs.GetReadOnly<Transform>(e).Position).Should().Equal(new Vector3(1, 2, 3), new Vector3(4, 5, 6));
        ecs.Despawn(level[0]);
        ecs.IsAlive(level[0]).Should().BeFalse();
        ecs.Has<Transform>(level[0]).Should().BeFalse("a stale handle answers as if the entity had nothing");
    }

    [Fact]
    public void Saving_Leaves_Out_A_Handle_Whose_Entity_Is_Gone()
    {
        var ecs = GetApp().World.Resource<EcsWorld>();
        var gone = ecs.Spawn();
        ecs.Add(gone, new Transform(Vector3.One));
        var handle = ecs.Handle(gone);
        ecs.Despawn(gone);

        SaveScene(_file, [handle]);

        LoadScene(_file).Should().BeEmpty();
    }
}
