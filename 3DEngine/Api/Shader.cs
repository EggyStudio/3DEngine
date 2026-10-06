using System.Numerics;

namespace Engine;

/// <summary>A custom shader for the immediate pass, by its id in the <see cref="ShaderStore"/>.</summary>
/// <remarks>A default shader has id 0, and drawing inside <see cref="Engine3D.BeginShaderMode"/> with it uses the engine's own.</remarks>
public readonly record struct Shader(int Id)
{
    /// <summary>Whether this names a shader that was loaded.</summary>
    public bool IsValid => Id > 0;
}
