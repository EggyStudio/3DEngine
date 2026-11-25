namespace Engine;

/// <summary>
/// Execution context wrapping <see cref="IGraphicsDevice"/>, <see cref="ICommandBuffer"/>,
/// and <see cref="DynamicBufferAllocator"/> for render graph node execution.
/// Nodes use this to begin tracked render passes and issue GPU commands.
/// </summary>
/// <seealso cref="INode"/>
/// <seealso cref="TrackedRenderPass"/>
internal sealed class RenderContext
{
    /// <summary>The low-level graphics device for resource creation and GPU commands.</summary>
    public IGraphicsDevice Device { get; }

    /// <summary>The active command buffer for recording GPU commands this frame.</summary>
    public ICommandBuffer CommandBuffer { get; private set; }

    /// <summary>Frame-aware dynamic buffer allocator for transient GPU data.
    /// Allocations are valid only for the current frame.</summary>
    public DynamicBufferAllocator? DynamicAllocator { get; private set; }

    // The frames contexts were made or begun again for, counted across every context.
    private static long _frames;

    /// <summary>
    /// The frame the context was made or last begun again for, a number no other frame shares,
    /// which a node that does a thing once a frame reads, since one context serves frame after
    /// frame.
    /// </summary>
    internal long Frame { get; private set; } = Interlocked.Increment(ref _frames);

    /// <summary>
    /// The context begun again for the next frame, on its own command buffer and allocator.
    /// </summary>
    internal RenderContext Again(ICommandBuffer commandBuffer, DynamicBufferAllocator? dynamicAllocator)
    {
        CommandBuffer = commandBuffer;
        DynamicAllocator = dynamicAllocator;
        Frame = Interlocked.Increment(ref _frames);
        return this;
    }

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

    /// <summary>
    /// Begins <paramref name="kept"/> again where it has ended, or else one made and kept in it, so
    /// a node that opens one pass in every frame makes it once.
    /// </summary>
    internal TrackedRenderPass BeginTrackedRenderPass(RenderPassDescriptor descriptor, ref TrackedRenderPass? kept) =>
        kept is { } pass && pass.TryBeginAgain(Device, CommandBuffer, descriptor) ? pass : kept = BeginTrackedRenderPass(descriptor);
}
