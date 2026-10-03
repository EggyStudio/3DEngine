namespace Engine;

/// <summary>Flags describing how a GPU buffer will be used.</summary>
[Flags]
public enum BufferUsage
{
    /// <summary>No usage flags set.</summary>
    None = 0,
    /// <summary>Buffer can be bound as a vertex buffer.</summary>
    Vertex = 1 << 0,
    /// <summary>Buffer can be bound as an index buffer.</summary>
    Index = 1 << 1,
    /// <summary>Buffer can be bound as a uniform (constant) buffer.</summary>
    Uniform = 1 << 2,
    /// <summary>Buffer can be used as a transfer source.</summary>
    TransferSrc = 1 << 3,
    /// <summary>Buffer can be used as a transfer destination.</summary>
    TransferDst = 1 << 4,
    /// <summary>Buffer is used for CPU→GPU staging transfers.</summary>
    Staging = 1 << 5
}

/// <summary>CPU access mode for buffer memory mapping.</summary>
public enum CpuAccessMode
{
    /// <summary>No CPU access (GPU-only memory).</summary>
    None,
    /// <summary>CPU can read from the buffer.</summary>
    Read,
    /// <summary>CPU can write to the buffer.</summary>
    Write,
    /// <summary>CPU can both read and write the buffer.</summary>
    ReadWrite
}

/// <summary>Descriptor for creating a GPU buffer.</summary>
/// <param name="Size">Size in bytes.</param>
/// <param name="Usage">Usage flags.</param>
/// <param name="CpuAccess">CPU access mode for mapping.</param>
public readonly record struct BufferDesc(ulong Size, BufferUsage Usage, CpuAccessMode CpuAccess = CpuAccessMode.None);
