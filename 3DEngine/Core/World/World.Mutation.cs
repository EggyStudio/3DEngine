namespace Engine;

public sealed partial class World
{
    /// <summary>Inserts or replaces a resource of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The resource type. Keyed by concrete type, with one instance a type at most.</typeparam>
    /// <param name="value">The resource instance to store. Replaces any existing resource of the same type.</param>
    public void InsertResource<T>(T value) where T : notnull
    {
        _resources[typeof(T)] = value;
        Interlocked.Increment(ref _resourceVersion);
    }

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or inserts <paramref name="value"/> and returns it.
    /// Atomic, so callers on several threads are safe.
    /// </summary>
    /// <typeparam name="T">The resource type to retrieve or insert.</typeparam>
    /// <param name="value">The fallback value to insert if the resource does not exist.</param>
    /// <returns>The existing or newly inserted resource instance.</returns>
    internal T GetOrInsertResource<T>(T value) where T : notnull =>
        _resources.TryGetValue(typeof(T), out var found) ? (T)found : Added((T)_resources.GetOrAdd(typeof(T), value));

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or creates one via <paramref name="factory"/>,
    /// inserts it, and returns it. Atomic, so callers on several threads are safe.
    /// The factory is only invoked when the resource is missing.
    /// </summary>
    /// <typeparam name="T">The resource type to retrieve or create.</typeparam>
    /// <param name="factory">A delegate invoked to create the resource when it does not exist.</param>
    /// <returns>The existing or newly created resource instance.</returns>
    internal T GetOrInsertResource<T>(Func<T> factory) where T : notnull =>
        _resources.TryGetValue(typeof(T), out var found) ? (T)found : Added((T)_resources.GetOrAdd(typeof(T), _ => factory()));

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or creates a default instance via <c>new T()</c>,
    /// inserts it, and returns it. Convenience overload for resources with parameterless constructors.
    /// </summary>
    /// <typeparam name="T">The resource type. Must have a public parameterless constructor.</typeparam>
    /// <returns>The existing or newly created resource instance.</returns>
    internal T InitResource<T>() where T : notnull, new() =>
        _resources.TryGetValue(typeof(T), out var found) ? (T)found : Added((T)_resources.GetOrAdd(typeof(T), _ => new T()));

    /// <summary>Removes the resource of type <typeparamref name="T"/> if present.</summary>
    /// <typeparam name="T">The resource type to remove.</typeparam>
    /// <returns><c>true</c> if a resource was removed; <c>false</c> if no resource of that type existed.</returns>
    internal bool RemoveResource<T>() where T : notnull
    {
        Interlocked.Increment(ref _resourceVersion);
        return _resources.TryRemove(typeof(T), out _);
    }

    /// <summary>
    /// Changes whenever a resource is inserted, replaced or removed, so a caller that keeps a
    /// resource it looked up knows when to look again.
    /// </summary>
    internal int ResourceVersion => Volatile.Read(ref _resourceVersion);

    private int _resourceVersion;

    private T Added<T>(T value)
    {
        Interlocked.Increment(ref _resourceVersion);
        return value;
    }
}
