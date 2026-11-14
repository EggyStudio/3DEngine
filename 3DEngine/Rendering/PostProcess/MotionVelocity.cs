using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Draws each moving mesh entity's movement on the picture since the frame before into an image,
/// which motion blur reads in place of the camera's movement where it is written (<c>velocity.slang</c>).
/// </summary>
/// <remarks>
/// It runs with per-object blur on and an entity moved, after the HDR frame and ahead of the blur.
/// The entities of each mesh are one draw, as instances of their world now and the frame before,
/// written into a buffer of the frame's own, of which there is one for each frame that may be in
/// flight, grown as more entities move. A fragment behind the scene's depth is dropped, so a still
/// thing in front of a moving one keeps the camera's movement. The image is the HDR frame's size in
/// half floats with no depth, and the bloom renderer keeps it with its other images of that size.
/// </remarks>
internal sealed class MotionVelocity : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 ViewProjection, LastViewProjection;
    }


    private readonly IBuffer?[] _instances = new IBuffer?[GpuTextures.RetireFrames + 1];
    private readonly int[] _capacity = new int[GpuTextures.RetireFrames + 1];
    private int _slot;

    private readonly ReadOnlyMemory<byte> _vertexSpv, _fragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _bindings;
    private IShader? _vertex, _fragment;
    private IDescriptorSetLayout? _layout;
    private IPipeline? _pipeline;
    // The pipelines for a mesh of positions alone, by its positions' format, three floats or packed,
    // each made the first time one moves.
    private readonly Dictionary<VertexFormat, IPipeline> _positionsPipelines = [];

    /// <summary>Creates the pass from <c>velocity.slang</c>, compiled.</summary>
    public MotionVelocity(ShaderProgram velocity) =>
        (_vertexSpv, _fragmentSpv, _bindings) = (velocity.Vertex, velocity.Fragment, velocity.LayoutOf(0));

    /// <summary>The layout of the set the pass reads the scene's depth through, made on first use.</summary>
    public IDescriptorSetLayout Layout(GraphicsDevice device) => _layout ??= device.CreateDescriptorSetLayout(_bindings);

    /// <summary>
    /// Clears <paramref name="target"/> and draws each of <paramref name="moving"/> into it, through
    /// <paramref name="viewProjection"/> now and <paramref name="lastViewProjection"/> the frame before,
    /// both taking a place against <paramref name="origin"/>, which each entity's places now and the
    /// frame before are taken against.
    /// </summary>
    public void Draw(RenderContext renderContext, GraphicsDevice device, RenderTarget target, IDescriptorSet depth, GpuMeshes meshes,
        MovingDraws moving, Matrix4x4 viewProjection, Matrix4x4 lastViewProjection, RenderOrigin origin)
    {
        _vertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        // The position alone is read, at the start of the mesh's vertex, whose stride is its
        // ModelVertex's or its positions' for a mesh of positions alone, three floats or packed.
        IPipeline Made(uint stride, VertexFormat format = VertexFormat.Float3) => device.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            target.RenderPass, _vertex, _fragment,
            Cull: CullMode.None,
            VertexBindings:
            [
                new VertexInputBindingDesc(0, stride),
                new VertexInputBindingDesc(1, (uint)Marshal.SizeOf<MovingInstance>(), PerInstance: true),
            ],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, format, 0),
                .. Enumerable.Range(0, 6).Select(row => new VertexInputAttributeDesc((uint)(1 + row), 1, VertexFormat.Float4, (uint)(row * 16))),
            ],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex, 0, (uint)Marshal.SizeOf<Push>())],
            DescriptorSetLayouts: [Layout(device)]));
        _pipeline ??= Made((uint)Marshal.SizeOf<ModelVertex>());

        // Each mesh's entities, gathered by mesh already, written as one run of instances.
        var buffer = Instances(device, moving.Count);
        var written = MemoryMarshal.Cast<byte, MovingInstance>(device.Map(buffer));
        var runs = new List<(int Mesh, int First, int Count)>();
        var next = 0;
        foreach (var (mesh, lists) in moving.ByMesh())
        {
            var first = next;
            // A packed mesh's rows now and the frame before scaled by the same quantum, the mesh's
            // as its vertices hold them this frame, so a re-packed mesh shows no motion it did not
            // make, and each taken against the origin, as it is written into the mapped buffer,
            // which is never read back.
            var quantum = meshes.Get(mesh) is { Quantum: not 1 and var packed } ? packed : 1;
            foreach (var list in lists)
                foreach (var instance in list)
                {
                    var placed = instance.Against(origin);
                    written[next++] = quantum == 1 ? placed : placed.Folded(quantum);
                }
            runs.Add((mesh, first, next - first));
        }
        device.Unmap(buffer);

        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, ClearColor.Black with { A = 0 }));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        var push = new Push { ViewProjection = viewProjection, LastViewProjection = lastViewProjection };
        IPipeline? bound = null;
        foreach (var (id, first, count) in runs)
        {
            if (meshes.Get(id) is not { } mesh) continue;
            var pipeline = mesh.Positions is { } format
                ? _positionsPipelines.TryGetValue(format, out var made) ? made : _positionsPipelines[format] = Made(PositionPacking.Stride(format), format)
                : _pipeline;
            if (!ReferenceEquals(pipeline, bound))
            {
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, depth);
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
                bound = pipeline;
            }
            pass.SetVertexBuffer(0, [mesh.Vertices, buffer], [0, 0]);
            if (mesh.Counted is { } counted)
                pass.DrawCounted(counted.Count, counted.Capacity, (uint)count, (uint)first, counted.First, counted.CountOffset);
            else
            {
                pass.SetIndexBuffer(mesh.Indices, 0, mesh.IndexType);
                pass.DrawIndexed(mesh.IndexCount, (uint)count, firstInstance: (uint)first);
            }
        }
    }

    // The next frame's instance buffer, grown to hold count. The one it replaces was last written
    // as many frames ago as frames are kept in flight, so it is no longer read.
    private IBuffer Instances(GraphicsDevice device, int count)
    {
        _slot = (_slot + 1) % _instances.Length;
        if (_instances[_slot] is { } kept && _capacity[_slot] >= count) return kept;
        _instances[_slot]?.Dispose();
        _capacity[_slot] = Math.Max(256, count + count / 2);
        return _instances[_slot] = device.CreateBuffer(new BufferDesc((ulong)(_capacity[_slot] * Marshal.SizeOf<MovingInstance>()), BufferUsage.Vertex, CpuAccessMode.Write));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var buffer in _instances) buffer?.Dispose();
        _pipeline?.Dispose();
        foreach (var made in _positionsPipelines.Values) made.Dispose();
        _layout?.Dispose();
        _vertex?.Dispose();
        _fragment?.Dispose();
    }
}
