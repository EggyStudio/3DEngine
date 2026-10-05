namespace Engine;

/// <summary>
/// Registers the user-facing materials subsystem, inserting a singleton
/// <see cref="MaterialLibrary"/> resource into the <see cref="App"/> world so any
/// downstream system, importer or behaviour can author and resolve materials through
/// the same engine-neutral API.
/// </summary>
/// <remarks>
/// <para>
/// Importers hand the materials they parse to the central library. To
/// override defaults, insert a <see cref="MaterialSettings"/> resource <i>before</i>
/// adding this plugin:
/// </para>
/// <example>
/// <code>
/// app.World.InsertResource(new MaterialSettings { DefaultDoubleSided = true });
/// app.AddPlugin(new MaterialPlugin());
/// </code>
/// </example>
/// </remarks>
/// <seealso cref="MaterialLibrary"/>
/// <seealso cref="MaterialDescription"/>
internal sealed class MaterialPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Materials");

    /// <inheritdoc />
    public void Build(App app)
    {
        if (app.World.ContainsResource<MaterialLibrary>())
        {
            Logger.Debug("MaterialPlugin: MaterialLibrary already present; skipping re-initialisation.");
            return;
        }

        var settings = app.World.GetOrInsertResource(() => new MaterialSettings());
        var library = new MaterialLibrary(settings);
        app.World.InsertResource(library);
        Logger.Info("MaterialPlugin: MaterialLibrary registered.");
    }
}