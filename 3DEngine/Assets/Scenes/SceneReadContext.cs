using System.Numerics;
using System.Text;
using System.Text.Json;

namespace Engine;

/// <summary>What a component's reader asks of the scene being loaded.</summary>
public sealed class SceneReadContext
{
    private readonly EcsWorld _ecs;
    private readonly Dictionary<string, int> _entities;
    private readonly AssetServer? _assets;
    private readonly string _file;

    // The file is the one being read, empty for text, whose folder a data file it names is looked
    // for in as well.
    internal SceneReadContext(EcsWorld ecs, Dictionary<string, int> entities, AssetServer? assets, string? file = null)
    {
        _ecs = ecs;
        _entities = entities;
        _assets = assets;
        _file = file ?? "";
    }

    /// <summary>The entity a scene id names, or none when the scene has no such entity.</summary>
    public Entity Resolve(string? id) =>
        id is not null && _entities.TryGetValue(id, out var entity) ? _ecs.Handle(entity) : default;

    /// <summary>A handle to the asset at a path, loading it, or none without a path or an asset server.</summary>
    public Handle<TAsset> Load<TAsset>(string? path) =>
        string.IsNullOrEmpty(path) || _assets is null ? default : _assets.Load<TAsset>(path);

    /// <summary>
    /// A reference to the data file with an id, found through <see cref="DataAssets"/>' table, or in
    /// the folder of the file being read as well, and read once, or none without an id, or one that
    /// reads as none where no file has it.
    /// </summary>
    public DataRef<T> Data<T>(string? id) => DataAssets.Find<T>(id, _assets, _file);
}
