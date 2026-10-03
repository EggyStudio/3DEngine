namespace Engine;

public sealed partial class World
{
    /// <summary>Number of resources currently stored in this world.</summary>
    public int ResourceCount => _resources.Count;

    /// <summary>Checks whether a resource of type <typeparamref name="T"/> exists in this world.</summary>
    /// <typeparam name="T">The resource type to look for.</typeparam>
    /// <returns><c>true</c> if a resource of type <typeparamref name="T"/> is present; otherwise <c>false</c>.</returns>
    public bool ContainsResource<T>() where T : notnull => 
        _resources.ContainsKey(typeof(T));

    /// <summary>Gets a required resource of type <typeparamref name="T"/>, or throws if missing.</summary>
    /// <typeparam name="T">The resource type to retrieve.</typeparam>
    /// <returns>The resource instance of type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no resource of type <typeparamref name="T"/> is found.</exception>
    public T Resource<T>() where T : notnull
    {
        if (_resources.TryGetValue(typeof(T), out var obj) && obj is T typed)
            return typed;
        throw new InvalidOperationException($"Resource of type {typeof(T).Name} not found.");
    }

    /// <summary>Returns the resource of type <typeparamref name="T"/>, or <c>null</c>/<c>default</c> if not present.</summary>
    /// <typeparam name="T">The resource type to retrieve.</typeparam>
    /// <returns>The resource instance, or <c>default</c> if not found.</returns>
    public T? TryResource<T>() where T : notnull =>
        _resources.TryGetValue(typeof(T), out var obj) ? (T?)obj : default;

    /// <summary>Tries to get a resource of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The resource type to retrieve.</typeparam>
    /// <param name="value">When this method returns <c>true</c>, contains the resource instance; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the resource was found; otherwise <c>false</c>.</returns>
    public bool TryGetResource<T>(out T value) where T : notnull
    {
        if (_resources.TryGetValue(typeof(T), out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Returns a snapshot of all resource types currently stored in this world.</summary>
    /// <returns>A read-only collection of <see cref="Type"/> objects for each stored resource.</returns>
    public IReadOnlyCollection<Type> ResourceTypes => _resources.Keys.ToArray();
}
