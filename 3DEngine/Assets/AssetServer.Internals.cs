namespace Engine;

public sealed partial class AssetServer
{
    private readonly record struct LoadRequest(AssetId Id, AssetPath Path, Type AssetType, bool IsReload)
    {
        public LoadRequest(AssetId id, AssetPath path, Type assetType) : this(id, path, assetType, false) { }
    }

    private sealed class CompletedLoad
    {
        public required AssetId Id { get; init; }
        public required AssetPath Path { get; init; }
        public required Type AssetType { get; init; }
        public object? Asset { get; init; }
        public Dictionary<string, object>? SubAssets { get; init; }
        public List<AssetPath>? Dependencies { get; init; }
        public required bool Success { get; init; }
        public string? Error { get; init; }
        public bool IsReload { get; init; }
    }
}
