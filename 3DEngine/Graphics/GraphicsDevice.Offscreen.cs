using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// Single-use command buffers, and the image layout transitions the render graph records between
/// nodes.
/// </summary>
public sealed unsafe partial class GraphicsDevice
{
    // -- Single-use command buffer (public wrappers)

    /// <inheritdoc />
    public ICommandBuffer BeginCommands()
    {
        var cmd = BeginSingleTimeCommands();
        return new VulkanCommandBuffer(cmd);
    }

    /// <inheritdoc />
    public void SubmitAndWait(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        EndSingleTimeCommands(vkCmd.Handle);
    }

    // -- Pipeline barrier (image layout transition)

    /// <inheritdoc />
    public void CmdPipelineBarrier(ICommandBuffer commandBuffer, IImage image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (image is not VulkanImage vkImage)
            throw new ArgumentException("Image was not created by this device.", nameof(image));

        var vkOld = ToVkImageLayout(oldLayout);
        var vkNew = ToVkImageLayout(newLayout);
        var (srcStage, srcAccess) = UseOf(oldLayout, before: true);
        var (dstStage, dstAccess) = UseOf(newLayout, before: false);
        PipelineBarrier(vkCmd.Handle, ImageBarrier(vkImage.Image, ColorLevels(0, 1), vkOld, vkNew, srcStage, srcAccess, dstStage, dstAccess));
        vkImage.Layout = vkNew;
    }

    // The stage and access an image in a layout is used by, the writes before a change of layout
    // and the reads and writes after it.
    private static (VkPipelineStageFlags2 Stage, VkAccessFlags2 Access) UseOf(ImageLayout layout, bool before) => layout switch
    {
        ImageLayout.ColorAttachmentOptimal => (VkPipelineStageFlags2.ColorAttachmentOutput,
            before ? VkAccessFlags2.ColorAttachmentWrite : VkAccessFlags2.ColorAttachmentRead | VkAccessFlags2.ColorAttachmentWrite),
        ImageLayout.ShaderReadOnlyOptimal => (VkPipelineStageFlags2.FragmentShader, before ? VkAccessFlags2.None : VkAccessFlags2.ShaderRead),
        ImageLayout.TransferDstOptimal => (VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        _ => (VkPipelineStageFlags2.None, VkAccessFlags2.None),
    };

    /// <summary>Maps an engine <see cref="ImageLayout"/> to the Vulkan <c>VkImageLayout</c> equivalent.</summary>
    private static VkImageLayout ToVkImageLayout(ImageLayout layout) => layout switch
    {
        ImageLayout.Undefined => VkImageLayout.Undefined,
        ImageLayout.ColorAttachmentOptimal => VkImageLayout.ColorAttachmentOptimal,
        ImageLayout.ShaderReadOnlyOptimal => VkImageLayout.ShaderReadOnlyOptimal,
        ImageLayout.TransferDstOptimal => VkImageLayout.TransferDstOptimal,
        _ => VkImageLayout.Undefined
    };
}
