namespace Engine;

/// <summary>
/// Interface for a typed draw function that knows how to render a specific <see cref="IPhaseItem"/>.
/// </summary>
/// <typeparam name="T">The phase item type.</typeparam>
/// <seealso cref="IPhaseItem"/>
/// <seealso cref="RenderPhase{T}"/>
public interface IDrawFunction<T> where T : struct, IPhaseItem
{
    /// <summary>Issues GPU draw commands for the given phase item.</summary>
    /// <param name="item">The phase item to render.</param>
    /// <param name="pass">The active tracked render pass.</param>
    /// <param name="renderWorld">The render world for accessing GPU resources.</param>
    void Draw(ref T item, TrackedRenderPass pass, RenderWorld renderWorld);
}
