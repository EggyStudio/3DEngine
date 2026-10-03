namespace Engine;

/// <summary>
/// Convenience extension methods for working with the asset system through <see cref="World"/>.
/// </summary>
/// <example>
/// <code>
/// // In a system:
/// var tex = world.LoadAsset&lt;Texture&gt;("textures/ground.png");
/// if (world.IsAssetLoaded(tex))
/// {
///     var data = world.GetAsset(tex);
///     // use data
/// }
/// </code>
/// <code>
/// // React to events:
/// foreach (var evt in world.ReadAssetEvents&lt;Texture&gt;())
///     if (evt.Kind == AssetEventKind.Modified)
///         RebuildGpu(evt.Id);
/// </code>
/// </example>
/// <seealso cref="AssetServer"/>
/// <seealso cref="Assets{T}"/>
/// <seealso cref="Handle{T}"/>
public static class WorldAssetExtensions
{
    /// <summary>
    /// Loads an asset through the <see cref="AssetServer"/>. Shorthand for
    /// <c>world.Resource&lt;AssetServer&gt;().Load&lt;T&gt;(path)</c>.
    /// </summary>
    /// <typeparam name="T">The expected asset type.</typeparam>
    /// <param name="world">The world containing the <see cref="AssetServer"/>.</param>
    /// <param name="path">Relative asset path.</param>
    /// <returns>A strong handle to the asset.</returns>
    public static Handle<T> LoadAsset<T>(this World world, string path) => 
        world.Resource<AssetServer>().Load<T>(path);

    /// <summary>Checks if an asset has been loaded.</summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="world">The world.</param>
    /// <param name="handle">The handle to check.</param>
    /// <returns><c>true</c> if the asset is available in <see cref="Assets{T}"/>.</returns>
    public static bool IsAssetLoaded<T>(this World world, Handle<T> handle)
    {
        if (!world.TryGetResource<Assets<T>>(out var assets)) 
            return false;
        return assets.Contains(handle);
    }

    /// <summary>Gets a loaded asset by handle.</summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="world">The world.</param>
    /// <param name="handle">The handle referencing the asset.</param>
    /// <returns>The loaded asset.</returns>
    /// <exception cref="KeyNotFoundException">The asset is not loaded.</exception>
    public static T GetAsset<T>(this World world, Handle<T> handle) => 
        world.Resource<Assets<T>>().Get(handle);

    /// <summary>Tries to get a loaded asset by handle.</summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="world">The world.</param>
    /// <param name="handle">The handle referencing the asset.</param>
    /// <param name="asset">The loaded asset, or <c>default</c> if not loaded.</param>
    /// <returns><c>true</c> if the asset is loaded.</returns>
    public static bool TryGetAsset<T>(this World world, Handle<T> handle, out T asset)
    {
        asset = default!;
        if (!world.TryGetResource<Assets<T>>(out var assets)) 
            return false;
        return assets.TryGet(handle, out asset!);
    }

    /// <summary>Reads asset lifecycle events for this frame.</summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="world">The world.</param>
    /// <returns>A read-only list of asset events.</returns>
    public static IReadOnlyList<AssetEvent<T>> ReadAssetEvents<T>(this World world) => 
        Events.Get<AssetEvent<T>>(world).Read();

    /// <summary>Gets the <see cref="LoadState"/> for an asset handle.</summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="world">The world.</param>
    /// <param name="handle">The handle to query.</param>
    /// <returns>The current load state.</returns>
    public static LoadState GetAssetLoadState<T>(this World world, Handle<T> handle) => 
        world.Resource<AssetServer>().GetLoadState(handle);
}
