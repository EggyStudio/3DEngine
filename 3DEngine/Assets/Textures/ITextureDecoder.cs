namespace Engine;

/// <summary>
/// Backend-agnostic decoder interface that converts a raw image stream into a
/// <see cref="Texture"/>. Implementations live in backend modules (e.g.
/// <c>StbTextureDecoder</c> in <c>Engine.Textures.Stb</c>; future <c>Ktx2TextureDecoder</c>
/// in <c>Engine.Textures.Ktx2</c>, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Decoders are invoked from <see cref="TextureAssetLoader"/> on a background worker
/// thread; they must not retain references to the input stream after returning.
/// </para>
/// <para>
/// <b>Color space:</b> decoders never apply color-space conversion to pixel data.
/// They may set <see cref="Texture.ColorSpace"/> from a hint passed in via
/// <see cref="TextureLoadSettings.ColorSpace"/> or
/// from format-native metadata (KTX2 / PNG sRGB chunk). Callers - typically
/// <c>SceneSpawner</c> when materialising a <see cref="SceneMaterialPayload"/> - decide
/// the semantic role of the texture.
/// </para>
/// </remarks>
/// <seealso cref="TextureDecoderRegistry"/>
/// <seealso cref="TextureAssetLoader"/>
public interface ITextureDecoder
{
    /// <summary>
    /// File extensions this decoder handles, including the leading dot (e.g.
    /// <c>[".png", ".jpg", ".jpeg", ".bmp", ".tga", ".hdr"]</c>).
    /// </summary>
    string[] Extensions { get; }

    /// <summary>Identifier used by <see cref="Texture.SourceFormat"/> (e.g. <c>"stb"</c>).</summary>
    string FormatId { get; }

    /// <summary>
    /// Decodes a texture from <paramref name="context"/>. Called on a background thread.
    /// </summary>
    Task<Texture> DecodeAsync(AssetLoadContext context, TextureLoadSettings settings, CancellationToken ct);
}

/// <summary>
/// Per-load decode hints forwarded by <see cref="TextureAssetLoader"/>.
/// </summary>
/// <remarks>
/// Today this only carries the color-space override that scene material binding needs
/// (BaseColor / Emissive should be sRGB; Normal / MetallicRoughness / Occlusion should be
/// Linear). Future fields: requested mip range, max-resolution clamp, anisotropy hint.
/// </remarks>
public sealed class TextureLoadSettings
{
    /// <summary>
    /// Override the decoded texture's <see cref="Texture.ColorSpace"/>. <c>null</c> lets
    /// the decoder pick (defaults to <see cref="TextureColorSpace.Linear"/> when no
    /// format-native hint is present).
    /// </summary>
    public TextureColorSpace? ColorSpace { get; init; }

    /// <summary>
    /// When <c>true</c>, the loader runs <see cref="TextureMipGenerator.WithMipChain"/>
    /// over the decoded base level to produce a complete mip chain. Defaults to
    /// <c>false</c> - the renderer is free to generate mips on upload, and not every
    /// texture wants them (UI / pixel art / single-sample lookup tables).
    /// </summary>
    public bool GenerateMips { get; init; }

    /// <summary>Reusable default settings (no overrides).</summary>
    public static TextureLoadSettings Default { get; } = new();
}

/// <summary>
/// World-resource registry that lets multiple <see cref="ITextureDecoder"/> backends
/// coexist behind one <see cref="TextureAssetLoader"/>. Mirrors
/// <see cref="SceneReaderRegistry"/>.
/// </summary>
/// <remarks>
/// Inserted into the <see cref="World"/> by <see cref="TexturesPlugin"/>. Backend plugins
/// (<see cref="StbTexturesPlugin"/>, future Ktx2 / Dds / Exr ones) call
/// <see cref="RegisterDecoder"/> during <see cref="IPlugin.Build"/>.
/// </remarks>
public sealed class TextureDecoderRegistry
{
    private readonly Dictionary<string, ITextureDecoder> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ITextureDecoder> _byFormat = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a decoder for all of its declared extensions and its format id.</summary>
    /// <remarks>
    /// Last-write wins per extension: a more capable backend (e.g. KTX2) registered after
    /// a generic one (e.g. Stb) takes precedence for any shared extensions.
    /// </remarks>
    public void RegisterDecoder(ITextureDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _byFormat[decoder.FormatId] = decoder;
        foreach (var ext in decoder.Extensions)
            _byExtension[ext] = decoder;
    }

    /// <summary>Looks up a decoder by file extension (e.g. <c>".png"</c>).</summary>
    public ITextureDecoder? FindDecoderByExtension(string extension)
        => _byExtension.TryGetValue(extension, out var d) ? d : null;

    /// <summary>Looks up a decoder by format id (e.g. <c>"stb"</c>).</summary>
    public ITextureDecoder? FindDecoderByFormat(string formatId)
        => _byFormat.TryGetValue(formatId, out var d) ? d : null;

    /// <summary>All currently registered extensions (for <see cref="TextureAssetLoader"/> wiring).</summary>
    public IReadOnlyCollection<string> Extensions => _byExtension.Keys;

    /// <summary>All currently registered decoders.</summary>
    public IReadOnlyCollection<ITextureDecoder> Decoders => _byFormat.Values;
}