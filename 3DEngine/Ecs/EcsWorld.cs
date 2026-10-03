namespace Engine;

/// <summary>
/// Partial core of the Entity-Component-System world with sparse-set-based component storage.
/// </summary>
/// <remarks>
/// <para>
/// Entities are integer IDs allocated by a generational pool (<see cref="EntityPool"/>).
/// Components are stored in per-type <see cref="ComponentStore{T}"/> instances backed by
/// <see cref="SparseSet{T}"/> for O(1) add/remove/lookup and cache-friendly iteration.
/// </para>
/// <para>
/// This class is split across multiple partial files:
/// <list>
///   <item><description><c>EcsWorld.cs</c> type declarations and internal storage.</description></item>
///   <item><description><c>EcsWorld.API.cs</c> public entity/component/query API.</description></item>
///   <item><description><c>EcsWorld.Components.cs</c> <c>ComponentStore&lt;T&gt;</c> implementation.</description></item>
///   <item><description><c>EcsWorld.Pool.cs</c> <see cref="EntityPool"/> allocator.</description></item>
///   <item><description><c>EcsWorld.SparseSet.cs</c> <see cref="SparseSet{T}"/> data structure.</description></item>
///   <item><description><c>EcsWorld.RefIterators.cs</c> zero-allocation ref-based iterators.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var ecs = new EcsWorld();
///
/// // Spawn entities and attach components
/// int player = ecs.Spawn();
/// ecs.Add(player, new Position { X = 0, Y = 0 });
/// ecs.Add(player, new Health { Current = 100, Max = 100 });
///
/// // Query entities by component type
/// foreach (var (entity, pos, hp) in ecs.Query&lt;Position, Health&gt;())
///     Console.WriteLine($"Entity {entity} at ({pos.X},{pos.Y}) HP={hp.Current}");
///
/// // Zero-allocation ref iteration
/// foreach (var rc in ecs.QueryRef&lt;Position&gt;())
///     rc.Component.X += 1.0f;
/// </code>
/// </example>
/// <seealso cref="EcsCommands"/>
/// <seealso cref="EcsPlugin"/>
/// <seealso cref="BehaviorContext"/>
public sealed partial class EcsWorld
{
    private readonly EntityPool _entities = new();
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    /// <summary>
    /// Flat cache of all component stores, maintained in parallel with <see cref="_stores"/>.
    /// Used by <see cref="BeginFrame"/> and <see cref="Despawn"/> to avoid allocating a
    /// <see cref="Dictionary{TKey,TValue}.ValueCollection"/> enumerator each frame.
    /// </summary>
    private readonly List<IComponentStore> _storeList = new();

    /// <summary>Internal marker interface for type-erased component storage.</summary>
    private interface IComponentStore
    {
        /// <summary>Number of components in this store.</summary>
        int Count { get; }
        /// <summary>Removes the component for <paramref name="entity"/>, returning any <see cref="IDisposable"/>.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <param name="disposable">Set to the component's <see cref="IDisposable"/> implementation, or <c>null</c>.</param>
        /// <returns><c>true</c> if the component was removed.</returns>
        bool TryRemove(int entity, out IDisposable? disposable);
        /// <summary>Clears all per-frame change-tracking bits.</summary>
        void ClearChangedTicks();
    }
}
