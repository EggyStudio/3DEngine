using System.Numerics;

namespace Engine;

/// <summary>
/// Convenience helpers for loading and spawning <i>model</i> files (FBX, OBJ, COLLADA,
/// glTF, ...). A model loads as a <see cref="SceneAsset"/> through the
/// <see cref="ISceneReader"/> registry, as any scene does, so these methods are aliases of
/// <see cref="SceneSpawnExtensions"/> that read better where the asset is a model.
/// </summary>
/// <remarks>
/// <para>
/// Every model format resolves to <c>AssimpModelLoader</c>, which produces a
/// <see cref="SceneAsset"/>, so any <see cref="SceneSpawnExtensions"/> helper accepts model
/// paths too.
/// </para>
/// </remarks>
/// <example>
/// <para>From a behavior, a model file is loaded and spawned in one call:</para>
/// <code>
/// [Behavior]
/// public struct HeroSpawnTest
/// {
///     [OnStartup]
///     public static void Start(BehaviorContext ctx)
///     {
///         ctx.SpawnModel("characters/hero.glb");
///     }
/// }
/// </code>
/// <para>With a placement helper:</para>
/// <code>
/// ctx.SpawnModel("vehicles/tank.fbx", ModelSpawn.At(new Vector3(5, 0, 0)));
/// </code>
/// <para>Loaded and not spawned, to be spawned later and more than once:</para>
/// <code>
/// var modelHandle = ctx.LoadModel("environment/rock.obj");
/// // ...later...
/// ctx.Cmd.SpawnScene(modelHandle, ModelSpawn.At(position));
/// </code>
/// </example>
/// <seealso cref="SceneSpawnExtensions"/>
/// <seealso cref="ModelsPlugin"/>
/// <seealso cref="TextureLoadExtensions"/>
public static class ModelLoadExtensions
{
    // -- Loading shortcuts (SceneAsset under the hood, "Model" naming for clarity)

    /// <summary>Loads a model file as a <see cref="SceneAsset"/>. Shorthand for <c>server.Load&lt;SceneAsset&gt;(path)</c>.</summary>
    public static Handle<SceneAsset> LoadModel(this AssetServer server, string path) =>
        server.Load<SceneAsset>(path);

    /// <summary>Loads a model file through the world's <see cref="AssetServer"/>.</summary>
    public static Handle<SceneAsset> LoadModel(this World world, string path) =>
        world.Resource<AssetServer>().Load<SceneAsset>(path);

    /// <summary>Loads a model file through the behavior context's world.</summary>
    public static Handle<SceneAsset> LoadModel(this BehaviorContext ctx, string path) =>
        ctx.World.Resource<AssetServer>().Load<SceneAsset>(path);

    // -- Deferred spawn-on-load (driven by SceneSpawnSystem)

    /// <summary>
    /// Queues a deferred spawn: when <paramref name="handle"/> finishes loading,
    /// <see cref="SceneSpawnSystem"/> will materialize the model into ECS entities.
    /// Returns the same <see cref="EcsCommands"/> for fluent chaining.
    /// </summary>
    public static EcsCommands SpawnModel(this EcsCommands cmd, Handle<SceneAsset> handle, SceneSpawnSettings? settings = null) =>
        cmd.SpawnScene(handle, settings);

    /// <summary>
    /// One-call helper: loads <paramref name="path"/> through the <see cref="AssetServer"/>
    /// and queues a deferred spawn-on-load. Returns the load handle so callers can poll
    /// load state, attach to hot-reload events, or use it to look up the spawn record in
    /// <see cref="SpawnedScenes"/>.
    /// </summary>
    /// <param name="ctx">The behavior context.</param>
    /// <param name="path">Asset path resolvable by the <see cref="AssetServer"/>.</param>
    /// <param name="settings">
    /// Optional spawn-time policy. <c>null</c> applies <see cref="SceneSpawnSettings.Default"/>.
    /// Use the <see cref="ModelSpawn"/> helpers (e.g. <see cref="ModelSpawn.At(Vector3)"/>)
    /// for common one-liners.
    /// </param>
    public static Handle<SceneAsset> SpawnModel(this BehaviorContext ctx, string path, SceneSpawnSettings? settings = null) =>
        ctx.SpawnScene(path, settings);

    /// <summary>
    /// One-call helper for systems that hold a <see cref="World"/> and an
    /// <see cref="EcsCommands"/> directly (no <see cref="BehaviorContext"/>): loads the
    /// model and queues the spawn-on-load request.
    /// </summary>
    public static Handle<SceneAsset> SpawnModel(this World world, EcsCommands cmd, string path, SceneSpawnSettings? settings = null) =>
        world.SpawnScene(cmd, path, settings);

    // -- Synchronous (no-load) spawn from an in-memory Scene

    /// <summary>
    /// Spawns an in-memory <see cref="Scene"/> immediately (no asset/load step). Thin
    /// wrapper over <see cref="SceneSpawnExtensions.SpawnScene(EcsWorld, Scene, SceneSpawnSettings?, ulong)"/>
    /// for callers who already have a parsed <see cref="Scene"/> in hand (tests,
    /// generated content, runtime-imported buffers).
    /// </summary>
    public static List<int> SpawnModel(this EcsWorld ecs, Scene scene, SceneSpawnSettings? settings = null, ulong sceneAssetId = 0) =>
        ecs.SpawnScene(scene, settings, sceneAssetId);
}

/// <summary>
/// Static factory for the most common <see cref="SceneSpawnSettings"/> shapes when
/// spawning a model. It is <see cref="SceneSpawn"/> under a model's name, so a call to
/// <c>SpawnModel(...)</c> pairs with <c>ModelSpawn.At(...)</c>.
/// </summary>
/// <example>
/// <code>
/// ctx.SpawnModel("hero.glb",   ModelSpawn.At(new Vector3(0, 0, 0)));
/// ctx.SpawnModel("tank.fbx",   ModelSpawn.At(new Vector3(5, 0, 0), Quaternion.Identity));
/// ctx.SpawnModel("rock.obj",   ModelSpawn.With(Matrix4x4.CreateScale(2f)));
/// ctx.SpawnModel("level.gltf", ModelSpawn.WithPurposes(ScenePurposeMask.Render));
/// </code>
/// </example>
public static class ModelSpawn
{
    /// <inheritdoc cref="SceneSpawn.At(Vector3)"/>
    public static SceneSpawnSettings At(Vector3 position) => SceneSpawn.At(position);

    /// <inheritdoc cref="SceneSpawn.At(Vector3, Quaternion)"/>
    public static SceneSpawnSettings At(Vector3 position, Quaternion rotation) => SceneSpawn.At(position, rotation);

    /// <inheritdoc cref="SceneSpawn.With(Matrix4x4)"/>
    public static SceneSpawnSettings With(Matrix4x4 placement) => SceneSpawn.With(placement);

    /// <inheritdoc cref="SceneSpawn.WithPurposes(ScenePurposeMask)"/>
    public static SceneSpawnSettings WithPurposes(ScenePurposeMask purposes) => SceneSpawn.WithPurposes(purposes);
}