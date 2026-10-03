namespace Engine;

/// <summary>
/// Defines a transformation step in the asset processing pipeline. Processors run on
/// source assets to produce optimized versions (e.g. texture compression, mesh optimization).
/// </summary>
/// <remarks>
/// <para>
/// Processors are registered with the <see cref="AssetProcessorPipeline"/> and matched by
/// file extension. They read source assets, transform them, and write the result to the
/// processed output directory.
/// </para>
/// <para>
/// <c>AssetProcessor</c> - an offline/background pipeline that
/// converts source assets into optimized runtime formats.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class TextureCompressor : IAssetProcessor
/// {
///     public string[] Extensions => [".png", ".jpg"];
///     public string OutputExtension => ".ktx2";
///
///     public async Task&lt;ProcessResult&gt; ProcessAsync(ProcessContext ctx, CancellationToken ct)
///     {
///         var compressed = CompressToKtx2(ctx.SourceBytes);
///         return ProcessResult.Ok(compressed);
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="AssetProcessorPipeline"/>
public interface IAssetProcessor
{
    /// <summary>Source file extensions this processor handles.</summary>
    string[] Extensions { get; }

    /// <summary>
    /// The output extension for processed assets. E.g. <c>".ktx2"</c> for compressed textures.
    /// If <c>null</c>, the original extension is preserved.
    /// </summary>
    string? OutputExtension => null;

    /// <summary>Processes a source asset into an optimized form.</summary>
    /// <param name="context">The processing context with source bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The processing result with output bytes.</returns>
    Task<ProcessResult> ProcessAsync(ProcessContext context, CancellationToken ct);
}

/// <summary>Context provided to <see cref="IAssetProcessor.ProcessAsync"/>.</summary>
/// <seealso cref="IAssetProcessor"/>
public sealed class ProcessContext
{
    /// <summary>The source asset path (relative).</summary>
    public AssetPath SourcePath { get; }

    /// <summary>The raw source bytes.</summary>
    public byte[] SourceBytes { get; }

    /// <summary>Creates a new processing context.</summary>
    internal ProcessContext(AssetPath sourcePath, byte[] sourceBytes)
    {
        SourcePath = sourcePath;
        SourceBytes = sourceBytes;
    }

    /// <summary>Returns the source bytes as a read-only span.</summary>
    public ReadOnlySpan<byte> AsSpan() => SourceBytes;

    /// <summary>Returns the source bytes as a stream.</summary>
    public Stream AsStream() => new MemoryStream(SourceBytes, writable: false);
}

/// <summary>Result of an <see cref="IAssetProcessor.ProcessAsync"/> call.</summary>
/// <seealso cref="IAssetProcessor"/>
public sealed class ProcessResult
{
    /// <summary>Whether processing succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>The processed output bytes.</summary>
    public byte[]? OutputBytes { get; init; }

    /// <summary>Error message on failure.</summary>
    public string? Error { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static ProcessResult Ok(byte[] output) => new()
    {
        Success = true,
        OutputBytes = output,
    };

    /// <summary>Creates a failed result.</summary>
    public static ProcessResult Fail(string error) => new()
    {
        Success = false,
        Error = error,
    };
}
