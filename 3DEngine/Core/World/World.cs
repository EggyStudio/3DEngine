using System.Collections.Concurrent;

namespace Engine;

/// <summary>
/// Minimal ECS-like world focusing on resource storage.
/// Resources are keyed by their concrete <see cref="Type"/>; each type may have at most one instance.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe for concurrent reads and writes (backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>).
/// Implements <see cref="IDisposable"/> to clean up any resources that themselves implement
/// <see cref="IDisposable"/>.
/// </para>
/// <para>
/// This is the central data container shared across all systems. Use <see cref="InsertResource{T}"/>
/// to store singleton-style resources and <see cref="Resource{T}"/> to retrieve them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var world = new World();
/// world.InsertResource(new GameState { Score = 0 });
///
/// // Retrieve a required resource (throws if missing)
/// var state = world.Resource&lt;GameState&gt;();
///
/// // Safely check and retrieve
/// if (world.TryGetResource&lt;GameState&gt;(out var s))
///     Console.WriteLine(s.Score);
/// </code>
/// </example>
/// <seealso cref="App"/>
/// <seealso cref="Events{T}"/>
public sealed partial class World : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.World");

    private readonly ConcurrentDictionary<Type, object> _resources = new();
    
    /// <summary>
    /// Removes all resources, disposing any that implement <see cref="IDisposable"/>.
    /// Exceptions during individual dispose calls are logged and swallowed so that
    /// remaining resources are still cleaned up.
    /// </summary>
    public void Clear()
    {
        foreach (var kv in _resources)
            if (kv.Value is IDisposable d)
            {
                try
                {
                    d.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error disposing resource {kv.Key.Name}", ex);
                }
            }

        _resources.Clear();
        Logger.Trace("World cleared - all resources removed.");
    }

    /// <summary>
    /// Disposes all <see cref="IDisposable"/> resources and clears the world.
    /// Equivalent to calling <see cref="Clear"/>.
    /// </summary>
    public void Dispose() => Clear();
}
