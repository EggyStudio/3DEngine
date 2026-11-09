namespace Engine;

/// <summary>
/// A reference to a data file holding a <typeparamref name="T"/>, by the id the file was given at
/// its first save, which a rename or a move of the file keeps.
/// </summary>
/// <typeparam name="T">What the file holds, a <see cref="DataAssetAttribute"/> type or an <see cref="AnimationStateMachine"/>.</typeparam>
/// <remarks>
/// <para>
/// A component holds one as a field, which a scene file writes as the id and resolves as it loads,
/// through the table <see cref="DataAssets"/> keeps of the ids and the files that hold them. Every
/// reference to a file shares the one value read from it, which a save through
/// <see cref="DataAssets"/> replaces for all of them. <c>default</c> refers to nothing.
/// </para>
/// <para>
/// A reference whose file is missing, cannot be read or holds another type reads as none, with the
/// reason, naming the id, in <see cref="Problem"/> and said once in the log, and its
/// <see cref="Value"/> is the type's default. It keeps its <see cref="Id"/>, so a scene saved again
/// still names the file.
/// </para>
/// </remarks>
public readonly struct DataRef<T> : IEquatable<DataRef<T>>
{
    private readonly DataAssets.Held<T>? _held;

    internal DataRef(DataAssets.Held<T> held) => _held = held;

    /// <summary>The id of the file, or null for a reference to nothing.</summary>
    public string? Id => _held?.Id;

    /// <summary>Whether it reads no file, referring to nothing or to a file that is missing, cannot be read or holds another type.</summary>
    public bool IsNone => _held is not { Problem: null };

    /// <summary>The file's value, shared by every reference to it, or the type's default where it reads none.</summary>
    public T Value => _held is { } held ? held.Value : DataAssets.Fallback<T>();

    /// <summary>Where its file is, or null where it reads none.</summary>
    public string? Path => _held is { Problem: null } held ? held.Path : null;

    /// <summary>Why it reads none, naming its id or its file, or null where it reads its file or refers to nothing.</summary>
    public string? Problem => _held?.Problem;

    /// <summary>The file's value, where it reads one.</summary>
    /// <returns>Whether it reads its file.</returns>
    public bool TryGet(out T value)
    {
        value = Value;
        return !IsNone;
    }

    /// <inheritdoc />
    public bool Equals(DataRef<T> other) => string.Equals(Id, other.Id, StringComparison.Ordinal) && (Id is not null || ReferenceEquals(_held, other._held));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DataRef<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Id is { } id ? StringComparer.Ordinal.GetHashCode(id) : 0;

    /// <summary>Whether two references name the same file.</summary>
    public static bool operator ==(DataRef<T> left, DataRef<T> right) => left.Equals(right);

    /// <summary>Whether two references name different files.</summary>
    public static bool operator !=(DataRef<T> left, DataRef<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Path ?? (Id is { } id ? $"none ({id})" : "none");
}
