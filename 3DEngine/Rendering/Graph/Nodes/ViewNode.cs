namespace Engine;

/// <summary>
/// Convenience base that auto-iterates <see cref="ExtractedView"/> render entities, calling
/// <see cref="Run(RenderGraphContext, RenderContext, ExtractedView, RenderWorld)"/> per camera.
/// </summary>
/// <seealso cref="INode"/>
/// <seealso cref="ExtractedView"/>
public abstract class ViewNode : INode
{
    /// <inheritdoc />
    public virtual SlotInfo[] Input() => Array.Empty<SlotInfo>();

    /// <inheritdoc />
    public virtual SlotInfo[] Output() => Array.Empty<SlotInfo>();

    /// <inheritdoc />
    public virtual void Update(RenderWorld renderWorld) { }

    /// <summary>Iterates all extracted view entities and calls the per-camera Run overload.</summary>
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        foreach (var (_, view) in renderWorld.Entities.Query<ExtractedView>())
            Run(graphContext, renderContext, view, renderWorld);
    }

    /// <summary>Executes rendering logic for a single camera view.</summary>
    /// <param name="graphContext">Context for accessing slot values and running sub-graphs.</param>
    /// <param name="renderContext">Context wrapping the graphics device and command buffer.</param>
    /// <param name="view">The extracted camera view to render from.</param>
    /// <param name="renderWorld">The render world containing render resources.</param>
    protected abstract void Run(RenderGraphContext graphContext, RenderContext renderContext, ExtractedView view, RenderWorld renderWorld);
}
