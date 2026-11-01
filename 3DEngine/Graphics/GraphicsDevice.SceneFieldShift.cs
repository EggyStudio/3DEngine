using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    // The buffer a cascade's cells pass through as a shift moves them, made the first time and kept,
    // as large as a cascade of the widest texel, eight bytes.
    private IBuffer? _fieldShift;

    /// <summary>
    /// Records, between <see cref="RecordSceneFieldOpen"/> and <see cref="RecordSceneFieldClose"/>,
    /// cascade <paramref name="cascade"/> moved <paramref name="cells"/> of its cells along
    /// <paramref name="axis"/>, 0 for x, 1 for y and 2 for z, as a cascade whose corner went that
    /// far keeps the cells it held. Its still distances, colors, faces and emission, its second
    /// planes and its kept words move, and the field the passes read is made the still field again,
    /// the shapes stamped in it gone until the frame stamps them again.
    /// </summary>
    /// <remarks>
    /// The cells the shift brings in hold what those places held before it, which the build that
    /// follows turns into nothing near as it starts, or into the meshes there as it ends
    /// (<see cref="RecordSceneFieldBuild"/>'s <c>empty</c>). Each image goes through a buffer, since a
    /// copy within an image may not overlap itself, its cells laid out as the copy packs them.
    /// </remarks>
    public void RecordSceneFieldShift(ICommandBuffer commandBuffer, GpuSceneField field, int cascade, int axis, int cells)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd || cells == 0) return;
        var cmd = vkCmd.Handle;
        var size = (uint)field.Resolution;
        var moved = (uint)Math.Abs(cells);
        if (moved >= size) return;
        _fieldShift ??= CreateBuffer(new BufferDesc((ulong)size * size * size * 8, BufferUsage.TransferSrc | BufferUsage.TransferDst));
        var staging = ((VulkanBuffer)_fieldShift).Buffer;
        var slice = (int)size * cascade;

        // The cells kept, where they lie before the move and after it, a cell of the old place at
        // `cells` past the new one's.
        var extent = new VkExtent3D(axis == 0 ? size - moved : size, axis == 1 ? size - moved : size, axis == 2 ? size - moved : size);
        VkOffset3D At(int along) => new(axis == 0 ? along : 0, axis == 1 ? along : 0, (axis == 2 ? along : 0) + slice);
        var (from, to) = (At(Math.Max(cells, 0)), At(Math.Max(-cells, 0)));
        var layers = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1);

        // What the frame's work wrote before is seen by the copies, and each copy through the
        // buffer is seen by the next before it fills the buffer again.
        void Settle() => MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer,
            VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferWrite | VkAccessFlags2.TransferRead,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite);
        Settle();
        foreach (var image in new[] { field.Still, field.StillPaint, field.StillFaces, field.StillGlow, field.SecondImage })
        {
            var outward = new VkBufferImageCopy { bufferOffset = 0, imageSubresource = layers, imageOffset = from, imageExtent = extent };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, image, VkImageLayout.General, staging, 1, &outward);
            Settle();
            var back = outward with { imageOffset = to };
            _deviceApi.vkCmdCopyBufferToImage(cmd, staging, image, VkImageLayout.General, 1, &back);
            Settle();
        }

        // The kept words, a uint a cell, the cascade's whole run into the buffer, then each run of
        // cells along x kept back to its new place.
        if (field.KeptCells is { } keptCells)
        {
            var kept = ((VulkanBuffer)keptCells).Buffer;
            var run = (ulong)size * size * size * 4;
            var whole = new VkBufferCopy { srcOffset = run * (ulong)cascade, dstOffset = 0, size = run };
            _deviceApi.vkCmdCopyBuffer(cmd, kept, staging, 1, &whole);
            Settle();
            var rows = new List<VkBufferCopy>();
            for (uint z = 0; z < extent.depth; z++)
                for (uint y = 0; y < extent.height; y++)
                {
                    ulong Word(VkOffset3D at) => (((ulong)(z + (uint)(at.z - slice)) * size + y + (uint)at.y) * size + (uint)at.x) * 4;
                    rows.Add(new VkBufferCopy { srcOffset = Word(from), dstOffset = run * (ulong)cascade + Word(to), size = (ulong)extent.width * 4 });
                }
            var regions = rows.ToArray();
            fixed (VkBufferCopy* first = regions)
                _deviceApi.vkCmdCopyBuffer(cmd, staging, kept, (uint)regions.Length, first);
            Settle();
        }

        // The field the passes read, its colors, faces and emission, made the still ones again over the cascade.
        var cascadeBox = new VkImageCopy
        {
            srcSubresource = layers,
            srcOffset = new VkOffset3D(0, 0, slice),
            dstSubresource = layers,
            dstOffset = new VkOffset3D(0, 0, slice),
            extent = new VkExtent3D(size, size, size),
        };
        _deviceApi.vkCmdCopyImage(cmd, field.Still, VkImageLayout.General, field.Field, VkImageLayout.General, 1, &cascadeBox);
        _deviceApi.vkCmdCopyImage(cmd, field.StillPaint, VkImageLayout.General, field.AlbedoImage, VkImageLayout.General, 1, &cascadeBox);
        _deviceApi.vkCmdCopyImage(cmd, field.StillFaces, VkImageLayout.General, field.FacesImage, VkImageLayout.General, 1, &cascadeBox);
        _deviceApi.vkCmdCopyImage(cmd, field.StillGlow, VkImageLayout.General, field.GlowImage, VkImageLayout.General, 1, &cascadeBox);
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite | VkAccessFlags2.TransferRead,
            VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferWrite);
    }

    // The buffer the shifts pass cells through, let go with the device.
    private void DisposeFieldShift() => _fieldShift?.Dispose();
}
