namespace Engine;

/// <summary>A font baked into a texture of glyphs at one size.</summary>
/// <remarks>
/// Drawing it at another size scales the glyphs, which blurs a bilinear atlas and blocks a
/// point-filtered one, unless the font was loaded as <see cref="FontType.Sdf"/>. A font loaded
/// from a file is baked again at a larger size it is drawn at, a quarter or more past its own, so
/// large text stays sharp, and that bake is kept for the next time.
/// </remarks>
public sealed class Font
{
    internal Font(Texture2D texture, float baseSize, float lineHeight, Dictionary<int, Glyph> glyphs, Image atlas = default, FontType type = FontType.Default)
    {
        Type = type;
        Atlas = atlas;
        Texture = texture;
        BaseSize = baseSize;
        LineHeight = lineHeight;
        Glyphs = glyphs;
    }

    /// <summary>The atlas the glyphs are drawn from.</summary>
    public Texture2D Texture { get; }

    // The atlas's pixels, kept for drawing text into an image on the CPU.
    internal Image Atlas { get; }

    /// <summary>The size in pixels the glyphs were baked at.</summary>
    public float BaseSize { get; }

    /// <summary>The distance between lines at <see cref="BaseSize"/>.</summary>
    public float LineHeight { get; }

    /// <summary>The glyphs, by code point.</summary>
    public IReadOnlyDictionary<int, Glyph> Glyphs { get; }

    /// <summary>How the glyphs were baked.</summary>
    /// <remarks>
    /// An <see cref="FontType.Sdf"/> atlas holds in its alpha the distance from each texel to the
    /// glyph's edge, 0.5 on the edge and an eighth more for each pixel of the bake inside it, as
    /// raylib's does, so raylib's <c>sdf.fs</c> reads it unchanged.
    /// </remarks>
    public FontType Type { get; }

    /// <summary>Whether the font has an atlas to draw from.</summary>
    public bool IsValid => Texture.IsValid && Glyphs.Count > 0;

    // Bakes the font's file again at a size, for a font loaded from one, and the bakes made, by size.
    internal Func<int, Font?>? Rebake { get; private set; }

    // The same font, able to bake itself again at larger sizes.
    internal Font WithRebake(Func<int, Font?> rebake)
    {
        Rebake = rebake;
        return this;
    }
    private readonly Dictionary<int, Font> _larger = [];

    // At most this many larger bakes are kept, past which the largest serves bigger text.
    private const int MaxBakes = 8;

    /// <summary>The bakes made at larger sizes, which unloading the font frees with it.</summary>
    internal IEnumerable<Font> Bakes => _larger.Values;

    /// <summary>
    /// The bake text <paramref name="fontSize"/> pixels high is drawn from: this font, or one baked
    /// from its file at the size, rounded up to four pixels, when that is a quarter or more past
    /// this one's.
    /// </summary>
    internal Font ForSize(float fontSize)
    {
        if (Rebake is null || Type == FontType.Sdf || fontSize < BaseSize * 1.25f) return this;
        var size = Math.Min(256, (int)MathF.Ceiling(fontSize / 4) * 4);
        if (_larger.TryGetValue(size, out var known)) return known;
        if (_larger.Count >= MaxBakes) return _larger.Values.MaxBy(f => f.BaseSize)!;
        return Rebake(size) is { IsValid: true } baked ? _larger[size] = baked : this;
    }
}
