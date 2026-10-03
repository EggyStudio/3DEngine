namespace Engine;

/// <summary>Attachment load operation when a render pass begins.</summary>
public enum LoadOp
{
    /// <summary>Clear the attachment to a specified value.</summary>
    Clear,
    /// <summary>Preserve existing contents.</summary>
    Load,
    /// <summary>Contents are undefined (don't care).</summary>
    DontCare
}

/// <summary>Attachment store operation when a render pass ends.</summary>
public enum StoreOp
{
    /// <summary>Store the attachment contents for later use.</summary>
    Store,
    /// <summary>Contents are not needed after the render pass (don't care).</summary>
    DontCare
}

/// <summary>Descriptor for creating an offscreen render pass with a single color attachment.</summary>
/// <param name="ColorFormat">Pixel format of the color attachment.</param>
/// <param name="ClearOnLoad">Whether to clear the attachment when the render pass begins.</param>
public readonly record struct RenderPassDesc(ImageFormat ColorFormat, bool ClearOnLoad);

/// <summary>Descriptor for creating a framebuffer from a render pass and a color image view.</summary>
/// <param name="RenderPass">The render pass this framebuffer is compatible with.</param>
/// <param name="ColorAttachment">The image view used as the color attachment.</param>
/// <param name="Extent">Framebuffer dimensions in pixels.</param>
public readonly record struct FramebufferDesc(IRenderPass RenderPass, IImageView ColorAttachment, Extent2D Extent);
