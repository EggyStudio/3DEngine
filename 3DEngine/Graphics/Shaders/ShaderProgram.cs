namespace Engine;

/// <summary>The SPIR-V of every entry point in one Slang file, by stage.</summary>
/// <remarks>
/// One file holds a program's vertex and fragment stages together, so the two cannot drift
/// apart in what they pass between them. Each stage's SPIR-V names its entry point <c>main</c>,
/// whatever the function is called in the source.
/// </remarks>
/// <seealso cref="SlangLoader"/>
internal sealed class ShaderProgram
{
    /// <summary>Creates a program from compiled stages.</summary>
    /// <param name="name">The file the program was compiled from, for messages.</param>
    /// <param name="stages">The SPIR-V of each stage.</param>
    /// <param name="uniforms">The uniforms its stages declare at the top level, by name.</param>
    /// <param name="textures">The textures its stages sample in the first descriptor set, by name and binding.</param>
    /// <param name="buffers">The storage buffers its compute stage uses in the first descriptor set, by name and binding.</param>
    /// <param name="images">The images its compute stage writes in the first descriptor set, by name and binding.</param>
    /// <param name="bindings">Every descriptor its stages declare, in every set, with the stages that declare it.</param>
    /// <param name="vertexInputs">The inputs its vertex stage takes by their semantics, none where they are not known so.</param>
    public ShaderProgram(string name, IReadOnlyDictionary<ShaderStage, byte[]> stages, IReadOnlyList<ShaderUniform>? uniforms = null,
        IReadOnlyList<ShaderTexture>? textures = null, IReadOnlyList<ShaderTexture>? buffers = null, IReadOnlyList<ShaderTexture>? images = null,
        IReadOnlyList<(ShaderBinding Binding, ShaderStageFlags Stages)>? bindings = null, IReadOnlyList<ShaderInput>? vertexInputs = null)
    {
        VertexInputs = vertexInputs ?? [];
        Bindings = bindings ?? [];
        Buffers = buffers ?? [];
        Images = images ?? [];
        Name = name;
        Stages = stages;
        Uniforms = uniforms ?? [];
        Textures = textures ?? [];
        UniformSize = Uniforms.Count == 0 ? 0 : (Uniforms.Max(u => u.Offset + u.Size) + 15) / 16 * 16;
    }

    /// <summary>
    /// The inputs the vertex stage takes, each by its semantic and the location Slang gave it, so a
    /// pass feeds each stream of its vertices where the stage reads it, whatever order the stage
    /// declares them in. Empty where the stage has an input with no semantic, or there is no vertex
    /// stage, when a pass feeds it by the engine's fixed locations.
    /// </summary>
    public IReadOnlyList<ShaderInput> VertexInputs { get; }

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

    /// <summary>One past the highest location a stage takes an input at, or 0 for a stage it lacks or one that takes none.</summary>
    /// <remarks>
    /// Read from the SPIR-V itself rather than the reflection, so a stage from the shader cache
    /// answers as a stage compiled now does.
    /// </remarks>
    public int InputLocations(ShaderStage stage) => Stages.TryGetValue(stage, out var spirv) ? InputLocations(spirv) : 0;

    internal static int InputLocations(ReadOnlySpan<byte> spirv) => Locations(spirv, storageClass: 1);

    /// <summary>One past the highest location a stage writes an output at, for a fragment stage the color attachments it writes, or 0 for one that writes none.</summary>
    internal static int OutputLocations(ReadOnlySpan<byte> spirv) => Locations(spirv, storageClass: 3);

    /// <summary>The locations a stage takes inputs at, so a pipeline feeds those alone.</summary>
    internal static IReadOnlySet<int> InputLocationSet(ReadOnlySpan<byte> spirv) => LocationSet(spirv, storageClass: 1);

