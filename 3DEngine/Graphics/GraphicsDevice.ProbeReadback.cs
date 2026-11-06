using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

// The world probes' images read back whole, for the tests and the console, with the frames in
// flight finished.
internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>The faces of the light each probe receives, four floats a texel, read back for a test with the frames in flight finished.</summary>
    internal float[] ReadIlluminationCubes(GpuIllumination gi)
    {
        var (p, c) = ((uint)gi.Probes, (uint)gi.Cascades);
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc(6 * p * p * p * c * 8, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = BeginSingleTimeCommands();
            var whole = ColorLevels(0, 1);
            PipelineBarrier(cmd, ImageBarrier(gi.Cubes, whole, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));
            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(6 * p, p, p * c),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, gi.Cubes, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            PipelineBarrier(cmd, ImageBarrier(gi.Cubes, whole, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead));
            EndSingleTimeCommands(cmd);
            return Halves(buffer);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Each probe's own light of the last two frames, its share and its place, four texels a probe
    /// along x and a cascade's probes a run of layers, four floats a texel, read back with the
    /// frames in flight finished. A place's w is 2 for a probe whose rays the frame carried past its
    /// interval and moved within their texels (gi_trace.slang).
    /// </summary>
    internal float[] ReadIlluminationHeld(GpuIllumination gi)
    {
        var (p, c) = ((uint)gi.Probes, (uint)gi.Cascades);
        return ReadGeneralImage(gi.HeldImage, new VkExtent3D(4 * p, p, p * c), 4);
    }

    /// <summary>
    /// How many probes of each cascade the frame carried past their interval with their rays moving
    /// within their texels, read from their places' w.
    /// </summary>
    internal int[] ReadCarriedMoving(GpuIllumination gi)
    {
        var (held, p) = (ReadIlluminationHeld(gi), gi.Probes);
        var moving = new int[gi.Cascades];
        for (int c = 0; c < gi.Cascades; c++)
            for (int z = 0; z < p; z++)
                for (int y = 0; y < p; y++)
                    for (int x = 0; x < p; x++)
                        if (held[((((c * p + z) * p + y) * 4 * p) + 3 * p + x) * 4 + 3] > 1.5f) moving[c]++;
        return moving;
    }

    /// <summary>Whether the frame carried one probe past its interval with its rays moving, read from its place's w.</summary>
    internal bool ReadCarriedMoving(GpuIllumination gi, int cascade, int x, int y, int z)
    {
        var p = gi.Probes;
        return ReadIlluminationHeld(gi)[((((cascade * p + z) * p + y) * 4 * p) + 3 * p + x) * 4 + 3] > 1.5f;
    }

    /// <summary>
    /// A cascade's rays' light or its merge, four floats a texel, its probes' octahedrons side by side
    /// and a layer of probes a slice, read back with the frames in flight finished.
    /// </summary>
    internal float[] ReadProbeVolume(GpuIllumination gi, int cascade, bool merged) =>
        ReadProbeTexels(gi, cascade, (merged ? gi.Merged : gi.Radiance)[cascade].Image, 4);

    /// <summary>
    /// How far each of a cascade's rays went, two floats a texel laid out as
    /// <see cref="ReadProbeVolume"/> lays its light, the distance, below zero where the rays met a
    /// face from behind, and the share kept before the interval, read back with the frames in flight
    /// finished.
    /// </summary>
    internal float[] ReadProbeDistances(GpuIllumination gi, int cascade) => ReadProbeTexels(gi, cascade, gi.Distances[cascade].Image, 2);

    // A cascade's volume of half floats, so many a texel, read back.
    private float[] ReadProbeTexels(GpuIllumination gi, int cascade, VkImage image, int channels)
    {
        var side = (uint)(gi.Probes * gi.Texels[cascade]);
        return ReadGeneralImage(image, new VkExtent3D(side, side, (uint)gi.Probes), channels);
    }

    // An image of half floats, so many a texel, read back from the general layout the probes'
    // images stay in, which a copy reads.
    private float[] ReadGeneralImage(VkImage image, VkExtent3D extent, int channels)
    {
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc(extent.width * extent.height * extent.depth * (uint)channels * 2, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = BeginSingleTimeCommands();
            MemoryBarrier(cmd, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead);
            var region = new VkBufferImageCopy { imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1), imageExtent = extent };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, image, VkImageLayout.General, buffer.Buffer, 1, &region);
            EndSingleTimeCommands(cmd);
            return Halves(buffer);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    // A buffer of half floats read as floats.
    private float[] Halves(VulkanBuffer buffer)
    {
        var halves = MemoryMarshal.Cast<byte, Half>(Map(buffer));
        var values = new float[halves.Length];
        for (int i = 0; i < halves.Length; i++) values[i] = (float)halves[i];
        return values;
    }
}
