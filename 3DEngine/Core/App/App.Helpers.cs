namespace Engine;

public sealed partial class App
{
    /// <summary>Inserts or replaces a world resource of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The resource type. Each type may have at most one instance in the <see cref="World"/>.</typeparam>
    /// <param name="value">The resource instance to store.</param>
    /// <returns>This <see cref="App"/> instance for fluent chaining.</returns>
    /// <example>
    /// <code>
    /// app.InsertResource(new GameState { Level = 1, IsPlaying = true });
    /// </code>
    /// </example>
    public App InsertResource<T>(T value) where T : notnull
    {
        World.InsertResource(value);
        Logger.Trace($"Resource inserted: {typeof(T).Name}");
        return this;
    }

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or inserts <paramref name="value"/> and returns it.
    /// Atomic - safe for concurrent callers.
    /// </summary>
    /// <typeparam name="T">The resource type to retrieve or insert.</typeparam>
    /// <param name="value">The fallback value to insert if the resource does not exist.</param>
    /// <returns>The existing or newly inserted resource instance.</returns>
    public T GetOrInsertResource<T>(T value) where T : notnull
        => World.GetOrInsertResource(value);

    /// <summary>
    /// Returns the existing resource of type <typeparamref name="T"/>, or creates a default instance via <c>new T()</c>,
    /// inserts it, and returns it.
    /// </summary>
    /// <typeparam name="T">The resource type. Must have a public parameterless constructor.</typeparam>
    /// <returns>The existing or newly created resource instance.</returns>
    public T InitResource<T>() where T : notnull, new()
        => World.InitResource<T>();
}
