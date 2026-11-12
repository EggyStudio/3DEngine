using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

// The screen's probes' images and buffers made and cleared on the device, by global illumination
// for each view it places probes in, and by the model pass for the empty one it binds where light
// does not bounce.
internal sealed unsafe partial class GpuScreenProbes
{
    /// <summary>Makes the screen probes of a window <paramref name="width"/> by <paramref name="height"/> pixels, a probe every <paramref name="tile"/> pixels a side.</summary>
    public static GpuScreenProbes Create(GraphicsDevice device, uint width, uint height, int tile, float glowTexel = 2)
    {
        var across = (int)((width + tile - 1) / tile);
        var down = (int)((height + tile - 1) / tile);
        var desc = new ImageDesc(new Extent2D((uint)across, (uint)down), ImageFormat.R16G16B16A16_Float,
            ImageUsage.Sampled | ImageUsage.Storage | ImageUsage.TransferDst | ImageUsage.TransferSrc);
        var irradiance = device.CreateImage(desc);
        var geometry = device.CreateImage(desc);
        var kept = device.CreateImage(desc);
        var blended = device.CreateImage(desc);
        var history = device.CreateImage(desc);
        var lastGeometry = device.CreateImage(desc);
        var irradianceView = device.CreateImageView(irradiance);
        var geometryView = device.CreateImageView(geometry);
        var keptView = device.CreateImageView(kept);
        var blendedView = device.CreateImageView(blended);
        var historyView = device.CreateImageView(history);
        var lastGeometryView = device.CreateImageView(lastGeometry);
        device.Name(irradiance, "Screen probes' light");
        device.Name(geometry, "Screen probes' surfaces");
        device.Name(kept, "Screen probes' light, kept over the frames");
        device.Name(blended, "Screen probes' light, blended");
        device.Name(history, "Screen probes' light kept, the frame before");
        device.Name(lastGeometry, "Screen probes' surfaces, the frame before");
        var view = device.CreateBuffer(new BufferDesc(GpuScreenProbes.ViewBytes, BufferUsage.Uniform | BufferUsage.TransferDst));
        var glowSeen = device.CreateBuffer(new BufferDesc((ulong)(across * down) * GpuScreenProbes.GlowSeenBytes, BufferUsage.Storage | BufferUsage.TransferDst));
        var rayLog = device.CreateBuffer(new BufferDesc(GpuScreenProbes.RayLogBytes, BufferUsage.Storage, CpuAccessMode.Read));
        device.Name(rayLog, "Screen probe's rays, as gi.screenprobe asks for them");
        // The glow lights' light a texel each of glowTexel pixels a side, a texel of the half-sized
        // depth AmbientOcclusionRenderer draws up to 1,080 rows (GpuScreenProbes.GlowTexelAt).
        var glowDesc = desc with { Extent = new Extent2D(Math.Max(1, (uint)MathF.Ceiling(width / glowTexel)), Math.Max(1, (uint)MathF.Ceiling(height / glowTexel))) };
        IImage[] glowImages = [.. Enumerable.Range(0, 4).Select(_ => device.CreateImage(glowDesc)), device.CreateImage(glowDesc with { Format = ImageFormat.R8G8B8A8_UNorm }),
            device.CreateImage(glowDesc), device.CreateImage(glowDesc)];
        var glowViews = glowImages.Select(device.CreateImageView).ToArray();
        var glow = new ScreenGlow(glowImages[0], glowViews[0], glowImages[1], glowViews[1], glowImages[2], glowViews[2], glowImages[3], glowViews[3],
            glowImages[4], glowViews[4], glowImages[5], glowViews[5], glowImages[6], glowViews[6]);
        device.Name(glow.Light, "Screen's glow light");
        device.Name(glow.LastLight, "Screen's glow light, the frame before");
        device.Name(glow.State, "Screen's glow light's spread and frames");
        device.Name(glow.LastState, "Screen's glow light's spread and frames, the frame before");
        device.Name(glow.Normal, "Screen's glow light's normals");
        device.Name(glow.Far, "Screen's far glow light");
        device.Name(glow.LastFar, "Screen's far glow light, the frame before");
        IImage[] cycleImages = [.. Enumerable.Range(0, 6).Select(_ => device.CreateImage(desc))];
        var cycleViews = cycleImages.Select(device.CreateImageView).ToArray();
        var cycle = new ScreenCycle(cycleImages[0], cycleViews[0], cycleImages[1], cycleViews[1], cycleImages[2], cycleViews[2], cycleImages[3], cycleViews[3],
            cycleImages[4], cycleViews[4], cycleImages[5], cycleViews[5]);
        device.Name(cycle.Partial, "Screen probes' light over the places' cycle being gathered");
        device.Name(cycle.LastPartial, "Screen probes' light over the places' cycle being gathered, the frame before");
        device.Name(cycle.Full, "Screen probes' light over the last whole cycle of places");
        device.Name(cycle.LastFull, "Screen probes' light over the last whole cycle of places, the frame before");
        device.Name(cycle.Eased, "Screen probes' light the last whole cycle eases from");
        device.Name(cycle.LastEased, "Screen probes' light the last whole cycle eases from, the frame before");
        var sampler = device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        // Every image holds nothing, ready to be sampled, until the first frame places the probes.
        var cmd = device.BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        IImage[] made = [irradiance, geometry, kept, blended, history, lastGeometry, .. glow.Images, .. cycle.Images];
        VkImage[] images = [.. made.Select(image => ((VulkanImage)image).Image)];
        device.PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        // The glow light's texels on no surface, their distance from the eye -1.
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        var nowhere = new VkClearColorValue(0f, 0f, 0f, -1f);
        foreach (var image in made)
            device.Api.vkCmdClearColorImage(cmd, ((VulkanImage)image).Image, VkImageLayout.General,
                image == glow.Light || image == glow.LastLight ? &nowhere : &none, 1, &whole);
        device.Api.vkCmdFillBuffer(cmd, ((VulkanBuffer)glowSeen).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        device.PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead))]);
        device.EndSingleTimeCommands(cmd);
        foreach (var image in made) ((VulkanImage)image).Layout = VkImageLayout.ShaderReadOnlyOptimal;

        return new GpuScreenProbes(across, down, tile, irradiance, irradianceView, geometry, geometryView, sampler, view, () =>
        {
            sampler.Dispose();
            view.Dispose();
            glowSeen.Dispose();
            rayLog.Dispose();
            IDisposable[] all = [irradianceView, geometryView, keptView, blendedView, historyView, lastGeometryView, .. glow.Views, .. cycle.Views,
                irradiance, geometry, kept, blended, history, lastGeometry, .. glow.Images, .. cycle.Images];
            foreach (var one in all) one.Dispose();
        }, kept, keptView, blended, blendedView, history, historyView, lastGeometry, lastGeometryView, glowSeen, glow, rayLog) { GlowTexel = glowTexel, Cycle = cycle };
    }
}
