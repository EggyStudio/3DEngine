using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>What a component's writer asks of the scene being saved.</summary>
public sealed class SceneWriteContext
{
    private readonly EcsWorld _ecs;

    internal SceneWriteContext(EcsWorld ecs) => _ecs = ecs;

    /// <summary>The scene id of the entity a field refers to, or null for none or one that is gone.</summary>
    public string? IdOf(Entity entity) =>
        _ecs.TryResolve(entity, out var id) && _ecs.TryGet<SceneId>(id, out var sceneId) ? sceneId.Value : null;

    /// <summary>The path of the asset a handle names, or null for none.</summary>
    public string? PathOf<TAsset>(Handle<TAsset> handle) => handle.IsValid ? handle.Path.ToString() : null;
}
