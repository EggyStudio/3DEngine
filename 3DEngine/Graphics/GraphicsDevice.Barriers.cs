using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    // Barriers are synchronization2's, each naming the stages and accesses on both of its sides in
    // one structure, where the first form split stages from accesses across the call and the struct.

    /// <summary>One image's change of layout over <paramref name="range"/>, waiting on the stages and accesses before it for those after.</summary>
    private static VkImageMemoryBarrier2 ImageBarrier(VkImage image, VkImageSubresourceRange range, VkImageLayout from, VkImageLayout to,
        VkPipelineStageFlags2 srcStage, VkAccessFlags2 srcAccess, VkPipelineStageFlags2 dstStage, VkAccessFlags2 dstAccess) => new()
    {
        srcStageMask = srcStage,
        srcAccessMask = srcAccess,
        dstStageMask = dstStage,
        dstAccessMask = dstAccess,
        oldLayout = from,
        newLayout = to,
        srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
        dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
        image = image,
        subresourceRange = range,
    };

    /// <summary>The color levels of an image, from <paramref name="firstLevel"/>, of its one layer.</summary>
    private static VkImageSubresourceRange ColorLevels(uint firstLevel, uint levelCount) =>
        new(VkImageAspectFlags.Color, firstLevel, levelCount, 0, 1);

    /// <summary>Records image barriers, and a memory barrier beside them when there is one.</summary>
    private void PipelineBarrier(VkCommandBuffer cmd, ReadOnlySpan<VkImageMemoryBarrier2> images, VkMemoryBarrier2? memory = null)
    {
        var global = memory.GetValueOrDefault();
        fixed (VkImageMemoryBarrier2* barriers = images)
        {
            var dependency = new VkDependencyInfo
            {
                memoryBarrierCount = memory is null ? 0u : 1u,
                pMemoryBarriers = memory is null ? null : &global,
                imageMemoryBarrierCount = (uint)images.Length,
                pImageMemoryBarriers = barriers,
            };
            _deviceApi.vkCmdPipelineBarrier2(cmd, &dependency);
        }
    }

    /// <summary>Records one image barrier.</summary>
    private void PipelineBarrier(VkCommandBuffer cmd, VkImageMemoryBarrier2 image) => PipelineBarrier(cmd, [image]);

    /// <summary>Records a memory barrier alone, which covers every buffer and image.</summary>
    private void MemoryBarrier(VkCommandBuffer cmd, VkPipelineStageFlags2 srcStage, VkAccessFlags2 srcAccess,
        VkPipelineStageFlags2 dstStage, VkAccessFlags2 dstAccess) =>
        PipelineBarrier(cmd, [], new VkMemoryBarrier2
        {
            srcStageMask = srcStage,
            srcAccessMask = srcAccess,
            dstStageMask = dstStage,
            dstAccessMask = dstAccess,
        });
}
