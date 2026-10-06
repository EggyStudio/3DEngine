namespace Engine;

/// <summary>Discriminator for <see cref="AssetEvent{T}"/> lifecycle transitions.</summary>
/// <seealso cref="AssetEvent{T}"/>
public enum AssetEventKind
{
    /// <summary>A new asset was loaded and added to <see cref="Assets{T}"/>.</summary>
    Added,
    /// <summary>An existing asset was replaced due to hot-reload.</summary>
    Modified,
    /// <summary>An asset was removed from <see cref="Assets{T}"/>.</summary>
    Removed,
    /// <summary>The asset and all its transitive dependencies are fully loaded.</summary>
    LoadedWithDependencies,
}
