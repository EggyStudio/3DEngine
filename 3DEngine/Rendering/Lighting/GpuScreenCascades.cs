using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// The light the levels of the screen's cascades past the first hold along each of their probes'
/// ways, which <c>gi_screen_cascades.slang</c> writes level by level, the last first, and reads
/// back as it merges each level into the one before, where <c>gi.screen_cascades</c> asks.
/// </summary>
internal sealed unsafe class GpuScreenCascades : IDisposable
{
    /// <summary>
    /// The levels, the first the screen's probes, the last reaching the scene's end.
    /// </summary>
    public const int Levels = 5;

    /// <summary>
    /// The first level's octahedron's texels along each side, twice as many each level.
    /// </summary>
    public const int FirstTexels = 4;

    private GpuScreenCascades(uint width, uint height, int tile, IImage light, IImageView lightView) =>
        (Width, Height, Tile, Light, LightView) = (width, height, tile, light, lightView);

    /// <summary>The window's pixels across.</summary>
    public uint Width { get; }

    /// <summary>The window's pixels down.</summary>
    public uint Height { get; }

    /// <summary>The first level's tile, the screen's probes'.</summary>
    public int Tile { get; }

    /// <summary>
    /// The light along each way of each probe of the levels past the first, merged with the levels
    /// past it, each level's probes below the level before's, each probe a square of its ways.
    /// </summary>
    public IImage Light { get; }

    /// <summary>The view of <see cref="Light"/>.</summary>
    public IImageView LightView { get; }

    /// <summary>
    /// Makes the levels of a window <paramref name="width"/> by <paramref name="height"/> pixels
    /// whose first level's probes stand a <paramref name="tile"/> apart.
    /// </summary>
    public static GpuScreenCascades Create(GraphicsDevice device, uint width, uint height, int tile)
    {
        uint across = 0, down = 0;
        for (int level = 1; level < Levels; level++)
        {
            var (side, texels) = ((uint)(tile << level), (uint)(FirstTexels << level));
            across = Math.Max(across, (width + side - 1) / side * texels);
            down += (height + side - 1) / side * texels;
        }
        var light = device.CreateImage(new ImageDesc(new Extent2D(across, down), ImageFormat.R16G16B16A16_Float, ImageUsage.Storage | ImageUsage.TransferDst));
        device.Name(light, "The screen's cascades' levels");
        var lightView = device.CreateImageView(light);
        var cmd = device.BeginSingleTimeCommands();
        var image = ((VulkanImage)light).Image;
        var whole = ColorLevels(0, 1);
        device.PipelineBarrier(cmd, [ImageBarrier(image, whole, VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite)]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        device.Api.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        device.PipelineBarrier(cmd, [ImageBarrier(image, whole, VkImageLayout.General, VkImageLayout.General,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite)]);
        device.EndSingleTimeCommands(cmd);
        ((VulkanImage)light).Layout = VkImageLayout.General;
        return new GpuScreenCascades(width, height, tile, light, lightView);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        LightView.Dispose();
        Light.Dispose();
    }
}
