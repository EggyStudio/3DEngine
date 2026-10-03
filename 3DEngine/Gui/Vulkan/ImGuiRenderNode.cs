using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Engine;

/// <summary>
/// Render graph node that draws ImGui draw data using Vulkan.
/// Reads draw data directly from ImGui (valid after ImGui.Render(), before next NewFrame()).
/// Manages its own pipeline and font atlas texture; vertex/index buffers are
/// transiently allocated from the <see cref="DynamicBufferAllocator"/> each frame.
/// Draws into the shared <see cref="ActiveSwapchainPass"/> opened by <see cref="MainPassNode"/>
/// (no separate render pass begin/end).
/// </summary>
/// <seealso cref="VulkanImGuiPlugin"/>
internal sealed class ImGuiRenderNode : INode, IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.ImGui.Vulkan");

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;

    // Pipeline and font resources, created lazily on first Run.
    private IPipeline? _pipeline;
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private IImage? _fontImage;
    private IImageView? _fontImageView;
    private ISampler? _fontSampler;
    private IDescriptorSet? _fontDescriptorSet;

    /// <summary>Creates a new <see cref="ImGuiRenderNode"/> with pre-compiled shader SPIR-V bytecode.</summary>
    /// <param name="vertexSpv">Compiled SPIR-V bytecode for the ImGui vertex shader.</param>
    /// <param name="fragmentSpv">Compiled SPIR-V bytecode for the ImGui fragment shader.</param>
    public ImGuiRenderNode(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <inheritdoc />
    public unsafe void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        // Close the ImGui frame here (Stage.Last) so all Stage.Render UI emitters have run.
        ImGui.Render();

        var drawData = ImGui.GetDrawData();
        if (!drawData.Valid || drawData.CmdListsCount == 0)
            return;

        // Draw into the shared swapchain pass opened by MainPassNode.
        var activePass = renderWorld.TryGet<ActiveSwapchainPass>();
        if (activePass is null) return;

        var gfx = renderContext.Device;
        var allocator = renderContext.DynamicAllocator;
        var swapchainTarget = renderWorld.TryGet<SwapchainTarget>();
        if (swapchainTarget is null) return;

        if (_pipeline is null)
        {
            CreatePipelineAndFontAtlas(gfx, swapchainTarget.RenderPass);
        }

        int totalVertices = drawData.TotalVtxCount;
        int totalIndices = drawData.TotalIdxCount;
        if (totalVertices == 0 || totalIndices == 0)
            return;

        ulong vertexSize = (ulong)(totalVertices * sizeof(ImDrawVert));
        ulong indexSize = (ulong)(totalIndices * sizeof(ushort));

        DynamicAllocation vertexAlloc, indexAlloc;
        if (allocator is not null)
        {
            vertexAlloc = allocator.Allocate(vertexSize, BufferUsage.Vertex);
            indexAlloc = allocator.Allocate(indexSize, BufferUsage.Index);
        }
        else
        {
            return; // No allocator - cannot upload ImGui geometry.
        }

        // Upload vertex/index data, concatenating each ImGui command list into the
        // single transient vertex and index buffer.
        {
            var vtxSpan = allocator.Map(vertexAlloc);
            var idxSpan = allocator.Map(indexAlloc);

            int vtxOffset = 0;
            int idxOffset = 0;
            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                var cmdList = drawData.CmdLists[n];
                int vtxBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
                int idxBytes = cmdList.IdxBuffer.Size * sizeof(ushort);

                new Span<byte>((void*)cmdList.VtxBuffer.Data, vtxBytes)
                    .CopyTo(vtxSpan.Slice(vtxOffset, vtxBytes));
                new Span<byte>((void*)cmdList.IdxBuffer.Data, idxBytes)
                    .CopyTo(idxSpan.Slice(idxOffset, idxBytes));

                vtxOffset += vtxBytes;
                idxOffset += idxBytes;
            }

            allocator.Unmap(vertexAlloc);
            allocator.Unmap(indexAlloc);
        }

        var pass = activePass.Pass;
        var extent = activePass.Extent;

        // Orthographic projection matching ImGui's display rect.
        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        var projection = new Matrix4x4(
            2.0f / (R - L), 0, 0, 0,
            0, 2.0f / (B - T), 0, 0,
            0, 0, -1.0f, 0,
            -(R + L) / (R - L), -(T + B) / (B - T), 0, 1.0f
        );

        // Viewport in framebuffer pixels, accounting for DPI / framebuffer scale.
        float fbScaleX = drawData.FramebufferScale.X;
        float fbScaleY = drawData.FramebufferScale.Y;
        float fbWidth = drawData.DisplaySize.X * fbScaleX;
        float fbHeight = drawData.DisplaySize.Y * fbScaleY;
        if (fbWidth <= 0 || fbHeight <= 0)
            return;

        pass.SetViewport(0, 0, fbWidth, fbHeight, 0, 1);

        pass.SetPipeline(_pipeline!);
        pass.SetBindGroup(_pipeline!, _fontDescriptorSet!);

        var projBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in projection));
        pass.PushConstants(_pipeline!, ShaderStageFlags.Vertex, 0, projBytes);

        pass.SetVertexBuffer(0, new[] { vertexAlloc.Buffer }, new ulong[] { vertexAlloc.Offset });
        pass.SetIndexBuffer(indexAlloc.Buffer, indexAlloc.Offset, IndexType.UInt16);

        var clipOff = drawData.DisplayPos;
        var clipScale = drawData.FramebufferScale;

        int globalVtxOffset = 0;
        int globalIdxOffset = 0;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];
            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var pcmd = cmdList.CmdBuffer[i];

                // User callbacks are not supported by this backend.
                if (pcmd.UserCallback != IntPtr.Zero)
                    continue;

                var clipMin = new Vector2(
                    (pcmd.ClipRect.X - clipOff.X) * clipScale.X,
                    (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y);
                var clipMax = new Vector2(
                    (pcmd.ClipRect.Z - clipOff.X) * clipScale.X,
                    (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y);

                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                    continue;

                // Clamp scissor rect to framebuffer bounds (negative origin not allowed).
                int sx = Math.Max(0, (int)clipMin.X);
                int sy = Math.Max(0, (int)clipMin.Y);
                uint sw = (uint)(clipMax.X - sx);
                uint sh = (uint)(clipMax.Y - sy);

                if (sw == 0 || sh == 0) continue;

                pass.SetScissor(sx, sy, sw, sh);

                pass.DrawIndexed(
                    pcmd.ElemCount,
                    instanceCount: 1,
                    firstIndex: (uint)(pcmd.IdxOffset + globalIdxOffset),
                    vertexOffset: (int)(pcmd.VtxOffset + globalVtxOffset),
                    firstInstance: 0);
            }

            globalVtxOffset += cmdList.VtxBuffer.Size;
            globalIdxOffset += cmdList.IdxBuffer.Size;
        }

        // Restore full-framebuffer scissor for any subsequent overlay nodes.
        pass.SetScissor(0, 0, extent.Width, extent.Height);
    }

    /// <summary>Creates the ImGui graphics pipeline (with alpha blending and push-constant projection) and uploads the font atlas texture.</summary>
    private unsafe void CreatePipelineAndFontAtlas(IGraphicsDevice gfx, IRenderPass renderPass)
    {
        Logger.Info("Creating ImGui Vulkan pipeline and font atlas...");

        var vsDesc = new ShaderDesc(ShaderStage.Vertex, _vertexSpv, "main");
        var fsDesc = new ShaderDesc(ShaderStage.Fragment, _fragmentSpv, "main");
        _vertexShader = gfx.CreateShader(vsDesc);
        _fragmentShader = gfx.CreateShader(fsDesc);

        // ImDrawVert layout: pos (vec2, 8 bytes), uv (vec2, 8 bytes), col (uint32, 4 bytes) = 20 bytes
        var vertexBindings = new[]
        {
            new VertexInputBindingDesc(0, (uint)sizeof(ImDrawVert))
        };
        var vertexAttributes = new[]
        {
            new VertexInputAttributeDesc(0, 0, VertexFormat.Float2, 0),         // aPos
            new VertexInputAttributeDesc(1, 0, VertexFormat.Float2, 8),         // aUV
            new VertexInputAttributeDesc(2, 0, VertexFormat.UNormR8G8B8A8, 16) // aColor
        };
        var pushConstants = new[]
        {
            new PushConstantRange(ShaderStageFlags.Vertex, 0, (uint)sizeof(Matrix4x4))
        };

        var pipelineDesc = new GraphicsPipelineDesc(
            renderPass, _vertexShader, _fragmentShader,
            BlendEnabled: true,
            CullBackFace: false,
            VertexBindings: vertexBindings,
            VertexAttributes: vertexAttributes,
            PushConstantRanges: pushConstants);

        _pipeline = gfx.CreateGraphicsPipeline(pipelineDesc);
        Logger.Info("ImGui pipeline created.");

        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);

        var imageDesc = new ImageDesc(
            new Extent2D((uint)width, (uint)height),
            ImageFormat.R8G8B8A8_UNorm,
            ImageUsage.Sampled | ImageUsage.TransferDst);
        _fontImage = gfx.CreateImage(imageDesc);
        _fontImageView = gfx.CreateImageView(_fontImage);
        _fontSampler = gfx.CreateSampler(new SamplerDesc(
            SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge,
            SamplerAddressMode.ClampToEdge));

        int dataSize = width * height * bytesPerPixel;
        var pixelData = new ReadOnlySpan<byte>((void*)pixels, dataSize);
        gfx.UploadTexture2D(_fontImage, pixelData, (uint)width, (uint)height, bytesPerPixel);

        _fontDescriptorSet = gfx.CreateDescriptorSet();
        var samplerBinding = new CombinedImageSamplerBinding(_fontImageView, _fontSampler, 1);
        gfx.UpdateDescriptorSet(_fontDescriptorSet, uniformBinding: null, samplerBinding);

        // Tag the atlas with a non-zero ID and free CPU-side pixel memory.
        io.Fonts.SetTexID((IntPtr)1);
        io.Fonts.ClearTexData();

        Logger.Info($"ImGui font atlas uploaded: {width}x{height} R8G8B8A8_UNorm.");
    }

    /// <summary>Disposes the font descriptor set, sampler, image view, image, and shader modules.</summary>
    public void Dispose()
    {
        _fontDescriptorSet?.Dispose();
        _fontSampler?.Dispose();
        _fontImageView?.Dispose();
        _fontImage?.Dispose();
        _fragmentShader?.Dispose();
        _vertexShader?.Dispose();
    }
}
