namespace Engine;

/// <summary>
/// The ImGui texture ids a program's textures are drawn by, which the ImGui pass reads back into
/// the texture each draw command names.
/// </summary>
/// <remarks>
/// The font atlas is ImGui's texture 1, and a program's texture is its id with a bit above its
/// 32, so neither is taken for the other and the pass binds the font atlas wherever a command
/// names neither.
/// </remarks>
internal static class ImGuiTextures
{
    private const long Marked = 1L << 32;

    /// <summary>The id ImGui draws <paramref name="texture"/> by, or none for a texture that is not loaded.</summary>
    public static IntPtr IdOf(Texture2D texture) => texture.IsValid ? (IntPtr)(Marked | (uint)texture.Id) : IntPtr.Zero;

    /// <summary>Whether <paramref name="id"/> names a program's texture, and which.</summary>
    public static bool TextureOf(IntPtr id, out int texture)
    {
        var value = (long)id;
        texture = (value & Marked) != 0 ? (int)(uint)value : 0;
        return texture > 0;
    }
}
