namespace Engine;

/// <summary>
/// Primary graphics device interface for GPU resource creation, frame management, and draw commands.
/// </summary>
/// <remarks>
/// Implementations include <see cref="GraphicsDevice"/> (Vulkan) and <see cref="NullGraphicsDevice"/> (headless/testing).
/// The device must be initialized via <see cref="Initialize"/> before any other method is called.
/// </remarks>
/// <seealso cref="GraphicsDevice"/>
/// <seealso cref="NullGraphicsDevice"/>
public interface IGraphicsDevice : IDisposable
{
    /// <summary>Whether the device has been successfully initialized.</summary>
    bool IsInitialized { get; }

    /// <summary>The swapchain providing presentable images.</summary>
    ISwapchain Swapchain { get; }

    /// <summary>Information about the selected graphics adapter (GPU).</summary>
    GraphicsAdapterInfo AdapterInfo { get; }

    /// <summary>Total number of frames that may be in flight simultaneously.</summary>
    int FramesInFlight { get; }

    /// <summary>Initializes the graphics device, creating the instance, surface, physical/logical device, swapchain, and sync objects.</summary>
    /// <param name="surfaceSource">Platform-specific surface provider for window integration.</param>
    /// <param name="appName">Application name passed to the Vulkan instance.</param>
    void Initialize(ISurfaceSource surfaceSource, string appName = "3DEngine");

    /// <summary>Begins a new frame, acquiring a swapchain image and returning a frame context for recording commands.</summary>
    /// <param name="clearOverride">Optional clear color override; defaults to <see cref="ClearColor.Black"/>.</param>
    /// <returns>An <see cref="IFrameContext"/> for recording draw commands.</returns>
    IFrameContext BeginFrame(ClearColor? clearOverride = null);

    /// <summary>Ends the current frame, submitting recorded commands and presenting the swapchain image.</summary>
    /// <param name="frameContext">The frame context returned by <see cref="BeginFrame"/>.</param>
    void EndFrame(IFrameContext frameContext);

    /// <summary>Handles window resize by recreating swapchain resources.</summary>
    void OnResize();

    // -- Swapchain Resources --

    /// <summary>The swapchain-compatible render pass. Nodes use this to begin their own render passes targeting the swapchain.</summary>
    IRenderPass SwapchainRenderPass { get; }

    /// <summary>The swapchain-compatible render pass with <c>loadOp = Load</c>.
    /// Nodes that render after the initial clear pass use this to preserve existing framebuffer content.</summary>
    IRenderPass SwapchainLoadRenderPass { get; }

    /// <summary>Gets the swapchain framebuffer for the given image index (from <see cref="IFrameContext.FrameIndex"/>).</summary>
    /// <param name="imageIndex">The swapchain image index.</param>
    /// <returns>The framebuffer for the specified swapchain image.</returns>
    IFramebuffer GetSwapchainFramebuffer(uint imageIndex);

    // -- Buffer API --

    /// <summary>Creates a GPU buffer with the specified descriptor.</summary>
    /// <param name="desc">Buffer creation descriptor (size, usage, CPU access).</param>
    /// <returns>A new <see cref="IBuffer"/> handle.</returns>
    IBuffer CreateBuffer(BufferDesc desc);

    /// <summary>Maps a buffer's memory for CPU access and returns a writable span.</summary>
    /// <param name="buffer">The buffer to map.</param>
    /// <returns>A <see cref="Span{T}"/> of bytes over the mapped memory.</returns>
    Span<byte> Map(IBuffer buffer);

    /// <summary>Unmaps a previously mapped buffer.</summary>
    /// <param name="buffer">The buffer to unmap.</param>
    void Unmap(IBuffer buffer);

    // -- Image / Sampler API --

    /// <summary>Creates a GPU image (texture or render target).</summary>
    /// <param name="desc">Image creation descriptor.</param>
    /// <returns>A new <see cref="IImage"/> handle.</returns>
    IImage CreateImage(ImageDesc desc);

    /// <summary>Creates a typed view into an existing image.</summary>
    /// <param name="image">The image to create a view for.</param>
    /// <returns>A new <see cref="IImageView"/> handle.</returns>
    IImageView CreateImageView(IImage image);

    /// <summary>Creates a texture sampler with the specified filtering and addressing modes.</summary>
    /// <param name="desc">Sampler creation descriptor.</param>
    /// <returns>A new <see cref="ISampler"/> handle.</returns>
    ISampler CreateSampler(SamplerDesc desc);

    // -- Shader / Pipeline API --

