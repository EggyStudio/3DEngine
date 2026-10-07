namespace Engine;

/// <summary>
/// The positions a font's GPOS table gives a run of glyphs, by a plan of its features: the marks of
/// a script that writes its vowels above and below its letters, as Arabic's harakat, put on the
/// letter they follow by the anchors the font gives both, and pairs of letters moved apart or
/// together, as kerning does.
/// </summary>
/// <remarks>
/// <para>
/// A plan's lookups are applied in the order the table lists them, each over the whole run, as
/// <see cref="GlyphSubstitution"/>'s are: single and pair adjustments, a pair's in both its formats,
/// and marks put on a base, on a ligature's component and on another mark, and the contextual and
/// chained contextual positionings, reached directly or through an extension. A value record's
/// placements and its horizontal advance are taken and its device tables left aside, so a font of
/// variations is positioned as its default instance is drawn.
/// </para>
/// <para>
/// A cursive attachment joins a letter's exit to the entry of the next one the lookup does not pass
/// over, as Nastaliq is written, read right to left, as the runs of Arabic it positions are: the
/// glyph before gives up its advance past its exit and this one's advance ends at its entry, and
/// across the line the glyph the lookup's right-to-left flag makes the child, the first where it is
/// set, is moved to meet the other and carried with it (<see cref="ShapedGlyph.Cursive"/>), as
/// HarfBuzz joins them.
/// </para>
/// <para>
/// A mark is put on the glyph before it that is no mark, or on the mark before it, as HarfBuzz
/// finds the base. A mark attached gives up the moves made to it before, and its anchor is put on
/// its base's where that glyph is drawn, a mark on a ligature on the component it followed when the
/// ligature was made, or on the last one where it followed none.
/// </para>
/// </remarks>
internal sealed class GlyphPositioning : GlyphLayout
{
    // A glyph's own advance, in the font's units, which a cursive attachment sets anew.
    private readonly Func<int, int> _advance;

    private GlyphPositioning(byte[] data, int gpos, int gdef, Func<int, int> advance) : base(data, gpos, gdef) => _advance = advance;

    private protected override int ContextType => 7;

