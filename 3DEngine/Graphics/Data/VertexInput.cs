namespace Engine;

/// <summary>Vertex attribute data format for vertex input descriptions.</summary>
public enum VertexFormat
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
public enum IndexType
{
    /// <summary>16-bit unsigned integer indices.</summary>
    UInt16,
    /// <summary>32-bit unsigned integer indices.</summary>
    UInt32
}

/// <summary>Flags identifying shader stages for push constants and descriptor bindings.</summary>
[Flags]
public enum ShaderStageFlags
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
public readonly record struct VertexInputBindingDesc(uint Binding, uint Stride);

/// <summary>Describes a single vertex attribute within a binding.</summary>
/// <param name="Location">Shader attribute location.</param>
/// <param name="Binding">Vertex buffer binding slot.</param>
/// <param name="Format">Data format of the attribute.</param>
/// <param name="Offset">Byte offset within the vertex.</param>
public readonly record struct VertexInputAttributeDesc(uint Location, uint Binding, VertexFormat Format, uint Offset);

/// <summary>Describes a push constant range accessible from specified shader stages.</summary>
/// <param name="StageFlags">Shader stages that can access this range.</param>
/// <param name="Offset">Byte offset of the range.</param>
/// <param name="Size">Byte size of the range.</param>
public readonly record struct PushConstantRange(ShaderStageFlags StageFlags, uint Offset, uint Size);