    /// <summary>Creates a shader module from SPIR-V bytecode.</summary>
    /// <param name="desc">Shader creation descriptor.</param>
    /// <returns>A new <see cref="IShader"/> handle.</returns>
    IShader CreateShader(ShaderDesc desc);

    /// <summary>Creates a compiled graphics pipeline (vertex + fragment shaders, vertex layout, blend state).</summary>
    /// <param name="desc">Pipeline creation descriptor.</param>
    /// <returns>A new <see cref="IPipeline"/> handle.</returns>
    IPipeline CreateGraphicsPipeline(GraphicsPipelineDesc desc);

    // -- Descriptor API --

    /// <summary>Creates a descriptor set layout from an array of binding descriptions.</summary>
    /// <param name="bindings">The layout bindings.</param>
    /// <returns>A new <see cref="IDescriptorSetLayout"/> handle.</returns>
    IDescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutBinding[] bindings);

    /// <summary>Allocates a descriptor set using the default global layout (binding 0=UBO, 1=sampler).</summary>
    /// <returns>A new <see cref="IDescriptorSet"/> handle.</returns>
    IDescriptorSet CreateDescriptorSet();

    /// <summary>Allocates a descriptor set using a custom layout.</summary>
    /// <param name="layout">The descriptor set layout to allocate from.</param>
    /// <returns>A new <see cref="IDescriptorSet"/> handle.</returns>
    IDescriptorSet CreateDescriptorSet(IDescriptorSetLayout layout);

    /// <summary>Updates a descriptor set with optional uniform buffer and combined image sampler bindings.</summary>
    /// <param name="descriptorSet">The descriptor set to update.</param>
    /// <param name="uniformBinding">Optional uniform buffer binding.</param>
    /// <param name="samplerBinding">Optional combined image sampler binding.</param>
    void UpdateDescriptorSet(IDescriptorSet descriptorSet, in UniformBufferBinding? uniformBinding, in CombinedImageSamplerBinding? samplerBinding);

    // -- Draw API --

    /// <summary>Binds a graphics pipeline for subsequent draw commands.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="pipeline">The pipeline to bind.</param>
    void BindGraphicsPipeline(ICommandBuffer commandBuffer, IPipeline pipeline);

    /// <summary>Binds a descriptor set to a pipeline for shader resource access.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="pipeline">The pipeline the descriptor set is compatible with.</param>
    /// <param name="descriptorSet">The descriptor set to bind.</param>
    void BindDescriptorSet(ICommandBuffer commandBuffer, IPipeline pipeline, IDescriptorSet descriptorSet);

    /// <summary>Issues a non-indexed draw call.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="vertexCount">Number of vertices to draw.</param>
    /// <param name="instanceCount">Number of instances to draw.</param>
    /// <param name="firstVertex">Index of the first vertex.</param>
    /// <param name="firstInstance">Index of the first instance.</param>
    void Draw(ICommandBuffer commandBuffer, uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);

    /// <summary>Issues an indexed draw call.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="indexCount">Number of indices to draw.</param>
    /// <param name="instanceCount">Number of instances to draw.</param>
    /// <param name="firstIndex">Index of the first index.</param>
    /// <param name="vertexOffset">Value added to each index before indexing into the vertex buffer.</param>
    /// <param name="firstInstance">Index of the first instance.</param>
    void DrawIndexed(ICommandBuffer commandBuffer, uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0);

    /// <summary>Binds one or more vertex buffers to the command buffer.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="firstBinding">First binding slot index.</param>
    /// <param name="buffers">Array of vertex buffers to bind.</param>
    /// <param name="offsets">Byte offsets into each buffer.</param>
    void BindVertexBuffers(ICommandBuffer commandBuffer, uint firstBinding, IBuffer[] buffers, ulong[] offsets);

    /// <summary>Binds an index buffer to the command buffer.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="buffer">The index buffer.</param>
    /// <param name="offset">Byte offset into the buffer.</param>
    /// <param name="indexType">Index element type (16-bit or 32-bit).</param>
    void BindIndexBuffer(ICommandBuffer commandBuffer, IBuffer buffer, ulong offset, IndexType indexType);

    // -- Dynamic State --

    /// <summary>Sets the viewport for subsequent draw commands.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="x">Viewport X origin.</param>
    /// <param name="y">Viewport Y origin.</param>
    /// <param name="width">Viewport width.</param>
    /// <param name="height">Viewport height.</param>
    /// <param name="minDepth">Minimum depth value.</param>
    /// <param name="maxDepth">Maximum depth value.</param>
    void SetViewport(ICommandBuffer commandBuffer, float x, float y, float width, float height, float minDepth, float maxDepth);

    /// <summary>Sets the scissor rectangle for subsequent draw commands.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="x">Scissor X origin.</param>
    /// <param name="y">Scissor Y origin.</param>
    /// <param name="width">Scissor width.</param>
    /// <param name="height">Scissor height.</param>
    void SetScissor(ICommandBuffer commandBuffer, int x, int y, uint width, uint height);

    /// <summary>Uploads push constant data for the specified shader stages.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="pipeline">The pipeline defining the push constant layout.</param>
    /// <param name="stageFlags">Shader stages that will consume the data.</param>
    /// <param name="offset">Byte offset into the push constant range.</param>
    /// <param name="data">The push constant data.</param>
    void PushConstants(ICommandBuffer commandBuffer, IPipeline pipeline, ShaderStageFlags stageFlags, uint offset, ReadOnlySpan<byte> data);

    /// <summary>Uploads pixel data to a 2D image/texture.</summary>
    /// <param name="image">The destination image.</param>
    /// <param name="data">Raw pixel data.</param>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <param name="bytesPerPixel">Bytes per pixel (e.g., 4 for RGBA8).</param>
    void UploadTexture2D(IImage image, ReadOnlySpan<byte> data, uint width, uint height, int bytesPerPixel);

    /// <summary>
    /// Records texture upload commands into an existing command buffer without a GPU stall.
    /// The staging buffer is kept alive until <see cref="FlushDeferredStagingBuffers"/> is called
    /// (typically after the frame's fence signals completion).
    /// </summary>
    /// <param name="commandBuffer">The command buffer to record upload commands into (must be outside a render pass).</param>
    /// <param name="image">The destination image.</param>
    /// <param name="data">Raw pixel data.</param>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <param name="bytesPerPixel">Bytes per pixel (e.g., 4 for RGBA8).</param>
    void UploadTexture2DDeferred(ICommandBuffer commandBuffer, IImage image, ReadOnlySpan<byte> data, uint width, uint height, int bytesPerPixel);

    /// <summary>
    /// Disposes all staging buffers from deferred texture uploads for the given in-flight frame slot.
    /// Called after the slot's fence has been waited on, guaranteeing the GPU has finished reading.
    /// </summary>
    /// <param name="inFlightIndex">The in-flight frame slot index (0 .. <see cref="FramesInFlight"/>-1).</param>
    void FlushDeferredStagingBuffers(int inFlightIndex);

    // -- Offscreen Rendering --

    /// <summary>Creates an offscreen render pass with a single color attachment.</summary>
    /// <param name="desc">Render pass creation descriptor.</param>
    /// <returns>A new <see cref="IRenderPass"/> handle.</returns>
    IRenderPass CreateRenderPass(RenderPassDesc desc);

    /// <summary>Creates a framebuffer wrapping an image view for use with an offscreen render pass.</summary>
    /// <param name="desc">Framebuffer creation descriptor.</param>
    /// <returns>A new <see cref="IFramebuffer"/> handle.</returns>
    IFramebuffer CreateFramebuffer(FramebufferDesc desc);

    /// <summary>Allocates and begins a single-use command buffer for immediate GPU work.</summary>
    /// <returns>An <see cref="ICommandBuffer"/> ready for recording commands.</returns>
    ICommandBuffer BeginCommands();

    /// <summary>Ends, submits, and waits for a single-use command buffer, then frees it.</summary>
    /// <param name="commandBuffer">The command buffer returned by <see cref="BeginCommands"/>.</param>
    void SubmitAndWait(ICommandBuffer commandBuffer);

    /// <summary>Records the beginning of a render pass on the given command buffer.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="renderPass">The render pass to begin.</param>
    /// <param name="framebuffer">The framebuffer to render into.</param>
    /// <param name="extent">The render area extent.</param>
    /// <param name="clear">Optional clear color; <c>null</c> means load existing contents.</param>
    void CmdBeginRenderPass(ICommandBuffer commandBuffer, IRenderPass renderPass, IFramebuffer framebuffer, Extent2D extent, ClearColor? clear = null);

    /// <summary>Records the end of the current render pass on the given command buffer.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    void CmdEndRenderPass(ICommandBuffer commandBuffer);

    /// <summary>Records an image layout transition pipeline barrier on the given command buffer.</summary>
    /// <param name="commandBuffer">The active command buffer.</param>
    /// <param name="image">The image to transition.</param>
    /// <param name="oldLayout">The current layout of the image.</param>
    /// <param name="newLayout">The target layout.</param>
    void CmdPipelineBarrier(ICommandBuffer commandBuffer, IImage image, ImageLayout oldLayout, ImageLayout newLayout);
}
