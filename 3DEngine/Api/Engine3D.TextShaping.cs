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

    // The plan of a font's features for Arabic, in HarfBuzz's stages: composing and the local forms
    // at every glyph, the isolated, final, medial and initial forms each at the letters in that
    // position, the required ligatures, the contextual alternates, then the marks' positional forms
    // and the ligatures a text font makes, or null where the font has none of the four forms.
    private static GlyphSubstitution.Plan? ArabicPlan(GlyphSubstitution table) =>
        table.PlanFor("arab", ("isol", 1), ("fina", 2), ("medi", 4), ("init", 8)) is null ? null
            : table.PlanInStages("arab", [("ccmp", 0), ("locl", 0)], [("isol", (byte)ArabicJoining.Form.Isolated)], [("fina", (byte)ArabicJoining.Form.Final)],
                [("medi", (byte)ArabicJoining.Form.Medial)], [("init", (byte)ArabicJoining.Form.Initial)], [("rlig", 0)], [("rclt", 0), ("calt", 0)],
                [("mset", 0), ("liga", 0), ("clig", 0)]);

    // The plan of a font's positions for Arabic: the marks above and below put on their letters and
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
        return font.Joining is { } joining ? font.ShapedText(text, t => [.. ShapeText(font, joining, t).Select(key => new PlacedKey(key))]) : Runes(text);
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

    // The characters of a run that select or join rather than draw, the joiners, the variation
    // selectors and the tags, which Unicode marks as default ignorable.
    private static bool DefaultIgnorable(int c) => c is 0x200C or 0x200D or (>= 0xFE00 and <= 0xFE0F) or (>= 0xE0000 and <= 0xE0FFF);
}
