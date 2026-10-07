namespace Engine;

/// <summary>Vertex attribute data format for vertex input descriptions.</summary>
internal enum VertexFormat
{
    /// <summary>Two 32-bit floats (VK_FORMAT_R32G32_SFLOAT).</summary>
    Float2,
    /// <summary>Three 32-bit floats (VK_FORMAT_R32G32B32_SFLOAT).</summary>
    Float3,
    /// <summary>Four 32-bit floats (VK_FORMAT_R32G32B32A32_SFLOAT).</summary>
    Float4,
    /// <summary>Four 8-bit unsigned normalized values (VK_FORMAT_R8G8B8A8_UNORM).</summary>
    UNormR8G8B8A8
}

/// <summary>Index buffer element type.</summary>
internal enum IndexType
{
    /// <summary>16-bit unsigned integer indices.</summary>
    UInt16,
    /// <summary>32-bit unsigned integer indices.</summary>
    UInt32
}

/// <summary>Flags identifying shader stages for push constants and descriptor bindings.</summary>
[Flags]
internal enum ShaderStageFlags
{
    /// <summary>Vertex shader stage.</summary>
    Vertex = 1,
    /// <summary>Fragment (pixel) shader stage.</summary>
    Fragment = 2,
    /// <summary>All shader stages.</summary>
    All = Vertex | Fragment
}

/// <summary>Describes a vertex buffer binding (stride and binding slot).</summary>
/// <param name="Binding">Binding slot index.</param>
/// <param name="Stride">Byte stride between consecutive vertices.</param>
/// <param name="PerInstance">Whether the binding steps once per instance rather than once per vertex.</param>
internal readonly record struct VertexInputBindingDesc(uint Binding, uint Stride, bool PerInstance = false);

/// <summary>Describes a single vertex attribute within a binding.</summary>
/// <param name="Location">Shader attribute location.</param>
/// <param name="Binding">Vertex buffer binding slot.</param>
/// <param name="Format">Data format of the attribute.</param>
/// <param name="Offset">Byte offset within the vertex.</param>
internal readonly record struct VertexInputAttributeDesc(uint Location, uint Binding, VertexFormat Format, uint Offset);

/// <summary>An attribute of one of the engine's vertex formats, with the semantic a shader names it by, as <c>NORMAL0</c>.</summary>
/// <param name="Semantic">The semantic, its name and number as one word.</param>
/// <param name="Attribute">The attribute, at the location the engine's own shaders read it at.</param>
internal readonly record struct VertexStream(string Semantic, VertexInputAttributeDesc Attribute)
{
    /// <summary>
    /// The attributes a vertex stage is fed: each stream the stage names by its semantic, at the
    /// location Slang gave it, where <paramref name="named"/> holds the stage's inputs, and where it
    /// holds none, each stream at its own location, those the stage reads where
    /// <paramref name="read"/> says which.
    /// </summary>
    /// <param name="streams">The streams of the pass's vertices.</param>
    /// <param name="named">The stage's inputs by semantic, empty where they are not known so.</param>
    /// <param name="read">The locations the stage reads, or null for every stream.</param>
    /// <param name="missing">The semantics the stage names that no stream gives, which it reads as zero.</param>
    public static VertexInputAttributeDesc[] Placed(IEnumerable<VertexStream> streams, IReadOnlyList<ShaderInput> named, IReadOnlySet<int>? read,
        out string[] missing)
    {
        missing = [];
        if (named.Count == 0)
            return [.. streams.Select(s => s.Attribute).Where(a => read is null || read.Contains((int)a.Location))];
        var given = streams.ToDictionary(s => s.Semantic, s => s.Attribute);
        missing = [.. named.Where(input => !given.ContainsKey(input.Semantic)).Select(input => input.Semantic)];
        return [.. named.Where(input => given.ContainsKey(input.Semantic)).Select(input => given[input.Semantic] with { Location = (uint)input.Location })];
    }
}

/// <summary>Describes a push constant range accessible from specified shader stages.</summary>
/// <param name="StageFlags">Shader stages that can access this range.</param>
/// <param name="Offset">Byte offset of the range.</param>
/// <param name="Size">Byte size of the range.</param>
internal readonly record struct PushConstantRange(ShaderStageFlags StageFlags, uint Offset, uint Size);
