using System.Buffers.Binary;

namespace Engine;

/// <summary>
/// The substitutions a font's GSUB table makes to compose glyphs (its <c>ccmp</c> feature), which
/// join a sequence of emoji into the one picture it stands for: a family from its people and the
/// joiners between them, a flag from two regional indicators, a skin tone from a person and a
/// modifier, a keycap from a digit and its marks.
/// </summary>
/// <remarks>
/// <para>
/// The lookups of the feature are applied in the order the table lists them, each over the whole
/// run of glyphs from its start, as a shaper applies them: single, multiple and ligature
/// substitutions, and the contextual and chained contextual ones in each of their three formats,
/// which apply others at places of the run they match, reached directly or through an extension.
/// An alternate or a reverse chained substitution is not made, and no glyph is skipped by its
/// class, which the emoji fonts read (Noto Color Emoji, Twemoji and Segoe UI Emoji) have no use
/// for, their lookups taking every glyph.
/// </para>
/// <para>
/// A glyph marked <see cref="Ignorable"/>, a character such as U+FE0F that selects how the one
/// before it is drawn, is passed over while a sequence is matched where it does not match itself,
/// as HarfBuzz passes over the characters Unicode marks as default ignorable, since a font's
/// ligature for a keycap or a rainbow flag leaves out the U+FE0F the text has inside it.
/// </para>
/// <para>
/// The script is the default one, or the first the table names where it has none, and its default
/// language system.
/// </para>
/// </remarks>
internal sealed class GlyphSubstitution
{
    private readonly byte[] _data;
    private readonly int _lookupList;

    // The lookups the feature applies, in the table's order, and every lookup they reach through
    // the contexts, which the glyphs a run can become are gathered from.
    private readonly int[] _applied;
    private readonly int[] _reached;

    // How deep a context's lookups may call others, past which a font's loop of them is cut.
    private const int MaxDepth = 8;

    /// <summary>
    /// The bit a glyph of a run is marked with where its character is default ignorable, which
    /// matching passes over where it does not match, and which a substitution clears.
    /// </summary>
    public const int Ignorable = 1 << 16;

    private GlyphSubstitution(byte[] data, int gsub, int[] applied)
    {
        _data = data;
        _lookupList = gsub + U16(gsub + 8);
        _applied = applied;
        var reached = new SortedSet<int>(applied);
        var pending = new Stack<int>(applied);
        while (pending.Count > 0)
            foreach (var nested in Nested(pending.Pop()))
                if (nested < LookupCount && reached.Add(nested)) pending.Push(nested);
        _reached = [.. reached];
    }

