namespace Engine;

/// <summary>
/// Events fired by the <see cref="AssetServer"/> when asset lifecycle state changes.
/// Systems can react to these via <c>Events.Get&lt;AssetEvent&lt;T&gt;&gt;(world)</c>.
/// </summary>
/// <typeparam name="T">The asset type.</typeparam>
/// <remarks>
/// <para>
/// Events are enqueued during the <see cref="Stage.PreUpdate"/> asset processing system
/// and cleared at <see cref="Stage.Last"/>. Systems in <see cref="Stage.Update"/> through
/// <see cref="Stage.Render"/> can read them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // React to texture hot-reload in an Update system
/// foreach (var evt in world.ReadEvents&lt;AssetEvent&lt;Texture&gt;&gt;())
/// {
///     if (evt.Kind == AssetEventKind.Modified)
///         RebuildGpuTexture(evt.Id);
/// }
/// </code>
/// </example>
/// <seealso cref="AssetEventKind"/>
/// <seealso cref="Assets{T}"/>
/// <seealso cref="AssetServer"/>
public readonly record struct AssetEvent<T>
{
    /// <summary>The kind of lifecycle transition.</summary>
    public AssetEventKind Kind { get; init; }

    /// <summary>The ID of the affected asset.</summary>
    public AssetId Id { get; init; }

    /// <summary>The handle of the affected asset.</summary>
    public Handle<T> Handle { get; init; }

    /// <summary>Creates an <see cref="AssetEventKind.Added"/> event.</summary>
    public static AssetEvent<T> Added(Handle<T> handle) => new()
    {
        Kind = AssetEventKind.Added,
        Id = handle.Id,
        Handle = handle,
    };

    /// <summary>Creates an <see cref="AssetEventKind.Modified"/> event (hot-reload).</summary>
    public static AssetEvent<T> Modified(Handle<T> handle) => new()
    {
        Kind = AssetEventKind.Modified,
        Id = handle.Id,
        Handle = handle,
    };

    /// <summary>Creates an <see cref="AssetEventKind.Removed"/> event.</summary>
    public static AssetEvent<T> Removed(Handle<T> handle) => new()
    {
        Kind = AssetEventKind.Removed,
        Id = handle.Id,
        Handle = handle,
    };

    /// <summary>Creates a <see cref="AssetEventKind.LoadedWithDependencies"/> event.</summary>
    public static AssetEvent<T> LoadedWithDependencies(Handle<T> handle) => new()
    {
        Kind = AssetEventKind.LoadedWithDependencies,
        Id = handle.Id,
        Handle = handle,
    };

    /// <inheritdoc />
    public override string ToString() => $"AssetEvent<{typeof(T).Name}>({Kind}, {Id})";
}

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
