namespace Engine;

/// <summary>
/// Backend-agnostic scene plugin. Registers the in-engine scene model
/// (<see cref="Scene"/>, <see cref="SceneNode"/>, <see cref="SceneAsset"/>) and a
/// <see cref="SceneReaderRegistry"/> resource that readers plug into.
/// </summary>
/// <remarks>
/// <para>
/// Each reader registers for its own file extensions and the registry dispatches by
/// extension. The Assimp model reader is the one the engine ships. <see cref="AssetServer"/>
/// creates <see cref="Assets{T}"/> on first load, so this plugin does not insert it.
/// </para>
/// </remarks>
/// <seealso cref="Scene"/>
/// <seealso cref="SceneAsset"/>
/// <seealso cref="ISceneReader"/>
/// <seealso cref="ISceneWriter"/>
internal sealed class ScenesPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes");

    /// <inheritdoc />
    /// <remarks>
    /// Foundational for the model-import backend (<c>AssimpModelPlugin</c>), which registers with the
    /// <see cref="SceneReaderRegistry"/> this plugin creates. Built between
    /// <see cref="PluginOrder.Foundation"/> (asset pipeline) and
    /// <see cref="PluginOrder.Default"/>.
    /// </remarks>
    public int Order => PluginOrder.Foundation + 100;

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("ScenesPlugin: Registering scene model (backend-agnostic)...");

        // Backend-agnostic registry. Backends call Register(...) from their own plugin
        // (such as the Assimp model reader) to opt-in their format support.
        app.World.InsertResource(new SceneReaderRegistry());

        // The spawned scenes, which SceneHotReloadSystem reads to find the entities to
        // despawn when a SceneAsset reloads.
        app.World.InsertResource(new SpawnedScenes());

        // Auto-spawn driver: turns SpawnSceneRequest components into ECS entities once
        // the underlying SceneAsset finishes loading. Runs in PreUpdate so spawned
        // entities are visible to gameplay systems in the same frame.
        // Model references first, so a scene file's models are asked for in the frame it loads.
        // Scene files a level places first, so the models they name are asked for in the same frame.
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(SceneRefSystem.Run, "SceneRefSystem").MainThreadOnly());
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(ModelRefSystem.Run, "ModelRefSystem").MainThreadOnly());
        // Both spawn and despawn entities, which the main thread does.
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(SceneSpawnSystem.Run, "SceneSpawnSystem").MainThreadOnly());

        // Hot-reload driver: watches AssetEvent<SceneAsset>.Modified and re-spawns the
        // tracked entity set in place. It runs in the spawn driver's stage, and asset events
        // last until Stage.Last, so the order between them does not matter.
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(SceneHotReloadSystem.Run, "SceneHotReloadSystem").MainThreadOnly());

        // What a level loaded through its references is let go some time after no entity uses it.
        app.World.InsertResource(new AssetRelease());
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(AssetRelease.Run, "AssetRelease").MainThreadOnly());

        Logger.Info("ScenesPlugin: Scene model ready.");
    }
}