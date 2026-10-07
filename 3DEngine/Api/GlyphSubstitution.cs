using System.Buffers.Binary;

namespace Engine;

/// <summary>
/// The substitutions a font's GSUB table makes, by plans of its features: its <c>ccmp</c> feature,
/// which joins a sequence of emoji into the one picture it stands for (a family from its people and
/// the joiners between them, a flag from two regional indicators, a skin tone from a person and a
/// modifier, a keycap from a digit and its marks), and the features of a script that joins its
/// letters, as Arabic's choose each letter's form by the letters beside it.
/// </summary>
/// <remarks>
/// <para>
/// A plan's lookups are applied in the order the table lists them, each over the whole run of
/// glyphs from its start, as a shaper applies them, a lookup of a feature that names positions
/// only at the glyphs its mask takes: single, multiple and ligature substitutions, and the
/// contextual and chained contextual ones in each of their three formats, which apply others at
/// places of the run they match, reached directly or through an extension. An alternate or a
/// reverse chained substitution is not made.
/// </para>
/// <para>
/// A lookup's flags are honored by the glyph classes of the font's GDEF table. A lookup that
/// ignores marks, base glyphs or ligatures passes over them as it matches and is not applied at
/// one, and a mark of another attachment class, or outside the mark filtering set it names, is
/// passed over too. A glyph marked <see cref="ShapedGlyph.Ignorable"/>, a character such as U+FE0F
/// that selects how the one before it is drawn, is passed over while a sequence is matched where it
/// does not match itself, as HarfBuzz passes over the characters Unicode marks as default ignorable,
/// since a font's ligature for a keycap or a rainbow flag leaves out the U+FE0F inside it.
/// </para>
/// <para>
/// A plan's script is the one asked for where the table has it, or else the default one, or else
/// the first the table names, and its default language system with its required feature.
/// </para>
/// </remarks>
internal sealed partial class GlyphSubstitution
{
    private readonly byte[] _data;
    private readonly int _gsub, _lookupList, _gdef;

    // The ccmp feature of the default script, which joins emoji, or null where the table has none.
    private readonly Plan? _compose;

    // How deep a context's lookups may call others, past which a font's loop of them is cut.
    private const int MaxDepth = 8;

    /// <summary>
    /// The bit a glyph is marked with, in the run of glyph numbers <see cref="Apply(IEnumerable{int})"/>
    /// takes, where its character is default ignorable.
    /// </summary>
    public const int Ignorable = 1 << 16;

    /// <summary>
    /// The lookups a run is shaped by, each with the mask of the positions it is applied at, 0 for a
    /// lookup applied at every glyph, in the table's order, and every lookup they reach through the
    /// contexts, which the glyphs a run can become are gathered from.
    /// </summary>
    internal sealed class Plan((int Lookup, byte Mask)[] lookups, int[] reached)
    {
        public IReadOnlyList<(int Lookup, byte Mask)> Lookups { get; } = lookups;
        public IReadOnlyList<int> Reached { get; } = reached;
    }

    private GlyphSubstitution(byte[] data, int gsub, int gdef)
    {
        _data = data;
        _gsub = gsub;
        _lookupList = gsub + U16(gsub + 8);
        _gdef = gdef;
        _compose = PlanFor(null, ("ccmp", 0));
    }

    /// <summary>
    /// Reads the GSUB table at <paramref name="gsub"/>, with the GDEF table at <paramref name="gdef"/>
    /// for the glyph classes its lookups' flags name, -1 where the font has none, or null where the
    /// table cannot be read.
    /// </summary>
    public static GlyphSubstitution? Read(byte[] data, int gsub, int gdef = -1)
    {
        try
        {
            return new GlyphSubstitution(data, gsub, gdef);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A table pointing past the file's end is one to leave unread, as the reader leaves a
            // glyph whose outline does.
            return null;
        }
    }

    /// <summary>Whether the table has a <c>ccmp</c> feature, which composes emoji.</summary>
    public bool Composes => _compose is not null;

