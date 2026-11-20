using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The renderer's teardown while worker threads make a pass's pipelines, which are made of the
/// passes' shaders and layouts, so the teardown frees none of those until the workers are done.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class PipelineTeardownTests : IDisposable
{
    public void Dispose()
    {
        Logger.Heard = null;
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    public void A_Teardown_Frees_No_Pass_While_Worker_Threads_Make_Its_Pipelines()
    {
        // A plane's mirrored view, whose pipelines worker threads make after the frame that asks
        // for them, each held after its layout is made until the teardown either says it waits for
        // the workers or says the passes are freed, where the held ones stop short of the driver
        // rather than read shader modules freed under them.
        var config = Config.Default.WithWindow("teardown test", 160, 90) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.5f, -0.7f, -0.4f)), Color.White, 2.5f);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 2.2f, 7.5f), new Vector3(0, 0.4f, 0), Vector3.UnitY, 50);
        void Frame()
        {
            BeginDrawing();
            ClearBackground(new Color(120, 120, 120));
            BeginMode3D(camera);
            DrawModelEx(cube, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(14, 0.2f, 14), new Color(20, 20, 22));
            DrawModelEx(cube, new Vector3(0, 0.75f, 0), Vector3.UnitY, 0, new Vector3(1.5f, 1.5f, 1.5f), new Color(220, 40, 30));
            EndMode3D();
            EndDrawing();
        }
        Frame();
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        using var inside = new ManualResetEventSlim();
        using var gate = new ManualResetEventSlim();
        var freed = false;
        var freedFirst = false;
        Logger.Heard = (_, _, message) =>
        {
            if (message.StartsWith("Render graph nodes disposed", StringComparison.Ordinal))
                freed = true;
            if (freed || message.StartsWith("Waiting for the pipelines worker threads make", StringComparison.Ordinal))
                gate.Set();
        };
        device.WhileMakingPipeline = () =>
        {
            if (!Thread.CurrentThread.IsThreadPoolThread) return;
            inside.Set();
            gate.Wait(TimeSpan.FromSeconds(60));
            if (freed)
            {
                freedFirst = true;
                throw new InvalidOperationException("the passes were freed while their pipeline was being made");
            }
        };
        try
        {
            SetReflectionPlane(0, Vector3.UnitY);
            Frame();
            inside.Wait(TimeSpan.FromSeconds(60)).Should().BeTrue("a worker thread makes the mirrored view's pipelines");
            UnloadModel(cube);
            var closing = () => CloseWindow();
            closing.Should().NotThrow("the teardown waits for the pipelines being made before it frees the passes");
            freedFirst.Should().BeFalse("no pass is freed while a worker thread makes its pipeline");
        }
        finally
        {
            gate.Set();
            device.WhileMakingPipeline = null;
            Logger.Heard = null;
        }
    }
}
