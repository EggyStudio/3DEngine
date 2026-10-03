namespace Engine;

/// <summary>
/// Execution context wrapping <see cref="IGraphicsDevice"/>, <see cref="ICommandBuffer"/>,
/// and <see cref="DynamicBufferAllocator"/> for render graph node execution.
/// Nodes use this to begin tracked render passes and issue GPU commands.
/// </summary>
/// <seealso cref="INode"/>
/// <seealso cref="TrackedRenderPass"/>
public sealed class RenderContext
{
    /// <summary>The low-level graphics device for resource creation and GPU commands.</summary>
    public IGraphicsDevice Device { get; }

    /// <summary>The active command buffer for recording GPU commands this frame.</summary>
    public ICommandBuffer CommandBuffer { get; }

    /// <summary>Frame-aware dynamic buffer allocator for transient GPU data.
    /// Allocations are valid only for the current frame.</summary>
    public DynamicBufferAllocator? DynamicAllocator { get; }

    /// <summary>Creates a new render context for the current frame.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="commandBuffer">The command buffer for this frame.</param>
    /// <param name="dynamicAllocator">Optional dynamic buffer allocator.</param>
    internal RenderContext(IGraphicsDevice device, ICommandBuffer commandBuffer, DynamicBufferAllocator? dynamicAllocator)
    {
        Device = device;
        CommandBuffer = commandBuffer;
        DynamicAllocator = dynamicAllocator;
    }

    /// <summary>
    /// Begins a tracked render pass from the given descriptor.
    /// The returned <see cref="TrackedRenderPass"/> auto-ends on <see cref="IDisposable.Dispose"/>.
    /// </summary>
    /// <param name="descriptor">Describes the render pass, framebuffer, extent, and load/store ops.</param>
    /// <returns>A <see cref="TrackedRenderPass"/> wrapping the active render pass.</returns>
    public TrackedRenderPass BeginTrackedRenderPass(RenderPassDescriptor descriptor) =>
        new TrackedRenderPass(Device, CommandBuffer, descriptor);
}