    /// <summary>
    /// Reads the GPOS table at <paramref name="gpos"/>, with the GDEF table at <paramref name="gdef"/>
    /// for the glyph classes, -1 where the font has none, and each glyph's own advance from
    /// <paramref name="advance"/>, or null where the table cannot be read.
    /// </summary>
    public static GlyphPositioning? Read(byte[] data, int gpos, int gdef, Func<int, int> advance)
    {
        try
        {
            return new GlyphPositioning(data, gpos, gdef, advance);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Whether the GDEF table classes a glyph as a mark, whose advance a shaper takes away.</summary>
    public bool IsMark(int glyph) => GlyphClass(glyph) == 3;

    private protected override int? ApplySubtable(int type, int sub, List<ShapedGlyph> run, int at, (int Value, int MarkSet) flags)
    {
        var format = U16(sub);
        var glyph = run[at].Glyph;
        switch (type)
        {
            case 1 when Coverage(sub + U16(sub + 2), glyph) is { } index:
            {
                var values = U16(sub + 4);
                if (format == 2 && index >= U16(sub + 6) || format is not (1 or 2)) return null;
                run[at] = Moved(run[at], values, format == 1 ? sub + 6 : sub + 8 + index * Size(values));
                return at + 1;
            }
            case 2 when Coverage(sub + U16(sub + 2), glyph) is { } index:
                return Pair(format, sub, index, run, at, flags);
            case 3 when format == 1 && Coverage(sub + U16(sub + 2), glyph) is { } index:
                return Cursive(sub, index, run, at, flags);
            case 4 or 5 or 6 when format == 1 && Coverage(sub + U16(sub + 2), glyph) is { } mark:
                return Attach(type, sub, mark, run, at, flags);
            default:
                return null;
        }
    }

    // A pair adjustment of a glyph and the next one the lookup does not pass over, found by the
    // second glyph in the first format or by both glyphs' classes in the second, each moved by its
    // value record. It gives the place of the second, or the one after it where its record moved it.
    private int? Pair(int format, int sub, int index, List<ShapedGlyph> run, int at, (int Value, int MarkSet) flags)
    {
        var second = at + 1;
        while (second < run.Count && (Skips(flags, run[second].Glyph) || run[second].Ignorable)) second++;
        if (second >= run.Count) return null;
        int first = U16(sub + 4), then = U16(sub + 6), size = Size(first) + Size(then);
        int values;
        if (format == 1)
        {
            if (index >= U16(sub + 8)) return null;
            var set = sub + U16(sub + 10 + index * 2);
            if (PairRecord(set, run[second].Glyph, 2 + size) is not { } record) return null;
            values = record + 2;
        }
        else if (format == 2)
        {
            int class1 = Class(sub + U16(sub + 8), run[at].Glyph), class2 = Class(sub + U16(sub + 10), run[second].Glyph);
            if (class1 >= U16(sub + 12) || class2 >= U16(sub + 14)) return null;
            values = sub + 16 + (class1 * U16(sub + 14) + class2) * size;
        }
        else return null;
        run[at] = Moved(run[at], first, values);
        run[second] = Moved(run[second], then, values + Size(first));
        return then != 0 ? second + 1 : second;
    }

    // The record of a pair set whose second glyph is the one given, the set's records sorted by it.
    private int? PairRecord(int set, int glyph, int size)
    {
        int lo = 0, hi = U16(set) - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var record = set + 2 + mid * size;
            var second = U16(record);
            if (glyph < second) hi = mid - 1;
            else if (glyph > second) lo = mid + 1;
            else return record;
        }
        return null;
    }

    // A glyph's entry joined to the exit of the glyph before it the lookup does not pass over. Read
    // right to left, the glyph before is moved right by what lies past its exit and its advance cut
    // by as much, and this one's advance is set to end at its entry. The child, the glyph before
    // where the lookup's right-to-left flag is set and this one where not, is moved up or down to
    // meet its parent, and a chain the child was joined by before is turned to end at the new parent,
    // so the whole of it moves with it.
    private int? Cursive(int sub, int index, List<ShapedGlyph> run, int at, (int Value, int MarkSet) flags)
    {
        var count = U16(sub + 4);
        if (index >= count) return null;
        var entry = U16(sub + 6 + index * 4);
        if (entry == 0) return null;
        var before = at - 1;
        while (before >= 0 && (Skips(flags, run[before].Glyph) || run[before].Ignorable)) before--;
        if (before < 0 || Coverage(sub + U16(sub + 2), run[before].Glyph) is not { } previous || previous >= count) return null;
        var exit = U16(sub + 6 + previous * 4 + 2);
        if (exit == 0) return null;
        var (exitX, exitY) = Anchor(sub + exit);
        var (entryX, entryY) = Anchor(sub + entry);

        var past = exitX + run[before].X;
        run[before] = run[before] with { X = run[before].X - past, Advance = run[before].Advance - past };
        run[at] = run[at] with { Advance = entryX + run[at].X - _advance(run[at].Glyph) };

        var rightToLeft = (flags.Value & 1) != 0;
        var (child, parent) = rightToLeft ? (before, at) : (at, before);
        Unchain(run, child, parent);
        run[child] = run[child] with { Y = rightToLeft ? entryY - exitY : exitY - entryY, Cursive = parent - child };
        // A parent joined to the child before is let go, so the two do not hang on each other.
        if (run[parent].Cursive == child - parent) run[parent] = run[parent] with { Y = 0, Cursive = 0 };
        return at + 1;
    }

    // Turns the chain a glyph was joined by before toward it, each glyph along it now hanging on the
    // one that hung on it, so the chain moves with the glyph's new parent, stopping at that parent.
    private static void Unchain(List<ShapedGlyph> run, int glyph, int parent)
    {
        var chain = run[glyph].Cursive;
        if (chain == 0) return;
        run[glyph] = run[glyph] with { Cursive = 0 };
        var next = glyph + chain;
        if (next == parent || next < 0 || next >= run.Count) return;
        Unchain(run, next, parent);
        run[next] = run[next] with { Y = -run[glyph].Y, Cursive = -chain };
    }

    // A mark put on the glyph it follows: on the base or the ligature before it, passing over the
    // marks between, or on the mark right before it, by the anchor of the mark's class there, which
    // the mark's own anchor is put on.
    private int? Attach(int type, int sub, int mark, List<ShapedGlyph> run, int at, (int Value, int MarkSet) flags)
    {
        int classes = U16(sub + 6), marks = sub + U16(sub + 8), targets = sub + U16(sub + 10);
        var target = at - 1;
        if (type == 6)
        {
            // The mark before, passing over only those of another attachment class or set.
            var only = (flags.Value & ~0x0E, flags.MarkSet);
            while (target >= 0 && (Skips(only, run[target].Glyph) || run[target].Ignorable)) target--;
            if (target < 0 || HasGlyphClasses && !IsMark(run[target].Glyph) || !OnSameGlyph(run[at], run[target])) return null;
        }
        else
            while (target >= 0 && (IsMarkOf(run[target].Glyph, sub) || run[target].Ignorable)) target--;
        if (target < 0 || Coverage(sub + U16(sub + 4), run[target].Glyph) is not { } covered) return null;
        if (mark >= U16(marks)) return null;
        var markClass = U16(marks + 2 + mark * 4);
        if (markClass >= classes || covered >= U16(targets)) return null;

        int anchors = targets, row = covered;
        if (type == 5)
        {
            // The ligature's components each have a row of anchors, the mark on the one it followed.
            anchors = targets + U16(targets + 2 + covered * 2);
            var components = U16(anchors);
            if (components == 0) return null;
            var (glyph, ligature) = (run[at], run[target]);
            row = glyph.Ligature != 0 && glyph.Ligature == ligature.Ligature && glyph.Component > 0 ? Math.Min(components, glyph.Component) - 1 : components - 1;
        }
        var offset = U16(anchors + 2 + (row * classes + markClass) * 2);
        if (offset == 0) return null;
        var (baseX, baseY) = Anchor(anchors + offset);
        var own = U16(marks + 2 + mark * 4 + 2);
        var (markX, markY) = own == 0 ? (0, 0) : Anchor(marks + own);
        run[at] = run[at] with { X = baseX - markX, Y = baseY - markY, Attached = at - target };
        return at + 1;
    }

    // Whether a glyph is a mark, by the GDEF table, or where it classes no glyph, by a mark
    // subtable's coverage of its marks.
    private bool IsMarkOf(int glyph, int sub) => IsMark(glyph) || !HasGlyphClasses && Coverage(sub + U16(sub + 2), glyph) is not null;

    // Whether two marks are on the same glyph, as HarfBuzz judges it: neither inside a ligature, or
    // both on one component of the same one, or either a ligature of marks itself.
    private static bool OnSameGlyph(ShapedGlyph mark, ShapedGlyph before) =>
        mark.Ligature == before.Ligature
            ? mark.Ligature == 0 || mark.Component == before.Component
            : mark.Ligature > 0 && mark.Component == 0 || before.Ligature > 0 && before.Component == 0;

    // An anchor's point, in each of its three formats, the contour point and the device tables of
    // the later two left aside.
    private (int X, int Y) Anchor(int table) => (S16(table + 2), S16(table + 4));

    // A glyph moved by a value record of a format: its placement across and up and its advance
    // across, the advance up of vertical text and the device tables passed over.
    private ShapedGlyph Moved(ShapedGlyph glyph, int format, int values)
    {
        int x = 0, y = 0, advance = 0;
        if ((format & 1) != 0) (x, values) = (S16(values), values + 2);
        if ((format & 2) != 0) (y, values) = (S16(values), values + 2);
        if ((format & 4) != 0) advance = S16(values);
        return glyph with { X = glyph.X + x, Y = glyph.Y + y, Advance = glyph.Advance + advance };
    }

    // How many bytes a value record of a format takes, two for each field it has.
    private static int Size(int format) => System.Numerics.BitOperations.PopCount((uint)format & 0xFF) * 2;
}
