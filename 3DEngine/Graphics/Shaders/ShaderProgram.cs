namespace Engine;

/// <summary>The SPIR-V of every entry point in one Slang file, by stage.</summary>
/// <remarks>
/// One file holds a program's vertex and fragment stages together, so the two cannot drift
/// apart in what they pass between them. Each stage's SPIR-V names its entry point <c>main</c>,
/// whatever the function is called in the source.
/// </remarks>
/// <seealso cref="SlangLoader"/>
public sealed class ShaderProgram
{
    /// <summary>Creates a program from compiled stages.</summary>
    /// <param name="name">The file the program was compiled from, for messages.</param>
    /// <param name="stages">The SPIR-V of each stage.</param>
    public ShaderProgram(string name, IReadOnlyDictionary<ShaderStage, byte[]> stages)
    {
        Name = name;
        Stages = stages;
    }

    /// <summary>The file the program was compiled from.</summary>
    public string Name { get; }

    /// <summary>The SPIR-V of each stage the file defines.</summary>
    public IReadOnlyDictionary<ShaderStage, byte[]> Stages { get; }

    /// <summary>The vertex stage's SPIR-V.</summary>
    /// <exception cref="InvalidOperationException">The file defines no vertex stage.</exception>
    public byte[] Vertex => Stage(ShaderStage.Vertex);

    /// <summary>The fragment stage's SPIR-V.</summary>
    /// <exception cref="InvalidOperationException">The file defines no fragment stage.</exception>
    public byte[] Fragment => Stage(ShaderStage.Fragment);

    private byte[] Stage(ShaderStage stage) =>
        Stages.TryGetValue(stage, out var bytecode)
            ? bytecode
            : throw new InvalidOperationException($"'{Name}' defines no {stage.ToString().ToLowerInvariant()} entry point.");
}
