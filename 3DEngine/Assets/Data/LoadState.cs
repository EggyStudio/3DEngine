namespace Engine;

/// <summary>Describes the current loading state of an asset referenced by a <see cref="Handle{T}"/>.</summary>
/// <seealso cref="Handle{T}"/>
/// <seealso cref="AssetServer"/>
public enum LoadState
{
    /// <summary>The asset has not started loading.</summary>
    NotLoaded,
    /// <summary>The asset is currently being loaded on a background thread.</summary>
    Loading,
    /// <summary>The asset loaded successfully and is available in <see cref="Assets{T}"/>.</summary>
    Loaded,
    /// <summary>The asset failed to load. Check logs for details.</summary>
    Failed,
}
