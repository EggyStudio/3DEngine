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
    private GpuSplitStore(SplitStoreShape shape, IBuffer uints, IBuffer light, IBuffer merged) =>
        (Shape, Uints, Light, Merged) = (shape, uints, light, merged);

    /// <summary>
    /// The least range of a storage buffer's binding Vulkan lets a device hold, which lavapipe
    /// holds, so every buffer of the store binds whole within it on any device.
    /// </summary>
    internal const ulong LeastStorageRange = 1UL << 27;

    /// <summary>
    /// The store's shape, which its passes are pushed and its buffers are laid out by.
    /// </summary>
    public SplitStoreShape Shape { get; }

    /// <summary>
    /// The store's uints, its counts, table, pools, free lists and the rays' sums.
    /// </summary>
    public IBuffer Uints { get; }

    /// <summary>
    /// The store's light its probes keep along their ways, a float4 a way, the first half of the
    /// light the module lays out.
    /// </summary>
    public IBuffer Light { get; }

    /// <summary>
    /// The store's light merged from the cascades above, laid out as <see cref="Light"/> is, the
    /// second half of the light the module lays out, kept apart so each half binds within
    /// <see cref="LeastStorageRange"/> where the whole light need not.
    /// </summary>
    public IBuffer Merged { get; }

    /// <summary>
    /// The bytes of the store's buffers of <paramref name="shape"/>, its uints, its own light and
    /// its merged light, as <see cref="Create"/> makes them.
    /// </summary>
    public static ulong[] Bytes(SplitStoreShape shape) => [4UL * (ulong)shape.Uints, 16UL * (ulong)shape.OwnLights, 16UL * (ulong)shape.OwnLights];

    /// <summary>
    /// The span along each axis over which every probe's key repeats, 512 of the coarsest
    /// cascade's spacing, since a key keeps the nine low bits of its cell along each axis, and a
    /// whole count of 512 of every finer spacing, so places moved by it keep every probe's key.
    /// </summary>
    public static double Repeat(SplitStoreShape shape) => 512.0 * shape.Spacing * Math.Pow(2, shape.Cascades - 1);

    /// <summary>
    /// The frame's origin against the store's, which stands at the frame's origin rounded to the
    /// nearest whole count of <see cref="Repeat"/> along each axis, so the store's places stay
    /// within half of it of the eye and the frame's origin moving with the eye moves no probe.
    /// </summary>
    /// <remarks>
    /// An entry keeps its probe's whole cell beside its key, so where the store's origin steps by
    /// a repeat no probe reads back as its cell, and the store fills again from the points seen.
    /// </remarks>
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
        var bytes = Bytes(shape);
        var buffers = new IBuffer[bytes.Length];
        string[] names = ["Split cascades' uints", "Split cascades' own light", "Split cascades' merged light"];
        var cmd = device.BeginSingleTimeCommands();
        for (int i = 0; i < buffers.Length; i++)
        {
            buffers[i] = device.CreateBuffer(new BufferDesc(bytes[i], BufferUsage.Storage | BufferUsage.TransferDst));
            device.Name(buffers[i], names[i]);
            device.Api.vkCmdFillBuffer(cmd, ((VulkanBuffer)buffers[i]).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        }
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        device.EndSingleTimeCommands(cmd);
        return new GpuSplitStore(shape, buffers[0], buffers[1], buffers[2]);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Merged.Dispose();
        Light.Dispose();
        Uints.Dispose();
    }
}
