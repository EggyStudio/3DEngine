namespace Engine;

/// <summary>
/// Asset loader that compiles GLSL source files into SPIR-V bytecode using <see cref="GlslCompiler"/>.
/// Infers shader stage from the file name convention: <c>*.vert.glsl</c> → Vertex, <c>*.frag.glsl</c> → Fragment.
/// Always compiles from source - shaderc is fast for small shaders so no disk cache is needed.
/// </summary>
/// <example>
/// <code>
/// server.RegisterLoader(new GlslLoader());
/// Handle&lt;byte[]&gt; vs = server.Load&lt;byte[]&gt;("shaders/mesh.vert.glsl");
/// Handle&lt;byte[]&gt; fs = server.Load&lt;byte[]&gt;("shaders/mesh.frag.glsl");
/// </code>
/// </example>
/// <seealso cref="GlslCompiler"/>
/// <seealso cref="IAssetLoader{T}"/>
public sealed class GlslLoader : IAssetLoader<byte[]>
{
    /// <inheritdoc />
    public string[] Extensions => [".glsl"];

    /// <inheritdoc />
    public async Task<AssetLoadResult<byte[]>> LoadAsync(AssetLoadContext context, CancellationToken ct)
    {
        var source = await context.ReadAllTextAsync(ct);
        var fileName = context.Path.FileName;
        var stage = InferStage(fileName);

        try
        {
            var bytecode = GlslCompiler.Compile(source, fileName, stage);
            return AssetLoadResult<byte[]>.Ok(bytecode);
        }
        catch (Exception ex)
        {
            return AssetLoadResult<byte[]>.Fail($"GLSL compilation failed for '{fileName}': {ex.Message}");
        }
    }

    private static ShaderStage InferStage(string fileName)
    {
        if (fileName.Contains(".vert.", StringComparison.OrdinalIgnoreCase))
            return ShaderStage.Vertex;
        if (fileName.Contains(".frag.", StringComparison.OrdinalIgnoreCase))
            return ShaderStage.Fragment;
        // Default to vertex if ambiguous - caller should use .vert.glsl / .frag.glsl naming
        return ShaderStage.Vertex;
    }
}
