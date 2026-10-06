using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Engine;

/// <summary>
/// Render graph node that draws ImGui draw data using Vulkan.
/// Reads draw data directly from ImGui (valid after ImGui.Render(), before next NewFrame()).
/// Manages its own pipelines and font atlas texture; vertex/index buffers are
/// transiently allocated from the <see cref="DynamicBufferAllocator"/> each frame.
/// Draws the main viewport into the shared <see cref="ActiveSwapchainPass"/> opened by
/// <see cref="MainPassNode"/>, and each other viewport into its own window's swapchain once that
/// pass has ended (<see cref="SdlImGuiViewports"/>).
/// </summary>
/// <seealso cref="VulkanImGuiPlugin"/>
internal sealed class ImGuiRenderNode : INode, IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.ImGui.Vulkan");

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;

    // A pipeline for each pass drawn into, since the window's may be multisampled where a
    // viewport's window's swapchain is not, and the font, created lazily on first Run.
    private readonly Dictionary<IRenderPass, IPipeline> _pipelines = [];
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private IImage? _fontImage;
    private IImageView? _fontImageView;
    private ISampler? _fontSampler;
    private IDescriptorSet? _fontDescriptorSet;

    // Whether ImGui had windows of its own last frame, which are closed when the program turns
    // viewports off.
    private bool _hadViewports;

    /// <summary>Creates a new <see cref="ImGuiRenderNode"/> with pre-compiled shader SPIR-V bytecode.</summary>
    /// <param name="vertexSpv">Compiled SPIR-V bytecode for the ImGui vertex shader.</param>
    /// <param name="fragmentSpv">Compiled SPIR-V bytecode for the ImGui fragment shader.</param>
    public ImGuiRenderNode(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        // Close the ImGui frame here (Stage.Last) so all Stage.Render UI emitters have run.
        ImGui.Render();

        // Draw into the shared swapchain pass opened by MainPassNode.
        var activePass = renderWorld.TryGet<ActiveSwapchainPass>();
        var swapchainTarget = renderWorld.TryGet<SwapchainTarget>();
        if (activePass is null || swapchainTarget is null) return;
        Draw(renderContext, activePass.Pass, swapchainTarget.RenderPass, ImGui.GetDrawData(), activePass.Extent);
    }

    /// <summary>
    /// Makes, moves and closes the windows of ImGui's viewports, where a program has them on, and
    /// draws each into its own window's swapchain, which the frame's submit presents with the main
    /// window's.
    /// </summary>
    public void AfterWindowPass(RenderContext renderContext, RenderWorld renderWorld)
    {
        if (!SdlImGuiViewports.Installed) return;
        var device = renderContext.Device as GraphicsDevice;
        SdlImGuiViewports.Device = device;
        if (!SdlImGuiViewports.Enabled)
        {
            if (_hadViewports) SdlImGuiViewports.DestroyWindows();
            _hadViewports = false;
        }
        else _hadViewports = true;

        // Every frame, on or off, since ImGui holds a program that turns viewports on between two
        // frames to having called it after the first.
        SdlImGuiViewports.Update();
        if (!_hadViewports || device is not { HasWindowSurfaces: true }) return;

        var viewports = ImGui.GetPlatformIO().Viewports;
        for (int i = 1; i < viewports.Size; i++)
        {
            var viewport = viewports[i];
            if ((viewport.Flags & ImGuiViewportFlags.IsMinimized) != 0 || !viewport.DrawData.Valid) continue;
            if (SdlImGuiViewports.SurfaceOf(viewport) is not { } surface || !device.AcquireWindowImage(surface)) continue;

            // Begun once the image is acquired, whatever is drawn, so the clear leaves it ready to present.
            using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
                surface.Pass, surface.Framebuffer, surface.Size, LoadOp.Clear, StoreOp.Store, ClearColor.Black));
            Draw(renderContext, pass, surface.Pass, viewport.DrawData, surface.Size);
        }
    }

    // Draws one viewport's draw data into the pass open on its window, which is of renderPass.
    private unsafe void Draw(RenderContext renderContext, TrackedRenderPass pass, IRenderPass renderPass, ImDrawDataPtr drawData, Extent2D extent)
    {
        if (!drawData.Valid || drawData.CmdListsCount == 0)
            return;

        var gfx = renderContext.Device;
        var allocator = renderContext.DynamicAllocator;
        if (allocator is null)
            return; // No allocator - cannot upload ImGui geometry.

        var pipeline = Pipeline(gfx, renderPass);

        int totalVertices = drawData.TotalVtxCount;
        int totalIndices = drawData.TotalIdxCount;
        if (totalVertices == 0 || totalIndices == 0)
            return;

        ulong vertexSize = (ulong)(totalVertices * sizeof(ImDrawVert));
        ulong indexSize = (ulong)(totalIndices * sizeof(ushort));

        var vertexAlloc = allocator.Allocate(vertexSize, BufferUsage.Vertex);
        var indexAlloc = allocator.Allocate(indexSize, BufferUsage.Index);

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

        // Orthographic projection matching ImGui's display rect, which is the desktop's with
        // viewports on and the window's without.
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

        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, _fontDescriptorSet!);

        var projBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in projection));
        pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, projBytes);

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

    // The pipeline for a pass, made the first time it is drawn into, with the font on first use.
    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass)
    {
        if (_pipelines.TryGetValue(renderPass, out var pipeline)) return pipeline;
        if (_fontDescriptorSet is null) CreateFontAtlas(gfx);
        return _pipelines[renderPass] = CreatePipeline(gfx, renderPass);
    }

    /// <summary>Creates the ImGui graphics pipeline for a pass, with alpha blending and push-constant projection.</summary>
    private unsafe IPipeline CreatePipeline(IGraphicsDevice gfx, IRenderPass renderPass)
    {
        Logger.Info("Creating an ImGui Vulkan pipeline...");

        _vertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv, "main"));
        _fragmentShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv, "main"));

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
            Cull: CullMode.None,
            VertexBindings: vertexBindings,
            VertexAttributes: vertexAttributes,
            PushConstantRanges: pushConstants);

        var pipeline = gfx.CreateGraphicsPipeline(pipelineDesc);
        Logger.Info("ImGui pipeline created.");
        return pipeline;
    }

    /// <summary>Uploads the font atlas texture and makes the set it is read through.</summary>
    private unsafe void CreateFontAtlas(IGraphicsDevice gfx)
    {
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

    /// <summary>
    /// Closes the viewports' windows and swapchains while the device is there to destroy them on,
    /// then disposes the pipelines, which this node made itself rather than through the pipeline
    /// cache, the font descriptor set, sampler, image view, image, and shader modules.
    /// </summary>
    public void Dispose()
    {
        SdlImGuiViewports.DestroyWindows();
        SdlImGuiViewports.Device = null;
        foreach (var pipeline in _pipelines.Values) pipeline.Dispose();
        _pipelines.Clear();
        _fontDescriptorSet?.Dispose();
        _fontSampler?.Dispose();
        _fontImageView?.Dispose();
        _fontImage?.Dispose();
        _fragmentShader?.Dispose();
        _vertexShader?.Dispose();
    }
}
