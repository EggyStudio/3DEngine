using System.Numerics;
using Engine;

var config = Config.Default;
new App(config)
    .AddPlugin(new DefaultPlugins())
    .AddPlugin(new WebViewPlugin())
    // .AddPlugin(new EditorPlugin())
    .Run();

[Behavior]
public struct TriangleMeshTest
{
    [OnStartup]
    public static void Start(BehaviorContext ctx)
    {
        var camera = ctx.Ecs.Spawn();
        ctx.Ecs.Add(camera, new Camera(fovY: 60f, near: 0.1f, far: 1000f));
        ctx.Ecs.Add(camera, new Transform(new Vector3(0, 0, 5)));

        var mesh = ctx.Ecs.Spawn();
        ctx.Ecs.Add(mesh, new Mesh(new[] { new Vector3(0, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0) }));
        ctx.Ecs.Add(mesh, new Material(new Vector4(1, 1, 1, 1)));
        ctx.Ecs.Add(mesh, new Transform(Vector3.Zero));
    }
}

/// <summary>
/// Mirrors <see cref="TriangleMeshTest"/> but sources its geometry from the bundled
/// <c>teapot.usdz</c> asset instead of a hard-coded vertex list. Kicks off an async
/// <see cref="AssetServer"/> load on startup and, once the resulting <see cref="SceneAsset"/>
/// arrives, walks <see cref="SceneNode.Components"/> to spawn one ECS entity per mesh.
/// </summary>
/// <remarks>
/// The <c>UsdSceneReader</c> currently returns an empty <see cref="Scene"/> (stub), so this
/// behavior will load and traverse successfully but spawn no visible geometry until the
/// real USD prim traversal lands. The plumbing (handle, asset event, hot reload) is
/// exercised end-to-end in the meantime.
/// </remarks>
[Behavior]
public struct TeapotSceneTest
{
    public Handle<SceneAsset> Handle;
    public bool Spawned;

    [OnStartup]
    public static void Start(BehaviorContext ctx)
    {
        // Camera is already spawned by TriangleMeshTest; only request the scene asset here.
        // The behavior struct doubles as an ECS component carrying the in-flight handle,
        // so the [OnUpdate] instance method below runs per entity that owns it.
        var loader = ctx.Ecs.Spawn();
        ctx.Ecs.Add(loader, new TeapotSceneTest { Handle = ctx.Res<AssetServer>().Load<SceneAsset>("teapot.usdz") });
    }

    [OnUpdate]
    public void SpawnWhenLoaded(BehaviorContext ctx)
    {
        if (Spawned) return;

        // Assets<SceneAsset> is created lazily by AssetServer when the first SceneAsset
        // finishes loading; until then the resource simply isn't there yet.
        if (!ctx.World.TryGetResource<Assets<SceneAsset>>(out var assets)) return;
        if (!assets.TryGet(Handle, out var sceneAsset)) return;

        foreach (var node in sceneAsset.Scene.Traverse())
        {
            var positions = ExtractPositions(node);
            if (positions is null) continue;

            var entity = ctx.Ecs.Spawn();
            ctx.Ecs.Add(entity, new Mesh(positions));
            ctx.Ecs.Add(entity, new Material(new Vector4(1, 1, 1, 1)));
            ctx.Ecs.Add(entity, node.LocalTransform);
        }

        Spawned = true;
    }

    private static Vector3[]? ExtractPositions(SceneNode node)
    {
        // Backend-agnostic: any payload that exposes positions (e.g. a future
        // SceneMeshPayload from UsdSceneReader) gets translated to a runtime Mesh component.
        foreach (var component in node.Components)
        {
            if (component is Vector3[] positions) return positions;
            if (component is Mesh mesh) return mesh.Positions;
        }
        return null;
    }
}