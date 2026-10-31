using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>Makes the screen probes of a window <paramref name="width"/> by <paramref name="height"/> pixels, a probe every <paramref name="tile"/> pixels a side.</summary>
    public GpuScreenProbes CreateScreenProbes(uint width, uint height, int tile)
    {
        var across = (int)((width + tile - 1) / tile);
        var down = (int)((height + tile - 1) / tile);
        var desc = new ImageDesc(new Extent2D((uint)across, (uint)down), ImageFormat.R16G16B16A16_Float,
            ImageUsage.Sampled | ImageUsage.Storage | ImageUsage.TransferDst | ImageUsage.TransferSrc);
        var irradiance = CreateImage(desc);
        var geometry = CreateImage(desc);
        var kept = CreateImage(desc);
        var blended = CreateImage(desc);
        var history = CreateImage(desc);
        var lastGeometry = CreateImage(desc);
        var irradianceView = CreateImageView(irradiance);
        var geometryView = CreateImageView(geometry);
        var keptView = CreateImageView(kept);
        var blendedView = CreateImageView(blended);
        var historyView = CreateImageView(history);
        var lastGeometryView = CreateImageView(lastGeometry);
        Name(irradiance, "Screen probes' light");
        Name(geometry, "Screen probes' surfaces");
        Name(kept, "Screen probes' light, kept over the frames");
        Name(blended, "Screen probes' light, blended");
        Name(history, "Screen probes' light kept, the frame before");
        Name(lastGeometry, "Screen probes' surfaces, the frame before");
        var view = CreateBuffer(new BufferDesc(GpuScreenProbes.ViewBytes, BufferUsage.Uniform | BufferUsage.TransferDst));
        var glowSeen = CreateBuffer(new BufferDesc((ulong)(across * down) * GpuScreenProbes.GlowSeenBytes, BufferUsage.Storage | BufferUsage.TransferDst));
        var rayLog = CreateBuffer(new BufferDesc(GpuScreenProbes.RayLogBytes, BufferUsage.Storage, CpuAccessMode.Read));
        Name(rayLog, "Screen probe's rays, as gi.screenprobe asks for them");
        // The glow lights' light a texel of the view's depth each, half the view's size as
        // AmbientOcclusionRenderer draws it.
        var glowDesc = desc with { Extent = new Extent2D(Math.Max(1, width / 2), Math.Max(1, height / 2)) };
        IImage[] glowImages = [.. Enumerable.Range(0, 4).Select(_ => CreateImage(glowDesc)), CreateImage(glowDesc with { Format = ImageFormat.R8G8B8A8_UNorm }),
            CreateImage(glowDesc), CreateImage(glowDesc)];
        var glowViews = glowImages.Select(CreateImageView).ToArray();
        var glow = new ScreenGlow(glowImages[0], glowViews[0], glowImages[1], glowViews[1], glowImages[2], glowViews[2], glowImages[3], glowViews[3],
            glowImages[4], glowViews[4], glowImages[5], glowViews[5], glowImages[6], glowViews[6]);
        Name(glow.Light, "Screen's glow light");
        Name(glow.LastLight, "Screen's glow light, the frame before");
        Name(glow.State, "Screen's glow light's spread and frames");
        Name(glow.LastState, "Screen's glow light's spread and frames, the frame before");
        Name(glow.Normal, "Screen's glow light's normals");
        Name(glow.Far, "Screen's far glow light");
        Name(glow.LastFar, "Screen's far glow light, the frame before");
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        // Every image holds nothing, ready to be sampled, until the first frame places the probes.
        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        IImage[] made = [irradiance, geometry, kept, blended, history, lastGeometry, .. glow.Images];
        VkImage[] images = [.. made.Select(image => ((VulkanImage)image).Image)];
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        // The glow light's texels on no surface, their distance from the eye -1.
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        var nowhere = new VkClearColorValue(0f, 0f, 0f, -1f);
        foreach (var image in made)
            _deviceApi.vkCmdClearColorImage(cmd, ((VulkanImage)image).Image, VkImageLayout.General,
                image == glow.Light || image == glow.LastLight ? &nowhere : &none, 1, &whole);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)glowSeen).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead))]);
        EndSingleTimeCommands(cmd);
        foreach (var image in made) ((VulkanImage)image).Layout = VkImageLayout.ShaderReadOnlyOptimal;

        return new GpuScreenProbes(across, down, tile, irradiance, irradianceView, geometry, geometryView, sampler, view, () =>
        {
            sampler.Dispose();
            view.Dispose();
            glowSeen.Dispose();
            rayLog.Dispose();
            IDisposable[] all = [irradianceView, geometryView, keptView, blendedView, historyView, lastGeometryView, .. glow.Views,
                irradiance, geometry, kept, blended, history, lastGeometry, .. glow.Images];
            foreach (var one in all) one.Dispose();
        }, kept, keptView, blended, blendedView, history, historyView, lastGeometry, lastGeometryView, glowSeen, glow, rayLog);
    }

    /// <summary>
    /// Records the screen probes' work after the world's probes are merged, the view written, each
    /// probe placed on the window's depth and its rays traced and summed, left ready for the model
    /// pass to sample.
    /// </summary>
    /// <remarks>
    /// With <paramref name="glowOnly"/> set the probes stand for the glow lights alone, as at
    /// <see cref="GlobalIllumination.Low"/>, which leaves the screen's probes out, each placed and
    /// marching to the lights with no ray traced, filtered or held, so the glow lights' light on the
    /// depth's texels is made from them as it is from the whole probes.
    /// </remarks>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordScreenProbes(ICommandBuffer commandBuffer, GpuIllumination gi, GpuScreenProbes screen, GpuSceneField field,
        IImageView depth, ISampler depthSampler, ReadOnlySpan<byte> view, bool glowLights, int loggedProbe = -1, bool glowOnly = false,
        bool loggedNotANumber = false, bool lightThrough = false)
    {
        var run = new ProbeRun(this, commandBuffer, [(ScreenStage, 1), (FilterStage, glowOnly ? 0 : 2), (GlowStage, glowLights ? 1 : 0)]);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = view[..(Math.Min(view.Length, GpuScreenProbes.ViewBytes) & ~3)];
        fixed (byte* data = bytes)
            _deviceApi.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)screen.View).Buffer, 0, (ulong)bytes.Length, data);
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead);
        // The glow lights' buffer written again only once the frame before's passes have read it.
        MemoryBarrier(cmd, readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite);
        var glow = screen.Glow;
        VkImage[] images = [.. new[] { screen.Irradiance, screen.Geometry, screen.Kept, screen.Blended, screen.History, screen.LastGeometry }.Concat(glow.Images)
            .Select(image => ((VulkanImage)image).Image)];
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            readers | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderRead | VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead))]);

        var set = run.Set(ScreenStage);
        run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 1, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 2, VkDescriptorType.UniformBuffer, field.Info);
        run.Image(set, 3, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 4, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.GlowView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 5, VkDescriptorType.UniformBuffer, gi.Lights);
        run.Image(set, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.CubesView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 7, VkDescriptorType.SampledImage, gi.Merged[0].View, null, VkImageLayout.General);
        run.Image(set, 12, VkDescriptorType.SampledImage, gi.Merged[Math.Min(1, gi.Cascades - 1)].View, null, VkImageLayout.General);
        run.Image(set, 8, VkDescriptorType.StorageImage, ((VulkanImageView)screen.IrradianceView).View, null, VkImageLayout.General);
        run.Image(set, 9, VkDescriptorType.StorageImage, ((VulkanImageView)screen.GeometryView).View, null, VkImageLayout.General);
        run.Image(set, 11, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.ReachView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 10, VkDescriptorType.UniformBuffer, screen.View);
        run.Buffer(set, 13, VkDescriptorType.StorageBuffer, screen.GlowSeen);
        run.Buffer(set, 14, VkDescriptorType.StorageBuffer, screen.RayLog);
        run.Image(set, 15, VkDescriptorType.SampledImage, ((VulkanImageView)field.FacesView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 16, VkDescriptorType.SampledImage, ((VulkanImageView)field.SecondView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        ReadOnlySpan<uint> push = [(uint)gi.Texels[0], gi.Cascades > 1 ? (uint)gi.Texels[1] : 0, (uint)(loggedProbe + 1), (glowOnly ? 1u : 0) | (loggedNotANumber ? 2u : 0) | (lightThrough ? 4u : 0)];
        var (pipeline, layout, _) = GiStages[ScreenStage];
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
        fixed (uint* p = push)
            _deviceApi.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, 16, p);
        _deviceApi.vkCmdDispatch(cmd, (uint)(screen.Across + 7) / 8, (uint)(screen.Down + 7) / 8, 1);

        // Each probe's light kept over the frames, then blended with its neighbors' on like
        // surfaces, where they traced any, two passes of gi_screen_filter.slang.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        if (!glowOnly)
        {
            var (filterPipeline, filterLayout, _) = GiStages[FilterStage];
            _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, filterPipeline);
            foreach (var (pass, from, to) in new[] { (0u, screen.IrradianceView, screen.KeptView), (1u, screen.KeptView, screen.BlendedView) })
            {
                var blend = run.Set(FilterStage);
                run.Image(blend, 0, VkDescriptorType.SampledImage, ((VulkanImageView)from).View, null, VkImageLayout.General);
                run.Image(blend, 1, VkDescriptorType.SampledImage, ((VulkanImageView)screen.GeometryView).View, null, VkImageLayout.General);
                run.Image(blend, 2, VkDescriptorType.StorageImage, ((VulkanImageView)to).View, null, VkImageLayout.General);
                run.Image(blend, 3, VkDescriptorType.SampledImage, ((VulkanImageView)screen.HistoryView).View, null, VkImageLayout.General);
                run.Image(blend, 4, VkDescriptorType.SampledImage, ((VulkanImageView)screen.LastGeometryView).View, null, VkImageLayout.General);
                run.Buffer(blend, 5, VkDescriptorType.UniformBuffer, screen.View);
                run.Image(blend, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
                _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, filterLayout, 0, 1, &blend, 0, null);
                ReadOnlySpan<uint> which = [pass, 0, 0, 0];
                fixed (uint* p = which)
                    _deviceApi.vkCmdPushConstants(cmd, filterLayout, VkShaderStageFlags.Compute, 0, 16, p);
                _deviceApi.vkCmdDispatch(cmd, (uint)(screen.Across + 7) / 8, (uint)(screen.Down + 7) / 8, 1);
                MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
            }
        }

        // The glow lights' light on each texel of the view's depth, from the probes around it and
        // the frame before's, beside the blend, which writes none of what it reads. A frame with no
        // glow lights skips it, the model pass reading none of it then. Its threads leaving at
        // once, it cost each frame of an app of a cube 0.09 s on lavapipe at 320 by 180, an eighth
        // of it.
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
            _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, glowPipeline);
            _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, glowLayout, 0, 1, &lit, 0, null);
            _deviceApi.vkCmdDispatch(cmd, (glowExtent.Width + 7) / 8, (glowExtent.Height + 7) / 8, 1);
        }

        // This frame's kept light and surfaces, and its glow light, held for the next frame's.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite);
        var region = new VkImageCopy
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            extent = new VkExtent3D((uint)screen.Across, (uint)screen.Down, 1),
        };
        if (!glowOnly)
        {
            _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)screen.Kept).Image, VkImageLayout.General, ((VulkanImage)screen.History).Image, VkImageLayout.General, 1, &region);
            _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)screen.Geometry).Image, VkImageLayout.General, ((VulkanImage)screen.LastGeometry).Image, VkImageLayout.General, 1, &region);
        }
        region.extent = new VkExtent3D(glowExtent.Width, glowExtent.Height, 1);
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)glow.Light).Image, VkImageLayout.General, ((VulkanImage)glow.LastLight).Image, VkImageLayout.General, 1, &region);
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)glow.State).Image, VkImageLayout.General, ((VulkanImage)glow.LastState).Image, VkImageLayout.General, 1, &region);
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)glow.Far).Image, VkImageLayout.General, ((VulkanImage)glow.LastFar).Image, VkImageLayout.General, 1, &region);

        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferWrite | VkAccessFlags2.TransferRead,
            readers, VkAccessFlags2.ShaderRead))]);
        return run;
    }
}
