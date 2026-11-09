namespace Engine;

/// <summary>
/// The file and entry point each SPIR-V module the compiler gave was compiled from, by a hash of its
/// bytes, so a pipeline the driver makes is named in the log by the shaders it is made from, which
/// reach the device as bytes alone.
/// </summary>
internal static class ShaderNames
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<long, string> Names = new();

    /// <summary>Notes the file and entry point <paramref name="spirv"/> was compiled from, as <c>gi_merge.slang's computeMain</c>.</summary>
    internal static void Note(ReadOnlySpan<byte> spirv, string name) => Names[Key(spirv)] = name;

    /// <summary>The name <see cref="Note"/> gave the module, or one saying it came from elsewhere, as SPIR-V a program built in.</summary>
    internal static string Of(ReadOnlySpan<byte> spirv) => Names.TryGetValue(Key(spirv), out var name) ? name : "SPIR-V the compiler did not give";

    // The bytes' hash for this process with their length above it, the names kept being this
    // process's alone.
    private static long Key(ReadOnlySpan<byte> spirv)
    {
        var hash = new HashCode();
        hash.AddBytes(spirv);
        return (long)spirv.Length << 32 | (uint)hash.ToHashCode();
    }
}
