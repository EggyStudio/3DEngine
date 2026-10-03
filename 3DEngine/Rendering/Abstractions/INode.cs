namespace Engine;

/// <summary>
/// A render graph node with typed input/output slots.
/// Nodes own their own render passes and declare resource dependencies via slots.
/// </summary>
/// <seealso cref="ViewNode"/>
/// <seealso cref="RenderGraph"/>
public interface INode
{
    /// <summary>Declares the node's input slots (data it consumes from upstream nodes).</summary>
    SlotInfo[] Input() => Array.Empty<SlotInfo>();

    /// <summary>Declares the node's output slots (data it produces for downstream nodes).</summary>
    SlotInfo[] Output() => Array.Empty<SlotInfo>();

    /// <summary>Called before Run() to prepare GPU resources. Receives the render world for resource access.</summary>
    /// <param name="renderWorld">The render world containing render resources.</param>
    void Update(RenderWorld renderWorld) { }

    /// <summary>Executes the node's rendering logic - creating render passes, binding pipelines, issuing draw calls.</summary>
    /// <param name="graphContext">Context for accessing slot values and running sub-graphs.</param>
    /// <param name="renderContext">Context wrapping the graphics device, command buffer, and dynamic allocator.</param>
    /// <param name="renderWorld">The render world containing render resources.</param>
    void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld);
}
