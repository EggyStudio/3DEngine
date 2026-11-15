using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    // Each sampler the device holds by the settings it is made with, the device's limits applied,
    // with how many of the samplers handed out hold it. The device allows only so many at once, its
    // own maxSamplerAllocationCount, and a scene of many textures asks for few distinct settings, so
    // every texture asking for the same shares one.
    private readonly Dictionary<SamplerDesc, (VkSampler Handle, int Holders)> _samplers = [];
    private readonly Lock _samplerGate = new();
    private uint _samplerLimit = uint.MaxValue;
    private bool _samplerLimitLogged;

    /// <summary>How many samplers the device holds, one for each distinct set of settings held.</summary>
    internal int SamplerCount
    {
        get { lock (_samplerGate) return _samplers.Count; }
    }

    /// <summary>
    /// The most samplers the device holds at once, its own maxSamplerAllocationCount unless a test
    /// lowers it to read a device that allows fewer.
    /// </summary>
    internal uint SamplerLimit
    {
        get => _samplerLimit;
        set => _samplerLimit = Math.Max(1, value);
    }

    /// <summary>
    /// Gives a sampler of the settings asked for, the one the device holds for them where it holds
    /// one, so the device makes one sampler for each distinct set of settings.
    /// </summary>
    /// <param name="desc">Sampler creation descriptor.</param>
    /// <returns>A new <see cref="ISampler"/> handle, which lets the device's sampler go when disposed.</returns>
    public ISampler CreateSampler(SamplerDesc desc)
    {
        // The settings as the device makes them, so two that differ only where the device makes no
        // difference share a sampler.
        var made = desc with
        {
            MaxAnisotropy = Math.Clamp(desc.MaxAnisotropy, 1, _maxAnisotropy),
            MipFilter = desc.MipFilter ?? desc.MinFilter,
            AddressU = Made(desc.AddressU),
            AddressV = Made(desc.AddressV),
            AddressW = Made(desc.AddressW),
        };
        lock (_samplerGate)
        {
            if (_samplers.TryGetValue(made, out var held))
            {
                _samplers[made] = (held.Handle, held.Holders + 1);
                return new VulkanSampler(this, desc, made, held.Handle);
            }
            if (_samplers.Count >= _samplerLimit && !_samplerLimitLogged)
            {
                _samplerLimitLogged = true;
                Logger.Error($"The device holds {_samplers.Count} samplers, the most it allows, and a sampler of other settings is asked for ({made}), which is made past the limit.");
            }
            var handle = MakeSampler(made);
            _samplers[made] = (handle, 1);
            return new VulkanSampler(this, desc, made, handle);
        }
    }

    // A mirror clamped at the edge where the device has it, and a clamp at the edge, the nearest of
    // the modes it has, where it has not.
    private SamplerAddressMode Made(SamplerAddressMode mode) =>
        mode == SamplerAddressMode.MirrorClampToEdge && !CanMirrorClamp ? SamplerAddressMode.ClampToEdge : mode;

    private VkSampler MakeSampler(SamplerDesc made)
    {
        VkSamplerCreateInfo info = new()
        {
            magFilter = made.MagFilter == SamplerFilter.Linear ? VkFilter.Linear : VkFilter.Nearest,
            minFilter = made.MinFilter == SamplerFilter.Linear ? VkFilter.Linear : VkFilter.Nearest,
            addressModeU = ToVkAddressMode(made.AddressU),
            addressModeV = ToVkAddressMode(made.AddressV),
            addressModeW = ToVkAddressMode(made.AddressW),
            anisotropyEnable = made.MaxAnisotropy > 1,
            maxAnisotropy = made.MaxAnisotropy,
            borderColor = VkBorderColor.IntOpaqueBlack,
            unnormalizedCoordinates = false,
            compareEnable = false,
            compareOp = VkCompareOp.Always,
            // Blends between mip levels when the texture is smoothed and picks the nearest level
            // when it is not, unless the description says which, and lets an image with mip levels
            // use all of them. An image without them has one level, which the bound leaves alone.
            mipmapMode = made.MipFilter == SamplerFilter.Linear ? VkSamplerMipmapMode.Linear : VkSamplerMipmapMode.Nearest,
            minLod = 0,
            maxLod = Vulkan.VK_LOD_CLAMP_NONE,
        };
        _deviceApi.vkCreateSampler(&info, null, out VkSampler sampler).CheckResult();
        return sampler;
    }

    // One holder of the sampler of these settings let go, the sampler destroyed with the last.
    private void LetGo(SamplerDesc made)
    {
        lock (_samplerGate)
        {
            if (!_samplers.TryGetValue(made, out var held)) return;
            if (held.Holders > 1)
            {
                _samplers[made] = (held.Handle, held.Holders - 1);
                return;
            }
            _samplers.Remove(made);
            _deviceApi.vkDestroySampler(held.Handle);
        }
    }

    /// <summary>Maps an engine <see cref="SamplerAddressMode"/> to the Vulkan equivalent.</summary>
    private static VkSamplerAddressMode ToVkAddressMode(SamplerAddressMode mode) => mode switch
    {
        SamplerAddressMode.ClampToEdge => VkSamplerAddressMode.ClampToEdge,
        SamplerAddressMode.MirrorRepeat => VkSamplerAddressMode.MirroredRepeat,
        SamplerAddressMode.MirrorClampToEdge => VkSamplerAddressMode.MirrorClampToEdge,
        SamplerAddressMode.Repeat => VkSamplerAddressMode.Repeat,
        _ => VkSamplerAddressMode.ClampToEdge
    };

    /// <summary>A sampler handed out, holding the device's sampler of its settings.</summary>
    /// <seealso cref="ISampler"/>
    internal sealed class VulkanSampler : ISampler
    {
        private readonly GraphicsDevice _device;
        private readonly SamplerDesc _made;

        /// <inheritdoc />
        public SamplerDesc Description { get; }

        /// <summary>The underlying Vulkan sampler handle, which other samplers of the same settings share.</summary>
        internal VkSampler Sampler;

        /// <summary>Creates a sampler handed out.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="desc">The sampler creation descriptor as asked.</param>
        /// <param name="made">The settings the device's sampler is made with.</param>
        /// <param name="sampler">The Vulkan sampler handle.</param>
        public VulkanSampler(GraphicsDevice device, SamplerDesc desc, SamplerDesc made, VkSampler sampler)
        {
            _device = device;
            _made = made;
            Description = desc;
            Sampler = sampler;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Sampler.Handle == 0) return;
            Sampler = default;
            _device.LetGo(_made);
        }
    }
}
