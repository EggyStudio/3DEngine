namespace Engine;

// Text shaped by a font's own substitutions, where a color font joins a sequence of emoji into the
// one picture it stands for, and the glyphs that joining can make, baked with the font.
public static partial class Engine3D
{
    /// <summary>The key a glyph a sequence of characters is joined into is kept under in a font's glyphs, past U+10FFFF.</summary>
    internal static int JoinedKey(int glyph) => 0x110000 + glyph;

    // The glyphs other than the characters' own that a font's substitutions can make of the
    // characters asked for, none where it makes none or its table cannot be read.
    private static int[] JoinedGlyphs(TrueTypeFont font, int[] asked)
    {
        if (font.Joins is not { } joins) return [];
        var glyphs = asked.Select(font.GlyphIndex).Where(g => g != 0).ToHashSet();
        try
        {
            return [.. joins.Reachable(glyphs).Where(g => g != 0 && !glyphs.Contains(g)).Order()];
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    /// <summary>
    /// The keys of the glyphs text is drawn with in a font, a character's code point each, and
    /// where the font joins sequences, each run of the characters its reader draws and the marks
    /// between them shaped by the font's substitutions, so a sequence the font joins is one glyph.
    /// </summary>
    /// <remarks>
    /// A run is made of the characters the reader draws, the joiner (U+200D), the variation
    /// selectors (U+FE0E and U+FE0F), the keycap (U+20E3) and the tags (U+E0020 to U+E007F), and a
    /// character those selectors or the keycap follow, as the digit of a keycap does.
    /// </remarks>
    internal static IEnumerable<int> TextKeys(Font font, string text) =>
        font.Joining is { } joining ? font.ShapedText(text, t => ShapeText(font, joining, t)) : Runes(text);

    private static IEnumerable<int> Runes(string text)
    {
        foreach (var rune in text.EnumerateRunes()) yield return rune.Value;
    }

    // Text as the keys of its glyphs, each run of the characters the reader draws shaped.
    private static int[] ShapeText(Font font, (TrueTypeFont Reader, HashSet<int> Drawn) joining, string text)
    {
        var keys = new List<int>(text.Length);
        var characters = LoadCodepoints(text);
        bool Joins(int at) =>
            joining.Drawn.Contains(characters[at]) || characters[at] is 0x200D or 0xFE0E or 0xFE0F or 0x20E3 or (>= 0xE0020 and <= 0xE007F)
            || at + 1 < characters.Length && characters[at + 1] is 0xFE0E or 0xFE0F or 0x20E3;
        for (int i = 0; i < characters.Length;)
        {
            var end = i;
            while (end < characters.Length && Joins(end)) end++;
            if (end - i < 2)
            {
                keys.Add(characters[i]);
                i = Math.Max(i + 1, end);
                continue;
            }
            keys.AddRange(Shape(font, joining.Reader, characters[i..end]));
            i = end;
        }
        return [.. keys];
    }

    // A run of characters shaped by the font's substitutions, each glyph that comes out given back as
    // the character it is the glyph of, or as the key of a joined glyph the font holds, or as '?'.
    // A default ignorable character nothing substituted is left out, as HarfBuzz hides one, so a
    // U+FE0F a sequence does not join is not drawn as the font's '?'.
    private static List<int> Shape(Font font, TrueTypeFont reader, int[] run)
    {
        var glyphs = run.Select(reader.GlyphIndex).ToArray();
        List<int> shaped;
        try
        {
            shaped = reader.Joins!.Apply(glyphs.Select((g, i) => g | (DefaultIgnorable(run[i]) ? GlyphSubstitution.Ignorable : 0)));
        }
        catch (ArgumentOutOfRangeException)
        {
            return [.. run];
        }
        var characterOf = new Dictionary<int, int>();
        for (int i = 0; i < run.Length; i++)
            if (glyphs[i] != 0) characterOf.TryAdd(glyphs[i], run[i]);
        // Characters the font lacks keep their glyph 0, which no substitution covers, in order.
        var lacking = new Queue<int>(run.Where((c, i) => glyphs[i] == 0 && !DefaultIgnorable(c)));
        var keys = new List<int>(shaped.Count);
        foreach (var entry in shaped)
        {
            var glyph = entry & 0xFFFF;
            if ((entry & GlyphSubstitution.Ignorable) != 0) continue;
            if (glyph == 0) keys.Add(lacking.TryDequeue(out var c) ? c : '?');
            else if (characterOf.TryGetValue(glyph, out var character)) keys.Add(character);
            else keys.Add(font.Glyphs.ContainsKey(JoinedKey(glyph)) ? JoinedKey(glyph) : '?');
        }
        return keys;
    }

    // The characters of a run that select or join rather than draw, the joiners, the variation
    // selectors and the tags, which Unicode marks as default ignorable.
    private static bool DefaultIgnorable(int c) => c is 0x200C or 0x200D or (>= 0xFE00 and <= 0xFE0F) or (>= 0xE0000 and <= 0xE0FFF);
}
