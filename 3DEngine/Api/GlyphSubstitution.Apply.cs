namespace Engine;

// Substituting a run's glyphs.
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

    private protected override int? ApplySubtable(int type, int sub, List<ShapedGlyph> run, int at, (int Value, int MarkSet) flags)
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
                    // stays after the ligature, numbered by the component it followed from 1, so
                    // positioning puts a mark on its own letter of the ligature.
                    var id = run.Max(g => g.Ligature) + 1;
                    for (int k = at + 1, component = 1; places.Count > 0 && k < places[^1]; k++)
                    {
                        if (places.Contains(k)) component++;
                        else run[k] = run[k] with { Ligature = id, Component = component };
                    }
                    for (int c = places.Count - 1; c >= 0; c--) run.RemoveAt(places[c]);
                    run[at] = run[at] with { Glyph = U16(ligature), Ignorable = false, Ligature = id, Component = 0 };
                    return at + 1;
                }
                return null;
            }
            default:
                return null;
        }
    }
}
