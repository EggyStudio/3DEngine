namespace Engine;

/// <summary>
/// Plugin that brings up the StbImageSharp backend for the texture-decode system.
/// Registers <see cref="StbTextureDecoder"/> with the
/// <see cref="TextureDecoderRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// Added by <see cref="TexturesPlugin"/>, and by itself by a program that decodes images
/// through StbImageSharp without the rest of that plugin, which few do.
/// </para>
/// </remarks>
/// <seealso cref="TexturesPlugin"/>
/// <seealso cref="StbTextureDecoder"/>
internal sealed class StbTexturesPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Textures.Stb");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("StbTexturesPlugin: Initializing StbImageSharp backend...");

        var registry = app.World.Resource<TextureDecoderRegistry>();

        var decoder = new StbTextureDecoder();
        registry.RegisterDecoder(decoder);

        Logger.Info(
            $"StbTexturesPlugin: StbImageSharp backend ready ({decoder.Extensions.Length} extension(s): " +
            string.Join(", ", decoder.Extensions) + ").");
    }
}