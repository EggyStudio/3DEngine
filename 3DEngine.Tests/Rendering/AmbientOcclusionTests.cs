using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Ambient occlusion, read from chosen pixels of frames drawn offscreen: a cube on a floor lit by
/// ambient light and a weak sun, in the corner of two walls, whose floor darkens beside the cube's
/// foot and in the corner and nowhere far from them, in the window and in a render texture alike,
/// and the frame's passes for it, which the profile names one by one.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class AmbientOcclusionTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-ao-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(2, 3, 5), new Vector3(-1, 0.5f, -1), Vector3.UnitY, 45);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("ambient occlusion test", 320, 240) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames of the cube on its floor, into the window or into a render texture of the
    // window's size shown over it, the last of them captured.
    private Image Capture(Model floor, Model cube, int frames = 4, RenderTexture2D? into = null)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < frames + 10 && !File.Exists(path); frame++)
        {
            if (into is { } texture)
            {
                BeginTextureMode(texture);
                Scene(floor, cube);
                EndTextureMode();
                BeginDrawing();
                ClearBackground(Color.Black);
                DrawTexture(texture.Texture, 0, 0, Color.White);
            }
            else
            {
                BeginDrawing();
                Scene(floor, cube);
            }
            if (frame == frames - 1) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        return LoadImage(path);
    }

    private void Scene(Model floor, Model cube)
    {
        ClearBackground(Color.Black);
        BeginMode3D(_camera);
        DrawModel(floor, Vector3.Zero, 1, Color.White);
        DrawModel(cube, new Vector3(0, 0.5f, 0), 1, Color.White);
        DrawModelEx(cube, new Vector3(-1.5f, 1, -1.5f), Vector3.UnitY, 0, new Vector3(5, 2, 0.2f), Color.White);
        DrawModelEx(cube, new Vector3(-3.9f, 1, 0.9f), Vector3.UnitY, 0, new Vector3(0.2f, 2, 5), Color.White);
        EndMode3D();
    }

    private static int Sum(Color c) => c.R + c.G + c.B;

    private Color At(Image image, Vector3 world)
    {
        var p = GetWorldToScreen(world, _camera);
        return GetImageColor(image, (int)p.X, (int)p.Y);
    }

    // The floor a tenth of a unit in front of the cube, the floor in the walls' corner, and the
    // floor a unit and a half from anything.
    private static readonly Vector3 Beside = new(0, 0, 0.6f), Corner = new(-3.7f, 0, -1.3f), Far = new(2.2f, 0, 0.3f);

    [NeedsVulkanFact]
    public void The_Floor_Darkens_Beside_A_Cube_And_In_A_Corner_And_Not_Far_From_Them()
    {
        Open();
        SetAmbientLight(Color.White, 0.6f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, -0.3f)), Color.White, 0.6f);
        var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var without = Capture(floor, cube);
        SetAmbientOcclusion(1);
        var with = Capture(floor, cube);

        Sum(At(with, Beside)).Should().BeLessThan(Sum(At(without, Beside)) - 30, "the cube closes off the floor at its foot");
        Sum(At(with, Corner)).Should().BeLessThan(Sum(At(without, Corner)) - 30, "and the walls the floor in their corner");
        Sum(At(with, Far)).Should().BeInRange(Sum(At(without, Far)) - 6, Sum(At(without, Far)) + 6, "nothing is near the floor out in the open");

        // Drawn through the HDR frame, which the model pass leaves its light linear for, it darkens alike.
        SetBloom(0.3f);
        var hdrWith = Capture(floor, cube);
        SetAmbientOcclusion(0);
        var hdrWithout = Capture(floor, cube);
        Sum(At(hdrWith, Beside)).Should().BeLessThan(Sum(At(hdrWithout, Beside)) - 30, "the HDR frame is darkened beside the cube too");
        UnloadModel(floor);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_Drawn_Through_A_Camera_Darkens_As_The_Window_Does()
    {
        Open();
        SetAmbientLight(Color.White, 0.6f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, -0.3f)), Color.White, 0.6f);
        var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var texture = LoadRenderTexture(GetScreenWidth(), GetScreenHeight());
        var without = Capture(floor, cube, into: texture);
        SetAmbientOcclusion(1);
        var window = Capture(floor, cube);
        var drawn = Capture(floor, cube, into: texture);

        foreach (var (place, name) in new[] { (Beside, "beside the cube"), (Corner, "in the walls' corner") })
        {
            Sum(At(drawn, place)).Should().BeLessThan(Sum(At(without, place)) - 30, $"the render texture's floor {name} is closed off");
            Sum(At(drawn, place)).Should().BeInRange(Sum(At(window, place)) - 9, Sum(At(window, place)) + 9,
                $"and darkens {name} as the window's does, {At(drawn, place)} against {At(window, place)}");
        }
        Sum(At(drawn, Far)).Should().BeInRange(Sum(At(without, Far)) - 6, Sum(At(without, Far)) + 6, "and not out in the open");
        UnloadRenderTexture(texture);
        UnloadModel(floor);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void The_Window_Gathers_Its_Batches_Draws_Its_Depth_And_Works_Out_Its_Occlusion_In_Nodes_Of_Their_Own()
    {
        Open();
        SetAmbientLight(Color.White, 0.6f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, -0.3f)), Color.White, 0.6f);
        var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        SetAmbientOcclusion(1);
        Capture(floor, cube);

        // The profile's cpu and gpu lines are the nodes', so the gather, the depth and the occlusion
        // each read apart, in the order they run.
        var nodes = renderer.Timings.NodeCpu.Select(n => n.Node).ToList();
        nodes.Should().ContainInOrder("model_batches", "window_depth", "ambient_occlusion", "global_illumination");
        renderer.RenderWorld.TryGet<WindowDepth>().Should().NotBeNull("the occlusion reads the depth");
        renderer.RenderWorld.TryGet<AmbientOcclusionImage>().Should().NotBeNull();

        // With nothing to read it, no field for the sun's contact shadows and no light bouncing,
        // neither is drawn.
        SetAmbientOcclusion(0);
        Capture(floor, cube);
        renderer.RenderWorld.TryGet<WindowDepth>().Should().BeNull("nothing reads the depth");
        renderer.RenderWorld.TryGet<AmbientOcclusionImage>().Should().BeNull();
        UnloadModel(floor);
        UnloadModel(cube);
    }
}
