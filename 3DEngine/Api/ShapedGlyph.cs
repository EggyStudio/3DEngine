namespace Engine;

/// <summary>
/// A glyph of a run being shaped: its index in the font, the grapheme cluster of the text it came
/// from, the positions of a joining script it stands in, as <see cref="GlyphSubstitution.Plan"/>'s
/// masks name them, and whether its character is default ignorable, which matching passes over
/// where it does not match and a substitution clears.
/// </summary>
internal readonly record struct ShapedGlyph(int Glyph, int Cluster, byte Mask = 0, bool Ignorable = false);
