namespace Engine;

/// <summary>
/// Built-in asset loader that reads files as raw <c>byte[]</c> arrays.
/// Useful for binary assets, SPIR-V bytecode, or any opaque data.
/// </summary>
/// <example>
/// <code>
/// server.RegisterLoader(new ByteArrayLoader());
/// Handle&lt;byte[]&gt; data = server.Load&lt;byte[]&gt;("data/binary.bin");
/// </code>
/// </example>
/// <seealso cref="IAssetLoader{T}"/>
/// <seealso cref="StringLoader"/>
public sealed class ByteArrayLoader : IAssetLoader<byte[]>
{
    /// <inheritdoc />
    public string[] Extensions { get; }

    /// <summary>
    /// Creates a new <see cref="ByteArrayLoader"/> with the specified extensions.
    /// Defaults to common binary extensions.
    /// </summary>
    /// <param name="extensions">File extensions to handle. If empty, uses defaults.</param>
    public ByteArrayLoader(params string[] extensions)
    {
        Extensions = extensions.Length > 0
            ? extensions
            : [".bin", ".dat", ".bytes", ".spv"];
    }

    /// <inheritdoc />
    public async Task<AssetLoadResult<byte[]>> LoadAsync(AssetLoadContext context, CancellationToken ct)
    {
        var bytes = await context.ReadAllBytesAsync(ct);
        return AssetLoadResult<byte[]>.Ok(bytes);
    }
}

/// <summary>
/// Built-in asset loader that reads files as UTF-8 <see cref="string"/>s.
/// Useful for text files, JSON, TOML, GLSL sources, scripts, etc.
/// </summary>
/// <example>
/// <code>
/// server.RegisterLoader(new StringLoader());
/// Handle&lt;string&gt; glsl = server.Load&lt;string&gt;("shaders/mesh.vert.glsl");
/// </code>
/// </example>
/// <seealso cref="IAssetLoader{T}"/>
/// <seealso cref="ByteArrayLoader"/>
public sealed class StringLoader : IAssetLoader<string>
{
    /// <inheritdoc />
    public string[] Extensions { get; }

    /// <summary>
    /// Creates a new <see cref="StringLoader"/> with the specified extensions.
    /// Defaults to common text extensions.
    /// </summary>
    /// <param name="extensions">File extensions to handle. If empty, uses defaults.</param>
    public StringLoader(params string[] extensions)
    {
        Extensions = extensions.Length > 0
            ? extensions
            : [".txt", ".json", ".toml", ".yaml", ".yml", ".xml", ".csv", ".hlsl", ".wgsl", ".html", ".css", ".js", ".md"];
    }

    /// <inheritdoc />
    public async Task<AssetLoadResult<string>> LoadAsync(AssetLoadContext context, CancellationToken ct)
    {
        var text = await context.ReadAllTextAsync(ct);
        return AssetLoadResult<string>.Ok(text);
    }
}
