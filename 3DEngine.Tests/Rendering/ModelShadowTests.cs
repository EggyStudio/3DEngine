using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>A model's material deciding whether it casts a shadow, read from the floor under it.</summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class ModelShadowTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-shadow-").FullName;
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    // Draws frames until the first one's capture has been written, as ReferenceFrameTests does.
    private Image Capture(Action draw, string name)
    {
        var path = Path.Combine(_directory, name + ".png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    [NeedsVulkanFact]
    public void A_Material_That_Casts_No_Shadow_Leaves_The_Floor_Under_It_Lit()
    {
        var config = Config.Default.WithWindow("shadow test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(new Vector3(0.01f, -1, 0), Color.White, 1, castsShadows: true);
        var floor = LoadModelFromMesh(GenMeshPlane(10, 10, 1, 1));
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 16, 16));
        var camera = new Camera3D(new Vector3(0, 6, 4), Vector3.Zero, Vector3.UnitY, 45);
        Action Scene(bool casts) => () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(floor, Vector3.Zero, 1, Color.White);
            sphere.Materials[0] = new ModelMaterial(Color.White) { CastsShadows = casts };
            DrawModel(sphere, new Vector3(0, 1.5f, 0), 1, Color.White);
            EndMode3D();
        };

        var shadowed = Capture(Scene(casts: true), "casts");
        var lit = Capture(Scene(casts: false), "none");

        var under = GetWorldToScreen(Vector3.Zero, camera);
        var (x, y) = ((int)under.X, (int)under.Y);
        GetImageColor(shadowed, x, y).R.Should().BeLessThan((byte)(GetImageColor(lit, x, y).R - 60), "the sphere's shadow darkens the floor under it");
        GetImageColor(lit, x, y).R.Should().BeGreaterThan(150, "with no shadow the floor is lit by the sun");
        UnloadModel(floor);
        UnloadModel(sphere);
    }
}
