namespace Engine;

/// <summary>
/// A render graph node with typed input/output slots.
/// Nodes own their own render passes and declare resource dependencies via slots.
/// </summary>
/// <seealso cref="ViewNode"/>
/// <seealso cref="RenderGraph"/>
internal interface INode
{
    /// <summary>Declares the node's input slots (data it consumes from upstream nodes).</summary>
    SlotInfo[] Input() => Array.Empty<SlotInfo>();

    /// <summary>Declares the node's output slots (data it produces for downstream nodes).</summary>
    SlotInfo[] Output() => Array.Empty<SlotInfo>();

    /// <summary>Called before Run() to prepare GPU resources. Receives the render world for resource access.</summary>
    /// <param name="renderWorld">The render world containing render resources.</param>
    void Update(RenderWorld renderWorld) { }

    /// <summary>Runs the node: begins its render passes, binds its pipelines and records its draws.</summary>
    /// <param name="graphContext">Context for accessing slot values and running sub-graphs.</param>
    /// <param name="renderContext">Context wrapping the graphics device, command buffer, and dynamic allocator.</param>
    /// <param name="renderWorld">The render world containing render resources.</param>
    void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld);

    /// <summary>
    /// Called once every node has run and the window's pass has ended, for a node that draws into
    /// windows beside the main one, whose passes cannot begin while the window's is open.
    /// </summary>
    /// <param name="renderContext">Context wrapping the graphics device, command buffer, and dynamic allocator.</param>
    /// <param name="renderWorld">The render world containing render resources.</param>
    void AfterWindowPass(RenderContext renderContext, RenderWorld renderWorld) { }
}
