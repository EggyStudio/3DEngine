namespace Engine;

/// <summary>How a font's glyphs are baked, as raylib's <c>FontType</c>.</summary>
public enum FontType
{
    /// <summary>Glyphs as coverage, smoothed at their edges.</summary>
    Default,

    /// <summary>Glyphs as coverage, which this engine bakes the same as <see cref="Default"/>.</summary>
    Bitmap,

    /// <summary>
    /// Glyphs as signed distance fields, which stay sharp drawn far larger than their bake.
    /// </summary>
    Sdf,
}
