namespace Engine;

/// <summary>What a texture shows past its edges, where a texture coordinate leaves 0 to 1.</summary>
/// <remarks>
/// <see cref="MirrorClamp"/> needs a feature of Vulkan 1.2 that every desktop driver in use and
/// lavapipe have, and a device without it clamps there.
/// </remarks>
public enum TextureWrap
{
    /// <summary>The texture again, tiled, as a floor of repeated tiles is drawn.</summary>
    Repeat,
    /// <summary>The edge pixel stretched on, so a sprite's edge does not take color from its far side.</summary>
    Clamp,
    /// <summary>The texture again, mirrored across each edge.</summary>
    MirrorRepeat,
    /// <summary>The texture mirrored once across its edge at 0, then its edge pixel stretched on, as raylib's <c>TEXTURE_WRAP_MIRROR_CLAMP</c>.</summary>
    MirrorClamp,
}
