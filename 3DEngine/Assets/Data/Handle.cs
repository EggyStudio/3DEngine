using System.Collections.Concurrent;

namespace Engine;

/// <summary>
/// Type-safe reference to a loaded (or loading) asset of type <typeparamref name="T"/>.
/// Handles are lightweight value types that can be stored in components, resources, and collections.
/// </summary>
/// <typeparam name="T">The asset type this handle points to.</typeparam>
/// <remarks>
/// <para>
/// Handles are returned immediately by <see cref="AssetServer.Load{T}(string)"/> before the asset
/// finishes loading. The actual asset data becomes available in <see cref="Assets{T}"/> once the
/// background load completes (typically the next frame).
/// </para>
/// <para>
/// Internally backed by reference counting: strong handles keep the asset alive; when all strong
/// handles are dropped the asset becomes eligible for removal. Use <see cref="MakeWeak"/> to
/// create an observation-only handle that doesn't prevent cleanup.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Load returns a handle immediately
/// Handle&lt;Texture&gt; texture = assetServer.Load&lt;Texture&gt;("textures/ground.png");
///
/// // Later, access the loaded asset
/// if (assets.TryGet(texture, out var tex))
///     BindTexture(tex);
/// </code>
/// </example>
/// <seealso cref="AssetId"/>
/// <seealso cref="Assets{T}"/>
/// <seealso cref="AssetServer"/>
public readonly struct Handle<T> : IEquatable<Handle<T>>
{
    /// <summary>The unique asset identifier this handle refers to.</summary>
    public AssetId Id { get; }

    /// <summary>The asset path that was used to load this asset.</summary>
    public AssetPath Path { get; }

    /// <summary>Whether this is a strong (ref-counted) or weak (observation-only) handle.</summary>
    public bool IsStrong { get; }

    /// <summary>Whether this is a weak (observation-only) handle.</summary>
    public bool IsWeak => !IsStrong;

    /// <summary>Returns <c>true</c> when this handle points to a valid asset ID.</summary>
    public bool IsValid => Id.IsValid;

    /// <summary>Returns an invalid / default handle.</summary>
    public static Handle<T> Invalid => default;

    internal Handle(AssetId id, AssetPath path, bool strong)
    {
        Id = id;
        Path = path;
        IsStrong = strong;
    }

    /// <summary>Creates a weak copy of this handle that does not prevent the asset from being unloaded.</summary>
    /// <returns>A new weak <see cref="Handle{T}"/> pointing to the same asset.</returns>
    public Handle<T> MakeWeak() => new(Id, Path, strong: false);

    /// <summary>Creates a strong copy of this handle.</summary>
    /// <returns>A new strong <see cref="Handle{T}"/> pointing to the same asset.</returns>
    public Handle<T> MakeStrong() => new(Id, Path, strong: true);

    /// <inheritdoc />
    public bool Equals(Handle<T> other) => Id == other.Id;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Handle<T> other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();
    /// <inheritdoc />
    public override string ToString() => $"Handle<{typeof(T).Name}>({Id}, {Path}, {(IsStrong ? "Strong" : "Weak")})";

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Handle<T> left, Handle<T> right) => left.Equals(right);
    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Handle<T> left, Handle<T> right) => !left.Equals(right);
}

/// <summary>
/// Type-erased handle used internally by the <see cref="AssetServer"/> for tracking loads
/// across different asset types.
/// </summary>
/// <seealso cref="Handle{T}"/>
internal readonly record struct UntypedHandle(AssetId Id, AssetPath Path, Type AssetType, bool IsStrong);

/// <summary>
/// Thread-safe reference counter for asset handles. Tracks strong handle count per <see cref="AssetId"/>.
/// When the count drops to zero the asset is eligible for removal.
/// </summary>
internal static class HandleRefCounts
{
    private static readonly ConcurrentDictionary<AssetId, int> Counts = new();

    /// <summary>Increments the reference count for the given asset.</summary>
    internal static void Increment(AssetId id) =>
        Counts.AddOrUpdate(id, 1, (_, c) => c + 1);

    /// <summary>Decrements the reference count. Returns the new count (0 = eligible for removal).</summary>
    internal static int Decrement(AssetId id) =>
        Counts.AddOrUpdate(id, 0, (_, c) => Math.Max(0, c - 1));

    /// <summary>Gets the current strong reference count.</summary>
    internal static int GetCount(AssetId id) =>
        Counts.GetValueOrDefault(id);

    /// <summary>Removes tracking for the given asset entirely.</summary>
    internal static void Remove(AssetId id) =>
        Counts.TryRemove(id, out _);
}
