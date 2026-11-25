using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

internal sealed unsafe partial class GlobalIlluminationStages
{
    /// <summary>
    /// Records the window's surfaces lit by the module's gather (<c>gi_surface.slang</c>) after the
    /// world's probes are merged and their faces and reach written, a thread a pixel of
    /// <paramref name="surface"/>, each pixel's point read from <paramref name="depth"/> through
    /// <paramref name="view"/>, left ready for the model pass to read.
    /// </summary>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordSurfaceGather(ICommandBuffer commandBuffer, GpuIllumination gi, GpuSurfaceGather surface, GpuSceneField field,
        IImageView depth, ISampler depthSampler, ReadOnlySpan<byte> view)
    {
        var run = new ProbeRun(this, device, commandBuffer, [(SurfaceStage, 1)]);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        device.MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = view[..(Math.Min(view.Length, GpuScreenProbes.ViewBytes) & ~3)];
        fixed (byte* data = bytes)
            device.Api.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)surface.View).Buffer, 0, (ulong)bytes.Length, data);
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead);
        var light = ((VulkanImage)surface.Light).Image;
        device.PipelineBarrier(cmd, [ImageBarrier(light, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead)]);

        var set = run.Set(SurfaceStage);
        run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 1, VkDescriptorType.UniformBuffer, field.Info);
        run.Buffer(set, 2, VkDescriptorType.UniformBuffer, surface.View);
        run.Image(set, 3, VkDescriptorType.SampledImage, ((VulkanImageView)gi.CubesView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 4, VkDescriptorType.SampledImage, ((VulkanImageView)gi.ReachView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        // Each of five cascades' merged light, its rays' distances and its share of the sky, the
        // last the probes have again where they have fewer.
        for (int c = 0; c < 5; c++)
        {
            var at = Math.Min(c, gi.Cascades - 1);
            run.Image(set, (uint)(5 + c), VkDescriptorType.SampledImage, gi.Merged[at].View, null, VkImageLayout.General);
            run.Image(set, (uint)(10 + c), VkDescriptorType.SampledImage, gi.Distances[at].View, null, VkImageLayout.General);
            run.Image(set, (uint)(15 + c), VkDescriptorType.SampledImage, gi.MergedSky[at].View, null, VkImageLayout.General);
        }
        run.Image(set, 20, VkDescriptorType.StorageImage, ((VulkanImageView)surface.LightView).View, null, VkImageLayout.General);
        uint TexelsOf(int c) => c < gi.Cascades ? (uint)gi.Texels[c] : 0;
        ReadOnlySpan<uint> push = [TexelsOf(0), TexelsOf(1), TexelsOf(2), TexelsOf(3), TexelsOf(4), 0, 0, 0];
        var (pipeline, layout, _) = GiStages[SurfaceStage];
        device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
        device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
        fixed (uint* p = push)
            device.Api.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, 32, p);
        device.Api.vkCmdDispatch(cmd, (surface.Width + 7) / 8, (surface.Height + 7) / 8, 1);
        MarkGlobalIllumination(commandBuffer, "gi_surface");

        device.PipelineBarrier(cmd, [ImageBarrier(light, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, readers, VkAccessFlags2.ShaderRead)]);
        return run;
    }

    /// <summary>Whether the gather's kernel was given and its pipeline made.</summary>
    internal bool GathersSurfaces => GiStages[SurfaceStage].Pipeline.Handle != 0;
}
