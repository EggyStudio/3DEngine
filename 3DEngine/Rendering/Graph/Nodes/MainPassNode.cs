using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Main render graph node that begins the swapchain render pass with <see cref="LoadOp.Clear"/>,
/// sets up the camera UBO, and drains <see cref="Opaque3dPhase"/> and <see cref="Transparent3dPhase"/>
/// via their draw functions.
/// The render pass is left OPEN and published as <see cref="ActiveSwapchainPass"/> in the
/// <see cref="RenderWorld"/> so downstream overlay nodes (webview, imgui) can draw into the
/// same pass without the overhead of separate begin/end cycles.
/// <see cref="Renderer"/> ends the pass after all graph nodes have executed.
/// </summary>
/// <seealso cref="ActiveSwapchainPass"/>
/// <seealso cref="MeshPipeline"/>
/// <seealso cref="CameraExtract"/>
/// <seealso cref="Opaque3dPhase"/>
/// <seealso cref="Transparent3dPhase"/>
public sealed class MainPassNode : INode, IDisposable
{
    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;

    private IDescriptorSet? _cameraSet;
    private MeshPipeline? _meshPipeline;
    private DrawMeshOpaque? _drawOpaque;
    private DrawMeshTransparent? _drawTransparent;

    /// <summary>Creates a new <see cref="MainPassNode"/> with pre-compiled mesh shader SPIR-V bytecode.</summary>
    /// <param name="vertexSpv">Compiled SPIR-V bytecode for the mesh vertex shader.</param>
    /// <param name="fragmentSpv">Compiled SPIR-V bytecode for the mesh fragment shader.</param>
    public MainPassNode(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        var gfx = renderContext.Device;
        var swapchainTarget = renderWorld.TryGet<SwapchainTarget>();
        if (swapchainTarget is null) return;

        var clearColor = renderWorld.TryGet<ClearColor>() is { } cc ? cc : ClearColor.Black;

        var passDesc = new RenderPassDescriptor(
            swapchainTarget.RenderPass,
            swapchainTarget.Framebuffer,
            swapchainTarget.Extent,
            LoadOp.Clear,
            StoreOp.Store,
            clearColor);

        var pass = renderContext.BeginTrackedRenderPass(passDesc);

        var extent = swapchainTarget.Extent;
        pass.SetViewport(0, 0, extent.Width, extent.Height, 0, 1);
        pass.SetScissor(0, 0, extent.Width, extent.Height);

        renderWorld.Set(new ActiveSwapchainPass(pass, extent));

        // -- Camera-dependent drawing --
        ExtractedView? firstView = null;
        foreach (var (_, view) in renderWorld.Entities.Query<ExtractedView>())
        {
            firstView = view;
            break;
        }

        if (firstView is null)
            return; // Clear was already issued; overlays still draw.

        var camera = firstView.Value;

        // -- Prepare camera UBO --
        var allocator = renderContext.DynamicAllocator;
        if (allocator is not null)
        {
            var uniform = new CameraUniform
            {
                View = camera.View,
                Projection = camera.Projection
            };

            var sizeBytes = (ulong)Marshal.SizeOf<CameraUniform>();
            var alloc = allocator.Allocate(sizeBytes, BufferUsage.Uniform);

            var span = allocator.Map(alloc);
            MemoryMarshal.Write(span, in uniform);
            allocator.Unmap(alloc);

            _cameraSet ??= gfx.CreateDescriptorSet();

            var uboBinding = new UniformBufferBinding(alloc.Buffer, 0, alloc.Offset, sizeBytes);
            gfx.UpdateDescriptorSet(_cameraSet, uboBinding, samplerBinding: null);
        }

        if (_cameraSet is null) return;

        if (_meshPipeline is null)
        {
            var cache = renderWorld.TryGet<PipelineCache>();
            _meshPipeline = new MeshPipeline(gfx, swapchainTarget.RenderPass,
                _vertexSpv, _fragmentSpv, cache);
            _drawOpaque = new DrawMeshOpaque(_meshPipeline.Pipeline);
            _drawTransparent = new DrawMeshTransparent(_meshPipeline.Pipeline);
        }

        pass.SetPipeline(_meshPipeline.Pipeline);
        pass.SetBindGroup(_meshPipeline.Pipeline, _cameraSet);

        // -- Drain opaque phase (front-to-back) --
        var opaquePhase = renderWorld.TryGet<Opaque3dPhase>();
        if (opaquePhase is not null)
        {
            var items = opaquePhase.Phase.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var drawFn = item.DrawFunction ?? _drawOpaque!;
                drawFn.Draw(ref item, pass, renderWorld);
            }
        }

        // -- Drain transparent phase (back-to-front) --
        var transparentPhase = renderWorld.TryGet<Transparent3dPhase>();
        if (transparentPhase is not null)
        {
            var items = transparentPhase.Phase.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var drawFn = item.DrawFunction ?? _drawTransparent!;
                drawFn.Draw(ref item, pass, renderWorld);
            }
        }
    }

    /// <summary>Disposes the persistent camera descriptor set.</summary>
    public void Dispose()
    {
        _cameraSet?.Dispose();
    }
}
