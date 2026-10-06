namespace Engine;

/// <summary>A finger on the screen, by the id the platform gives it while it stays down.</summary>
/// <param name="Id">The finger's id, the same from the moment it touches until it lifts.</param>
/// <param name="Position">Where it is, in window pixels from the top left corner.</param>
public readonly record struct TouchPoint(long Id, System.Numerics.Vector2 Position);
