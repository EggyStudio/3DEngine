namespace Engine;

/// <summary>Pixel format for images and render targets.</summary>
internal enum ImageFormat
{
    /// <summary>No defined format.</summary>
    Undefined,
    /// <summary>8-bit unsigned normalized RGBA (sRGB-compatible).</summary>
    R8G8B8A8_UNorm,
    /// <summary>8-bit unsigned normalized BGRA (common swapchain format).</summary>
    B8G8R8A8_UNorm,
    /// <summary>24-bit depth + 8-bit stencil.</summary>
    D24_UNorm_S8_UInt,
    /// <summary>32-bit floating-point depth (no stencil).</summary>
    D32_Float,
    /// <summary>
    /// 8-bit RGBA whose color the sampler decodes from sRGB to linear, for a view of an
    /// <see cref="R8G8B8A8_UNorm"/> image that a shader lights in linear space.
    /// </summary>
    R8G8B8A8_Srgb,
    /// <summary>8-bit BGRA decoded from sRGB to linear when sampled, for a view of a <see cref="B8G8R8A8_UNorm"/> image.</summary>
    B8G8R8A8_Srgb,
    /// <summary>16-bit floating-point RGBA, which holds linear light past 1.</summary>
    R16G16B16A16_Float,
}

/// <summary>Flags describing how a GPU image will be used.</summary>
[Flags]
internal enum ImageUsage
{
    /// <summary>No usage flags set.</summary>
    None          = 0,
    /// <summary>Image can be used as a color attachment in a render pass.</summary>
    ColorAttachment      = 1 << 0,
    /// <summary>Image can be used as a depth/stencil attachment in a render pass.</summary>
    DepthStencilAttachment = 1 << 1,
    /// <summary>Image can be sampled in a shader.</summary>
    Sampled       = 1 << 2,
    /// <summary>Image can be used as a transfer source.</summary>
    TransferSrc   = 1 << 3,
    /// <summary>Image can be used as a transfer destination.</summary>
    TransferDst   = 1 << 4,
    /// <summary>Image can be written by a compute shader, as a <c>RWTexture2D</c>.</summary>
    Storage       = 1 << 5
}

/// <summary>Descriptor for creating a GPU image.</summary>
/// <param name="Extent">Image dimensions in pixels.</param>
/// <param name="Format">Pixel format.</param>
/// <param name="Usage">Usage flags.</param>
/// <param name="MipLevels">How many mip levels the image has, each half the size of the one before. One for none.</param>
internal readonly record struct ImageDesc(Extent2D Extent, ImageFormat Format, ImageUsage Usage, uint MipLevels = 1)
{
    /// <summary>The levels down to one pixel for an image of the given size, the full mip chain.</summary>
    public static uint FullMipChain(uint width, uint height) => (uint)System.Numerics.BitOperations.Log2(Math.Max(1, Math.Max(width, height))) + 1;
}

/// <summary>Abstract image layout states used for pipeline barrier transitions.</summary>
internal enum ImageLayout
{
    /// <summary>Undefined / don't-care initial layout.</summary>
    Undefined,
    /// <summary>Optimal layout for use as a color attachment (render target).</summary>
    ColorAttachmentOptimal,
    /// <summary>Optimal layout for sampling from a shader.</summary>
    ShaderReadOnlyOptimal,
    /// <summary>Optimal layout for use as a transfer destination.</summary>
    TransferDstOptimal,
}
