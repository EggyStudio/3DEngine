namespace Engine;

/// <summary>A component that names the key an entity is found by, through <see cref="EcsWorld.Index{T,TKey}"/>.</summary>
/// <typeparam name="TKey">The key's type, as a cell's coordinates or a name.</typeparam>
/// <example>
/// <code>
/// public struct Fire : IKeyed&lt;(int X, int Y, int Z)&gt;
/// {
///     public (int X, int Y, int Z) Cell;
///     public readonly (int X, int Y, int Z) Key => Cell;
/// }
/// </code>
/// </example>
public interface IKeyed<TKey> where TKey : notnull
{
    /// <summary>The key the entity holding this component is found by.</summary>
    TKey Key { get; }
}
