using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Subsurface scattering, read from chosen pixels of frames drawn offscreen: two white spheres lit
/// from the side, the left one's material scattering light under its surface, red farthest,
/// whose line between lit and shadowed softens and reddens where the right one's, unmarked, is
/// left as it was.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class SubsurfaceTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-subsurface-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);
    private static readonly Vector3 Left = new(-1.3f, 0, 0), Right = new(1.3f, 0, 0);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("subsurface test", 320, 240) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames of the two spheres, the left one's material scattering over the radius given,
    // the last of them captured.
    private Image Capture(Model sphere, float radius)
    {
        var scattering = sphere.Materials[0] with { SubsurfaceRadius = radius, SubsurfaceColor = new Color(255, 90, 60) };
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 14 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawMesh(sphere.Meshes[0], scattering, Matrix4x4.CreateTranslation(Left));
            DrawMesh(sphere.Meshes[0], sphere.Materials[0], Matrix4x4.CreateTranslation(Right));
            EndMode3D();
            if (frame == 3) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        return LoadImage(path);
    }

    // The pixel of the point on a sphere facing the camera dx of its radius from its middle, along x.
    private Color At(Image image, Vector3 center, float dx)
    {
        var p = GetWorldToScreen(center + new Vector3(dx, 0, MathF.Sqrt(1 - dx * dx)), _camera);
        return GetImageColor(image, (int)p.X, (int)p.Y);
    }

    private static int Sum(Color c) => c.R + c.G + c.B;

    [NeedsVulkanFact]
    public void A_Lit_Sphere_Whose_Material_Scatters_Softens_Its_Terminator_And_Bleeds_Red_Where_The_Unmarked_One_Does_Not()
    {
        Open();
        // The light comes from the right and a little in front, so each sphere's terminator lies a
        // fifth of its radius left of its middle, the left of it in shadow.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, 0, -0.2f)), Color.White, 1.5f);
        SetAmbientLight(Color.White, 0.05f);
        var sphere = LoadModelFromMesh(GenMeshSphere(1, 48, 48));

        // A radius far under a pixel spreads nothing, and draws the frame through the same passes.
        // A radius of the sphere's own is wax's on a candle.
        var without = Capture(sphere, 1e-5f);
        var with = Capture(sphere, 1f);

        foreach (var dx in new[] { -0.6f, -0.4f, -0.2f, 0f, 0.3f, 0.6f })
            At(with, Right, dx).Should().Be(At(without, Right, dx), $"the unmarked sphere is not touched at {dx} of its radius");

        // A tenth of the radius past the terminator, in the sun's shadow, lit by the ambient light alone.
        var (scattered, plain) = (At(with, Left, -0.3f), At(without, Left, -0.3f));
        Sum(scattered).Should().BeGreaterThan(Sum(plain) + 15, $"light scattered under the surface reaches past the terminator, {scattered} against {plain}");
        (scattered.R - scattered.G).Should().BeGreaterThan(plain.R - plain.G + 10, $"and red travels farthest, {scattered} against {plain}");
        var (litWith, litWithout) = (At(with, Left, 0.5f), At(without, Left, 0.5f));
        Sum(litWith).Should().BeInRange(Sum(litWithout) - 30, Sum(litWithout) + 30, $"the lit side away from the terminator holds its light, {litWith} against {litWithout}");
        UnloadModel(sphere);
    }
}
