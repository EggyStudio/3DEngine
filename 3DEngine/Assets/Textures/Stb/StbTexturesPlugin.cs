namespace Engine;

/// <summary>
/// Plugin that brings up the StbImageSharp backend for the texture-decode system.
/// Registers <see cref="StbTextureDecoder"/> with the
/// <see cref="TextureDecoderRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// Pulled in automatically by <see cref="TexturesPlugin"/>; standalone consumers can
/// still add it directly if they want StbImageSharp coverage without the rest of the
/// textures aggregator (uncommon).
/// </para>
/// </remarks>
/// <seealso cref="TexturesPlugin"/>
/// <seealso cref="StbTextureDecoder"/>
public sealed class StbTexturesPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Textures.Stb");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("StbTexturesPlugin: Initialising StbImageSharp backend...");

        var registry = app.World.Resource<TextureDecoderRegistry>();

        var decoder = new StbTextureDecoder();
        registry.RegisterDecoder(decoder);

        Logger.Info(
            $"StbTexturesPlugin: StbImageSharp backend ready ({decoder.Extensions.Length} extension(s): " +
            string.Join(", ", decoder.Extensions) + ").");
    }
}