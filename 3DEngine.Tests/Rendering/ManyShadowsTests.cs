using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// More shadowed point and spot lights than the four the first layouts held, each beside a block,
/// read from the floor where each block's shadow falls.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class ManyShadowsTests : IDisposable
{
    private const int Lights = 6;
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-many-shadows-").FullName;
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(0, 16, 0.01f), Vector3.Zero, Vector3.UnitY, 60);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    private static float At(int i) => (i - (Lights - 1) / 2f) * 4;

    private Image Capture(Model floor, Model block)
    {
        var path = Path.Combine(_directory, $"{_captures++}.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawModel(floor, Vector3.Zero, 1, Color.White);
            for (int i = 0; i < Lights; i++) DrawModel(block, new Vector3(At(i), 0.5f, 0), 1, Color.White);
            EndMode3D();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    // Each light's shadow lands past its block, away from the light, where the floor is read far
    // enough out that no block's top, leaning outward in the camera's perspective, covers it.
    private void EachLightShadows(Func<int, bool, LightHandle> light)
    {
        var config = Config.Default.WithWindow("many shadows", 320, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var floor = LoadModelFromMesh(GenMeshPlane(30, 8, 1, 1));
        var block = LoadModelFromMesh(GenMeshCube(0.5f, 1, 0.5f));
        var lights = Enumerable.Range(0, Lights).Select(i => light(i, true)).ToList();

        var shadowed = Capture(floor, block);
        foreach (var made in lights) SetLightCastsShadows(made, false);
        var open = Capture(floor, block);

        for (int i = 0; i < Lights; i++)
        {
            var spot = GetWorldToScreenEx(new Vector3(At(i) + 1.1f, 0, 0), _camera, 320, 120);
            var (x, y) = ((int)spot.X, (int)spot.Y);
            GetImageColor(shadowed, x, y).R.Should().BeLessThan((byte)(GetImageColor(open, x, y).R * 0.6f),
                $"light {i + 1} of {Lights} shadows the floor past its block");
        }
        UnloadModel(floor);
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    public void Six_Point_Lights_Each_Cast_A_Shadow()
    {
        EachLightShadows((i, casts) => CreatePointLight(new Vector3(At(i) - 0.7f, 1.2f, 0), Color.White, 3, range: 2.5f, castsShadows: casts));
    }

    [NeedsVulkanFact]
    public void Six_Spot_Lights_Each_Cast_A_Shadow()
    {
        EachLightShadows((i, casts) => CreateSpotLight(new Vector3(At(i) - 0.7f, 1.2f, 0), new Vector3(0.6f, -1, 0), Color.White, 3,
            innerAngle: 40, outerAngle: 50, range: 3, castsShadows: casts));
    }
}
