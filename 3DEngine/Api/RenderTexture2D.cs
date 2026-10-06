namespace Engine;

/// <summary>An image drawing can be sent to with <see cref="Engine3D.BeginTextureMode"/>, and drawn afterward through <see cref="TextureAsset"/>.</summary>
/// <param name="Texture">The color drawn.</param>
/// <param name="Depth">
/// The depth drawn, in red, from 0 at the camera's near plane to 1 at its far one, and 1 where
/// nothing was drawn, for a shader that fogs, outlines or softens by distance.
/// </param>
public readonly record struct RenderTexture2D(Texture2D Texture, Texture2D Depth = default)
{
    /// <summary>Whether this names a render texture that was loaded.</summary>
    public bool IsValid => Texture.IsValid;

    private readonly Texture2D[]? _textures;

    /// <summary>
    /// Every texture the target draws into, in the order of the formats it was loaded with,
    /// <see cref="Texture"/> first, and that one alone for a target loaded without formats.
    /// </summary>
    public IReadOnlyList<Texture2D> Textures
    {
        get => _textures ?? [Texture];
        init => _textures = [.. value];
    }
}
