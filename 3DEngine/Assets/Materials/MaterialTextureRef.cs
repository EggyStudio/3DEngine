namespace Engine;

/// <summary>
/// Reference to a texture file that backs an input slot of a <see cref="MaterialDescription"/>.
/// </summary>
/// <param name="AssetPath">
/// Resolved asset path (relative to the source stage / project root) of the texture file.
/// May be a virtual path inside a packaged container; the texture loader is responsible
/// for resolving it the same way the original asset resolver did.
/// </param>
/// <param name="UvSet">
/// UV channel index used to sample this texture (<c>0</c> = primary UVs, <c>1</c> = secondary).
/// </param>
/// <param name="WrapS">Texture-coordinate wrap mode along the S axis.</param>
/// <param name="WrapT">Texture-coordinate wrap mode along the T axis.</param>
internal sealed record MaterialTextureRef(
    string AssetPath,
    int UvSet = 0,
    TextureWrapMode WrapS = TextureWrapMode.Repeat,
    TextureWrapMode WrapT = TextureWrapMode.Repeat);