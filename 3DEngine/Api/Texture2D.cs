namespace Engine;

/// <summary>A texture on the GPU, by its id in the <see cref="TextureStore"/>, with its size.</summary>
/// <remarks>A default texture has id 0 and is not loaded. Drawing it draws nothing.</remarks>
public readonly record struct Texture2D(int Id, int Width, int Height)
{
    /// <summary>Whether this names a texture that was loaded.</summary>
    public bool IsValid => Id > 0;

    /// <summary>How many mip levels the texture has: one until <see cref="Engine3D.GenTextureMipmaps"/> makes the rest.</summary>
    public int Mipmaps { get; init; } = 1;
}
