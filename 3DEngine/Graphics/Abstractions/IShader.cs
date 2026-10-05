namespace Engine;

/// <summary>Handle to a compiled GPU shader module.</summary>
internal interface IShader : IDisposable
{
    /// <summary>The descriptor used to create this shader.</summary>
    ShaderDesc Description { get; }
}
