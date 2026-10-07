namespace Engine;

/// <summary>A font baked into a texture of glyphs at one size.</summary>
/// <remarks>
/// Drawing it at another size scales the glyphs, which blurs a bilinear atlas and blocks a
/// point-filtered one, unless the font was loaded as <see cref="FontType.Sdf"/>. A font loaded
/// from a file is baked again at a larger size it is drawn at, a quarter or more past its own, so
/// large text stays sharp, and that bake is kept for the next time. Its glyphs are drawn in this
/// bake's boxes and advances scaled, and a font loaded from a file has its advances cut to whole
/// pixels and its glyphs a pixel higher than the atlas builder puts them, as raylib lays its text
/// out, so a line lies where raylib's does.
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
    /// <remarks>
    /// A glyph a sequence of characters is joined into, as an emoji font joins a family or a flag, is
    /// kept under a key past U+10FFFF, the last code point, which no character has.
    /// </remarks>
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

    // The font's reader and the characters it draws itself, where the font joins sequences of them
    // into glyphs of their own, as an emoji font joins a family or a flag, which text is shaped by
    // before it is drawn.
    internal (TrueTypeFont Reader, HashSet<int> Drawn)? Joining { get; private set; }

    // Text shaped by the font's substitutions and positions, by the string, since a program draws
    // the same text each frame. A few hundred are kept, and all let go past that.
    private readonly Dictionary<string, PlacedKey[]> _shaped = [];

    internal PlacedKey[] ShapedText(string text, Func<string, PlacedKey[]> shape)
    {
        if (_shaped.TryGetValue(text, out var keys)) return keys;
        if (_shaped.Count >= 256) _shaped.Clear();
        return _shaped[text] = shape(text);
    }

    // The same font, shaping its text by the reader's substitutions.
    internal Font WithJoining((TrueTypeFont Reader, HashSet<int> Drawn)? joining)
    {
        Joining = joining;
        return this;
    }

    /// <summary>
    /// How a font shapes Arabic: by its reader's substitutions under the <c>arab</c> script, the
    /// forms they make past the letters' own baked with it, and its positions where
    /// <see cref="Positions"/> is set, or, where <see cref="Plan"/> is null, by the presentation
    /// forms Unicode encodes, which it maps and was baked with.
    /// </summary>
    internal sealed record ArabicShaping(TrueTypeFont? Reader, GlyphLayout.Plan? Plan, GlyphLayout.Plan? Positions = null);

    // How the font shapes Arabic, null where none was asked for or it can shape none.
    internal ArabicShaping? Arabic { get; private set; }

    // The same font, shaping Arabic as given.
    internal Font WithArabic(ArabicShaping? arabic)
    {
        Arabic = arabic;
        return this;
    }

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
        if (Rebake(size) is not { IsValid: true } baked) return this;

        // Each glyph drawn from the larger bake's pixels into this font's box and advance scaled to
        // the bake's size, so text lies where it does drawn from this one, as raylib's, which
        // scales its one bake, does.
        var scale = size / BaseSize;
        var glyphs = (Dictionary<int, Glyph>)baked.Glyphs;
        foreach (var (codepoint, glyph) in glyphs.ToArray())
            if (Glyphs.TryGetValue(codepoint, out var own))
                glyphs[codepoint] = glyph with { X0 = own.X0 * scale, Y0 = own.Y0 * scale, X1 = own.X1 * scale, Y1 = own.Y1 * scale, Advance = own.Advance * scale };
        return _larger[size] = baked;
    }

    /// <summary>
    /// The same font with each glyph's advance cut to whole pixels, as raylib cuts those of a font
    /// it loads from a file, so a line of text is as long as raylib's, and each glyph a pixel
    /// higher, since the atlas builder puts the baseline at the ascent plus one rounded down where
    /// raylib puts it at the ascent cut to whole pixels.
    /// </summary>
    internal Font WithWholeAdvances()
    {
        var glyphs = (Dictionary<int, Glyph>)Glyphs;
        foreach (var (codepoint, glyph) in glyphs.ToArray())
            glyphs[codepoint] = glyph with { Advance = MathF.Floor(glyph.Advance), Y0 = glyph.Y0 - 1, Y1 = glyph.Y1 - 1 };
        return this;
    }
}
