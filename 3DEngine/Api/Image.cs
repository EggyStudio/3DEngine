namespace Engine;

/// <summary>Pixels in memory, four bytes each (red, green, blue, alpha), rows from the top.</summary>
/// <remarks>An image lives in managed memory and is collected like any array, so <see cref="Engine3D.UnloadImage"/> does nothing.</remarks>
public readonly record struct Image(byte[] Data, int Width, int Height)
{
    /// <summary>Whether the image holds pixels.</summary>
    public bool IsValid => Data is { Length: > 0 } && Width > 0 && Height > 0;
}
