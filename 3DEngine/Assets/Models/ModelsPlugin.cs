namespace Engine;

/// <summary>
/// Plugin for model import. Brings up the Assimp backend, so that any of the formats Assimp
/// reads (glTF, FBX, OBJ, COLLADA and about forty more) can be loaded as a
/// <see cref="SceneAsset"/> through the <see cref="AssetServer"/>.
/// </summary>
/// <remarks>
/// <para>
/// One importer covers every format, including glTF, so a model's meshes, materials and
/// texture paths arrive in the same payload shape whatever file they came from. The USD family
/// is refused (see <see cref="AssimpModelLoader"/>).
/// </para>
/// <para>
/// <b>Order:</b> add <i>after</i> <see cref="ScenesPlugin"/> and <see cref="AssetPlugin"/>;
/// <see cref="DefaultPlugins"/> wires this up. Standalone consumers can opt in:
/// <code>
/// app.AddPlugin(new ScenesPlugin())
///    .AddPlugin(new ModelsPlugin());
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="ScenesPlugin"/>
/// <seealso cref="AssimpModelPlugin"/>
public sealed class ModelsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Models");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("ModelsPlugin: Wiring model-import backends...");

        app.AddPlugin(new AssimpModelPlugin());

        Logger.Info("ModelsPlugin: Model backends ready.");
    }
}