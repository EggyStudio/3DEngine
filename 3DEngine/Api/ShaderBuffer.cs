using System.Runtime.InteropServices;

namespace Engine;

/// <summary>A block of GPU memory a compute shader reads and writes, by its id.</summary>
/// <param name="Id">The buffer's id, 0 for none.</param>
/// <param name="Size">Its size in bytes.</param>
public readonly record struct ShaderBuffer(int Id, int Size)
{
    /// <summary>Whether this names a buffer that was loaded.</summary>
    public bool IsValid => Id > 0;
}
