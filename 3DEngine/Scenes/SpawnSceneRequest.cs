namespace Engine;

/// <summary>
/// Intent component: "spawn the scene behind <see cref="Handle"/> into the world as soon
/// as the asset finishes loading, then remove me." Add this to a freshly spawned entity
/// (typically alongside the request itself) and let <see cref="SceneSpawnSystem"/> drive
/// the rest.
/// </summary>
/// <remarks>
/// <para>
/// This is the request side of the standard "load, then spawn" workflow. The shortest
/// way to author it from a behavior is the <see cref="SceneSpawnExtensions"/> helpers:
/// <code>
/// // one-call: load + queue a SpawnSceneRequest
/// ctx.SpawnScene("scenes/teapot.usdz");
///
/// // with placement
/// ctx.SpawnScene("scenes/teapot.usdz", SceneSpawn.At(new Vector3(0, 1, 0)));
/// </code>
/// The verbose form is still supported when callers need full control:
/// <code>
/// var handle = server.Load&lt;SceneAsset&gt;("scenes/teapot.usdz");
/// ctx.Cmd.Spawn((id, ecs) =&gt; ecs.Add(id, new SpawnSceneRequest { Handle = handle }));
/// </code>
/// The driver system polls <c>Assets&lt;SceneAsset&gt;</c> until the handle resolves, calls
/// <see cref="SceneSpawner.Spawn"/>, and then removes this component (so the spawn happens
/// exactly once). The original request entity stays alive as a stable "owner" of the
/// spawned subtree - editor / hot-reload can find it via the handle id.
/// </para>
/// </remarks>
public struct SpawnSceneRequest
{
    /// <summary>The scene asset to spawn once it finishes loading.</summary>
    public Handle<SceneAsset> Handle;

    /// <summary>
    /// Optional spawn-time policy override. <c>null</c> uses
    /// <see cref="SceneSpawnSettings.Default"/>.
    /// </summary>
    public SceneSpawnSettings? Settings;
}