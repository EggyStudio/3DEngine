namespace Engine;

/// <summary>
/// Backend-agnostic textures plugin. Registers the <see cref="TextureAsset"/> asset type, the
/// <see cref="TextureDecoderRegistry"/> resource, and a single shared
/// <see cref="TextureAssetLoader"/> with the <see cref="AssetServer"/>. Concrete backends
/// (e.g. <see cref="StbTexturesPlugin"/>) attach themselves to the registry during their
/// own <see cref="IPlugin.Build"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Split between the format-agnostic part and its decoders:</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     This plugin holds the <see cref="TextureAsset"/> asset, <see cref="ITextureDecoder"/>, the
///     registry and the loader, none of them native.
///   </description></item>
///   <item><description>
///     <see cref="StbTexturesPlugin"/> decodes through StbImageSharp, which reads PNG, JPEG,
///     BMP, TGA, PSD, GIF, HDR, PIC and PNM.
///   </description></item>
///   <item><description>
///     A decoder of another format registers for its own extensions, and when two decoders
///     claim one extension the last registered is used.
///   </description></item>
/// </list>
/// <para>
/// <b>Wiring:</b> add <i>after</i> <see cref="AssetPlugin"/>; <see cref="DefaultPlugins"/>
/// brings this up automatically. The plugin re-registers
/// <see cref="TextureAssetLoader"/> with the <see cref="AssetServer"/> after the bundled
/// backends are wired so that the loader's
/// <see cref="TextureAssetLoader.Extensions"/> array reflects every extension the active
/// backends advertise.
/// </para>
/// </remarks>
/// <seealso cref="ITextureDecoder"/>
/// <seealso cref="TextureDecoderRegistry"/>
/// <seealso cref="StbTexturesPlugin"/>
internal sealed class TexturesPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Textures");

    /// <inheritdoc />
    /// <remarks>
    /// Foundational for backend texture decoders (e.g. <c>StbTexturesPlugin</c>) that
    /// register with the <see cref="TextureDecoderRegistry"/> created here. Built
    /// between <see cref="PluginOrder.Foundation"/> (asset pipeline) and
    /// <see cref="PluginOrder.Default"/>.
    /// </remarks>
    public int Order => PluginOrder.Foundation + 100;

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("TexturesPlugin: Registering texture model (backend-agnostic)...");

        var registry = new TextureDecoderRegistry();
        app.World.InsertResource(registry);

        // Bring up the default backends so consumers get StbImageSharp coverage out of the box.
        app.AddPlugin(new StbTexturesPlugin());

        // After backends register their decoders, register one shared loader for all
        // accumulated extensions. Re-syncing after each new backend is supported via
        // TextureAssetLoader.RefreshExtensions + a re-call to RegisterLoader.
        var loader = new TextureAssetLoader(registry);
        app.World.InsertResource(loader);

        var server = app.World.Resource<AssetServer>();
        server.RegisterLoader(loader);
        Logger.Info(
            $"TexturesPlugin: TextureAssetLoader registered with AssetServer for {loader.Extensions.Length} extension(s): " +
            string.Join(", ", loader.Extensions));

        Logger.Info("TexturesPlugin: Texture pipeline ready.");
    }
}