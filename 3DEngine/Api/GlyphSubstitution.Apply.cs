namespace Engine;

// Applying a plan's lookups to a run of glyphs.
internal sealed partial class GlyphSubstitution
{
    /// <summary>
    /// The run of glyphs the <c>ccmp</c> feature makes of <paramref name="glyphs"/>, those of default
    /// ignorable characters marked <see cref="Ignorable"/>, which stay marked where nothing
    /// substituted them, the glyphs given where the table has no such feature.
    /// </summary>
    public List<int> Apply(IEnumerable<int> glyphs)
    {
        var run = glyphs.Select((g, i) => new ShapedGlyph(g & 0xFFFF, i, 0, (g & Ignorable) != 0)).ToList();
        if (_compose is not null) Apply(run, _compose);
        return [.. run.Select(g => g.Glyph | (g.Ignorable ? Ignorable : 0))];
    }

    /// <summary>
    /// Applies a plan to a run, each lookup over the whole run from its start at the glyphs its mask
    /// takes and its flags do not pass over, a glyph that comes of another keeping its cluster.
    /// </summary>
    public void Apply(List<ShapedGlyph> run, Plan plan)
    {
        foreach (var (lookup, mask) in plan.Lookups)
        {
            var flags = FlagsOf(lookup);
            for (int i = 0; i < run.Count;)
            {
                if (mask != 0 && (run[i].Mask & mask) == 0 || Skips(flags, run[i].Glyph))
                {
                    i++;
                    continue;
                }
                i = ApplyAt(lookup, run, i, 0) ?? i + 1;
            }
        }
    }

    // Applies one lookup at a place of the run, giving the place after what it substituted, or null
    // where none of its subtables apply there.
    private int? ApplyAt(int lookup, List<ShapedGlyph> run, int at, int depth)
    {
        if (depth > MaxDepth || lookup >= LookupCount) return null;
        var flags = FlagsOf(lookup);
        foreach (var (type, sub) in Subtables(lookup))
            if (ApplySubtable(type, sub, run, at, depth, flags) is { } next) return next;
        return null;
    }

    private int? ApplySubtable(int type, int sub, List<ShapedGlyph> run, int at, int depth, (int Value, int MarkSet) flags)
    {
        var format = U16(sub);
        var glyph = run[at].Glyph;
        switch (type)
        {
            case 1 when Coverage(sub + U16(sub + 2), glyph) is { } index:
                run[at] = run[at] with { Glyph = format == 1 ? (glyph + S16(sub + 4)) & 0xFFFF : U16(sub + 6 + index * 2), Ignorable = false };
                return at + 1;
            case 2 when Coverage(sub + U16(sub + 2), glyph) is { } index:
            {
                // The glyphs a glyph becomes keep its cluster and its positions.
                var sequence = sub + U16(sub + 6 + index * 2);
                var count = U16(sequence);
                var replaced = run[at];
                run.RemoveAt(at);
                run.InsertRange(at, Enumerable.Range(0, count).Select(i => replaced with { Glyph = U16(sequence + 2 + i * 2), Ignorable = false }));
                return at + count;
            }
            case 4 when Coverage(sub + U16(sub + 2), glyph) is { } index:
            {
                // The first ligature of the set whose components follow, which a font lists longest first.
                var set = sub + U16(sub + 6 + index * 2);
                var places = new List<int>();
                for (int l = 0; l < U16(set); l++)
                {
                    var ligature = set + U16(set + 2 + l * 2);
                    var components = U16(ligature + 2);
                    places.Clear();
                    for (int c = 1, next = at + 1; c < components; c++)
                    {
                        var component = U16(ligature + 4 + (c - 1) * 2);
                        var place = Forward(run, next, g => g == component, flags);
                        if (place < 0) break;
                        places.Add(place);
                        next = place + 1;
                    }
                    if (places.Count != components - 1) continue;
                    // The components go, the ligature taking the first one's cluster, and a glyph
                    // passed over between them, an ignorable one or a mark the lookup ignores,
                    // stays after the ligature.
                    for (int c = places.Count - 1; c >= 0; c--) run.RemoveAt(places[c]);
                    run[at] = run[at] with { Glyph = U16(ligature), Ignorable = false };
                    return at + 1;
                }
                return null;
            }
            case 5 or 6:
                return Context(type, sub, run, at, depth, flags);
            default:
                return null;
        }
    }

