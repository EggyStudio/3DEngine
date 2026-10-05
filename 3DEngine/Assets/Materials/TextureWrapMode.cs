namespace Engine;

/// <summary>Texture-coordinate wrap mode (matches glTF / <c>UsdUVTexture</c> semantics).</summary>
internal enum TextureWrapMode : byte
{
    /// <summary>Coordinates outside [0,1] wrap around (default).</summary>
    Repeat,

    /// <summary>Coordinates outside [0,1] mirror back into the range.</summary>
    Mirror,

    /// <summary>Coordinates outside [0,1] clamp to the texture edge.</summary>
    Clamp,

    /// <summary>Coordinates outside [0,1] sample a constant border color (typically transparent black).</summary>
    Black,
}