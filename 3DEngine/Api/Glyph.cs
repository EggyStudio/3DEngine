namespace Engine;

/// <summary>One character of a <see cref="Font"/>: where it sits relative to the pen, where it is in the atlas, and how far it moves the pen.</summary>
public readonly record struct Glyph(float X0, float Y0, float X1, float Y1, float U0, float V0, float U1, float V1, float Advance);
