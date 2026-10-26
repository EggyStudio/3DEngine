using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws a pole's shadow offscreen from above and reads how wide its edge is near the pole's foot
/// and near its tip, the sun given no width and given one (<see cref="E3D.SetShadowSoftness"/>).
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class ShadowSoftnessTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-softness-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void A_Poles_Shadow_Is_Sharper_At_Its_Foot_Than_At_Its_Tip_Where_The_Sun_Has_A_Width()
    {
        // A pole half a unit thick and five tall under a sun 45 degrees up, its shadow a strip
        // along x on the floor, read across at a unit from the pole, where the pole over it is a
        // unit tall, and at four and a half, where it is four and a half. With no width the edge
        // is a texel or so at both; the sun 3 degrees across spreads it by 0.05 a unit of height,
        // 0.24 units at the tip and 0.05 near the foot. Between a quarter and three quarters of
        // the light the edges read 0.04 units across at both hard, and 0.08 near the foot and 0.24
        // near the tip with the sun 3 degrees across, on the development machine's GPU.
        var config = Config.Default.WithWindow("softness test", 480, 270) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetAmbientLight(new Color(255, 255, 255), 0.2f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -1, 0)), Color.White, 2, castsShadows: true);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].Roughness = 1;
        var camera = new Camera3D(new Vector3(2.75f, 7, 0.001f), new Vector3(2.75f, 0, 0), Vector3.UnitY, 45);

        Image Capture(string name)
        {
            var path = Path.Combine(_folder.Path, name + ".png");
            for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(cube, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(30, 0.2f, 30), new Color(200, 200, 200));
                DrawModelEx(cube, new Vector3(0, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.5f, 5, 0.5f), new Color(160, 80, 60));
                EndMode3D();
                if (frame == 0) TakeScreenshot(path);
                EndDrawing();
            }
            File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
            GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
            return LoadImage(path);
        }

        // The pixels of the floor across the shadow at x whose light lies between a quarter and
        // three quarters of the way from the shadow's darkest to the lit floor's.
        int Edge(Image image, float x, float dark, float lit)
        {
            var count = 0;
            for (float z = -1; z <= 1; z += 0.01f)
            {
                var at = GetWorldToScreenEx(new Vector3(x, 0, z), camera, image.Width, image.Height);
                var share = (GetImageColor(image, (int)at.X, (int)at.Y).G - dark) / (lit - dark);
                if (share is > 0.25f and < 0.75f) count++;
            }
            return count;
        }
        (float Dark, float Lit) Range(Image image)
        {
            var (dark, lit) = (255f, 0f);
            for (float z = -1; z <= 1; z += 0.01f)
            {
                var at = GetWorldToScreenEx(new Vector3(1, 0, z), camera, image.Width, image.Height);
                var g = GetImageColor(image, (int)at.X, (int)at.Y).G;
                (dark, lit) = (Math.Min(dark, g), Math.Max(lit, g));
            }
            return (dark, lit);
        }

        var hard = Capture("hard");
        SetShadowSoftness(3);
        var soft = Capture("soft");

        var (hardDark, hardLit) = Range(hard);
        var (softDark, softLit) = Range(soft);
        (hardLit - hardDark).Should().BeGreaterThan(60, "the pole's shadow darkens the floor near its foot");
        var (hardFoot, hardTip) = (Edge(hard, 1, hardDark, hardLit), Edge(hard, 4.5f, hardDark, hardLit));
        var (softFoot, softTip) = (Edge(soft, 1, softDark, softLit), Edge(soft, 4.5f, softDark, softLit));
        hardTip.Should().BeLessThanOrEqualTo(hardFoot + 4, $"with the sun given no width the edge is as sharp at the tip as at the foot, {hardFoot} and {hardTip} hundredths");
        softFoot.Should().BeLessThanOrEqualTo(hardFoot + 7, $"near the foot the edge stays sharp, {softFoot} hundredths where {hardFoot}");
        softTip.Should().BeGreaterThanOrEqualTo(softFoot + 10, $"near the tip the edge is wider by the sun's width at the pole's height, {softTip} hundredths where {softFoot} near the foot");
        UnloadModel(cube);
    }
}
