namespace Engine;

/// <summary>
/// Context provided to <see cref="IAssetLoader{T}.LoadAsync"/> containing the byte stream,
/// asset path, and dependency-loading methods.
/// </summary>
/// <remarks>
/// <para>
/// Created by the <see cref="AssetServer"/> for each load request. The loader uses this to
/// read the raw bytes, discover the file extension, and load dependencies or sub-assets.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public async Task&lt;AssetLoadResult&lt;Texture&gt;&gt; LoadAsync(AssetLoadContext ctx, CancellationToken ct)
/// {
///     byte[] bytes = await ctx.ReadAllBytesAsync(ct);
///     var texture = DecodeImage(bytes);
///     return AssetLoadResult.Ok(texture);
/// }
/// </code>
/// </example>
/// <seealso cref="IAssetLoader{T}"/>
/// <seealso cref="AssetServer"/>
public sealed class AssetLoadContext : IDisposable
{
    private readonly Stream _stream;
    private readonly Func<AssetPath, AssetId> _loadDependency;

    /// <summary>The asset path being loaded.</summary>
    public AssetPath Path { get; }

    /// <summary>Creates a new load context.</summary>
    internal AssetLoadContext(Stream stream, AssetPath path, Func<AssetPath, AssetId> loadDependency)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _loadDependency = loadDependency;
        Path = path;
    }

    /// <summary>Returns the raw byte stream. The caller should NOT dispose this stream directly.</summary>
    /// <returns>The asset byte stream.</returns>
    public Stream GetStream() => _stream;

    /// <summary>Reads the entire stream into a byte array.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All bytes from the asset stream.</returns>
    public async Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default)
    {
        if (_stream is MemoryStream ms)
            return ms.ToArray();

        using var buffer = new MemoryStream();
        await _stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    /// <summary>Reads the entire stream as a UTF-8 string.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The asset contents as a string.</returns>
    public async Task<string> ReadAllTextAsync(CancellationToken ct = default)
    {
        using var reader = new StreamReader(_stream, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    /// <summary>
    /// Declares a dependency on another asset. The dependency is tracked by the <see cref="AssetServer"/>
    /// and must be loaded before <see cref="AssetEventKind.LoadedWithDependencies"/> fires.
    /// </summary>
    /// <param name="dependencyPath">The asset path of the dependency.</param>
    /// <returns>The <see cref="AssetId"/> of the dependency (may still be loading).</returns>
    public AssetId LoadDependency(AssetPath dependencyPath) => _loadDependency(dependencyPath);

    /// <inheritdoc />
    public void Dispose() => _stream.Dispose();
}
