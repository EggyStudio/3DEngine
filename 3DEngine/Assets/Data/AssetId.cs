namespace Engine;

/// <summary>
/// Globally unique identifier for a loaded asset. Wraps an incrementing <see cref="ulong"/>
/// combined with an internal generation counter for type safety.
/// </summary>
/// <remarks>
/// Asset IDs are assigned by the <see cref="AssetServer"/> when <c>Load</c> is called.
/// Two handles pointing to the same asset share the same <see cref="AssetId"/>.
/// The <see cref="Invalid"/> sentinel represents an uninitialized or failed handle.
/// </remarks>
/// <seealso cref="Handle{T}"/>
/// <seealso cref="AssetServer"/>
public readonly record struct AssetId : IComparable<AssetId>
{
    private static long _next;

    /// <summary>The raw numeric identifier. Zero is reserved for <see cref="Invalid"/>.</summary>
    public ulong Value { get; }

    /// <summary>Sentinel value representing an invalid / uninitialized asset.</summary>
    public static AssetId Invalid { get; } = new(0);

    /// <summary>Returns <c>true</c> when this ID is the <see cref="Invalid"/> sentinel.</summary>
    public bool IsValid => Value != 0;

    private AssetId(ulong value) => Value = value;

    /// <summary>Generates the next unique <see cref="AssetId"/>. Thread-safe.</summary>
    internal static AssetId Next() => new((ulong)Interlocked.Increment(ref _next));

    /// <inheritdoc />
    public int CompareTo(AssetId other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public override string ToString() => $"AssetId({Value})";
}
