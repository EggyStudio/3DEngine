using System.Numerics;

namespace Engine;

/// <summary>A custom shader for the immediate pass, by its id in the <c>ShaderStore</c>.</summary>
/// <remarks>A default shader has id 0, and drawing inside <c>E3D.BeginShaderMode</c> with it uses the engine's own.</remarks>
public readonly record struct Shader(int Id)
{
    /// <summary>Whether this names a shader that was loaded.</summary>
    public bool IsValid => Id > 0;
}
