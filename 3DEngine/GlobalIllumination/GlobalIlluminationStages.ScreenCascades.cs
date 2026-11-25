using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// What the screen's cascades are traced with where <c>gi.screen_cascades</c> asks, the levels past
/// the first, the scene the device's rays meet and the light from all around a ray of the last
/// level takes where it meets nothing.
/// </summary>
internal readonly record struct ScreenCascadeTrace(GpuScreenCascades Levels, GpuRayScene Rays, IImageView Environment, ISampler EnvironmentSampler);

internal sealed unsafe partial class GlobalIlluminationStages
{
    /// <summary>Whether the screen's cascades' build with the device's rays was made.</summary>
    internal bool TracesScreenCascades => GiStages[CascadeStage].Pipeline.Handle != 0;

    /// <summary>
    /// Records the screen's cascades, each level a dispatch of <c>gi_screen_cascades.slang</c>, the
    /// last first, each reading the one after it, the first writing the light arriving at the
    /// screen's probes' surfaces in place of the screen pass's rays.
    /// </summary>
    private void RecordScreenCascades(ProbeRun run, GpuIllumination gi, GpuScreenProbes screen, GpuSceneField field, IImageView depth, ISampler depthSampler,
        ScreenCascadeTrace trace)
    {
        var cmd = run.Commands;
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        var set = run.Set(CascadeStage);
        device.WriteRayScene(set, RayScreenFirst, trace.Rays);
        run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 1, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 2, VkDescriptorType.UniformBuffer, field.Info);
        run.Image(set, 3, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 4, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.GlowView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 5, VkDescriptorType.UniformBuffer, gi.Lights);
        run.Image(set, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.CubesView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 7, VkDescriptorType.CombinedImageSampler, gi.CubesAtView, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 8, VkDescriptorType.StorageImage, ((VulkanImageView)screen.IrradianceView).View, null, VkImageLayout.General);
        run.Image(set, 9, VkDescriptorType.StorageImage, ((VulkanImageView)trace.Levels.LightView).View, null, VkImageLayout.General);
        run.Buffer(set, 10, VkDescriptorType.UniformBuffer, screen.View);
        run.Image(set, 11, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)trace.Environment).View, trace.EnvironmentSampler,
            VkImageLayout.ShaderReadOnlyOptimal);
        var (pipeline, layout, _) = GiStages[CascadeStage];
        device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
        device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
        var (width, height, tile) = (trace.Levels.Width, trace.Levels.Height, trace.Levels.Tile);
        for (int level = GpuScreenCascades.Levels - 1; level >= 0; level--)
        {
            // The first level a thread a probe, the others a thread a way of each probe.
            var side = (uint)(tile << level);
            var texels = level == 0 ? 1u : (uint)(GpuScreenCascades.FirstTexels << level);
            var (across, down) = ((width + side - 1) / side * texels, (height + side - 1) / side * texels);
            ReadOnlySpan<uint> push = [(uint)level, GpuScreenCascades.Levels, GpuScreenCascades.FirstTexels, 0];
            fixed (uint* p = push)
                device.Api.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, 16, p);
            device.Api.vkCmdDispatch(cmd, (across + 7) / 8, (down + 7) / 8, 1);
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader,
                VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        }
    }
}
