using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Ambient occlusion, read from chosen pixels of frames drawn offscreen: a cube on a floor lit by
/// ambient light and a weak sun, in the corner of two walls, whose floor darkens beside the cube's
/// foot and in the corner and nowhere far from them.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class AmbientOcclusionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-ao-").FullName;
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(2, 3, 5), new Vector3(-1, 0.5f, -1), Vector3.UnitY, 45);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("ambient occlusion test", 320, 240) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames of the cube on its floor, the last of them captured.
    private Image Capture(Model floor, Model cube, int frames = 4)
    {
        var path = Path.Combine(_directory, $"{_captures++}.png");
        for (int frame = 0; frame < frames + 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawModel(floor, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(0, 0.5f, 0), 1, Color.White);
            DrawModelEx(cube, new Vector3(-1.5f, 1, -1.5f), Vector3.UnitY, 0, new Vector3(5, 2, 0.2f), Color.White);
            DrawModelEx(cube, new Vector3(-3.9f, 1, 0.9f), Vector3.UnitY, 0, new Vector3(0.2f, 2, 5), Color.White);
            EndMode3D();
            if (frame == frames - 1) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        return LoadImage(path);
    }

    private static int Sum(Color c) => c.R + c.G + c.B;

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

        Color At(Image image, Vector3 world)
        {
            var p = GetWorldToScreen(world, _camera);
            return GetImageColor(image, (int)p.X, (int)p.Y);
        }
        // The floor a tenth of a unit in front of the cube, the floor in the walls' corner, and the
        // floor a unit and a half from anything.
        var beside = new Vector3(0, 0, 0.6f);
        var corner = new Vector3(-3.7f, 0, -1.3f);
        var far = new Vector3(2.2f, 0, 0.3f);
        Sum(At(with, beside)).Should().BeLessThan(Sum(At(without, beside)) - 30, "the cube closes off the floor at its foot");
        Sum(At(with, corner)).Should().BeLessThan(Sum(At(without, corner)) - 30, "and the walls the floor in their corner");
        Sum(At(with, far)).Should().BeInRange(Sum(At(without, far)) - 6, Sum(At(without, far)) + 6, "nothing is near the floor out in the open");

        // Drawn through the HDR frame, which the model pass leaves its light linear for, it darkens alike.
        SetBloom(0.3f);
        var hdrWith = Capture(floor, cube);
        SetAmbientOcclusion(0);
        var hdrWithout = Capture(floor, cube);
        Sum(At(hdrWith, beside)).Should().BeLessThan(Sum(At(hdrWithout, beside)) - 30, "the HDR frame is darkened beside the cube too");
        UnloadModel(floor);
        UnloadModel(cube);
    }
}