    /// <summary>
    /// The fragment stage <paramref name="spirv"/> with the color it writes at location 0 held to
    /// what an eight-bit frame keeps of a color before blending, its alpha between 0 and 1 and each
    /// channel no less than 0, light past 1 kept, or the stage as it is where that color is not a
    /// float4 stored whole.
    /// </summary>
    /// <remarks>
    /// A shader of a program's own drawn into the window's HDR frame blends there as it would in an
    /// eight-bit frame, as raylib's do, so an alpha past 1, as a gamma correction raised to the
    /// alpha too gives, does not push the color under it below 0. Each store into the output is
    /// put through GLSL.std.450's FClamp between (0, 0, 0, 0) and (max, max, max, 1).
    /// </remarks>
    internal static byte[] HeldToEightBits(ReadOnlySpan<byte> spirv)
    {
        const uint magic = 0x07230203, opExtInstImport = 11, opExtInst = 12, opMemoryModel = 14, opTypeFloat = 22, opTypeVector = 23,
            opTypePointer = 32, opConstant = 43, opConstantComposite = 44, opVariable = 59, opStore = 62, opCopyMemory = 63,
            opAccessChain = 65, opInBoundsAccessChain = 66, opPtrAccessChain = 67, opDecorate = 71, location = 30, output = 3, fClamp = 43;
        var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(spirv);
        if (words.Length < 5 || words[0] != magic) return spirv.ToArray();

        uint glsl = 0, float32 = 0, vector4 = 0;
        int vectorAt = -1, memoryModelAt = -1;
        var pointers = new Dictionary<uint, (uint Class, uint Type)>();
        var variables = new List<(uint Id, uint Type)>();
        var locations = new Dictionary<uint, uint>();
        for (int at = 5, length; at < words.Length; at += length)
        {
            length = (int)(words[at] >> 16);
            if (length == 0 || length > words.Length - at) return spirv.ToArray();
            switch (words[at] & 0xffff)
            {
                case opExtInstImport when NameOf(words.Slice(at + 2, length - 2)) == "GLSL.std.450": glsl = words[at + 1]; break;
                case opMemoryModel: memoryModelAt = at; break;
                case opTypeFloat when words[at + 2] == 32: float32 = words[at + 1]; break;
                case opTypeVector when float32 != 0 && words[at + 2] == float32 && words[at + 3] == 4: (vector4, vectorAt) = (words[at + 1], at); break;
                case opTypePointer: pointers[words[at + 1]] = (words[at + 2], words[at + 3]); break;
                case opVariable when words[at + 3] == output: variables.Add((words[at + 2], words[at + 1])); break;
                case opDecorate when length >= 4 && words[at + 2] == location: locations[words[at + 1]] = words[at + 3]; break;
            }
        }
        var color = variables.FirstOrDefault(v => locations.TryGetValue(v.Id, out var l) && l == 0
                                                  && pointers.TryGetValue(v.Type, out var p) && p.Type == vector4 && vector4 != 0).Id;
        if (color == 0 || memoryModelAt < 0) return spirv.ToArray();

        // Only a color stored whole is held, so a stage that writes it a channel at a time is left as it is.
        var stores = new List<int>();
        for (int at = 5, length; at < words.Length; at += length)
        {
            length = (int)(words[at] >> 16);
            var opcode = words[at] & 0xffff;
            if (opcode is opAccessChain or opInBoundsAccessChain or opPtrAccessChain && words[at + 3] == color
                || opcode == opCopyMemory && words[at + 1] == color)
                return spirv.ToArray();
            if (opcode == opStore && words[at + 1] == color) stores.Add(at);
        }
        if (stores.Count == 0) return spirv.ToArray();

        var next = words[3];
        var import = glsl == 0;
        if (import) glsl = next++;
        uint zero = next++, one = next++, most = next++, low = next++, high = next++;
        var constants = new List<uint>
        {
            Instruction(opConstant, 4), float32, zero, BitConverter.SingleToUInt32Bits(0f),
            Instruction(opConstant, 4), float32, one, BitConverter.SingleToUInt32Bits(1f),
            Instruction(opConstant, 4), float32, most, BitConverter.SingleToUInt32Bits(float.MaxValue),
            Instruction(opConstantComposite, 7), vector4, low, zero, zero, zero, zero,
            Instruction(opConstantComposite, 7), vector4, high, most, most, most, one,
        };

        var held = new List<uint>(words.Length + 64) { words[0], words[1], words[2], 0, words[4] };
        for (int at = 5, length; at < words.Length; at += length)
        {
            length = (int)(words[at] >> 16);
            if (at == memoryModelAt && import)
            {
                // "GLSL.std.450" and its terminating zero in four words, as SPIR-V packs a string.
                held.Add(Instruction(opExtInstImport, 6));
                held.Add(glsl);
                var name = new byte[16];
                System.Text.Encoding.ASCII.GetBytes("GLSL.std.450").CopyTo(name, 0);
                held.AddRange(System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(name).ToArray());
            }
            if (stores.Contains(at))
            {
                var clamped = next++;
                held.AddRange([Instruction(opExtInst, 8), vector4, clamped, glsl, fClamp, words[at + 2], low, high]);
                held.Add(words[at]);
                held.Add(words[at + 1]);
                held.Add(clamped);
                for (int i = 3; i < length; i++) held.Add(words[at + i]);
                continue;
            }
            for (int i = 0; i < length; i++) held.Add(words[at + i]);
            if (at == vectorAt) held.AddRange(constants);
        }
        held[3] = next;
        return System.Runtime.InteropServices.MemoryMarshal.AsBytes(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(held)).ToArray();

        static uint Instruction(uint opcode, uint length) => length << 16 | opcode;
        static string NameOf(ReadOnlySpan<uint> operands)
        {
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(operands);
            var end = bytes.IndexOf((byte)0);
            return System.Text.Encoding.ASCII.GetString(end < 0 ? bytes : bytes[..end]);
        }
    }

    // One past the highest location of a variable of the storage class, Input or Output.
    private static int Locations(ReadOnlySpan<byte> spirv, uint storageClass) => LocationSet(spirv, storageClass).DefaultIfEmpty(-1).Max() + 1;

    // The locations of the variables of the storage class that have one, which a built-in lacks.
    private static HashSet<int> LocationSet(ReadOnlySpan<byte> spirv, uint storageClass)
    {
        const uint magic = 0x07230203, opVariable = 59, opDecorate = 71, location = 30;
        var words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(spirv);
        if (words.Length < 5 || words[0] != magic) return [];
        var locations = new Dictionary<uint, uint>();
        var inputs = new List<uint>();
        // After the five words of the header, each instruction's first word holds its length in
        // words above its opcode.
        for (int at = 5, length; at < words.Length; at += length)
        {
            length = (int)(words[at] >> 16);
            if (length == 0 || length > words.Length - at) break;
            var opcode = words[at] & 0xffff;
            if (opcode == opDecorate && length >= 4 && words[at + 2] == location) locations[words[at + 1]] = words[at + 3];
            else if (opcode == opVariable && length >= 4 && words[at + 3] == storageClass) inputs.Add(words[at + 2]);
        }
        return [.. inputs.Where(locations.ContainsKey).Select(id => (int)locations[id])];
    }
}
