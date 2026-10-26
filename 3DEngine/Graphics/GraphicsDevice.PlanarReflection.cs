using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The scene as the window's camera mirrored in the plane a program set sees it, drawn at half the
/// window's size in linear light and kept with its mips, which a surface lying in the plane reflects
/// (<see cref="E3D.SetReflectionPlane"/>).
/// </summary>
internal sealed class GpuPlanarReflection : IDisposable
{
    private readonly Action _dispose;

    internal GpuPlanarReflection(Extent2D window, RenderTarget target, IImage image, IImageView view, ISampler sampler, Action dispose)
    {
        Window = window;
        Target = target;
        Image = image;
        View = view;
        Sampler = sampler;
        _dispose = dispose;
    }

    /// <summary>The size of the window it was made for, twice its own.</summary>
    public Extent2D Window { get; }

    /// <summary>What the mirrored view is drawn into, in half floats with a depth of its own.</summary>
    public RenderTarget Target { get; }

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
    /// Makes what the mirrored view is drawn into and the picture a surface lying in the plane reads
    /// it from, for a window of <paramref name="width"/> by <paramref name="height"/> pixels, both at
    /// half its size, the picture black until the first view is drawn.
    /// </summary>
    public GpuPlanarReflection CreatePlanarReflection(uint width, uint height)
    {
        var half = new Extent2D(Math.Max(1, width / 2), Math.Max(1, height / 2));
        var target = CreateRenderTarget(half.Width, half.Height, ImageFormat.R16G16B16A16_Float, depth: true, multisampled: false);
        Name(target.ColorView.Image, "Planar reflection's view");
        var image = CreateImage(new ImageDesc(half, ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.TransferSrc | ImageUsage.TransferDst,
            ImageDesc.FullMipChain(half.Width, half.Height)));
        Name(image, "Planar reflection");
        var view = CreateImageView(image);
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, MipFilter: SamplerFilter.Linear));
        var vkImage = (VulkanImage)image;
        var levels = ColorLevels(0, LevelsOf(vkImage));
        var cmd = BeginSingleTimeCommands();
        PipelineBarrier(cmd, ImageBarrier(vkImage.Image, levels, VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite));
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        _deviceApi.vkCmdClearColorImage(cmd, vkImage.Image, VkImageLayout.General, &none, 1, &levels);
        PipelineBarrier(cmd, ImageBarrier(vkImage.Image, levels, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead));
        EndSingleTimeCommands(cmd);
        vkImage.Layout = VkImageLayout.ShaderReadOnlyOptimal;
        return new GpuPlanarReflection(new Extent2D(width, height), target, image, view, sampler, () =>
        {
            sampler.Dispose();
            view.Dispose();
            image.Dispose();
            target.Dispose();
        });
    }

    /// <summary>
    /// Records the mirrored view drawn into <paramref name="planar"/>'s target this frame copied into
    /// its picture and filtered down its mips, ahead of the window's pass that reads it.
    /// </summary>
    public void RecordPlanarReflection(ICommandBuffer commandBuffer, GpuPlanarReflection planar)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        RecordCopyDown(vkCmd.Handle, (VulkanImage)planar.Target.ColorView.Image, (VulkanImage)planar.Image);
    }

    // Copies source, which its pass left to be sampled, into the first level of target, which the
    // passes since read every level of, at target's size, and filters it down target's mips.
    private void RecordCopyDown(VkCommandBuffer cmd, VulkanImage source, VulkanImage target)
    {
        var sourceExtent = source.Description.Extent;
        var targetExtent = target.Description.Extent;
        var readers = VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
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
    }
}
