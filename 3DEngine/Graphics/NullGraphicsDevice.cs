namespace Engine;

/// <summary>
/// No-op graphics device implementation for headless runs, unit tests, and editor mode.
/// All resource creation methods throw <see cref="NotSupportedException"/>.
/// </summary>
/// <seealso cref="IGraphicsDevice"/>
/// <seealso cref="GraphicsDevice"/>
public sealed class NullGraphicsDevice : IGraphicsDevice
{
    /// <summary>Null swapchain that always reports a 1×1 extent.</summary>
    private sealed class NullSwapchain : ISwapchain
    {
        /// <inheritdoc />
        public Extent2D Extent { get; private set; } = new(1,1);
        /// <inheritdoc />
        public uint ImageCount => 1;
        /// <inheritdoc />
        public AcquireResult AcquireNextImage(out uint imageIndex) { imageIndex = 0; return AcquireResult.Success; }
        /// <inheritdoc />
        public void Resize(Extent2D newExtent) { Extent = newExtent; }
        /// <inheritdoc />
        public void Dispose() { }
    }
    /// <summary>No-op frame context that holds stub handles and a 1×1 extent.</summary>
    private sealed class NullFrameContext : IFrameContext
    {
        /// <inheritdoc />
        public uint FrameIndex { get; }
        /// <inheritdoc />
        public int InFlightIndex { get; }
        /// <inheritdoc />
        public int FramesInFlight => 1;
        /// <inheritdoc />
        public ICommandBuffer CommandBuffer { get; } = new NullCommandBuffer();
        /// <inheritdoc />
        public Extent2D Extent { get; }
        /// <summary>Creates a frame context for the given frame index.</summary>
        public NullFrameContext(uint idx) { FrameIndex = idx; InFlightIndex = (int)(idx % 1); Extent = new Extent2D(1,1); }
        /// <inheritdoc />
        public void Dispose() { }
    }
    /// <summary>No-op command buffer stub.</summary>
    private sealed class NullCommandBuffer : ICommandBuffer { }
    /// <summary>No-op render pass stub.</summary>
    private sealed class NullRenderPass : IRenderPass { }
    /// <summary>No-op framebuffer stub.</summary>
    private sealed class NullFramebuffer : IFramebuffer { }

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }
    /// <inheritdoc />
    public ISwapchain Swapchain { get; } = new NullSwapchain();
    /// <inheritdoc />
    public int FramesInFlight => 1;
    private uint _frame;
    /// <inheritdoc />
    public void Initialize(ISurfaceSource surfaceSource, string appName = "3DEngine") { IsInitialized = true; }
    /// <inheritdoc />
    public IFrameContext BeginFrame(ClearColor? clearOverride = null) { _frame++; return new NullFrameContext(_frame); }
    /// <inheritdoc />
    public void EndFrame(IFrameContext frameContext) { }
    /// <inheritdoc />
    public void OnResize() { }
    /// <inheritdoc />
    public void Dispose() { }
    /// <inheritdoc />
    public GraphicsAdapterInfo AdapterInfo => GraphicsAdapterInfo.Unknown;
    /// <inheritdoc />
    public IRenderPass SwapchainRenderPass => new NullRenderPass();
    /// <inheritdoc />
    public IRenderPass SwapchainLoadRenderPass => new NullRenderPass();
    /// <inheritdoc />
    public IFramebuffer GetSwapchainFramebuffer(uint imageIndex) => new NullFramebuffer();

    /// <inheritdoc />
    public IBuffer CreateBuffer(BufferDesc desc) => throw new NotSupportedException("NullGraphicsDevice does not support buffer creation.");
    /// <inheritdoc />
    public Span<byte> Map(IBuffer buffer) => throw new NotSupportedException("NullGraphicsDevice does not support buffer mapping.");
    /// <inheritdoc />
    public void Unmap(IBuffer buffer) { }

    /// <inheritdoc />
    public IImage CreateImage(ImageDesc desc) => throw new NotSupportedException("NullGraphicsDevice does not support images.");
    /// <inheritdoc />
    public IImageView CreateImageView(IImage image) => throw new NotSupportedException("NullGraphicsDevice does not support image views.");
    /// <inheritdoc />
    public ISampler CreateSampler(SamplerDesc desc) => throw new NotSupportedException("NullGraphicsDevice does not support samplers.");

    /// <inheritdoc />
    public IShader CreateShader(ShaderDesc desc) => throw new NotSupportedException("NullGraphicsDevice does not support shaders.");
    /// <inheritdoc />
    public IPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc) => throw new NotSupportedException("NullGraphicsDevice does not support pipelines.");

    /// <inheritdoc />
    public IDescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutBinding[] bindings) => throw new NotSupportedException("NullGraphicsDevice does not support descriptors.");
    /// <inheritdoc />
    public IDescriptorSet CreateDescriptorSet() => throw new NotSupportedException("NullGraphicsDevice does not support descriptors.");
    /// <inheritdoc />
    public IDescriptorSet CreateDescriptorSet(IDescriptorSetLayout layout) => throw new NotSupportedException("NullGraphicsDevice does not support descriptors.");
    /// <inheritdoc />
    public void UpdateDescriptorSet(IDescriptorSet descriptorSet, in UniformBufferBinding? uniformBinding, in CombinedImageSamplerBinding? samplerBinding)
        => throw new NotSupportedException("NullGraphicsDevice does not support descriptors.");

    /// <inheritdoc />
    public void BindGraphicsPipeline(ICommandBuffer commandBuffer, IPipeline pipeline) { }
    /// <inheritdoc />
    public void BindDescriptorSet(ICommandBuffer commandBuffer, IPipeline pipeline, IDescriptorSet descriptorSet) { }
    /// <inheritdoc />
    public void Draw(ICommandBuffer commandBuffer, uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0) { }
    /// <inheritdoc />
    public void DrawIndexed(ICommandBuffer commandBuffer, uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0) { }
    /// <inheritdoc />
    public void BindVertexBuffers(ICommandBuffer commandBuffer, uint firstBinding, IBuffer[] buffers, ulong[] offsets) { }
    /// <inheritdoc />
    public void BindIndexBuffer(ICommandBuffer commandBuffer, IBuffer buffer, ulong offset, IndexType indexType) { }
    /// <inheritdoc />
    public void SetViewport(ICommandBuffer commandBuffer, float x, float y, float width, float height, float minDepth, float maxDepth) { }
    /// <inheritdoc />
    public void SetScissor(ICommandBuffer commandBuffer, int x, int y, uint width, uint height) { }
    /// <inheritdoc />
    public void PushConstants(ICommandBuffer commandBuffer, IPipeline pipeline, ShaderStageFlags stageFlags, uint offset, ReadOnlySpan<byte> data) { }

    /// <inheritdoc />
    public void UploadTexture2D(IImage image, ReadOnlySpan<byte> data, uint width, uint height, int bytesPerPixel)
        => throw new NotSupportedException("NullGraphicsDevice does not support texture upload.");
    /// <inheritdoc />
    public void UploadTexture2DDeferred(ICommandBuffer commandBuffer, IImage image, ReadOnlySpan<byte> data, uint width, uint height, int bytesPerPixel)
        => throw new NotSupportedException("NullGraphicsDevice does not support texture upload.");
    /// <inheritdoc />
    public void FlushDeferredStagingBuffers(int inFlightIndex) { }

    /// <inheritdoc />
    public IRenderPass CreateRenderPass(RenderPassDesc desc)
        => throw new NotSupportedException("NullGraphicsDevice does not support offscreen rendering.");
    /// <inheritdoc />
    public IFramebuffer CreateFramebuffer(FramebufferDesc desc)
        => throw new NotSupportedException("NullGraphicsDevice does not support offscreen rendering.");
    /// <inheritdoc />
    public ICommandBuffer BeginCommands()
        => throw new NotSupportedException("NullGraphicsDevice does not support command buffers.");
    /// <inheritdoc />
    public void SubmitAndWait(ICommandBuffer commandBuffer)
        => throw new NotSupportedException("NullGraphicsDevice does not support command buffers.");
    /// <inheritdoc />
    public void CmdBeginRenderPass(ICommandBuffer commandBuffer, IRenderPass renderPass, IFramebuffer framebuffer, Extent2D extent, ClearColor? clear = null) { }
    /// <inheritdoc />
    public void CmdEndRenderPass(ICommandBuffer commandBuffer) { }
    /// <inheritdoc />
    public void CmdPipelineBarrier(ICommandBuffer commandBuffer, IImage image, ImageLayout oldLayout, ImageLayout newLayout) { }
}
