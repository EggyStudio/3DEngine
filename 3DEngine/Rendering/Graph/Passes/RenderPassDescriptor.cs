namespace Engine;

/// <summary>Descriptor for beginning a tracked render pass with per-attachment load/store ops.</summary>
/// <param name="RenderPass">The render pass to begin.</param>
/// <param name="Framebuffer">The framebuffer to render into.</param>
/// <param name="Extent">The render area dimensions.</param>
/// <param name="ColorLoadOp">Load operation for the color attachment (Clear, Load, or DontCare).</param>
/// <param name="ColorStoreOp">Store operation for the color attachment (Store or DontCare).</param>
/// <param name="ClearColor">Clear color when <paramref name="ColorLoadOp"/> is <see cref="LoadOp.Clear"/>.</param>
public readonly record struct RenderPassDescriptor(
    IRenderPass RenderPass,
    IFramebuffer Framebuffer,
    Extent2D Extent,
    LoadOp ColorLoadOp = LoadOp.Clear,
    StoreOp ColorStoreOp = StoreOp.Store,
    ClearColor? ClearColor = null);
