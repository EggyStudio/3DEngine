namespace Engine;

/// <summary>Lightweight description of a render texture target.</summary>
/// <seealso cref="RenderTextures"/>
public readonly struct RenderTextureDesc
{
    /// <summary>Texture width in pixels.</summary>
    public readonly int Width;

    /// <summary>Texture height in pixels.</summary>
    public readonly int Height;

    /// <summary>Creates a new <see cref="RenderTextureDesc"/> with the given dimensions.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public RenderTextureDesc(int width, int height)
    { Width = width; Height = height; }
}