    /// <summary>
    /// Reads the <c>ccmp</c> feature of the GSUB table at <paramref name="gsub"/>, or null where
    /// the table has none or cannot be read.
    /// </summary>
    public static GlyphSubstitution? Read(byte[] data, int gsub)
    {
        try
        {
            var probe = new GlyphSubstitution(data, gsub, []);
            var applied = probe.FeatureLookups(gsub, "ccmp");
            return applied.Length == 0 ? null : new GlyphSubstitution(data, gsub, applied);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A table pointing past the file's end is one to leave unread, as the reader leaves a
            // glyph whose outline does.
            return null;
        }
    }

    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));
    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));

    private int LookupCount => U16(_lookupList);

    // The lookups a feature of the default language system applies, in the table's order.
    private int[] FeatureLookups(int gsub, string tag)
    {
        int scripts = gsub + U16(gsub + 4), features = gsub + U16(gsub + 6);
        var featureCount = U16(features);
        IEnumerable<int> indices = Enumerable.Range(0, featureCount);
        if (U16(scripts) > 0)
        {
            // The default script, or the first named, and its default language system.
            var chosen = 0;
            for (int i = 0; i < U16(scripts); i++)
                if (System.Text.Encoding.ASCII.GetString(_data, scripts + 2 + i * 6, 4) == "DFLT") chosen = i;
            var script = scripts + U16(scripts + 2 + chosen * 6 + 4);
            if (U16(script) != 0)
            {
                var language = script + U16(script);
                var required = U16(language + 2);
                var listed = Enumerable.Range(0, U16(language + 4)).Select(i => (int)U16(language + 6 + i * 2));
                indices = required == 0xFFFF ? listed : listed.Prepend(required);
            }
        }
        var lookups = new SortedSet<int>();
        foreach (var index in indices.Where(i => i < featureCount))
        {
            int record = features + 2 + index * 6, feature = features + U16(record + 4);
            if (System.Text.Encoding.ASCII.GetString(_data, record, 4) != tag) continue;
            for (int i = 0; i < U16(feature + 2); i++) lookups.Add(U16(feature + 4 + i * 2));
        }
        return [.. lookups.Where(l => l < LookupCount)];
    }

    // A lookup's type and its subtables, an extension's followed to the subtable it holds.
    private IEnumerable<(int Type, int Table)> Subtables(int lookup)
    {
        var table = _lookupList + U16(_lookupList + 2 + lookup * 2);
        var type = U16(table);
        for (int i = 0; i < U16(table + 4); i++)
        {
            var sub = table + U16(table + 6 + i * 2);
            if (type == 7) yield return (U16(sub + 2), sub + (int)U32(sub + 4));
            else yield return (type, sub);
        }
    }

    // The lookups a lookup's contexts apply.
    private IEnumerable<int> Nested(int lookup)
    {
        foreach (var (type, sub) in Subtables(lookup))
            foreach (var record in Records(type, sub))
                yield return U16(record + 2);
    }

    // Each substitution record of a contextual subtable, the place of its sequence index.
    private IEnumerable<int> Records(int type, int sub)
    {
        var format = U16(sub);
        if (type == 5 && format == 3)
        {
            int glyphs = U16(sub + 2), count = U16(sub + 4);
            for (int i = 0; i < count; i++) yield return sub + 6 + glyphs * 2 + i * 4;
        }
        else if (type == 6 && format == 3)
        {
            var at = sub + 2;
            at += 2 + U16(at) * 2;
            at += 2 + U16(at) * 2;
            at += 2 + U16(at) * 2;
            for (int i = 0; i < U16(at); i++) yield return at + 2 + i * 4;
        }
        else if (type is 5 or 6 && format is 1 or 2)
        {
            int sets = sub + (format == 1 ? 4 : type == 5 ? 6 : 10);
            for (int s = 0; s < U16(sets); s++)
            {
                var offset = U16(sets + 2 + s * 2);
                if (offset == 0) continue;
                var set = sub + offset;
                for (int r = 0; r < U16(set); r++)
                {
                    var rule = set + U16(set + 2 + r * 2);
                    var (at, count) = RuleRecords(type, rule);
                    for (int i = 0; i < count; i++) yield return at + i * 4;
                }
            }
        }
    }

    // Where a rule of a contextual subtable's first or second format keeps its records, and how many.
    private (int At, int Count) RuleRecords(int type, int rule)
    {
        if (type == 5)
        {
            int glyphs = U16(rule), count = U16(rule + 2);
            return (rule + 4 + (glyphs - 1) * 2, count);
        }
        var at = rule;
        at += 2 + U16(at) * 2;
        at += 2 + (U16(at) - 1) * 2;
        at += 2 + U16(at) * 2;
        return (at + 2, U16(at));
    }

    /// <summary>
    /// The run of glyphs the feature makes of <paramref name="glyphs"/>, those of default ignorable
    /// characters marked <see cref="Ignorable"/>, which stay marked where nothing substituted them.
    /// </summary>
    public List<int> Apply(IEnumerable<int> glyphs)
    {
        var run = glyphs.ToList();
        foreach (var lookup in _applied)
            for (int i = 0; i < run.Count;)
                i = ApplyAt(lookup, run, i, 0) ?? i + 1;
        return run;
    }

    /// <summary>
    /// Every glyph a run of <paramref name="glyphs"/> could become through the feature, those given
    /// among them, found by substituting each lookup's covered glyphs among those reached until no
    /// more are, leaving aside the context that would choose between them.
    /// </summary>
    public HashSet<int> Reachable(IEnumerable<int> glyphs)
    {
        var reached = new HashSet<int>(glyphs);
        for (var grew = true; grew;)
        {
            var before = reached.Count;
            foreach (var lookup in _reached)
                foreach (var (type, sub) in Subtables(lookup))
                    Reach(type, sub, reached);
            grew = reached.Count > before;
        }
        return reached;
    }

    private void Reach(int type, int sub, HashSet<int> reached)
    {
        var format = U16(sub);
        var coverage = sub + U16(sub + 2);
        switch (type)
        {
            case 1:
                foreach (var (glyph, index) in Covered(coverage).Where(c => reached.Contains(c.Glyph)).ToArray())
                    reached.Add(format == 1 ? (glyph + S16(sub + 4)) & 0xFFFF : U16(sub + 6 + index * 2));
                break;
            case 2:
                foreach (var (_, index) in Covered(coverage).Where(c => reached.Contains(c.Glyph)).ToArray())
                {
                    var sequence = sub + U16(sub + 6 + index * 2);
                    for (int i = 0; i < U16(sequence); i++) reached.Add(U16(sequence + 2 + i * 2));
                }
                break;
            case 4:
                foreach (var (_, index) in Covered(coverage).Where(c => reached.Contains(c.Glyph)).ToArray())
                {
                    var set = sub + U16(sub + 6 + index * 2);
                    for (int l = 0; l < U16(set); l++)
                    {
                        var ligature = set + U16(set + 2 + l * 2);
                        var components = U16(ligature + 2);
                        var all = true;
                        for (int c = 1; c < components && all; c++) all = reached.Contains(U16(ligature + 4 + (c - 1) * 2));
                        if (all) reached.Add(U16(ligature));
                    }
                }
                break;
        }
    }

    // Applies one lookup at a place of the run, giving the place after what it substituted, or null
    // where none of its subtables apply there.
    private int? ApplyAt(int lookup, List<int> run, int at, int depth)
    {
        if (depth > MaxDepth || lookup >= LookupCount) return null;
        foreach (var (type, sub) in Subtables(lookup))
            if (ApplySubtable(type, sub, run, at, depth) is { } next) return next;
        return null;
    }

    private int? ApplySubtable(int type, int sub, List<int> run, int at, int depth)
    {
        var format = U16(sub);
        var glyph = run[at] & 0xFFFF;
        switch (type)
        {
            case 1 when Coverage(sub + U16(sub + 2), glyph) is { } index:
                run[at] = format == 1 ? (glyph + S16(sub + 4)) & 0xFFFF : U16(sub + 6 + index * 2);
                return at + 1;
            case 2 when Coverage(sub + U16(sub + 2), glyph) is { } index:
            {
                var sequence = sub + U16(sub + 6 + index * 2);
                var count = U16(sequence);
                run.RemoveAt(at);
                run.InsertRange(at, Enumerable.Range(0, count).Select(i => (int)U16(sequence + 2 + i * 2)));
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
                        var place = Forward(run, next, g => g == component);
                        if (place < 0) break;
                        places.Add(place);
                        next = place + 1;
                    }
                    if (places.Count != components - 1) continue;
                    // The components go, and an ignorable glyph passed over between them stays after
                    // the ligature.
                    for (int c = places.Count - 1; c >= 0; c--) run.RemoveAt(places[c]);
                    run[at] = U16(ligature);
                    return at + 1;
                }
                return null;
            }
            case 5 or 6:
                return Context(type, sub, run, at, depth);
            default:
                return null;
        }
    }

    // A contextual or chained contextual subtable: the rule that matches at a place, if one does, its
    // records applied to the places of its input, giving the place after the input.
    private int? Context(int type, int sub, List<int> run, int at, int depth)
    {
        var format = U16(sub);
        if (format == 3)
        {
            if (type == 5)
            {
                int glyphs = U16(sub + 2), count = U16(sub + 4);
                var places = Match(run, at, glyphs, i => g => Coverage(sub + U16(sub + 6 + i * 2), g) is not null, forward: true);
                return places is null ? null : Substitute(run, places, sub + 6 + glyphs * 2, count, depth);
            }
            var place = sub + 2;
            int backtrack = U16(place), backtracks = place + 2;
            place += 2 + backtrack * 2;
            int input = U16(place), inputs = place + 2;
            place += 2 + input * 2;
            int lookahead = U16(place), lookaheads = place + 2;
            place += 2 + lookahead * 2;
            var matched = Match(run, at, input, i => g => Coverage(sub + U16(inputs + i * 2), g) is not null, forward: true);
            if (matched is null
                || Match(run, at - 1, backtrack, i => g => Coverage(sub + U16(backtracks + i * 2), g) is not null, forward: false, first: true) is null
                || Match(run, matched[^1] + 1, lookahead, i => g => Coverage(sub + U16(lookaheads + i * 2), g) is not null, forward: true, first: true) is null)
                return null;
            return Substitute(run, matched, place + 2, U16(place), depth);
        }

        if (Coverage(sub + U16(sub + 2), run[at] & 0xFFFF) is not { } covered) return null;
        // The rule set of the first glyph, by its place in the coverage or by its class.
        int setIndex, classes = 0, backClasses = 0, aheadClasses = 0, sets;
        if (format == 1) (setIndex, sets) = (covered, sub + 4);
        else if (format == 2 && type == 5) (classes, sets, setIndex) = (sub + U16(sub + 4), sub + 6, Class(sub + U16(sub + 4), run[at] & 0xFFFF));
        else if (format == 2)
        {
            (backClasses, classes, aheadClasses) = (sub + U16(sub + 4), sub + U16(sub + 6), sub + U16(sub + 8));
            (sets, setIndex) = (sub + 10, Class(sub + U16(sub + 6), run[at] & 0xFFFF));
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
            var matched = Match(run, at, input, i => i == 0 ? _ => true : Is(U16(inputs + (i - 1) * 2), classes), forward: true);
            if (matched is null) continue;
            if (type == 5) return Substitute(run, matched, inputs + (input - 1) * 2, U16(place + 2), depth);
            place = inputs + (input - 1) * 2;
            int lookahead = U16(place), lookaheads = place + 2;
            place += 2 + lookahead * 2;
            if (Match(run, at - 1, backtrack, i => Is(U16(backtracks + i * 2), backClasses), forward: false, first: true) is null
                || Match(run, matched[^1] + 1, lookahead, i => Is(U16(lookaheads + i * 2), aheadClasses), forward: true, first: true) is null)
                continue;
            return Substitute(run, matched, place + 2, U16(place), depth);
        }
        return null;
    }

    // The places of a sequence of count glyphs from a place of the run, forward or back, the i-th
    // taken by entry(i), passing over ignorable glyphs that entry does not take, or null where the
    // sequence is not there. The glyph at the place itself is the first unless first is set, in
    // which case it may be passed over too.
    private static List<int>? Match(List<int> run, int from, int count, Func<int, Func<int, bool>> entry, bool forward, bool first = false)
    {
        var places = new List<int>(count);
        var at = from;
        for (int i = 0; i < count; i++)
        {
            var takes = entry(i);
            if (i == 0 && !first)
            {
                if (at < 0 || at >= run.Count || !takes(run[at] & 0xFFFF)) return null;
            }
            else
            {
                at = forward ? Forward(run, at, takes) : Backward(run, at, takes);
                if (at < 0) return null;
            }
            places.Add(at);
            at += forward ? 1 : -1;
        }
        return places;
    }

    // The place from which on the first glyph a test takes is, passing over ignorable glyphs it
    // does not take, or -1 where another glyph comes first or the run ends.
    private static int Forward(List<int> run, int from, Func<int, bool> takes)
    {
        for (int i = Math.Max(0, from); i < run.Count; i++)
        {
            if (takes(run[i] & 0xFFFF)) return i;
            if ((run[i] & Ignorable) == 0) return -1;
        }
        return -1;
    }

    // The same, looking back from a place toward the run's start.
    private static int Backward(List<int> run, int from, Func<int, bool> takes)
    {
        for (int i = Math.Min(from, run.Count - 1); i >= 0; i--)
        {
            if (takes(run[i] & 0xFFFF)) return i;
            if ((run[i] & Ignorable) == 0) return -1;
        }
        return -1;
    }

    // Applies a matched rule's records, each a lookup at a place of its input, in order, the places
    // after one moving with what it substitutes, and gives the place after the input.
    private int Substitute(List<int> run, List<int> places, int records, int count, int depth)
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

    // A glyph's place in a coverage table, or null where it is not covered.
    private int? Coverage(int table, int glyph)
    {
        var format = U16(table);
        int lo = 0, hi = U16(table + 2) - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (format == 1)
            {
                var g = U16(table + 4 + mid * 2);
                if (glyph < g) hi = mid - 1;
                else if (glyph > g) lo = mid + 1;
                else return mid;
            }
            else
            {
                int range = table + 4 + mid * 6, start = U16(range), end = U16(range + 2);
                if (glyph < start) hi = mid - 1;
                else if (glyph > end) lo = mid + 1;
                else return U16(range + 4) + glyph - start;
            }
        }
        return null;
    }

    // Every glyph a coverage table covers, with its place in it.
    private IEnumerable<(int Glyph, int Index)> Covered(int table)
    {
        if (U16(table) == 1)
            for (int i = 0; i < U16(table + 2); i++) yield return (U16(table + 4 + i * 2), i);
        else
            for (int r = 0; r < U16(table + 2); r++)
            {
                int range = table + 4 + r * 6, start = U16(range), end = U16(range + 2), first = U16(range + 4);
                for (int g = start; g <= end; g++) yield return (g, first + g - start);
            }
    }

    // A glyph's class in a class definition table, 0 for one it does not name.
    private int Class(int table, int glyph)
    {
        if (U16(table) == 1)
        {
            int start = U16(table + 2), count = U16(table + 4);
            return glyph >= start && glyph < start + count ? U16(table + 6 + (glyph - start) * 2) : 0;
        }
        int lo = 0, hi = U16(table + 2) - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2, range = table + 4 + mid * 6;
            if (glyph < U16(range)) hi = mid - 1;
            else if (glyph > U16(range + 2)) lo = mid + 1;
            else return U16(range + 4);
        }
        return 0;
    }
}
