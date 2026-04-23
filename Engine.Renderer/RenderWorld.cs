using System.Collections.Concurrent;

namespace Engine;

/// <summary>
/// Render-thread world, analogous to <see cref="World"/> but for GPU-side data.
/// Contains both singleton resources (cameras, swapchain target, pipeline cache) accessed via
/// <see cref="TryGet{T}"/>/<see cref="Set{T}"/>, <b>and</b> a full <see cref="EcsWorld"/>
/// for per-entity render components (<see cref="RenderMeshInstance"/>, <see cref="ExtractedView"/>).
/// </summary>
/// <remarks>
/// <para>
/// The render world is a complete <c>World</c> with entities and resources.
/// During the extract phase, game entities are cloned as render entities with render-specific
/// components. This enables per-entity visibility culling, GPU resource tracking, and batching
/// through standard ECS queries.
/// </para>
/// <para>
/// Render entities are cleared at the start of each extract phase via <see cref="ClearEntities"/>
/// and re-populated from the game world. Singleton resources persist across frames.
/// </para>
/// </remarks>
/// <seealso cref="Renderer"/>
/// <seealso cref="World"/>
/// <seealso cref="RenderMeshInstance"/>
/// <seealso cref="ExtractedView"/>
public sealed class RenderWorld
{
    private readonly ConcurrentDictionary<Type, object> _resources = new();

    /// <summary>
    /// ECS world for per-entity render components. Render entities are spawned during the
    /// extract phase and despawned at the start of the next frame via <see cref="ClearEntities"/>.
    /// </summary>
    /// <remarks>
    /// Use this for ECS queries over render entities:
    /// <code>
    /// foreach (var (entity, mesh) in renderWorld.Entities.Query&lt;RenderMeshInstance&gt;())
    ///     registry.GetOrCreate(mesh.MainEntityId, mesh.MeshData, gfx);
    /// </code>
    /// </remarks>
    public EcsWorld Entities { get; } = new();

    // ── Entity lifecycle ────────────────────────────────────────────────

    /// <summary>Spawns a new render entity and returns its ID.</summary>
    /// <returns>The newly allocated render entity ID.</returns>
    public int Spawn() => Entities.Spawn();

    /// <summary>
    /// Despawns all render entities, clearing per-entity component data for the next frame.
    /// Called at the start of the extract phase. Singleton resources are <b>not</b> affected.
    /// </summary>
    public void ClearEntities()
    {
        // Despawn all entities that have any component.
        // EcsWorld doesn't expose a "despawn all" - iterate known entities.
        var meshEntities = Entities.EntitiesWith<RenderMeshInstance>();
        for (int i = meshEntities.Length - 1; i >= 0; i--)
            Entities.Despawn(meshEntities[i]);

        var viewEntities = Entities.EntitiesWith<ExtractedView>();
        for (int i = viewEntities.Length - 1; i >= 0; i--)
            Entities.Despawn(viewEntities[i]);

        Entities.BeginFrame();
    }

    // ── Singleton resource API (unchanged) ──────────────────────────────

    /// <summary>Returns <c>true</c> if a resource of type <typeparamref name="T"/> is stored.</summary>
    /// <typeparam name="T">The resource type to check for.</typeparam>
    /// <returns><c>true</c> if present; otherwise <c>false</c>.</returns>
    public bool Contains<T>() where T : notnull => _resources.ContainsKey(typeof(T));

    /// <summary>Gets the resource of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <returns>The resource instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the resource is not present.</exception>
    public T Get<T>() where T : notnull => (T)_resources[typeof(T)];

    /// <summary>Tries to get the resource of type <typeparamref name="T"/>, returning <c>null</c> if missing.</summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <returns>The resource instance, or <c>default</c> if not present.</returns>
    public T? TryGet<T>() where T : notnull => _resources.TryGetValue(typeof(T), out var obj) ? (T?)obj : default;

    /// <summary>Sets (inserts or overwrites) a resource of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="value">The resource instance to store.</param>
    public void Set<T>(T value) where T : notnull => _resources[typeof(T)] = value!;

    /// <summary>Removes the resource of type <typeparamref name="T"/> if present.</summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <returns><c>true</c> if the resource was removed; <c>false</c> if it was not present.</returns>
    public bool Remove<T>() where T : notnull => _resources.TryRemove(typeof(T), out _);
}