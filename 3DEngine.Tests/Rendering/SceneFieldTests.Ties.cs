using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The field's paint where two meshes' faces stand in one plane looking the same way, every cell
// before them as near both, read as the frame's light run after run.
public sealed partial class SceneFieldTests
{
    // A floor slab with blocks of six colors sunk into its front, each block's front face in the
    // slab's, and a white ground before it the light bounced off them reaches, the light of each
    // pixel of the last frame at Low.
    private static float[] Tied()
    {
        var config = Config.Default.WithWindow("tied paint", 160, 90) with { Headless = true, Offscreen = true, Samples = 1, FrameSeconds = 1.0 / 60 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(GlobalIllumination.Low);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.6f)), Color.White, 2);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        Color[] colors = [new(230, 40, 30), new(40, 200, 60), new(30, 60, 230), new(230, 210, 40), new(200, 40, 200), new(40, 210, 210)];
        var camera = new Camera3D(new Vector3(0, 1.6f, 5.5f), new Vector3(0, 0.3f, 2), Vector3.UnitY, 60);
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 30; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.5f, 4), Vector3.UnitY, 0, new Vector3(12, 1, 4), Color.White);
            DrawModelEx(block, new Vector3(0, 0.5f, 0), Vector3.UnitY, 0, new Vector3(8, 1, 4), new Color(160, 160, 160));
            // Each block sinks half into the slab, its front face in the slab's front face at z 2.
            for (int i = 0; i < colors.Length; i++)
                DrawModelEx(block, new Vector3(i - 2.5f, 0.75f, 1.5f), Vector3.UnitY, 0, new Vector3(1, 1.5f, 1), colors[i]);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var light = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        UnloadModel(block);
        CloseWindow();
        UseApp(null);
        return light;
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Faces_Of_Two_Meshes_In_One_Plane_Paint_The_Field_The_Same_Run_After_Run()
    {
        // The cells before the slab's front lie as near its face as each block's, both looking the
        // same way, and a cell takes its paint from the one its word names, which ends in seven
        // bits of the mesh's own, so the device's order of its threads chooses nothing.
        var first = Tied();
        var second = Tied();
        var apart = first.Zip(second, (a, b) => MathF.Abs(a - b)).ToArray();
        apart.Count(d => d > 0).Should().Be(0, $"the frame reads the same in both runs, the most apart {apart.Max():0.000000}");
    }
}
