using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Per-frame upload of <see cref="RenderLights"/> into a transient uniform buffer
/// allocated through <see cref="DynamicBufferAllocator"/>. Stores the resulting
/// <see cref="UniformBufferBinding"/> as <see cref="FrameLightingBinding"/> on the
/// render world so downstream nodes can write it into the lighting descriptor set
/// of the mesh pipeline.
/// </summary>
public sealed class LightingUboPrepare : IPrepareSystem
{
    private static readonly ILogger Logger = Log.Category("Engine.Lighting");

    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        var lights = renderWorld.TryGet<RenderLights>();
        var allocator = renderContext.DynamicAllocator;
        if (allocator is null)
            return;

        // Always upload (even with zero lights) so the shader can rely on the binding
        // existing - the count is what the shader iterates against.
        var ubo = LightingUboPacker.Pack(lights?.All ?? (IReadOnlyList<RenderLight>)System.Array.Empty<RenderLight>());
        var sizeBytes = (ulong)LightingUboPacker.SizeBytes;

        var alloc = allocator.Allocate(sizeBytes, BufferUsage.Uniform);
        var span = allocator.Map(alloc);
        MemoryMarshal.Write(span, in ubo);
        allocator.Unmap(alloc);

        renderWorld.Set(new FrameLightingBinding(
            new UniformBufferBinding(alloc.Buffer, Binding: 0, alloc.Offset, sizeBytes),
            ubo.LightCount));

        Logger.FrameTrace($"LightingUboPrepare: uploaded {ubo.LightCount} light(s) into a {sizeBytes}-byte UBO.");
    }
}

/// <summary>
/// Render-world resource published by <see cref="LightingUboPrepare"/>: the
/// <see cref="UniformBufferBinding"/> for this frame's lighting UBO and the count
/// of valid entries inside it. Renamed each frame; consumers should read it
/// transiently rather than caching.
/// </summary>
/// <param name="Binding">Buffer binding suitable for <see cref="IGraphicsDevice.UpdateDescriptorSet"/>.</param>
/// <param name="LightCount">Number of valid <see cref="LightUboEntry"/> entries in the buffer.</param>
public sealed record FrameLightingBinding(UniformBufferBinding Binding, int LightCount);

