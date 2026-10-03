using System.Numerics;

namespace Engine;

/// <summary>
/// Convenience helpers that collapse the standard "load a scene asset → queue a deferred
/// spawn-on-load" boilerplate into single calls. The lower-level building blocks
/// (<see cref="AssetServer"/>, <see cref="EcsCommands"/>, <see cref="SpawnSceneRequest"/>,
/// <see cref="SceneSpawner"/>) remain available for callers who need fine-grained control.
/// </summary>
/// <example>
/// <para>From a behavior - one call replaces "load + Cmd.Spawn + Add SpawnSceneRequest":</para>
/// <code>
/// [Behavior]
/// public struct TeapotSceneTest
/// {
///     [OnStartup]
///     public static void Start(BehaviorContext ctx)
///     {
///         ctx.SpawnScene("teapot.usdz");
///     }
/// }
/// </code>
/// <para>With a custom placement:</para>
/// <code>
/// ctx.SpawnScene("teapot.usdz", SceneSpawn.At(new Vector3(0, 1, 0)));
/// </code>
/// <para>From a system that already holds an <see cref="EcsCommands"/>:</para>
/// <code>
/// var handle = world.LoadScene("teapot.usdz");
/// cmd.SpawnScene(handle);
/// </code>
/// </example>
/// <seealso cref="SpawnSceneRequest"/>
/// <seealso cref="SceneSpawner"/>
/// <seealso cref="SceneSpawnSettings"/>
public static class SceneSpawnExtensions
{
    // -- Loading shortcuts --

    /// <summary>Loads a <see cref="SceneAsset"/> via the <see cref="AssetServer"/>. Shorthand for <c>server.Load&lt;SceneAsset&gt;(path)</c>.</summary>
    public static Handle<SceneAsset> LoadScene(this AssetServer server, string path) =>
        server.Load<SceneAsset>(path);

    /// <summary>Loads a <see cref="SceneAsset"/> through the world's <see cref="AssetServer"/>.</summary>
    public static Handle<SceneAsset> LoadScene(this World world, string path) =>
        world.Resource<AssetServer>().Load<SceneAsset>(path);

    /// <summary>Loads a <see cref="SceneAsset"/> through the behavior context's world.</summary>
    public static Handle<SceneAsset> LoadScene(this BehaviorContext ctx, string path) =>
        ctx.World.Resource<AssetServer>().Load<SceneAsset>(path);

    // -- Deferred spawn-on-load (driven by SceneSpawnSystem) --

    /// <summary>
    /// Queues a deferred spawn: when <paramref name="handle"/> finishes loading,
    /// <see cref="SceneSpawnSystem"/> will materialize the scene into ECS entities.
    /// Returns the same <see cref="EcsCommands"/> for fluent chaining.
    /// </summary>
    public static EcsCommands SpawnScene(this EcsCommands cmd, Handle<SceneAsset> handle, SceneSpawnSettings? settings = null) =>
        cmd.Spawn((id, ecs) => ecs.Add(id, new SpawnSceneRequest { Handle = handle, Settings = settings }));

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
    /// Use the <see cref="SceneSpawn"/> helpers (e.g. <see cref="SceneSpawn.At(Vector3)"/>)
    /// for common one-liners.
    /// </param>
    public static Handle<SceneAsset> SpawnScene(this BehaviorContext ctx, string path, SceneSpawnSettings? settings = null)
    {
        var handle = ctx.LoadScene(path);
        ctx.Cmd.SpawnScene(handle, settings);
        return handle;
    }

    /// <summary>
    /// One-call helper for systems that hold a <see cref="World"/> and an
    /// <see cref="EcsCommands"/> directly (no <see cref="BehaviorContext"/>): loads the
    /// scene and queues the spawn-on-load request.
    /// </summary>
    public static Handle<SceneAsset> SpawnScene(this World world, EcsCommands cmd, string path, SceneSpawnSettings? settings = null)
    {
        var handle = world.LoadScene(path);
        cmd.SpawnScene(handle, settings);
        return handle;
    }

    // -- Synchronous (no-load) spawn from an in-memory Scene --

    /// <summary>
    /// Spawns an in-memory <see cref="Scene"/> into <paramref name="ecs"/> immediately
    /// (no asset/load step). Thin wrapper over <see cref="SceneSpawner.Spawn"/> for
    /// callers who already have a <see cref="Scene"/> in hand (tests, generated content,
    /// editor previews).
    /// </summary>
    public static List<int> SpawnScene(this EcsWorld ecs, Scene scene, SceneSpawnSettings? settings = null, ulong sceneAssetId = 0) =>
        SceneSpawner.Spawn(ecs, scene, settings, sceneAssetId);
}

/// <summary>
/// Static factory for the most common <see cref="SceneSpawnSettings"/> shapes. Use these
/// in place of inline <c>new SceneSpawnSettings { ... }</c> blocks to keep call sites short.
/// </summary>
/// <example>
/// <code>
/// ctx.SpawnScene("teapot.usdz", SceneSpawn.At(new Vector3(0, 1, 0)));
/// ctx.SpawnScene("level.usda",  SceneSpawn.With(Matrix4x4.CreateRotationY(MathF.PI)));
/// </code>
/// </example>
public static class SceneSpawn
{
    /// <summary>
    /// Settings that translate the spawned scene to <paramref name="position"/> in world
    /// space. All other fields default to <see cref="SceneSpawnSettings.Default"/>.
    /// </summary>
    public static SceneSpawnSettings At(Vector3 position) =>
        new() { Placement = Matrix4x4.CreateTranslation(position) };

    /// <summary>
    /// Settings that translate AND rotate the spawned scene. Equivalent to
    /// <c>With(rotation * translation(position))</c> in System.Numerics' row-vector convention.
    /// </summary>
    public static SceneSpawnSettings At(Vector3 position, Quaternion rotation) =>
        new() { Placement = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position) };

    /// <summary>Settings that apply an arbitrary placement matrix on top of the basis/unit conversion.</summary>
    public static SceneSpawnSettings With(Matrix4x4 placement) =>
        new() { Placement = placement };

    /// <summary>
    /// Settings that filter the spawned subtree by authoring purpose
    /// (e.g. <see cref="ScenePurposeMask.Render"/> only).
    /// </summary>
    public static SceneSpawnSettings WithPurposes(ScenePurposeMask purposes) =>
        new() { IncludePurposes = purposes };
}