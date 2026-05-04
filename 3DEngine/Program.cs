using System.Numerics;
using Engine;

var config = Config.Default;
new App(config)
    .AddPlugin(new DefaultPlugins())
    .AddPlugin(new WebViewPlugin())
    // .AddPlugin(new EditorPlugin())
    .Run();

[Behavior]
public struct CameraTest
{
    [OnStartup]
    public static void Start(BehaviorContext ctx)
    {
        var camera = ctx.Ecs.Spawn();
        ctx.Ecs.Add(camera, new Camera(fovY: 60f, near: 0.1f, far: 1000f));
        ctx.Ecs.Add(camera, new Transform(new Vector3(0, 0, 1)));
    }
}

[Behavior]
public struct TriangleMeshTest
{
    [OnStartup]
    public static void Start(BehaviorContext ctx)
    {
        var mesh = ctx.Ecs.Spawn();
        ctx.Ecs.Add(mesh, new Mesh(new[] { new Vector3(0, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0) }));
        ctx.Ecs.Add(mesh, new Material(new Vector4(1, 1, 1, 1)));
        ctx.Ecs.Add(mesh, new Transform(Vector3.Zero, Vector3.One * 0.1f));
    }
}

[Behavior]
public struct TeapotSceneTest
{
    [OnStartup]
    public static void Start(BehaviorContext ctx)
    {
        ctx.SpawnScene("teapot.usdz");
    }
}