using System.Numerics;

namespace Engine;

// Text shaped by a font's own substitutions, where a color font joins a sequence of emoji into the
// one picture it stands for, and the glyphs that joining can make, baked with the font.
public static partial class E3D
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

    // The glyphs other than the letters' own that a font's substitutions for Arabic can make of the
    // characters asked for, the forms a letter takes by the letters beside it and their ligatures.
    private static int[] FormedGlyphs(TrueTypeFont font, int[] asked, GlyphSubstitution.Plan plan)
    {
        var glyphs = asked.Select(font.GlyphIndex).Where(g => g != 0).ToHashSet();
        try
        {
            return [.. font.Substitutions!.Reachable(glyphs, plan).Where(g => g != 0 && !glyphs.Contains(g)).Order()];
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    // The plan of a font's features for Arabic, in HarfBuzz's stages, composing and the local forms
    // at every glyph, the isolated, final, medial and initial forms each at the letters in that
    // position, the required ligatures, the contextual alternates, then the marks' positional forms
    // and the ligatures a text font makes, or null where the font has none of the four forms.
    private static GlyphSubstitution.Plan? ArabicPlan(GlyphSubstitution table) =>
        table.PlanFor("arab", ("isol", 1), ("fina", 2), ("medi", 4), ("init", 8)) is null ? null
            : table.PlanInStages("arab", [("ccmp", 0), ("locl", 0)], [("isol", (byte)ArabicJoining.Form.Isolated)], [("fina", (byte)ArabicJoining.Form.Final)],
                [("medi", (byte)ArabicJoining.Form.Medial)], [("init", (byte)ArabicJoining.Form.Initial)], [("rlig", 0)], [("rclt", 0), ("calt", 0)],
                [("mset", 0), ("liga", 0), ("clig", 0)]);

    /// <summary>
    /// How a font loaded from a file shapes a line read left to right, its standard ligatures by
    /// the <c>liga</c> feature of its GSUB table under the latn script, where it has outlines to
    /// draw them from, and its pairs kerned by the pair adjustments of the <c>kern</c> feature of
    /// its GPOS table, or by its kern table where its GPOS kerns nothing, each read once for the
    /// glyphs of the characters asked for and the ligatures they make, or null where it does
    /// neither.
    /// </summary>
    /// <remarks>
    /// A ligature or a pair chosen by its context, which a Latin font's <c>liga</c> and <c>kern</c>
    /// rarely make, is not read, so a line is shaped by a lookup at each glyph and each pair, a few
    /// microseconds where the font's own engine took a few hundred.
    /// </remarks>
    internal static Font.LatinShaping? LatinPlan(TrueTypeFont? reader, IEnumerable<int> asked)
    {
        if (reader is null) return null;
        var characters = asked.Distinct().ToArray();
        var glyphOf = characters.ToDictionary(c => c, reader.GlyphIndex);
        var glyphs = glyphOf.Values.Where(g => g != 0).ToHashSet();
        List<Dictionary<int, (int[] Then, int Ligature)[]>> joins = [];
        if (reader.HasOutlines && reader.Substitutions is { } gsub && gsub.PlanFor("latn", ("liga", 0)) is { } liga)
        {
            try
            {
                joins = gsub.Ligatures(liga, gsub.Reachable(glyphs, liga));
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
        // Each ligature drawn as a character asked for where one maps to it, and otherwise as the
        // glyph baked under its key.
        var characterOf = new Dictionary<int, int>();
        foreach (var (c, g) in glyphOf)
            if (g != 0) characterOf.TryAdd(g, c);
        var ligatureKeys = joins.SelectMany(rules => rules.Values.SelectMany(listed => listed.Select(rule => rule.Ligature))).Distinct()
            .ToDictionary(g => g, g => characterOf.TryGetValue(g, out var c) ? c : JoinedKey(g));
        var pairs = reader.Positions is { } gpos && gpos.PlanFor("latn", ("kern", 0)) is { } kern
            ? gpos.PairAdvances(kern, [.. glyphs.Concat(ligatureKeys.Keys)])
            : [];
        var byTable = pairs.Count == 0 && reader.Kerns;
        return joins.Count == 0 && pairs.Count == 0 && !byTable ? null : new(reader, glyphOf, joins, ligatureKeys, pairs, byTable);
    }

    // The ligatures a font makes that are drawn as glyphs of their own, baked with it under their
    // keys.
    private static int[] LigatureGlyphs(Font.LatinShaping? latin) =>
        latin is null ? [] : [.. latin.LigatureKeys.Where(entry => entry.Value > 0x10FFFF).Select(entry => entry.Key).Order()];

    // The plan of a font's positions for Arabic, the marks above and below put on their letters and
    // on each other, the distances and the kerning between letters, and each letter joined to the
    // next by its exit and that one's entry, all at every glyph.
    private static GlyphLayout.Plan? ArabicPositions(GlyphPositioning table) =>
        table.PlanFor("arab", ("abvm", 0), ("blwm", 0), ("curs", 0), ("dist", 0), ("kern", 0), ("mark", 0), ("mkmk", 0));

    /// <summary>
    /// The keys of the glyphs text is drawn with in a font, in the order they are drawn from left to
    /// right, a character's code point each, and where the font joins sequences, each run of the
    /// characters its reader draws and the marks between them shaped by the font's substitutions,
    /// so a sequence the font joins is one glyph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A line with a character read right to left, Hebrew or Arabic, is shaped in the order it is
    /// stored, a run of Arabic by its letters' forms where the font shapes Arabic and its marks and
    /// pairs placed where the font positions them (<see cref="PlacedKeys"/>), then put in the order
    /// it is shown (<see cref="TextDirection"/>), and kept with the font as shaped text is, and a
    /// line of none is drawn in the order it is stored, as raylib draws it.
    /// </para>
    /// <para>
    /// A run is made of the characters the reader draws, the joiner (U+200D), the variation
    /// selectors (U+FE0E and U+FE0F), the keycap (U+20E3) and the tags (U+E0020 to U+E007F), and a
    /// character those selectors or the keycap follow, as the digit of a keycap does.
    /// </para>
    /// </remarks>
    internal static IEnumerable<int> TextKeys(Font font, string text) => PlacedKeys(font, text).Select(placed => placed.Key);

    /// <summary>
    /// The keys <see cref="TextKeys"/> gives, each with where shaping put its glyph, which only the
    /// positions a font gives a run of Arabic move from where its pen and its own advance put it.
    /// </summary>
    internal static IEnumerable<PlacedKey> PlacedKeys(Font font, string text)
    {
        text = Composed(font, text);
        if (TextDirection.HasRightToLeft(text))
            return font.ShapedText(text, t => [.. t.Split('\n').SelectMany((line, i) => i == 0 ? ShapeLine(font, line) : ShapeLine(font, line).Prepend(new PlacedKey('\n')))]);
        if (font.Joining is { } joining) return font.ShapedText(text, t => [.. ShapeText(font, joining, t).Select(key => new PlacedKey(key))]);
        if (font.Latin is { } latin) return font.ShapedText(text, t => ShapeLatin(font, latin, t));
        return Runes(text);
    }

    // Text with each character and the mark right after it composed into the one character Unicode
    // has for both where the font has that character, as HarfBuzz composes them, so e and a
    // combining acute are drawn as the font's é and not as e and the font's '?', and a mark is
    // composed with what the marks before it made of their letter. Text with no character from
    // U+0300 on, where the marks begin, is given back as it is, and text with nothing to compose is
    // read once and given back, so text drawn every frame is copied only where it changes.
    internal static string Composed(Font font, string text)
    {
        if (text.AsSpan().IndexOfAnyInRange('\u0300', '\uFFFF') < 0) return text;
        bool Composes(int before, int mark, out int both)
        {
            both = mark >= 0x300 && UnicodeCompositions.Of(before, mark) is { } made ? made : 0;
            return both != 0 && font.Glyphs.ContainsKey(both);
        }

        var previous = -1;
        var any = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (previous >= 0 && Composes(previous, rune.Value, out _))
            {
                any = true;
                break;
            }
            previous = rune.Value;
        }
        if (!any) return text;

        var composed = new List<int>(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (composed.Count > 0 && Composes(composed[^1], rune.Value, out var both)) composed[^1] = both;
            else composed.Add(rune.Value);
        }
        return string.Concat(composed.Select(char.ConvertFromUtf32));
    }

    private static IEnumerable<PlacedKey> Runes(string text)
    {
        foreach (var rune in text.EnumerateRunes()) yield return new PlacedKey(rune.Value);
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

    /// <summary>
    /// Text with nothing read right to left shaped a line at a time by a font's standard
    /// ligatures, where they are on, each glyph drawn as the character it is the glyph of and a
    /// ligature by its key, and each moving the pen on by its own advance and what kerning adds to
    /// it before the glyph that follows.
    /// </summary>
    /// <remarks>
    /// A ligature is made where its glyphs follow one another with nothing between them, so a
    /// zero width non-joiner keeps two letters apart, and one whose glyph was not baked is not
    /// made. A default ignorable character is left out, as HarfBuzz hides one. Text drawn each
    /// frame is shaped once a string, so the work is kept to a lookup at each glyph and each pair.
    /// </remarks>
    private static PlacedKey[] ShapeLatin(Font font, Font.LatinShaping latin, string text)
    {
        var reader = latin.Reader;
        // Each glyph, the character it was, the first of a ligature's, and whether it is a
        // ligature, a character's glyph read from those of the characters the font was baked with.
        var run = new List<(int Glyph, int Character, bool Joined)>(text.Length);
        foreach (var rune in text.EnumerateRunes())
            run.Add((latin.GlyphOf.TryGetValue(rune.Value, out var g) ? g : rune.Value == '\n' ? 0 : reader.GlyphIndex(rune.Value), rune.Value, false));
        if (font.Ligatures)
            foreach (var rules in latin.Joins)
                for (int at = 0; at < run.Count; at++)
                {
                    if (!rules.TryGetValue(run[at].Glyph, out var listed)) continue;
                    foreach (var (then, ligature) in listed)
                    {
                        if (at + then.Length >= run.Count || !font.Glyphs.ContainsKey(latin.LigatureKeys[ligature])) continue;
                        var follows = true;
                        for (int k = 0; k < then.Length && follows; k++) follows = run[at + 1 + k].Glyph == then[k];
                        if (!follows) continue;
                        run[at] = (ligature, run[at].Character, true);
                        run.RemoveRange(at + 1, then.Length);
                        break;
                    }
                }

        // What kerning adds, in the font's units at the bake's scale, a pixel height of ascent to
        // descent, the pair a newline ends left unkerned.
        var unit = font.BaseSize / (reader.Ascent - reader.Descent);
        var placed = new PlacedKey[run.Count];
        var count = 0;
        for (int i = 0; i < run.Count; i++)
        {
            var (glyph, character, joined) = run[i];
            if (DefaultIgnorable(character)) continue;
            var key = joined ? latin.LigatureKeys[glyph] : character;
            var next = i + 1;
            while (next < run.Count && DefaultIgnorable(run[next].Character)) next++;
            var kerned = character == '\n' || next >= run.Count || run[next].Character == '\n' ? 0
                : latin.Pairs.TryGetValue((uint)glyph << 16 | (uint)run[next].Glyph, out var adds) ? adds
                : latin.ByKernTable ? reader.Kerning(glyph, run[next].Glyph) : 0;
            placed[count++] = kerned == 0 ? new PlacedKey(key)
                : new PlacedKey(key, default, (TryGetGlyph(font, key, out var drawn) ? drawn.Advance : 0) + kerned * unit);
        }
        return count == placed.Length ? placed : placed[..count];
    }

    // The characters of a run that select or join rather than draw, the joiners, the variation
    // selectors and the tags, which Unicode marks as default ignorable.
    private static bool DefaultIgnorable(int c) => c is 0x200C or 0x200D or (>= 0xFE00 and <= 0xFE0F) or (>= 0xE0000 and <= 0xE0FFF);
}
