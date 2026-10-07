namespace Engine;

/// <summary>
/// A glyph of a run being shaped: its index in the font, the grapheme cluster of the text it came
/// from, the positions of a joining script it stands in, as <see cref="GlyphLayout.Plan"/>'s
/// masks name them, and whether its character is default ignorable, which matching passes over
/// where it does not match and a substitution clears.
/// </summary>
/// <remarks>
/// A ligature and the glyphs passed over between its components share <see cref="Ligature"/>, each
/// of those numbered by the component it followed, from 1, so a mark is put on its own letter of
/// the ligature. The other properties hold the moves positioning gives the glyph, in the font's units.
/// </remarks>
internal readonly record struct ShapedGlyph(int Glyph, int Cluster, byte Mask = 0, bool Ignorable = false)
{
    /// <summary>The ligature the glyph is or was passed over inside, numbered from 1, or 0 for none.</summary>
    public int Ligature { get; init; }

    /// <summary>The component of <see cref="Ligature"/> a glyph passed over inside it followed, or 0 for the ligature itself.</summary>
    public int Component { get; init; }

    /// <summary>How far right the glyph is drawn from where its advance puts it, or from where the glyph it is attached to is drawn.</summary>
    public int X { get; init; }

    /// <summary>How far up the glyph is moved, as <see cref="X"/> is across.</summary>
    public int Y { get; init; }

    /// <summary>How much the glyph's advance grows.</summary>
    public int Advance { get; init; }

    /// <summary>How many glyphs back the glyph it is attached to is, a mark's base, or 0 where it is attached to none.</summary>
    public int Attached { get; init; }

    /// <summary>
    /// How many glyphs on, ahead or back, the glyph it is joined to by a cursive attachment is, whose
    /// height it is moved with, or 0 where it is joined to none.
    /// </summary>
    public int Cursive { get; init; }
}
