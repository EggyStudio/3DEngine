using Vortice.Vulkan;
using static Engine.GraphicsDevice;
using static Engine.Illumination;

namespace Engine;

internal sealed unsafe partial class GlobalIlluminationStages
{
    /// <summary>
    /// Records the screen probes' work after the world's probes are merged, the view written, each
    /// probe placed on the window's depth and its rays traced and summed, left ready for the model
    /// pass to sample.
    /// </summary>
    /// <remarks>
    /// With <paramref name="glowOnly"/> set the probes stand for the glow lights alone, as where
    /// they are put in at <see cref="GlobalIllumination.Low"/>, each placed and
    /// marching to the lights with no ray traced, filtered or held, so the glow lights' light on the
    /// depth's texels is made from them as it is from the whole probes. Where the screen gathers by
    /// pixel (<see cref="GpuScreenProbes.Gathered"/>) the probes stand for the glow lights alone as
    /// well, and each texel of the gather's grid sends one ray, blended with the texels around it
    /// within the frame and keeping nothing of the frame before. With <paramref name="cascades"/>
    /// given the probes stand for the glow lights alone as well, and the screen's cascades traced
    /// by the device's rays give the light at their surfaces, blended with the probes around
    /// within the frame and keeping nothing of the frame before.
    /// </remarks>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordScreenProbes(ICommandBuffer commandBuffer, GpuIllumination gi, GpuScreenProbes screen, GpuSceneField field,
        IImageView depth, ISampler depthSampler, ReadOnlySpan<byte> view, bool glowLights, int loggedProbe = -1, bool glowOnly = false,
        bool loggedNotANumber = false, bool lightThrough = false, GpuRayScene? rays = null, ScreenCascadeTrace? cascades = null)
    {
        var pixels = glowOnly ? null : screen.Gathered;
        var traced = !glowOnly && pixels is null && TracesScreenCascades ? cascades : null;
        var probesGlowOnly = glowOnly || pixels is not null || traced is not null;
        var inFrame = pixels is not null || traced is not null;
        // The gather by pixel's rays meet the meshes through the device's rays where a ray scene is
        // given and the screen pass's build for them was made.
        var rayed = pixels is not null && rays is not null && GiStages[RayScreenStage].Pipeline.Handle != 0;
        var run = new ProbeRun(this, device, commandBuffer, [(ScreenStage, pixels is null || rayed ? 1 : 2), (RayScreenStage, rayed ? 1 : 0),
            (FilterStage, (probesGlowOnly ? 0 : 2) + (inFrame ? 1 : 0)), (GlowStage, glowLights ? 1 : 0), (CascadeStage, traced is null ? 0 : 1)]);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        device.MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = view[..(Math.Min(view.Length, GpuScreenProbes.ViewBytes) & ~3)];
        fixed (byte* data = bytes)
            device.Api.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)screen.View).Buffer, 0, (ulong)bytes.Length, data);
        // The gather's view the probes' with the grid's texel, across and down in place of the
        // tile's (GlobalIlluminationRenderer.ViewBytes).
        if (pixels is not null)
        {
            var gatherBytes = bytes.ToArray();
            var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(gatherBytes.AsSpan());
            (floats[36], floats[37], floats[38]) = (pixels.Size, pixels.Across, pixels.Down);
            fixed (byte* data = gatherBytes)
                device.Api.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)pixels.View).Buffer, 0, (ulong)gatherBytes.Length, data);
        }
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead);
        // The glow lights' buffer written again only once the frame before's passes have read it.
        device.MemoryBarrier(cmd, readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite);
        var glow = screen.Glow;
        VkImage[] images = [.. new[] { screen.Irradiance, screen.Geometry, screen.Kept, screen.Blended, screen.History, screen.LastGeometry }.Concat(glow.Images).Concat(screen.Cycle.Images)
            .Concat(pixels?.Images ?? []).Select(image => ((VulkanImage)image).Image)];
        device.PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            readers | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderRead | VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead))]);

        void Screen(IImageView light, IImageView surfaces, IBuffer viewBuffer, int across, int down, uint logged, uint mode, bool meshes = false)
        {
            var stage = meshes ? RayScreenStage : ScreenStage;
            // The bindings from the ray scene's on stand past its three in the build that reads it.
            uint At(int binding) => (uint)(meshes && binding >= RayScreenFirst ? binding + 3 : binding);
            var set = run.Set(stage);
            if (meshes)
                device.WriteRayScene(set, RayScreenFirst, rays!);
            run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 1, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Buffer(set, 2, VkDescriptorType.UniformBuffer, field.Info);
            run.Image(set, 3, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 4, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.GlowView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Buffer(set, 5, VkDescriptorType.UniformBuffer, gi.Lights);
            run.Image(set, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.CubesView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, At(20), VkDescriptorType.CombinedImageSampler, gi.CubesAtView, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 7, VkDescriptorType.SampledImage, gi.Merged[0].View, null, VkImageLayout.General);
            run.Image(set, 12, VkDescriptorType.SampledImage, gi.Merged[Math.Min(1, gi.Cascades - 1)].View, null, VkImageLayout.General);
            run.Image(set, 8, VkDescriptorType.StorageImage, ((VulkanImageView)light).View, null, VkImageLayout.General);
            run.Image(set, 9, VkDescriptorType.StorageImage, ((VulkanImageView)surfaces).View, null, VkImageLayout.General);
            run.Image(set, 11, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.ReachView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Buffer(set, 10, VkDescriptorType.UniformBuffer, viewBuffer);
            run.Buffer(set, 13, VkDescriptorType.StorageBuffer, screen.GlowSeen);
            run.Buffer(set, 14, VkDescriptorType.StorageBuffer, screen.RayLog);
            run.Image(set, 15, VkDescriptorType.SampledImage, ((VulkanImageView)field.FacesView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 16, VkDescriptorType.SampledImage, ((VulkanImageView)field.SecondView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
            // The third to fifth cascades', merged, the last the probes have again where they have
            // fewer.
            for (int c = 2; c < 5; c++)
                run.Image(set, At(15 + c), VkDescriptorType.SampledImage, gi.Merged[Math.Min(c, gi.Cascades - 1)].View, null, VkImageLayout.General);
            uint TexelsOf(int c) => c < gi.Cascades ? (uint)gi.Texels[c] : 0;
            // The probes stand in the cascades before the far cascade's alone (gi_screen.slang).
            var standing = (uint)Math.Min(gi.Cascades - (gi.Far ? 1 : 0), 5);
            ReadOnlySpan<uint> push = [TexelsOf(0), TexelsOf(1), logged, mode | (lightThrough ? 4u : 0) | standing << 8, TexelsOf(2), TexelsOf(3), TexelsOf(4),
                (uint)Math.Min(gi.Cascades, 5)];
            var (pipeline, layout, _) = GiStages[stage];
            device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
            device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
            fixed (uint* p = push)
                device.Api.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, 32, p);
            device.Api.vkCmdDispatch(cmd, (uint)(across + 7) / 8, (uint)(down + 7) / 8, 1);
        }

        Screen(screen.IrradianceView, screen.GeometryView, screen.View, screen.Across, screen.Down, (uint)(loggedProbe + 1),
            (probesGlowOnly ? 1u : 0) | (loggedNotANumber ? 2u : 0));
        // The gather's grid beside the probes, a ray from each texel and no probe logged or fed.
        if (pixels is not null)
            Screen(pixels.LightView, pixels.GeometryView, pixels.View, pixels.Across, pixels.Down, 0, 8u, rayed);
        MarkGlobalIllumination(commandBuffer, "gi_screen");
        if (traced is { } trace)
        {
            RecordScreenCascades(run, gi, screen, field, depth, depthSampler, trace);
            MarkGlobalIllumination(commandBuffer, "gi_screen_cascades");
        }

        // Each probe's light kept over the frames, then blended with its neighbors' on like
        // surfaces, where they traced any, two passes of gi_screen_filter.slang, or the gather's
        // texels blended with their neighbors' within the frame, its third, as the cascades' probes
        // are.
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        (uint Pass, IImageView From, IImageView Surfaces, IImageView To, IBuffer View, int Across, int Down)[] passes = pixels is not null
            ? [(2u, pixels.LightView, pixels.GeometryView, pixels.BlendedView, pixels.View, pixels.Across, pixels.Down)]
            : traced is not null ? [(2u, screen.IrradianceView, screen.GeometryView, screen.BlendedView, screen.View, screen.Across, screen.Down)]
            : probesGlowOnly ? []
            : [(0u, screen.IrradianceView, screen.GeometryView, screen.KeptView, screen.View, screen.Across, screen.Down),
                (1u, screen.KeptView, screen.GeometryView, screen.BlendedView, screen.View, screen.Across, screen.Down)];
        if (passes.Length > 0)
        {
            var (filterPipeline, filterLayout, _) = GiStages[FilterStage];
            device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, filterPipeline);
            foreach (var (pass, from, surfaces, to, viewBuffer, across, down) in passes)
            {
                var blend = run.Set(FilterStage);
                run.Image(blend, 0, VkDescriptorType.SampledImage, ((VulkanImageView)from).View, null, VkImageLayout.General);
                run.Image(blend, 1, VkDescriptorType.SampledImage, ((VulkanImageView)surfaces).View, null, VkImageLayout.General);
                run.Image(blend, 2, VkDescriptorType.StorageImage, ((VulkanImageView)to).View, null, VkImageLayout.General);
                run.Image(blend, 3, VkDescriptorType.SampledImage, ((VulkanImageView)screen.HistoryView).View, null, VkImageLayout.General);
                run.Image(blend, 4, VkDescriptorType.SampledImage, ((VulkanImageView)screen.LastGeometryView).View, null, VkImageLayout.General);
                run.Buffer(blend, 5, VkDescriptorType.UniformBuffer, viewBuffer);
                run.Image(blend, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
                run.Image(blend, 7, VkDescriptorType.SampledImage, ((VulkanImageView)screen.Cycle.LastPartialView).View, null, VkImageLayout.General);
                run.Image(blend, 8, VkDescriptorType.SampledImage, ((VulkanImageView)screen.Cycle.LastFullView).View, null, VkImageLayout.General);
                run.Image(blend, 9, VkDescriptorType.StorageImage, ((VulkanImageView)screen.Cycle.PartialView).View, null, VkImageLayout.General);
                run.Image(blend, 10, VkDescriptorType.StorageImage, ((VulkanImageView)screen.Cycle.FullView).View, null, VkImageLayout.General);
                run.Image(blend, 11, VkDescriptorType.SampledImage, ((VulkanImageView)screen.Cycle.LastEasedView).View, null, VkImageLayout.General);
                run.Image(blend, 12, VkDescriptorType.StorageImage, ((VulkanImageView)screen.Cycle.EasedView).View, null, VkImageLayout.General);
                device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, filterLayout, 0, 1, &blend, 0, null);
                ReadOnlySpan<uint> which = [pass, 0, 0, 0];
                fixed (uint* p = which)
                    device.Api.vkCmdPushConstants(cmd, filterLayout, VkShaderStageFlags.Compute, 0, 16, p);
                device.Api.vkCmdDispatch(cmd, (uint)(across + 7) / 8, (uint)(down + 7) / 8, 1);
                device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
            }
        }

        MarkGlobalIllumination(commandBuffer, "gi_filter");

        // The glow lights' light on each texel of the view's depth, from the probes around it and
        // the frame before's, beside the blend, which writes none of what it reads. A frame with no
        // glow lights skips it, the model pass reading none of it then, since run with its threads
        // leaving at once it still costs lavapipe a share of each frame.
        var glowExtent = glow.Light.Description.Extent;
        if (glowLights)
        {
            var lit = run.Set(GlowStage);
            run.Image(lit, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(lit, 1, VkDescriptorType.SampledImage, ((VulkanImageView)screen.GeometryView).View, null, VkImageLayout.General);
            run.Buffer(lit, 2, VkDescriptorType.UniformBuffer, screen.View);
            run.Buffer(lit, 3, VkDescriptorType.UniformBuffer, gi.Lights);
            run.Buffer(lit, 4, VkDescriptorType.UniformBuffer, field.Info);
            run.Buffer(lit, 5, VkDescriptorType.StorageBuffer, screen.GlowSeen);
            run.Image(lit, 6, VkDescriptorType.StorageImage, ((VulkanImageView)glow.LightView).View, null, VkImageLayout.General);
            run.Image(lit, 7, VkDescriptorType.SampledImage, ((VulkanImageView)glow.LastLightView).View, null, VkImageLayout.General);
            run.Image(lit, 8, VkDescriptorType.StorageImage, ((VulkanImageView)glow.StateView).View, null, VkImageLayout.General);
            run.Image(lit, 9, VkDescriptorType.SampledImage, ((VulkanImageView)glow.LastStateView).View, null, VkImageLayout.General);
            run.Image(lit, 10, VkDescriptorType.StorageImage, ((VulkanImageView)glow.FarView).View, null, VkImageLayout.General);
            run.Image(lit, 11, VkDescriptorType.StorageImage, ((VulkanImageView)glow.NormalView).View, null, VkImageLayout.General);
            run.Image(lit, 12, VkDescriptorType.SampledImage, ((VulkanImageView)glow.LastFarView).View, null, VkImageLayout.General);
            var (glowPipeline, glowLayout, _) = GiStages[GlowStage];
            device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, glowPipeline);
            device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, glowLayout, 0, 1, &lit, 0, null);
            device.Api.vkCmdDispatch(cmd, (glowExtent.Width + 7) / 8, (glowExtent.Height + 7) / 8, 1);
        }
        MarkGlobalIllumination(commandBuffer, "gi_glow");

        // This frame's kept light and surfaces, and its glow light, held for the next frame's.
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite);
        var region = new VkImageCopy
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            extent = new VkExtent3D((uint)screen.Across, (uint)screen.Down, 1),
        };
        if (!probesGlowOnly)
        {
            device.Api.vkCmdCopyImage(cmd, ((VulkanImage)screen.Kept).Image, VkImageLayout.General, ((VulkanImage)screen.History).Image, VkImageLayout.General, 1, &region);
            device.Api.vkCmdCopyImage(cmd, ((VulkanImage)screen.Geometry).Image, VkImageLayout.General, ((VulkanImage)screen.LastGeometry).Image, VkImageLayout.General, 1, &region);
            device.Api.vkCmdCopyImage(cmd, ((VulkanImage)screen.Cycle.Partial).Image, VkImageLayout.General, ((VulkanImage)screen.Cycle.LastPartial).Image, VkImageLayout.General, 1, &region);
            device.Api.vkCmdCopyImage(cmd, ((VulkanImage)screen.Cycle.Full).Image, VkImageLayout.General, ((VulkanImage)screen.Cycle.LastFull).Image, VkImageLayout.General, 1, &region);
            device.Api.vkCmdCopyImage(cmd, ((VulkanImage)screen.Cycle.Eased).Image, VkImageLayout.General, ((VulkanImage)screen.Cycle.LastEased).Image, VkImageLayout.General, 1, &region);
        }
        region.extent = new VkExtent3D(glowExtent.Width, glowExtent.Height, 1);
        device.Api.vkCmdCopyImage(cmd, ((VulkanImage)glow.Light).Image, VkImageLayout.General, ((VulkanImage)glow.LastLight).Image, VkImageLayout.General, 1, &region);
        device.Api.vkCmdCopyImage(cmd, ((VulkanImage)glow.State).Image, VkImageLayout.General, ((VulkanImage)glow.LastState).Image, VkImageLayout.General, 1, &region);
        device.Api.vkCmdCopyImage(cmd, ((VulkanImage)glow.Far).Image, VkImageLayout.General, ((VulkanImage)glow.LastFar).Image, VkImageLayout.General, 1, &region);

        device.PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferWrite | VkAccessFlags2.TransferRead,
            readers, VkAccessFlags2.ShaderRead))]);
        return run;
    }
}