    // A contextual or chained contextual subtable: the rule that matches at a place, if one does, its
    // records applied to the places of its input, giving the place after the input.
    private int? Context(int type, int sub, List<ShapedGlyph> run, int at, int depth, (int Value, int MarkSet) flags)
    {
        var format = U16(sub);
        if (format == 3)
        {
            if (type == 5)
            {
                int glyphs = U16(sub + 2), count = U16(sub + 4);
                var places = Match(run, at, glyphs, i => g => Coverage(sub + U16(sub + 6 + i * 2), g) is not null, forward: true, flags);
                return places is null ? null : Substitute(run, places, sub + 6 + glyphs * 2, count, depth);
            }
            var place = sub + 2;
            int backtrack = U16(place), backtracks = place + 2;
            place += 2 + backtrack * 2;
            int input = U16(place), inputs = place + 2;
            place += 2 + input * 2;
            int lookahead = U16(place), lookaheads = place + 2;
            place += 2 + lookahead * 2;
            var matched = Match(run, at, input, i => g => Coverage(sub + U16(inputs + i * 2), g) is not null, forward: true, flags);
            if (matched is null
                || Match(run, at - 1, backtrack, i => g => Coverage(sub + U16(backtracks + i * 2), g) is not null, forward: false, flags, first: true) is null
                || Match(run, matched[^1] + 1, lookahead, i => g => Coverage(sub + U16(lookaheads + i * 2), g) is not null, forward: true, flags, first: true) is null)
                return null;
            return Substitute(run, matched, place + 2, U16(place), depth);
        }

        if (Coverage(sub + U16(sub + 2), run[at].Glyph) is not { } covered) return null;
        // The rule set of the first glyph, by its place in the coverage or by its class.
        int setIndex, classes = 0, backClasses = 0, aheadClasses = 0, sets;
        if (format == 1) (setIndex, sets) = (covered, sub + 4);
        else if (format == 2 && type == 5) (classes, sets, setIndex) = (sub + U16(sub + 4), sub + 6, Class(sub + U16(sub + 4), run[at].Glyph));
        else if (format == 2)
        {
            (backClasses, classes, aheadClasses) = (sub + U16(sub + 4), sub + U16(sub + 6), sub + U16(sub + 8));
            (sets, setIndex) = (sub + 10, Class(sub + U16(sub + 6), run[at].Glyph));
        }
        else return null;
        if (setIndex >= U16(sets) || U16(sets + 2 + setIndex * 2) == 0) return null;
        var set = sub + U16(sets + 2 + setIndex * 2);

        // A rule's entries are glyphs in the first format and classes in the second.
        Func<int, bool> Is(int entry, int classDef) => format == 1 ? g => g == entry : g => Class(classDef, g) == entry;
        for (int r = 0; r < U16(set); r++)
        {
            var rule = set + U16(set + 2 + r * 2);
            var place = rule;
            int backtrack = 0, backtracks = 0;
            if (type == 6)
            {
                (backtrack, backtracks) = (U16(place), place + 2);
                place += 2 + backtrack * 2;
            }
            int input = U16(place);
            if (input == 0) continue;
            var inputs = place + (type == 5 ? 4 : 2);
            // The rule names the input after its first glyph, which the set was chosen by.
            var matched = Match(run, at, input, i => i == 0 ? _ => true : Is(U16(inputs + (i - 1) * 2), classes), forward: true, flags);
            if (matched is null) continue;
            if (type == 5) return Substitute(run, matched, inputs + (input - 1) * 2, U16(place + 2), depth);
            place = inputs + (input - 1) * 2;
            int lookahead = U16(place), lookaheads = place + 2;
            place += 2 + lookahead * 2;
            if (Match(run, at - 1, backtrack, i => Is(U16(backtracks + i * 2), backClasses), forward: false, flags, first: true) is null
                || Match(run, matched[^1] + 1, lookahead, i => Is(U16(lookaheads + i * 2), aheadClasses), forward: true, flags, first: true) is null)
                continue;
            return Substitute(run, matched, place + 2, U16(place), depth);
        }
        return null;
    }

    // The places of a sequence of count glyphs from a place of the run, forward or back, the i-th
    // taken by entry(i), passing over the glyphs the lookup's flags ignore and the ignorable glyphs
    // entry does not take, or null where the sequence is not there. The glyph at the place itself is
    // the first unless first is set, in which case it may be passed over too.
    private List<int>? Match(List<ShapedGlyph> run, int from, int count, Func<int, Func<int, bool>> entry, bool forward,
        (int Value, int MarkSet) flags, bool first = false)
    {
        var places = new List<int>(count);
        var at = from;
        for (int i = 0; i < count; i++)
        {
            var takes = entry(i);
            if (i == 0 && !first)
            {
                if (at < 0 || at >= run.Count || !takes(run[at].Glyph)) return null;
            }
            else
            {
                at = forward ? Forward(run, at, takes, flags) : Backward(run, at, takes, flags);
                if (at < 0) return null;
            }
            places.Add(at);
            at += forward ? 1 : -1;
        }
        return places;
    }

    // The place from which on the first glyph a test takes is, passing over the glyphs the
    // lookup's flags ignore and the ignorable glyphs the test does not take, or -1 where another
    // glyph comes first or the run ends.
    private int Forward(List<ShapedGlyph> run, int from, Func<int, bool> takes, (int Value, int MarkSet) flags)
    {
        for (int i = Math.Max(0, from); i < run.Count; i++)
        {
            if (Skips(flags, run[i].Glyph)) continue;
            if (takes(run[i].Glyph)) return i;
            if (!run[i].Ignorable) return -1;
        }
        return -1;
    }

    // The same, looking back from a place toward the run's start.
    private int Backward(List<ShapedGlyph> run, int from, Func<int, bool> takes, (int Value, int MarkSet) flags)
    {
        for (int i = Math.Min(from, run.Count - 1); i >= 0; i--)
        {
            if (Skips(flags, run[i].Glyph)) continue;
            if (takes(run[i].Glyph)) return i;
            if (!run[i].Ignorable) return -1;
        }
        return -1;
    }

    // Applies a matched rule's records, each a lookup at a place of its input, in order, the places
    // after one moving with what it substitutes, and gives the place after the input.
    private int Substitute(List<ShapedGlyph> run, List<int> places, int records, int count, int depth)
    {
        var end = places[^1] + 1;
        for (int i = 0; i < count; i++)
        {
            int sequence = U16(records + i * 4), lookup = U16(records + i * 4 + 2);
            if (sequence >= places.Count) continue;
            var before = run.Count;
            ApplyAt(lookup, run, places[sequence], depth + 1);
            var moved = run.Count - before;
            for (int p = sequence + 1; p < places.Count; p++) places[p] += moved;
            end += moved;
        }
        return Math.Max(end, places[0] + 1);
    }
}
