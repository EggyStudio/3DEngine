using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>A morph target's weight moving a mesh on the GPU, read from the pixels it covers.</summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class MorphRenderTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-morph-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private Image Capture(Model model, Camera3D camera)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    [NeedsVulkanFact]
    public void A_Morph_Weight_Moves_The_Mesh_The_GPU_Draws()
    {
        var config = Config.Default.WithWindow("morph test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var model = LoadModel(Path.Combine(AppContext.BaseDirectory, "resources", "morph.gltf"));
        var camera = new Camera3D(new Vector3(0, 1, 5), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        var above = GetWorldToScreenEx(new Vector3(0, 1.6f, 0), camera, 160, 120);
        var (x, y) = ((int)above.X, (int)above.Y);

        var rest = Capture(model, camera);
        SetModelMorphWeight(model, "Raise", 1);
        var raised = Capture(model, camera);

        GetImageColor(rest, x, y).R.Should().BeLessThan(10, "at rest the strip ends a unit up, below the point");
        GetImageColor(raised, x, y).R.Should().BeGreaterThan(100, "raised it reaches two units up, over the point");
        UnloadModel(model);
    }
}
