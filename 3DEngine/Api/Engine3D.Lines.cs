using System.Numerics;

namespace Engine;

// A line of text with letters read right to left made into the keys of its glyphs, shaped in the
// order it is stored, a run of Arabic by its forms, then put in the order it is shown.
public static partial class Engine3D
{
    // A line's keys in the order they are drawn from left to right. Each grapheme cluster becomes
    // keys of its own, a run of Arabic clusters shaped together since a letter's form depends on the
    // letters beside it, and the clusters are then put in the order the line is read, a cluster
    // keeping its keys in order, so a letter's marks stay after it.
    private static List<PlacedKey> ShapeLine(Font font, string line)
    {
        if (line.Length == 0) return [];
        var clusters = TextDirection.Clusters(line);
        var count = clusters.Starts.Length;
        var keys = new List<PlacedKey>[count];
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
        var shown = new List<PlacedKey>(clusters.Codepoints.Length);
        foreach (var c in TextDirection.Order(clusters.Levels)) shown.AddRange(keys[c]);
        return shown;
    }

    // A cluster's keys on its own, a mirrored character turned where it is read right to left, and
    // a sequence the font joins shaped as it is in a line of none.
    private static List<PlacedKey> ClusterKeys(Font font, TextDirection.LineClusters clusters, int c)
    {
        var codepoints = clusters.Codepoints.AsSpan(clusters.First[c], clusters.Count(c)).ToArray();
        if ((clusters.Levels[c] & 1) == 1 && TextDirection.Mirrored(codepoints[0]) is { } turned) codepoints[0] = turned;
        if (font.Joining is { } joining && codepoints.Length > 1)
            return [.. ShapeText(font, joining, string.Concat(codepoints.Select(char.ConvertFromUtf32))).Select(key => new PlacedKey(key))];
        return [.. codepoints.Select(key => new PlacedKey(key))];
    }

    // A run of Arabic clusters shaped, its marks put in the order they are shaped in, each letter
    // given its form by the letters beside it, by the font's substitutions under the arab script
    // and placed by its positions, or by the presentation forms it maps, each key kept with the
    // cluster it came from.
    private static void ShapeArabic(Font font, Font.ArabicShaping arabic, TextDirection.LineClusters clusters, int from, int to, List<PlacedKey>[] keys)
    {
        for (int c = from; c < to; c++) keys[c] = [];
        var start = clusters.First[from];
        var end = to < clusters.Starts.Length ? clusters.First[to] : clusters.Codepoints.Length;
        var codepoints = clusters.Codepoints[start..end];
        ArabicMarks.Reorder(codepoints);
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
                for (int i = 0; i < codepoints.Length; i++) keys[clusterOf[i]].Add(new PlacedKey(codepoints[i]));
                return;
            }
            // A glyph that is a character's own is drawn as that character, and one the substitutions
            // made as the glyph baked under its key, as a joined emoji's is.
            var characterOf = new Dictionary<int, int>();
            for (int i = 0; i < codepoints.Length; i++)
                if (reader.GlyphIndex(codepoints[i]) is var g and not 0) characterOf.TryAdd(g, codepoints[i]);
            var glyphKeys = run.Select(glyph => glyph.Glyph == 0 ? '?'
                : characterOf.TryGetValue(glyph.Glyph, out var character) ? character
                : font.Glyphs.ContainsKey(JoinedKey(glyph.Glyph)) ? JoinedKey(glyph.Glyph) : '?').ToArray();
            if (arabic.Positions is { } positions && Placed(font, reader, positions, run, glyphKeys, (clusters.Levels[from] & 1) == 1) is { } placed)
            {
                // The clusters in the order they are shown, from the right in a run read that way,
                // each glyph drawn as far from the pen as positioning put it from where the pen is then.
                var pen = 0f;
                foreach (var c in (clusters.Levels[from] & 1) == 1 ? Enumerable.Range(from, to - from).Reverse() : Enumerable.Range(from, to - from))
                    for (int i = 0; i < run.Count; i++)
                    {
                        if (run[i].Ignorable || run[i].Cluster != c) continue;
                        keys[c].Add(new PlacedKey(glyphKeys[i], placed[i].At - new Vector2(pen, 0), placed[i].Advance));
                        pen += placed[i].Advance;
                    }
                return;
            }
            for (int i = 0; i < run.Count; i++)
                if (!run[i].Ignorable) keys[run[i].Cluster].Add(new PlacedKey(glyphKeys[i]));
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
                    keys[clusterOf[i]].Add(new PlacedKey(joined));
                    for (int m = i + 1; m < next; m++) keys[clusterOf[m]].Add(new PlacedKey(codepoints[m]));
                    i = next;
                    continue;
                }
            }
            keys[clusterOf[i]].Add(new PlacedKey(forms[i] != ArabicJoining.Form.None && ArabicJoining.PresentationForm(c, forms[i]) is { } form && font.Glyphs.ContainsKey(form) ? form : c));
        }
    }

    // Where each glyph of a shaped run is drawn from the run's left edge, in pixels of the font's
    // bake, and how far it moves the pen, by the font's positions. A glyph advances by its own
    // advance and what positioning added to it, and a mark by none, as a shaper takes a mark's
    // away. The glyphs are laid from the right in a run read that way, each moved by its
    // placement, and a mark attached is put where its base is drawn and moved from there by its
    // anchor. A glyph a cursive attachment joined is moved up or down with the glyph it is joined
    // to, through the chain to the one that stays on the baseline. Null where the table points past
    // the file's end, which leaves the run placed by its glyphs' own advances.
    private static (Vector2 At, float Advance)[]? Placed(Font font, TrueTypeFont reader, GlyphLayout.Plan plan, List<ShapedGlyph> run,
        int[] glyphKeys, bool rightToLeft)
    {
        var unit = font.BaseSize / (reader.Ascent - reader.Descent);
        var placed = new (Vector2 At, float Advance)[run.Count];
        try
        {
            reader.Positions!.Apply(run, plan);
            for (int i = 0; i < run.Count; i++)
                placed[i].Advance = run[i].Ignorable || reader.Positions.IsMark(run[i].Glyph) ? 0
                    : (TryGetGlyph(font, glyphKeys[i], out var glyph) ? glyph.Advance : 0) + run[i].Advance * unit;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        // Each glyph's height, its own move and that of the glyph a cursive attachment joined it to,
        // which may lie ahead of it, each worked out once.
        var lifted = new int?[run.Count];
        int Lifted(int i, int depth)
        {
            if (lifted[i] is { } known) return known;
            var parent = i + run[i].Cursive;
            var carried = run[i].Cursive != 0 && parent >= 0 && parent < run.Count && depth < run.Count ? Lifted(parent, depth + 1) : 0;
            return (lifted[i] = run[i].Y + carried).Value;
        }

        var edge = rightToLeft ? placed.Sum(p => p.Advance) : 0;
        for (int i = 0; i < run.Count; i++)
        {
            if (rightToLeft) edge -= placed[i].Advance;
            var moved = new Vector2(run[i].X, -Lifted(i, 0)) * unit;
            placed[i].At = (run[i].Attached > 0 && run[i].Attached <= i ? placed[i - run[i].Attached].At : new Vector2(edge, 0)) + moved;
            if (!rightToLeft) edge += placed[i].Advance;
        }
        return placed;
    }
}
