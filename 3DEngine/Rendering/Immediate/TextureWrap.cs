namespace Engine;

/// <summary>What a texture shows past its edges, where a texture coordinate leaves 0 to 1.</summary>
/// <remarks>
/// raylib's mirror clamp is left out, since Vulkan has it only where the device turns on a feature
/// for it.
/// </remarks>
public enum TextureWrap
{
    /// <summary>The texture again, tiled, as a floor of repeated tiles is drawn.</summary>
    Repeat,
    /// <summary>The edge pixel stretched on, so a sprite's edge does not take color from its far side.</summary>
    Clamp,
    /// <summary>The texture again, mirrored across each edge.</summary>
    MirrorRepeat,
}
