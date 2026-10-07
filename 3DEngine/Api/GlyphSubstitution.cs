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
/// The flags, the scripts and the glyph classes are read as <see cref="GlyphLayout"/> says.
/// </para>
/// </remarks>
internal sealed partial class GlyphSubstitution : GlyphLayout
{
    // The ccmp feature of the default script, which joins emoji, or null where the table has none.
    private readonly Plan? _compose;

    /// <summary>
    /// The bit a glyph is marked with, in the run of glyph numbers <see cref="Apply(IEnumerable{int})"/>
    /// takes, where its character is default ignorable.
    /// </summary>
    public const int Ignorable = 1 << 16;

    private GlyphSubstitution(byte[] data, int gsub, int gdef) : base(data, gsub, gdef) => _compose = PlanFor(null, ("ccmp", 0));

    private protected override int ContextType => 5;

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
}