    /// <summary>
    /// The plan of the features named, each with the mask of the positions it is applied at, 0 for
    /// every glyph, under <paramref name="script"/>, or null where the table has none of them.
    /// </summary>
    public Plan? PlanFor(string? script, params (string Tag, byte Mask)[] features)
    {
        try
        {
            var masks = new SortedDictionary<int, byte>();
            foreach (var (tag, mask) in features)
                foreach (var lookup in FeatureLookups(script, tag))
                    // A lookup two features apply at every glyph or at either's positions.
                    masks[lookup] = masks.TryGetValue(lookup, out var had) ? (byte)(had == 0 || mask == 0 ? 0 : had | mask) : mask;
            if (masks.Count == 0) return null;
            var reached = new SortedSet<int>(masks.Keys);
            var pending = new Stack<int>(masks.Keys);
            while (pending.Count > 0)
                foreach (var nested in Nested(pending.Pop()))
                    if (nested < LookupCount && reached.Add(nested)) pending.Push(nested);
            return new Plan([.. masks.Select(entry => (entry.Key, entry.Value))], [.. reached]);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(at));
    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at));

    private int LookupCount => U16(_lookupList);

    // The lookups a feature of a script's default language system applies, in the table's order.
    private int[] FeatureLookups(string? asked, string tag)
    {
        int scripts = _gsub + U16(_gsub + 4), features = _gsub + U16(_gsub + 6);
        var featureCount = U16(features);
        IEnumerable<int> indices = Enumerable.Range(0, featureCount);
        if (U16(scripts) > 0)
        {
            // The script asked for, or the default one, or the first named, and its default language system.
            string Tag(int i) => System.Text.Encoding.ASCII.GetString(_data, scripts + 2 + i * 6, 4);
            var named = Enumerable.Range(0, U16(scripts)).ToArray();
            var chosen = named.Where(i => Tag(i) == asked).Concat(named.Where(i => Tag(i) == "DFLT")).DefaultIfEmpty(0).First();
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

    // A lookup's flags, and the mark filtering set it names where its flags use one.
    private (int Value, int MarkSet) FlagsOf(int lookup)
    {
        var table = _lookupList + U16(_lookupList + 2 + lookup * 2);
        var flags = U16(table + 2);
        return (flags, (flags & 0x10) != 0 ? U16(table + 6 + U16(table + 4) * 2) : 0);
    }

    // Whether a lookup of these flags passes over a glyph, by its class in the GDEF table: a base,
    // a ligature or a mark it ignores, or a mark of another attachment class or outside its set.
    private bool Skips((int Value, int MarkSet) flags, int glyph)
    {
        if (_gdef < 0 || (flags.Value & 0xFF1E) == 0) return false;
        var kind = GlyphClass(glyph);
        if ((flags.Value & 2) != 0 && kind == 1 || (flags.Value & 4) != 0 && kind == 2 || (flags.Value & 8) != 0 && kind == 3) return true;
        if (kind != 3) return false;
        if ((flags.Value & 0x10) != 0) return !InMarkSet(flags.MarkSet, glyph);
        return flags.Value >> 8 != 0 && MarkAttachClass(glyph) != flags.Value >> 8;
    }

    // A glyph's class in the GDEF table, 1 a base, 2 a ligature, 3 a mark and 4 a component, 0 unnamed.
    internal int GlyphClass(int glyph) => _gdef >= 0 && U16(_gdef + 4) != 0 ? Class(_gdef + U16(_gdef + 4), glyph) : 0;

    private int MarkAttachClass(int glyph) => _gdef >= 0 && U16(_gdef + 10) != 0 ? Class(_gdef + U16(_gdef + 10), glyph) : 0;

    // Whether a mark is in a mark glyph set of the GDEF table, which version 1.2 has.
    private bool InMarkSet(int set, int glyph)
    {
        if (_gdef < 0 || U32(_gdef) < 0x00010002 || U16(_gdef + 12) == 0) return false;
        var sets = _gdef + U16(_gdef + 12);
        return set < U16(sets + 2) && Coverage(sets + (int)U32(sets + 4 + set * 4), glyph) is not null;
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
    /// Every glyph a run of <paramref name="glyphs"/> could become through the <c>ccmp</c> feature,
    /// those given among them, found as <see cref="Reachable(IEnumerable{int}, Plan)"/> finds them.
    /// </summary>
    public HashSet<int> Reachable(IEnumerable<int> glyphs) => _compose is null ? [.. glyphs] : Reachable(glyphs, _compose);

    /// <summary>
    /// Every glyph a run of <paramref name="glyphs"/> could become through a plan, those given among
    /// them, found by substituting each lookup's covered glyphs among those reached until no more
    /// are, leaving aside the context and the positions that would choose between them.
    /// </summary>
    public HashSet<int> Reachable(IEnumerable<int> glyphs, Plan plan)
    {
        var reached = new HashSet<int>(glyphs);
        for (var grew = true; grew;)
        {
            var before = reached.Count;
            foreach (var lookup in plan.Reached)
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
