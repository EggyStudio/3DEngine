using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    // The formats of the textures the CPU writes, each copied into its image by the CPU itself
    // where the device copies it so and samples it as fast as one it did not (HostCopies).
    private static readonly ImageFormat[] HostCopied =
        [ImageFormat.R8G8B8A8_UNorm, ImageFormat.Bc1_UNorm, ImageFormat.Bc1Alpha_UNorm, ImageFormat.Bc5_UNorm, ImageFormat.Bc7_UNorm];

    // Each format the CPU copies into, with the most usages an image of it may have and still be
    // sampled as fast.
    private readonly Dictionary<VkFormat, VkImageUsageFlags> _hostCopyFormats = [];

    /// <summary>
    /// Whether textures written from the CPU are copied into their images by the CPU, with no
    /// staging buffer, Vulkan 1.4's host image copy, chosen when the device is made.
    /// </summary>
    internal bool CopiesOnHost { get; private set; }

    // The formats the CPU copies into, as the device's line names them.
    private string HostCopiedFormats => string.Join(", ", HostCopied.Where(format => _hostCopyFormats.ContainsKey(ToVkFormat(format))));

    /// <summary>Whether textures go through a staging buffer however the device could copy them, which a test compares the two ways by.</summary>
    internal bool StagesUploads { get; init; }

    // Chooses, before the device is made, whether it copies textures from the CPU and of which
    // formats: those the device copies so, into the layout the model pass samples them in and the
    // one a mip chain is blitted in, and samples as fast with the usage the copy needs as without
    // it, since a driver may lay out or compress such an image otherwise.
    private void ChooseHostCopy(bool offered)
    {
        _hostCopyFormats.Clear();
        CopiesOnHost = false;
        if (!offered || StagesUploads) return;
        var properties = new VkPhysicalDeviceHostImageCopyProperties();
        var whole = new VkPhysicalDeviceProperties2 { pNext = &properties };
        _instanceApi.vkGetPhysicalDeviceProperties2(_physicalDevice, &whole);
        var layouts = stackalloc VkImageLayout[(int)properties.copyDstLayoutCount];
        properties.pCopyDstLayouts = layouts;
        _instanceApi.vkGetPhysicalDeviceProperties2(_physicalDevice, &whole);
        var into = new ReadOnlySpan<VkImageLayout>(layouts, (int)properties.copyDstLayoutCount);
        if (!into.Contains(VkImageLayout.ShaderReadOnlyOptimal) || !into.Contains(VkImageLayout.TransferDstOptimal)) return;
        foreach (var format in HostCopied)
        {
            var vkFormat = ToVkFormat(format);
            var features = new VkFormatProperties3();
            var formatProperties = new VkFormatProperties2 { pNext = &features };
            _instanceApi.vkGetPhysicalDeviceFormatProperties2(_physicalDevice, vkFormat, &formatProperties);
            if ((features.optimalTilingFeatures & VkFormatFeatureFlags2.HostImageTransfer) == 0) continue;
            // A texture of four bytes a pixel may be written by a compute shader as well.
            VkImageUsageFlags[] usages = (features.optimalTilingFeatures & VkFormatFeatureFlags2.StorageImage) != 0
                ? [TextureUsage | VkImageUsageFlags.Storage, TextureUsage] : [TextureUsage];
            foreach (var usage in usages)
            {
                var performance = new VkHostImageCopyDevicePerformanceQuery();
                var imageProperties = new VkImageFormatProperties2 { pNext = &performance };
                var info = new VkPhysicalDeviceImageFormatInfo2
                {
                    format = vkFormat,
                    type = VkImageType.Image2D,
                    tiling = VkImageTiling.Optimal,
                    usage = usage | VkImageUsageFlags.HostTransfer,
                    flags = VkImageCreateFlags.MutableFormat,
                };
                if (_instanceApi.vkGetPhysicalDeviceImageFormatProperties2(_physicalDevice, &info, &imageProperties) != VkResult.Success || !performance.optimalDeviceAccess)
                    continue;
                _hostCopyFormats[vkFormat] = usage;
                break;
            }
        }
        CopiesOnHost = _hostCopyFormats.Count > 0;
    }

    // What the textures the CPU writes are made with, sampled, written and read for their mips.
    private const VkImageUsageFlags TextureUsage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc;

    // Whether an image of these usages and format is made so the CPU can copy into it, a sampled
    // texture written from the CPU whose usages the device copies into as fast.
    private bool HostCopies(ImageDesc desc) =>
        CopiesOnHost && desc.Usage.HasFlag(ImageUsage.Sampled) && desc.Usage.HasFlag(ImageUsage.TransferDst)
        && _hostCopyFormats.TryGetValue(ToVkFormat(desc.Format), out var most) && (ToVkImageUsage(desc.Usage) & ~most) == 0;

    // Moves an image no command has touched, its levels from the first to `levels`, into a layout,
    // on the CPU.
    private void TransitionOnHost(VulkanImage image, uint levels, VkImageLayout layout)
    {
        var transition = new VkHostImageLayoutTransitionInfo
        {
            image = image.Image,
            oldLayout = VkImageLayout.Undefined,
            newLayout = layout,
            subresourceRange = ColorLevels(0, levels),
        };
        _deviceApi.vkTransitionImageLayout(1, &transition).CheckResult();
        image.Layout = layout;
    }

    // Copies the bytes of each level into an image in `layout`, each level's rows packed one after
    // another as they are uploaded, on the CPU. The device sees them from the next submission on.
    private void CopyOnHost(VulkanImage image, VkImageLayout layout, ReadOnlySpan<nint> levels, uint width, uint height)
    {
        var regions = stackalloc VkMemoryToImageCopy[levels.Length];
        for (int i = 0; i < levels.Length; i++)
            regions[i] = new VkMemoryToImageCopy
            {
                pHostPointer = (void*)levels[i],
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, (uint)i, 0, 1),
                imageExtent = new VkExtent3D(Math.Max(1, width >> i), Math.Max(1, height >> i), 1),
            };
        var info = new VkCopyMemoryToImageInfo { dstImage = image.Image, dstImageLayout = layout, regionCount = (uint)levels.Length, pRegions = regions };
        _deviceApi.vkCopyMemoryToImage(&info).CheckResult();
    }
}
