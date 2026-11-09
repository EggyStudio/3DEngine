using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws a wall's shadow on a floor offscreen with the shadow map's texels larger than the pixels,
/// and reads how its edge changes from one pixel to the next.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ShadowEdgeTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-shadow-edge-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void A_Shadows_Edge_Across_Texels_Larger_Than_The_Pixels_Changes_Smoothly_From_Pixel_To_Pixel()
    {
        // A wall turned 30 degrees, so its shadow's edge crosses the map's texels on a slant, under a
        // sun 45 degrees up, in a map of 256 texels a tile, so a texel is some eight pixels across
        // the floor near the camera, and the floor read along a row across the edge. Compared at the
        // nine nearest texels the edge's light stood still across each texel and stepped at its side,
        // where weighed bilinearly at sixteen it ramps across the texel, and the bound on the
        // steepest step lies between the two.
        var config = Config.Default.WithWindow("shadow edge test", 480, 270) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetShadowMapSize(256);
        SetAmbientLight(new Color(255, 255, 255), 0.2f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -1, 0.2f)), Color.White, 2, castsShadows: true);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].Roughness = 1;
        var camera = new Camera3D(new Vector3(2.5f, 3, 1), new Vector3(2.5f, 0, 0), Vector3.UnitY, 45);
        var path = Path.Combine(_folder.Path, "edge.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(cube, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(30, 0.2f, 30), new Color(200, 200, 200));
            DrawModelEx(cube, new Vector3(0, 1.5f, 0), Vector3.UnitY, 30, new Vector3(0.3f, 3, 6), new Color(160, 80, 60));
            EndMode3D();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        var image = LoadImage(path);

        // Along each row the floor's green from one pixel to the next where it is between the
        // shadow's and the lit floor's, the largest step against the whole drop across the edge.
        var (dark, lit) = (255, 0);
        for (int y = 60; y < 210; y++)
            for (int x = 40; x < 440; x++)
            {
                var g = GetImageColor(image, x, y).G;
                (dark, lit) = (Math.Min(dark, g), Math.Max(lit, g));
            }
        (lit - dark).Should().BeGreaterThan(60, "the wall's shadow darkens the floor");
        var steepest = 0;
        for (int y = 60; y < 210; y++)
            for (int x = 40; x < 439; x++)
            {
                var (a, b) = (GetImageColor(image, x, y).G, GetImageColor(image, x + 1, y).G);
                if (Math.Min(a, b) > dark + 4 && Math.Max(a, b) < lit - 4) steepest = Math.Max(steepest, Math.Abs(a - b));
            }
        (steepest / (float)(lit - dark)).Should().BeLessThan(0.25f, $"the edge ramps over its texels rather than stepping at each, its steepest step {steepest} of a drop of {lit - dark}");
        UnloadModel(cube);
    }
}
