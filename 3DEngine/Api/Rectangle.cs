namespace Engine;

/// <summary>A rectangle by its top left corner and its size, in pixels unless said otherwise.</summary>
public readonly record struct Rectangle(float X, float Y, float Width, float Height);
