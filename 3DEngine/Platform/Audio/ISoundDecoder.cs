namespace Engine;

/// <summary>
/// Backend-agnostic sound decoder interface that converts a raw audio stream into a
/// decoded <see cref="Sound"/>. Mirrors <see cref="ITextureDecoder"/>: implementations
/// live in backend modules (or the capability module itself for trivial formats like
/// WAV); each advertises its file extensions and a stable format id.
/// </summary>
/// <seealso cref="SoundDecoderRegistry"/>
/// <seealso cref="SoundAssetLoader"/>
public interface ISoundDecoder
{
    /// <summary>File extensions this decoder handles, including the leading dot (e.g. <c>".wav"</c>).</summary>
    string[] Extensions { get; }

    /// <summary>Identifier used by <see cref="Sound.SourceFormat"/> (e.g. <c>"wav-builtin"</c>).</summary>
    string FormatId { get; }

    /// <summary>Decodes a sound from <paramref name="context"/>. Called on a background thread.</summary>
    Task<Sound> DecodeAsync(AssetLoadContext context, CancellationToken ct);
}

/// <summary>
/// World-resource registry that lets multiple <see cref="ISoundDecoder"/> backends
/// coexist behind one <see cref="SoundAssetLoader"/>. Mirrors
/// <see cref="TextureDecoderRegistry"/>.
/// </summary>
/// <remarks>
/// Inserted into the <see cref="World"/> by <see cref="SoundsPlugin"/>. Backend plugins
/// (built-in <c>WavSoundDecoder</c>; future Vorbis / Opus ones) call
/// <see cref="RegisterDecoder"/> during <see cref="IPlugin.Build"/>. Last-write wins
/// per extension so a more capable backend can override a generic one for shared
/// extensions.
/// </remarks>
public sealed class SoundDecoderRegistry
{
    private readonly Dictionary<string, ISoundDecoder> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISoundDecoder> _byFormat = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a decoder for all of its declared extensions and its format id.</summary>
    public void RegisterDecoder(ISoundDecoder decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _byFormat[decoder.FormatId] = decoder;
        foreach (var ext in decoder.Extensions)
            _byExtension[ext] = decoder;
    }

    /// <summary>Looks up a decoder by file extension (e.g. <c>".wav"</c>).</summary>
    public ISoundDecoder? FindDecoderByExtension(string extension)
        => _byExtension.TryGetValue(extension, out var d) ? d : null;

    /// <summary>Looks up a decoder by format id (e.g. <c>"wav-builtin"</c>).</summary>
    public ISoundDecoder? FindDecoderByFormat(string formatId)
        => _byFormat.TryGetValue(formatId, out var d) ? d : null;

    /// <summary>All currently registered extensions (for <see cref="SoundAssetLoader"/> wiring).</summary>
    public IReadOnlyCollection<string> Extensions => _byExtension.Keys;

    /// <summary>All currently registered decoders.</summary>
    public IReadOnlyCollection<ISoundDecoder> Decoders => _byFormat.Values;
}