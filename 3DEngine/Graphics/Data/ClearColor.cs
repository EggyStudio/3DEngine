namespace Engine;

/// <summary>RGBA clear color for render pass initialization.</summary>
/// <param name="R">Red channel (0–1).</param>
/// <param name="G">Green channel (0–1).</param>
/// <param name="B">Blue channel (0–1).</param>
/// <param name="A">Alpha channel (0–1).</param>
public readonly record struct ClearColor(float R, float G, float B, float A)
{
    /// <summary>Opaque black clear color.</summary>
    public static readonly ClearColor Black = new(0, 0, 0, 1);
}
