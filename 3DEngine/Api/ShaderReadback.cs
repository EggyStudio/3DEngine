namespace Engine;

/// <summary>
/// A storage buffer's bytes asked for with <c>ReadShaderBufferAsync</c>, read once the GPU has copied
/// them, by its id.
/// </summary>
/// <param name="Id">The readback's id, 0 for none.</param>
/// <param name="Size">How many bytes it reads.</param>
public readonly record struct ShaderReadback(int Id, int Size)
{
    /// <summary>Whether this names a readback that was asked for.</summary>
    public bool IsValid => Id > 0;
}
