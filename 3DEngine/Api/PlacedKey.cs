using System.Numerics;

namespace Engine;

/// <summary>
/// The key of a glyph text is drawn with in a font, and where shaping put it: drawn
/// <see cref="Offset"/> from the pen, in pixels of the font's bake, and moving the pen on by
/// <see cref="Advance"/>, or by the glyph's own advance where that is null.
/// </summary>
internal readonly record struct PlacedKey(int Key, Vector2 Offset = default, float? Advance = null);
