namespace Engine;

/// <summary>Pixel format for images and render targets.</summary>
public enum ImageFormat
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
    D32_Float
}

/// <summary>Flags describing how a GPU image will be used.</summary>
[Flags]
public enum ImageUsage
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
    TransferDst   = 1 << 4
}

/// <summary>Descriptor for creating a GPU image.</summary>
/// <param name="Extent">Image dimensions in pixels.</param>
/// <param name="Format">Pixel format.</param>
/// <param name="Usage">Usage flags.</param>
public readonly record struct ImageDesc(Extent2D Extent, ImageFormat Format, ImageUsage Usage);

/// <summary>Abstract image layout states used for pipeline barrier transitions.</summary>
public enum ImageLayout
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
