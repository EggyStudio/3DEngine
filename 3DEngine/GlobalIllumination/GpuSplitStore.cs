using System.Numerics;
using RadianceCascades;
using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// The RadianceCascades module's split cascades' store on the device, its uints and its light laid
/// out by <see cref="Shape"/>, empty when made and kept from frame to frame, which
/// <c>gi_split.slang</c> keeps the window's probes in where <c>gi.layout split</c> asks.
/// </summary>
internal sealed class GpuSplitStore : IDisposable
{
    private GpuSplitStore(SplitStoreShape shape, IBuffer uints, IBuffer light) => (Shape, Uints, Light) = (shape, uints, light);

    /// <summary>
    /// The store's shape, which its passes are pushed and its buffers are laid out by.
    /// </summary>
    public SplitStoreShape Shape { get; }

    /// <summary>
    /// The store's uints, its counts, table, pools, free lists and the rays' sums.
    /// </summary>
    public IBuffer Uints { get; }

    /// <summary>
    /// The store's light, its probes' own and that merged from above, a float4 a way.
    /// </summary>
    public IBuffer Light { get; }

    /// <summary>
    /// The span along each axis over which every probe's key repeats, 512 of the coarsest spacing
    /// the store holds, since a key keeps the nine low bits of its cell along each axis, and a
    /// whole count of 512 of every finer spacing, so places moved by it keep every probe's key and
    /// light.
    /// </summary>
    public static double Repeat(SplitStoreShape shape) => 512.0 * shape.Spacing * Math.Pow(2, shape.Levels + shape.Cascades - 2);

    /// <summary>
    /// The frame's origin against the store's, which stands at the frame's origin rounded to the
    /// nearest whole count of <see cref="Repeat"/> along each axis, so the store's places stay
    /// within half of it of the eye and the frame's origin moving with the eye moves no probe.
    /// </summary>
    public static Vector3 OriginAgainst(SplitStoreShape shape, RenderOrigin origin)
    {
        var repeat = Repeat(shape);
        float Less(double at) => (float)(at - Math.Floor(at / repeat + 0.5) * repeat);
        return new Vector3(Less(origin.X), Less(origin.Y), Less(origin.Z));
    }

    /// <summary>
    /// Makes the store of <paramref name="shape"/>, every uint and every light 0.
    /// </summary>
    public static GpuSplitStore Create(GraphicsDevice device, SplitStoreShape shape)
    {
        var uints = device.CreateBuffer(new BufferDesc(4UL * (ulong)shape.Uints, BufferUsage.Storage | BufferUsage.TransferDst));
        device.Name(uints, "Split cascades' uints");
        var light = device.CreateBuffer(new BufferDesc(16UL * (ulong)shape.Lights, BufferUsage.Storage | BufferUsage.TransferDst));
        device.Name(light, "Split cascades' light");
        var cmd = device.BeginSingleTimeCommands();
        device.Api.vkCmdFillBuffer(cmd, ((VulkanBuffer)uints).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        device.Api.vkCmdFillBuffer(cmd, ((VulkanBuffer)light).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        device.EndSingleTimeCommands(cmd);
        return new GpuSplitStore(shape, uints, light);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Light.Dispose();
        Uints.Dispose();
    }
}
