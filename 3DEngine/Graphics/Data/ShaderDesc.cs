namespace Engine;

/// <summary>Shader pipeline stage.</summary>
public enum ShaderStage
{
    /// <summary>The vertex stage, which transforms vertex positions.</summary>
    Vertex,
    /// <summary>The fragment (pixel) stage, which computes each pixel's color.</summary>
    Fragment,
    /// <summary>Compute shader stage, run by a dispatch over groups of threads rather than by drawing.</summary>
    Compute
}

/// <summary>Descriptor for creating a shader module from SPIR-V bytecode.</summary>
/// <param name="Stage">The pipeline stage this shader belongs to.</param>
/// <param name="Bytecode">The compiled SPIR-V bytecode.</param>
/// <param name="EntryPoint">The shader entry-point function name (defaults to <c>"main"</c>).</param>
public readonly record struct ShaderDesc(ShaderStage Stage, ReadOnlyMemory<byte> Bytecode, string EntryPoint = "main");
