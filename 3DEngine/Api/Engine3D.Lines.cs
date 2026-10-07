namespace Engine;

// A line of text with letters read right to left made into the keys of its glyphs, shaped in the
// order it is stored, a run of Arabic by its forms, then put in the order it is shown.
public static partial class Engine3D
{
    // A line's keys in the order they are drawn from left to right. Each grapheme cluster becomes
    // keys of its own, a run of Arabic clusters shaped together since a letter's form depends on the
    // letters beside it, and the clusters are then put in the order the line is read, a cluster
    // keeping its keys in order, so a letter's marks stay after it.
    private static List<int> ShapeLine(Font font, string line)
    {
        if (line.Length == 0) return [];
        var clusters = TextDirection.Clusters(line);
        var count = clusters.Starts.Length;
        var keys = new List<int>[count];
        for (int c = 0; c < count;)
        {
            // A run of clusters of Arabic at one level, which the font shapes where it shapes Arabic.
            var end = c;
            while (end < count && clusters.Levels[end] == clusters.Levels[c] && ArabicJoining.IsArabic(clusters.Codepoints[clusters.First[end]])) end++;
            if (end > c && font.Arabic is { } arabic)
            {
                ShapeArabic(font, arabic, clusters, c, end, keys);
                c = end;
                continue;
            }
            keys[c] = ClusterKeys(font, clusters, c);
            c++;
        }
        var shown = new List<int>(clusters.Codepoints.Length);
        foreach (var c in TextDirection.Order(clusters.Levels)) shown.AddRange(keys[c]);
        return shown;
    }

    // A cluster's keys on its own, a mirrored character turned where it is read right to left, and
    // a sequence the font joins shaped as it is in a line of none.
    private static List<int> ClusterKeys(Font font, TextDirection.LineClusters clusters, int c)
    {
        var codepoints = clusters.Codepoints.AsSpan(clusters.First[c], clusters.Count(c)).ToArray();
        if ((clusters.Levels[c] & 1) == 1 && TextDirection.Mirrored(codepoints[0]) is { } turned) codepoints[0] = turned;
        if (font.Joining is { } joining && codepoints.Length > 1)
            return [.. ShapeText(font, joining, string.Concat(codepoints.Select(char.ConvertFromUtf32)))];
        return [.. codepoints];
    }

    // A run of Arabic clusters shaped, each letter given its form by the letters beside it, by the
    // font's substitutions under the arab script, or by the presentation forms it maps, each key
    // kept with the cluster it came from.
    private static void ShapeArabic(Font font, Font.ArabicShaping arabic, TextDirection.LineClusters clusters, int from, int to, List<int>[] keys)
    {
        for (int c = from; c < to; c++) keys[c] = [];
        var start = clusters.First[from];
        var end = to < clusters.Starts.Length ? clusters.First[to] : clusters.Codepoints.Length;
        var codepoints = clusters.Codepoints[start..end];
        var clusterOf = new int[codepoints.Length];
        for (int c = from, i = 0; c < to; c++)
            for (int k = 0; k < clusters.Count(c); k++) clusterOf[i++] = c;
        var forms = ArabicJoining.Forms(codepoints);

        if (arabic is { Reader: { } reader, Plan: { } plan })
        {
            var run = codepoints.Select((c, i) => new ShapedGlyph(reader.GlyphIndex(c), clusterOf[i], (byte)forms[i], DefaultIgnorable(c))).ToList();
            try
            {
                reader.Substitutions!.Apply(run, plan);
            }
            catch (ArgumentOutOfRangeException)
            {
                // A table that points past the file's end leaves the run as its characters are.
                for (int i = 0; i < codepoints.Length; i++) keys[clusterOf[i]].Add(codepoints[i]);
                return;
            }
            // A glyph that is a character's own is drawn as that character, and one the substitutions
            // made as the glyph baked under its key, as a joined emoji's is.
            var characterOf = new Dictionary<int, int>();
            for (int i = 0; i < codepoints.Length; i++)
                if (reader.GlyphIndex(codepoints[i]) is var g and not 0) characterOf.TryAdd(g, codepoints[i]);
            foreach (var glyph in run)
            {
                if (glyph.Ignorable) continue;
                keys[glyph.Cluster].Add(glyph.Glyph == 0 ? '?'
                    : characterOf.TryGetValue(glyph.Glyph, out var character) ? character
                    : font.Glyphs.ContainsKey(JoinedKey(glyph.Glyph)) ? JoinedKey(glyph.Glyph) : '?');
            }
            return;
        }

        // The presentation forms: each letter as the form it takes where the font has it, and lam
        // with the alef after it as their one joined form, the alef's marks staying after it.
        for (int i = 0; i < codepoints.Length; i++)
        {
            var c = codepoints[i];
            if (c == 0x0644 && forms[i] is ArabicJoining.Form.Initial or ArabicJoining.Form.Medial)
            {
                var next = i + 1;
                while (next < codepoints.Length && ArabicJoining.TypeOf(codepoints[next]) == ArabicJoining.Joining.Transparent) next++;
                if (next < codepoints.Length && ArabicJoining.LamAlef(codepoints[next], forms[i] == ArabicJoining.Form.Medial) is { } joined
                    && font.Glyphs.ContainsKey(joined))
                {
                    keys[clusterOf[i]].Add(joined);
                    for (int m = i + 1; m < next; m++) keys[clusterOf[m]].Add(codepoints[m]);
                    i = next;
                    continue;
                }
            }
            keys[clusterOf[i]].Add(forms[i] != ArabicJoining.Form.None && ArabicJoining.PresentationForm(c, forms[i]) is { } form && font.Glyphs.ContainsKey(form) ? form : c);
        }
    }
}
