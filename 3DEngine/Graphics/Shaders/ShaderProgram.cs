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
    /// <param name="uniforms">The uniforms its stages declare at the top level, by name.</param>
    /// <param name="textures">The textures its stages sample in the first descriptor set, by name and binding.</param>
    /// <param name="buffers">The storage buffers its compute stage uses in the first descriptor set, by name and binding.</param>
    /// <param name="images">The images its compute stage writes in the first descriptor set, by name and binding.</param>
    /// <param name="bindings">Every descriptor its stages declare, in every set, with the stages that declare it.</param>
    public ShaderProgram(string name, IReadOnlyDictionary<ShaderStage, byte[]> stages, IReadOnlyList<ShaderUniform>? uniforms = null,
        IReadOnlyList<ShaderTexture>? textures = null, IReadOnlyList<ShaderTexture>? buffers = null, IReadOnlyList<ShaderTexture>? images = null,
        IReadOnlyList<(ShaderBinding Binding, ShaderStageFlags Stages)>? bindings = null)
    {
        Bindings = bindings ?? [];
        Buffers = buffers ?? [];
        Images = images ?? [];
        Name = name;
        Stages = stages;
        Uniforms = uniforms ?? [];
        Textures = textures ?? [];
        UniformSize = Uniforms.Count == 0 ? 0 : (Uniforms.Max(u => u.Offset + u.Size) + 15) / 16 * 16;
    }

    /// <summary>The uniforms the program declares at the top level, which a program sets by name.</summary>
    public IReadOnlyList<ShaderUniform> Uniforms { get; }

    /// <summary>The storage buffers a compute program reads and writes, by binding.</summary>
    public IReadOnlyList<ShaderTexture> Buffers { get; }

    /// <summary>The images a compute program writes, its <c>RWTexture2D</c>s, by binding.</summary>
    public IReadOnlyList<ShaderTexture> Images { get; }

    /// <summary>The textures the program samples in its first descriptor set, its engine module's among them, by binding.</summary>
    public IReadOnlyList<ShaderTexture> Textures { get; }

    /// <summary>
    /// The textures of its own, those not named in <paramref name="passTextures"/>, which its pass
    /// fills itself, in binding order.
    /// </summary>
    /// <remarks>
    /// Told by name, since Slang gives a shader's own textures the first bindings free, which is 0
    /// for a shader with no uniforms and after the uniform buffer for one with them.
    /// </remarks>
    public IReadOnlyList<ShaderTexture> OwnTextures(IReadOnlyCollection<string> passTextures) =>
        Textures.Where(t => !passTextures.Contains(t.Name)).OrderBy(t => t.Binding).ToArray();

    /// <summary>The size of the constant buffer the uniforms are read from, rounded up to 16 bytes, or 0 with none.</summary>
    public int UniformSize { get; }

    /// <summary>The file the program was compiled from.</summary>
    public string Name { get; }

    /// <summary>The SPIR-V of each stage the file defines.</summary>
    public IReadOnlyDictionary<ShaderStage, byte[]> Stages { get; }

    /// <summary>Every descriptor the program's stages declare, in every set, with the stages that declare it.</summary>
    public IReadOnlyList<(ShaderBinding Binding, ShaderStageFlags Stages)> Bindings { get; }

    /// <summary>
    /// The layout of descriptor set <paramref name="set"/> as the program declares it, in binding
    /// order, which a pipeline's layout is made from rather than typed beside it, so a binding added
    /// to a shader reaches the pipeline with no change to the code that draws with it.
    /// </summary>
    /// <remarks>
    /// The uniforms a program declares at the top level are read from a buffer at binding 0 of set 0
    /// that the reflection does not list as a descriptor, which a pass binding them adds itself.
    /// </remarks>
    public DescriptorSetLayoutBinding[] LayoutOf(int set) => Merge(
        Bindings.Where(b => b.Binding.Set == set).Select(b => new DescriptorSetLayoutBinding((uint)b.Binding.Binding, b.Binding.Type, b.Stages)));

    /// <summary>
    /// Layouts joined binding by binding, in binding order, the first to name a binding giving its
    /// kind and every one naming it adding its stages, as a pass's own bindings and a shader's are.
    /// </summary>
    public static DescriptorSetLayoutBinding[] Merge(params IEnumerable<DescriptorSetLayoutBinding>[] layouts)
    {
        var merged = new SortedDictionary<uint, DescriptorSetLayoutBinding>();
        foreach (var layout in layouts)
            foreach (var binding in layout)
                merged[binding.Binding] = merged.TryGetValue(binding.Binding, out var first)
                    ? first with { Stages = first.Stages | binding.Stages }
                    : binding;
        return [.. merged.Values];
    }

    /// <summary>The vertex stage's SPIR-V.</summary>
    /// <exception cref="InvalidOperationException">The file defines no vertex stage.</exception>
    public byte[] Vertex => Stage(ShaderStage.Vertex);

    /// <summary>The compute stage's SPIR-V.</summary>
    /// <exception cref="InvalidOperationException">The file defines no compute stage.</exception>
    public byte[] Compute => Stage(ShaderStage.Compute);

    /// <summary>The fragment stage's SPIR-V.</summary>
    /// <exception cref="InvalidOperationException">The file defines no fragment stage.</exception>
    public byte[] Fragment => Stage(ShaderStage.Fragment);

    private byte[] Stage(ShaderStage stage) =>
        Stages.TryGetValue(stage, out var bytecode)
            ? bytecode
            : throw new InvalidOperationException($"'{Name}' defines no {stage.ToString().ToLowerInvariant()} entry point.");
}
