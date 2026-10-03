namespace Engine;

/// <summary>Registry of named render textures available for cameras to target.</summary>
/// <seealso cref="RenderTextureDesc"/>
public sealed class RenderTextures
{
    private readonly Dictionary<string, RenderTextureDesc> _textures = new();

    /// <summary>Sets (inserts or overwrites) a named render texture.</summary>
    /// <param name="name">Unique name for the render texture.</param>
    /// <param name="desc">The texture descriptor.</param>
    public void Set(string name, RenderTextureDesc desc) => _textures[name] = desc;

    /// <summary>Tries to get the descriptor for a named render texture.</summary>
    /// <param name="name">Name of the render texture.</param>
    /// <param name="desc">When returning <c>true</c>, contains the descriptor; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
    public bool TryGet(string name, out RenderTextureDesc desc) => _textures.TryGetValue(name, out desc);

    /// <summary>Removes a named render texture.</summary>
    /// <param name="name">Name of the render texture to remove.</param>
    public void Remove(string name) => _textures.Remove(name);
}
