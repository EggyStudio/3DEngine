namespace Engine;

public sealed partial class World
{
    /// <summary>Inserts or replaces a resource of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The resource type. Keyed by concrete type - at most one instance per type.</typeparam>
    /// <param name="value">The resource instance to store. Replaces any existing resource of the same type.</param>
    public void InsertResource<T>(T value) where T : notnull => 
        _resources[typeof(T)] = value;

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or inserts <paramref name="value"/> and returns it.
    /// Atomic - safe for concurrent callers.
    /// </summary>
    /// <typeparam name="T">The resource type to retrieve or insert.</typeparam>
    /// <param name="value">The fallback value to insert if the resource does not exist.</param>
    /// <returns>The existing or newly inserted resource instance.</returns>
    public T GetOrInsertResource<T>(T value) where T : notnull => 
        (T)_resources.GetOrAdd(typeof(T), value);

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or creates one via <paramref name="factory"/>,
    /// inserts it, and returns it. Atomic - safe for concurrent callers.
    /// The factory is only invoked when the resource is missing.
    /// </summary>
    /// <typeparam name="T">The resource type to retrieve or create.</typeparam>
    /// <param name="factory">A delegate invoked to create the resource when it does not exist.</param>
    /// <returns>The existing or newly created resource instance.</returns>
    public T GetOrInsertResource<T>(Func<T> factory) where T : notnull => 
        (T)_resources.GetOrAdd(typeof(T), _ => factory());

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or creates a default instance via <c>new T()</c>,
    /// inserts it, and returns it. Convenience overload for resources with parameterless constructors.
    /// </summary>
    /// <typeparam name="T">The resource type. Must have a public parameterless constructor.</typeparam>
    /// <returns>The existing or newly created resource instance.</returns>
    public T InitResource<T>() where T : notnull, new() => 
        (T)_resources.GetOrAdd(typeof(T), _ => new T());

    /// <summary>Removes the resource of type <typeparamref name="T"/> if present.</summary>
    /// <typeparam name="T">The resource type to remove.</typeparam>
    /// <returns><c>true</c> if a resource was removed; <c>false</c> if no resource of that type existed.</returns>
    public bool RemoveResource<T>() where T : notnull => 
        _resources.TryRemove(typeof(T), out _);
}
