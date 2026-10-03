namespace Engine;

/// <summary>
/// <see cref="IAssetLoader{T}"/> for <see cref="Sound"/>. Single shared entry point for
/// every backend; dispatches to a concrete <see cref="ISoundDecoder"/> registered with
/// the <see cref="SoundDecoderRegistry"/> based on the file extension. Mirrors
/// <see cref="TextureAssetLoader"/>.
/// </summary>
public sealed class SoundAssetLoader : IAssetLoader<Sound>
{
    private readonly SoundDecoderRegistry _registry;
    private string[] _extensions;

    /// <summary>Creates a loader that dispatches to <paramref name="registry"/>.</summary>
    public SoundAssetLoader(SoundDecoderRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _extensions = registry.Extensions.ToArray();
    }

    /// <summary>Refreshes <see cref="Extensions"/> from the registry. Call after a new backend has been added.</summary>
    public void RefreshExtensions() => _extensions = _registry.Extensions.ToArray();

    /// <inheritdoc />
    public string[] Extensions => _extensions;

    /// <inheritdoc />
    public async Task<AssetLoadResult<Sound>> LoadAsync(AssetLoadContext context, CancellationToken ct)
    {
        try
        {
            var ext = context.Path.Extension;
            var decoder = _registry.FindDecoderByExtension(ext);
            if (decoder is null)
                return AssetLoadResult<Sound>.Fail(
                    $"SoundAssetLoader: no ISoundDecoder registered for extension '{ext}' (path: {context.Path}).");

            var sound = await decoder.DecodeAsync(context, ct);
            return AssetLoadResult<Sound>.Ok(sound);
        }
        catch (Exception ex)
        {
            return AssetLoadResult<Sound>.Fail(
                $"SoundAssetLoader: decode failed for '{context.Path}': {ex.Message}");
        }
    }
}