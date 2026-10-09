using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The window's scene as a frame drew it, in linear light at half the window's size with its mips,
/// which a glossy surface's reflection reads the light of a surface it met on the screen from in
/// the frame after.
/// </summary>
internal sealed class GpuReflectionHistory : IDisposable
{
    private readonly Action _dispose;

    internal GpuReflectionHistory(Extent2D window, IImage image, IImageView view, ISampler sampler, VkImage depth, IImageView depthView,
        ISampler depthSampler, Action dispose)
    {
        Window = window;
        Image = image;
        View = view;
        Sampler = sampler;
        Depth = depth;
        DepthView = depthView;
        DepthSampler = depthSampler;
        _dispose = dispose;
    }

    internal VkImage Depth { get; }

    /// <summary>The window's depth at half its size the same frame, which tells a reflection whether the picture showed the surface it met or something in front of it.</summary>
    public IImageView DepthView { get; }

    /// <summary>A sampler that reads a depth as it is.</summary>
    public ISampler DepthSampler { get; }

    /// <summary>The size of the window it was made for, twice its own.</summary>
    public Extent2D Window { get; }

    internal IImage Image { get; }

    /// <summary>Every level of the picture, to sample a blurred level of for a rough surface.</summary>
    public IImageView View { get; }

    /// <summary>A sampler that blends between texels and between levels.</summary>
    public ISampler Sampler { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// Makes the picture a reflection reads the frame before from, for a window of
    /// <paramref name="width"/> by <paramref name="height"/> pixels, black until the first frame is
    /// kept in it.
    /// </summary>
    public GpuReflectionHistory CreateReflectionHistory(uint width, uint height)
    {
        var half = new Extent2D(Math.Max(1, width / 2), Math.Max(1, height / 2));
        var image = CreateImage(new ImageDesc(half, ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.TransferSrc | ImageUsage.TransferDst,
            ImageDesc.FullMipChain(half.Width, half.Height)));
        Name(image, "Reflections' frame before");
        var view = CreateImageView(image);
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, MipFilter: SamplerFilter.Linear));

        // The depth beside it, at the size of the window's depth, which is copied into it.
        var (depth, depthMemory) = TargetImage(VkFormat.D32Sfloat, half.Width, half.Height, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst);
        var depthImage = new VulkanImage(this, depth, depthMemory, new ImageDesc(half, ImageFormat.D32_Float, ImageUsage.Sampled | ImageUsage.TransferDst));
        var depthView = new VulkanImageView(this, depthImage, TargetView(depth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth));
        var depthSampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        var vkImage = (VulkanImage)image;
        var levels = ColorLevels(0, LevelsOf(vkImage));
        var depthRange = new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, 1);
        var cmd = BeginSingleTimeCommands();
        PipelineBarrier(cmd,
        [
            ImageBarrier(vkImage.Image, levels, VkImageLayout.Undefined, VkImageLayout.General,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
            ImageBarrier(depth, depthRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        _deviceApi.vkCmdClearColorImage(cmd, vkImage.Image, VkImageLayout.General, &none, 1, &levels);
        var far = new VkClearDepthStencilValue(1f, 0);
        _deviceApi.vkCmdClearDepthStencilImage(cmd, depth, VkImageLayout.TransferDstOptimal, &far, 1, &depthRange);
        PipelineBarrier(cmd,
        [
            ImageBarrier(vkImage.Image, levels, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead),
            ImageBarrier(depth, depthRange, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead),
        ]);
        EndSingleTimeCommands(cmd);
        vkImage.Layout = VkImageLayout.ShaderReadOnlyOptimal;
        return new GpuReflectionHistory(new Extent2D(width, height), image, view, sampler, depth, depthView, depthSampler, () =>
        {
            depthSampler.Dispose();
            depthView.Dispose();
            depthImage.Dispose();
            sampler.Dispose();
            view.Dispose();
            image.Dispose();
        });
    }

    /// <summary>
    /// Records <paramref name="scene"/>, the window's scene the HDR frame drew this frame, copied
    /// into <paramref name="history"/> at half its size and filtered down its mips, and
    /// <paramref name="depth"/>, the window's depth at that size, copied beside it, after the model
    /// pass has read the frame before from them.
    /// </summary>
    public void RecordKeepFrame(ICommandBuffer commandBuffer, IImageView scene, IImageView depth, GpuReflectionHistory history)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        var cmd = vkCmd.Handle;
        var source = (VulkanImage)scene.Image;
        var target = (VulkanImage)history.Image;
        var sourceExtent = source.Description.Extent;
        var targetExtent = target.Description.Extent;
        var readers = VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;

        // The scene from the layout its pass left it in to be copied from, and every level of the
        // picture, read by the model pass a moment ago, to be written.
        PipelineBarrier(cmd,
        [
            ImageBarrier(source.Image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput | readers, VkAccessFlags2.ColorAttachmentWrite | VkAccessFlags2.ShaderRead,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead),
            ImageBarrier(target.Image, ColorLevels(0, LevelsOf(target)), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferDstOptimal,
                readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        VkImageBlit blit = new()
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
        };
        blit.srcOffsets[1] = new VkOffset3D((int)sourceExtent.Width, (int)sourceExtent.Height, 1);
        blit.dstOffsets[1] = new VkOffset3D((int)targetExtent.Width, (int)targetExtent.Height, 1);
        _deviceApi.vkCmdBlitImage(cmd, source.Image, VkImageLayout.TransferSrcOptimal, target.Image, VkImageLayout.TransferDstOptimal, 1, &blit, VkFilter.Linear);
        PipelineBarrier(cmd, ImageBarrier(source.Image, ColorLevels(0, 1), VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, readers, VkAccessFlags2.ShaderRead));
        RecordMipChain(cmd, target);

        // The depth, from the layout it is sampled in to be copied from, into the one beside the
        // picture, which the model pass read a moment ago.
        var depthImage = ((VulkanImage)depth.Image).Image;
        var depthRange = new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, 1);
        PipelineBarrier(cmd,
        [
            ImageBarrier(depthImage, depthRange, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead),
            ImageBarrier(history.Depth, depthRange, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferDstOptimal,
                readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        var depthExtent = depth.Image.Description.Extent;
        VkImageCopy copy = new()
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Depth, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Depth, 0, 0, 1),
            extent = new VkExtent3D(Math.Min(depthExtent.Width, targetExtent.Width), Math.Min(depthExtent.Height, targetExtent.Height), 1),
        };
        _deviceApi.vkCmdCopyImage(cmd, depthImage, VkImageLayout.TransferSrcOptimal, history.Depth, VkImageLayout.TransferDstOptimal, 1, &copy);
        PipelineBarrier(cmd,
        [
            ImageBarrier(depthImage, depthRange, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, readers, VkAccessFlags2.ShaderRead),
            ImageBarrier(history.Depth, depthRange, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, readers, VkAccessFlags2.ShaderRead),
        ]);
    }
}
