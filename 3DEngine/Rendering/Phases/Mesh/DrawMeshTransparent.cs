using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Draws a <see cref="TransparentPhaseItem"/> using the <see cref="MeshPipeline"/>.
/// Binds vertex buffers, pushes per-object constants, and issues a draw call.
/// </summary>
/// <seealso cref="TransparentPhaseItem"/>
/// <seealso cref="MeshPipeline"/>
public sealed class DrawMeshTransparent : IDrawFunction<TransparentPhaseItem>
{
    private readonly IPipeline _pipeline;

    /// <summary>Creates a draw function bound to the given mesh pipeline.</summary>
    /// <param name="pipeline">The compiled mesh pipeline.</param>
    public DrawMeshTransparent(IPipeline pipeline) => _pipeline = pipeline;

    /// <inheritdoc />
    public void Draw(ref TransparentPhaseItem item, TrackedRenderPass pass, RenderWorld renderWorld)
    {
        var registry = renderWorld.TryGet<MeshGpuRegistry>();
        if (registry is null || !registry.TryGet(item.EntityId, out var vertexBuffer))
            return;

        var pc = new MeshPushConstants
        {
            Model = item.ModelMatrix,
            Albedo = item.Albedo
        };

        pass.PushConstants(_pipeline, ShaderStageFlags.All,
            0, MemoryMarshal.AsBytes(new ReadOnlySpan<MeshPushConstants>(in pc)));

        pass.SetVertexBuffer(0, new[] { vertexBuffer }, new ulong[] { 0 });
        pass.Draw((uint)item.VertexCount);
    }
}
