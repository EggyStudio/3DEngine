namespace Engine;


/// <summary>
/// Wraps <see cref="IGraphicsDevice.CmdBeginRenderPass"/> / <see cref="IGraphicsDevice.CmdEndRenderPass"/>
/// with typed helpers for pipeline binding, vertex/index buffers, draw calls, and push constants.
/// Auto-ends the render pass on <see cref="Dispose"/>.
/// </summary>
/// <seealso cref="RenderContext"/>
/// <seealso cref="RenderPassDescriptor"/>
public sealed class TrackedRenderPass : IDisposable
{
    private readonly IGraphicsDevice _gfx;
    private readonly ICommandBuffer _cmd;
    private bool _ended;

    /// <summary>Creates and begins a tracked render pass from the given descriptor.</summary>
    internal TrackedRenderPass(IGraphicsDevice gfx, ICommandBuffer cmd, RenderPassDescriptor desc)
    {
        _gfx = gfx;
        _cmd = cmd;

        ClearColor? clear = desc.ColorLoadOp == LoadOp.Clear
            ? (desc.ClearColor ?? Engine.ClearColor.Black)
            : null;
        gfx.CmdBeginRenderPass(cmd, desc.RenderPass, desc.Framebuffer, desc.Extent, clear);
    }

    /// <summary>Binds a graphics pipeline for subsequent draw calls.</summary>
    public void SetPipeline(IPipeline pipeline) => _gfx.BindGraphicsPipeline(_cmd, pipeline);

    /// <summary>Sets the viewport rectangle.</summary>
    public void SetViewport(float x, float y, float w, float h, float minDepth = 0, float maxDepth = 1)
        => _gfx.SetViewport(_cmd, x, y, w, h, minDepth, maxDepth);

    /// <summary>Sets the scissor rectangle.</summary>
    public void SetScissor(int x, int y, uint w, uint h) => _gfx.SetScissor(_cmd, x, y, w, h);

    /// <summary>Binds one or more vertex buffers.</summary>
    public void SetVertexBuffer(uint firstBinding, IBuffer[] buffers, ulong[] offsets)
        => _gfx.BindVertexBuffers(_cmd, firstBinding, buffers, offsets);

    /// <summary>Binds an index buffer.</summary>
    public void SetIndexBuffer(IBuffer buffer, ulong offset, IndexType indexType)
        => _gfx.BindIndexBuffer(_cmd, buffer, offset, indexType);

    /// <summary>Binds a descriptor set to a pipeline.</summary>
    public void SetBindGroup(IPipeline pipeline, IDescriptorSet descriptorSet)
        => _gfx.BindDescriptorSet(_cmd, pipeline, descriptorSet);

    /// <summary>Issues a non-indexed draw call.</summary>
    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        => _gfx.Draw(_cmd, vertexCount, instanceCount, firstVertex, firstInstance);

    /// <summary>Issues an indexed draw call.</summary>
    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
        => _gfx.DrawIndexed(_cmd, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);

    /// <summary>Uploads push constant data.</summary>
    public void PushConstants(IPipeline pipeline, ShaderStageFlags stages, uint offset, ReadOnlySpan<byte> data)
        => _gfx.PushConstants(_cmd, pipeline, stages, offset, data);

    /// <summary>Explicitly ends the render pass. Safe to call multiple times.</summary>
    public void EndRenderPass()
    {
        if (!_ended)
        {
            _gfx.CmdEndRenderPass(_cmd);
            _ended = true;
        }
    }

    /// <inheritdoc />
    public void Dispose() => EndRenderPass();
}
