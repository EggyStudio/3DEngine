using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// The window's surfaces lit by the world's probes as the RadianceCascades module gathers a surface
/// point, a texel a pixel, which <c>gi_surface.slang</c> writes and the model pass reads in place
/// of the probes' faces where <c>gi.surface</c> asks, and the view it was gathered through, as the
/// screen's probes' own (<see cref="GpuScreenProbes.View"/>).
/// </summary>
internal sealed unsafe class GpuSurfaceGather : IDisposable
{
    private GpuSurfaceGather(uint width, uint height, IImage light, IImageView lightView, IBuffer view, ISampler sampler, IImage? causes,
        IImageView? causesView) =>
        (Width, Height, Light, LightView, View, Sampler, Causes, CausesView) = (width, height, light, lightView, view, sampler, causes, causesView);

    /// <summary>The pixels across.</summary>
    public uint Width { get; }

    /// <summary>The pixels down.</summary>
    public uint Height { get; }

    /// <summary>
    /// The light each pixel's point takes over pi in rgb, -1 where the depth holds no surface or no
    /// level of probes stands about the point, and the share of the sky in it in alpha.
    /// </summary>
    public IImage Light { get; }

    /// <summary>The view of <see cref="Light"/>.</summary>
    public IImageView LightView { get; }

    /// <summary>
    /// What each pixel's gather from the split cascades is read by, four half floats a pixel kept
    /// in the general layout, as <c>gi_split.slang</c> writes it where <c>gi.split_cause</c> asks,
    /// or none.
    /// </summary>
    public IImage? Causes { get; }

    /// <summary>The view of <see cref="Causes"/>, or none.</summary>
    public IImageView? CausesView { get; }

    /// <summary>
    /// The view the light was gathered through, <see cref="GpuScreenProbes.ViewBytes"/> long.
    /// </summary>
    public IBuffer View { get; }

    /// <summary>A sampler that reads a texel as it is.</summary>
    public ISampler Sampler { get; }

    /// <summary>
    /// Makes the gather of a window <paramref name="width"/> by <paramref name="height"/> pixels,
    /// every texel -1 until the first frame gathers it, so the model pass reads the probes' faces
    /// there, and where <paramref name="causes"/> is set the image its causes are read into.
    /// </summary>
    public static GpuSurfaceGather Create(GraphicsDevice device, uint width, uint height, bool causes = false)
    {
        // Copied out too, where a test reads what each pixel gathered.
        var light = device.CreateImage(new ImageDesc(new Extent2D(width, height), ImageFormat.R16G16B16A16_Float,
            ImageUsage.Sampled | ImageUsage.Storage | ImageUsage.TransferDst | ImageUsage.TransferSrc));
        device.Name(light, "Surfaces' light as the module gathers it");
        var lightView = device.CreateImageView(light);
        var view = device.CreateBuffer(new BufferDesc(GpuScreenProbes.ViewBytes, BufferUsage.Uniform | BufferUsage.TransferDst));
        var sampler = device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        var cmd = device.BeginSingleTimeCommands();
        var image = ((VulkanImage)light).Image;
        var whole = ColorLevels(0, 1);
        device.PipelineBarrier(cmd, [ImageBarrier(image, whole, VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite)]);
        var none = new VkClearColorValue(-1f, -1f, -1f, -1f);
        device.Api.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        device.PipelineBarrier(cmd, [ImageBarrier(image, whole, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead)]);
        IImage? cause = null;
        if (causes)
        {
            cause = device.CreateImage(new ImageDesc(new Extent2D(width, height), ImageFormat.R16G16B16A16_Float, ImageUsage.Storage | ImageUsage.TransferSrc));
            device.Name(cause, "Surfaces' gather read by its causes");
            device.PipelineBarrier(cmd, [ImageBarrier(((VulkanImage)cause).Image, whole, VkImageLayout.Undefined, VkImageLayout.General,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite)]);
        }
        device.EndSingleTimeCommands(cmd);
        ((VulkanImage)light).Layout = VkImageLayout.ShaderReadOnlyOptimal;
        if (cause is not null) ((VulkanImage)cause).Layout = VkImageLayout.General;
        return new GpuSurfaceGather(width, height, light, lightView, view, sampler, cause, cause is null ? null : device.CreateImageView(cause));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Sampler.Dispose();
        View.Dispose();
        LightView.Dispose();
        Light.Dispose();
        CausesView?.Dispose();
        Causes?.Dispose();
    }
}
