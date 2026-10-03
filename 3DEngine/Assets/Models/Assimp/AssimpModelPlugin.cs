namespace Engine;

/// <summary>
/// Plugin that brings up the Open Asset Import Library backend for the model-import
/// system. Registers <see cref="AssimpModelLoader"/> with the <see cref="AssetServer"/>
/// and the matching <see cref="AssimpModelReader"/> with the
/// <see cref="SceneReaderRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coverage:</b> Assimp parses about forty formats, among them glTF, FBX, OBJ,
/// COLLADA (.dae), 3DS, BLEND, PLY, STL and X. The list comes from the native library at
/// runtime via <see cref="Assimp.AssimpContext.GetSupportedImportFormats"/>, and this plugin
/// registers the loader for every extension it advertises except the USD family.
/// </para>
/// <para>
/// <b>Order:</b> add <i>after</i> <see cref="ScenesPlugin"/> and <see cref="AssetPlugin"/>;
/// <see cref="ModelsPlugin"/> wires this up.
/// </para>
/// </remarks>
/// <seealso cref="ModelsPlugin"/>
/// <seealso cref="AssimpModelReader"/>
public sealed class AssimpModelPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Models.Assimp");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("AssimpModelPlugin: Initialising Assimp backend...");

        var reader = new AssimpModelReader();
        var loader = new AssimpModelLoader(reader);

        var registry = app.World.Resource<SceneReaderRegistry>();
        registry.RegisterReader(reader);

        var server = app.World.Resource<AssetServer>();
        server.RegisterLoader(loader);
        Logger.Debug($"AssimpModelPlugin: AssimpModelLoader registered with AssetServer for {loader.Extensions.Length} extensions.");

        Logger.Info("AssimpModelPlugin: Assimp backend ready.");
    }
}