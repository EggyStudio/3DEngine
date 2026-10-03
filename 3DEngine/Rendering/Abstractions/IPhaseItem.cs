namespace Engine;

/// <summary>
/// Marker interface for items in a <see cref="RenderPhase{T}"/>.
/// Each phase item represents a single draw call with a sort key, batch key, and draw function.
/// </summary>
/// <seealso cref="RenderPhase{T}"/>
/// <seealso cref="IDrawFunction{T}"/>
public interface IPhaseItem
{
    /// <summary>Sort key for depth ordering. Lower values render first in opaque (front-to-back);
    /// higher values render first in transparent (back-to-front).</summary>
    int SortKey { get; }

    /// <summary>Batch key for grouping items that share the same GPU resources (vertex buffer, pipeline).
    /// Items with the same batch key are drawn consecutively, reducing redundant state changes.</summary>
    int BatchKey { get; }
}
