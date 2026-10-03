using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Render graph node that draws the frame's <see cref="ModelDrawList"/> into the swapchain pass,
/// after the ECS meshes and before the immediate shapes, with <c>model.slang</c>.
/// </summary>
/// <remarks>
/// Each draw binds its mesh's buffers from <see cref="GpuMeshes"/> and its texture from
/// <see cref="GpuTextures"/>, and pushes its transform, the rotation part of its world matrix and
/// its color. The pipeline does not cull, because a model loaded from a file may wind its
/// triangles either way, and the cost is small next to drawing a back face wrong.
/// </remarks>
public sealed class ModelNode : INode, IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 Transform;
        public Vector4 NormalX;
        public Vector4 NormalY;
        public Vector4 NormalZ;
        public Vector4 Color;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private IPipeline? _pipeline;

    /// <summary>Creates the node from the compiled stages of <c>model.slang</c>.</summary>
    public ModelNode(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        if (draws is null || draws.Draws.Count == 0) return;

        var activePass = renderWorld.TryGet<ActiveSwapchainPass>();
        var swapchainTarget = renderWorld.TryGet<SwapchainTarget>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (activePass is null || swapchainTarget is null || meshes is null || textures is null) return;

        var gfx = renderContext.Device;
        var pipeline = Pipeline(gfx, swapchainTarget.RenderPass, renderWorld);
        var pass = activePass.Pass;
        pass.SetPipeline(pipeline);

        foreach (var draw in draws.Draws)
        {
            // A mesh unloaded after its draw was recorded is skipped.
            if (meshes.Get(draw.Mesh) is not { } mesh) continue;

            pass.SetBindGroup(pipeline, textures.SetFor(gfx, draw.Texture));
            pass.SetVertexBuffer(0, [mesh.Vertices], [0]);
            pass.SetIndexBuffer(mesh.Indices, 0, IndexType.UInt32);

            var w = draw.World;
            var push = new Push
            {
                Transform = w * draw.ViewProjection,
                NormalX = new Vector4(w.M11, w.M21, w.M31, 0),
                NormalY = new Vector4(w.M12, w.M22, w.M32, 0),
                NormalZ = new Vector4(w.M13, w.M23, w.M33, 0),
                Color = draw.Color.ToVector4(),
            };
            pass.PushConstants(pipeline, ShaderStageFlags.All, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
            pass.DrawIndexed(mesh.IndexCount);
        }
    }

    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld)
    {
        if (_pipeline is not null) return _pipeline;

        _vertexShader = gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragmentShader = gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));

        var desc = new GraphicsPipelineDesc(
            renderPass,
            _vertexShader,
            _fragmentShader,
            BlendEnabled: true,
            CullBackFace: false,
            VertexBindings: [new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ModelVertex>())],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0),
                new VertexInputAttributeDesc(1, 0, VertexFormat.Float3, 12),
                new VertexInputAttributeDesc(2, 0, VertexFormat.Float2, 24),
            ],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.All, 0, (uint)Marshal.SizeOf<Push>())],
            DepthTestEnabled: true,
            DepthWriteEnabled: true,
            DepthCompareOp: CompareOp.LessOrEqual);

        _pipeline = renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
        return _pipeline;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _fragmentShader?.Dispose();
        _vertexShader?.Dispose();
    }
}
