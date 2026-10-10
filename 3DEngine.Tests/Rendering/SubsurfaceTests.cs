using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Subsurface scattering, read from chosen pixels of frames drawn offscreen: two white spheres lit
/// from the side, the left one's material scattering light under its surface, red farthest,
/// whose line between lit and shadowed softens and reddens where the right one's, unmarked, is
/// left as it was; and slabs lit from behind, a thin one that scatters showing the light on its
/// front where a thick one barely does and an unmarked one does not.
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

    [NeedsVulkanTheory]
    [InlineData(SubsurfaceQuality.Low)]
    [InlineData(SubsurfaceQuality.Medium)]
    [InlineData(SubsurfaceQuality.High)]
    public void A_Lit_Sphere_Whose_Material_Scatters_Softens_Its_Terminator_And_Bleeds_Red_Where_The_Unmarked_One_Does_Not(SubsurfaceQuality quality)
    {
        Open();
        SetSubsurfaceQuality(quality);
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

    [NeedsVulkanTheory]
    [InlineData("lamp through the field")]
    [InlineData("sun through its map")]
    [InlineData("point light through its map")]
    [InlineData("spot light through its map")]
    public void A_Thin_Slab_Lit_From_Behind_Shows_The_Light_On_Its_Front_Where_A_Thick_One_Barely_Does(string light)
    {
        // Three white slabs a unit square facing the camera, lit only from behind: a thin one and a
        // thick one whose material scatters over 0.3, and a thin one that does not. Behind them a lamp
        // whose light's way through each is measured in the scene's distance field, or the sun, a
        // point light or a spot light that casts shadows with no field, whose own shadow map
        // measures it. The thin one's front read (143, 118, 102) by the lamp through the field,
        // (180, 162, 150) by the sun and (149, 133, 122) by the point and the spot light through
        // their maps, the thick one's (59, 39, 39) and (48, 39, 39), its light a unit through faded
        // near to nothing, and the unmarked one's 39 in every channel, the ambient light alone, as
        // this test measured on an RTX 4070.
        // A point or a spot light that casts shadows and no field measures through its own map.
        Open();
        switch (light)
        {
            case "lamp through the field":
                SetSceneField(1, 0.15f);
                CreatePointLight(new Vector3(0, 0, -2.5f), Color.White, 3, range: 10);
                break;
            case "sun through its map":
                CreateDirectionalLight(Vector3.UnitZ, Color.White, 0.5f, castsShadows: true);
                break;
            case "point light through its map":
                CreatePointLight(new Vector3(0, 0, -2.5f), Color.White, 3, range: 10, castsShadows: true);
                break;
            default:
                CreateSpotLight(new Vector3(0, 0, -2.5f), Vector3.UnitZ, Color.White, 3, 50, 60, range: 10, castsShadows: true);
                break;
        }
        SetAmbientLight(Color.White, 0.02f);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var scattering = slab.Materials[0] with { SubsurfaceRadius = 0.3f, SubsurfaceColor = new Color(255, 90, 60) };
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var (thin, thick, plain) = (new Vector3(-1.3f, 0, 0), new Vector3(0, 0, -0.6f), new Vector3(1.3f, 0, 0));
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 12 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawMesh(slab.Meshes[0], scattering, Matrix4x4.CreateScale(1, 1, 0.04f) * Matrix4x4.CreateTranslation(thin));
            DrawMesh(slab.Meshes[0], scattering, Matrix4x4.CreateScale(1, 1, 1.2f) * Matrix4x4.CreateTranslation(thick));
            DrawMesh(slab.Meshes[0], slab.Materials[0], Matrix4x4.CreateScale(1, 1, 0.04f) * Matrix4x4.CreateTranslation(plain));
            EndMode3D();
            if (frame == SceneFieldPlan.SettleFrames + 6) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        var image = LoadImage(path);
        Color Front(Vector3 middle, float depth)
        {
            var p = GetWorldToScreen(middle + new Vector3(0, 0, depth / 2), camera);
            return GetImageColor(image, (int)p.X, (int)p.Y);
        }

        var (thinFront, thickFront, plainFront) = (Front(thin, 0.04f), Front(thick, 1.2f), Front(plain, 0.04f));
        Sum(thinFront).Should().BeGreaterThan(Sum(plainFront) + 60, $"light comes through the thin slab to its front, {thinFront} against the unmarked {plainFront}");
        (thinFront.R - thinFront.B).Should().BeGreaterThan(20, $"red, which travels farthest, most, {thinFront}");
        Sum(thickFront).Should().BeLessThan(Sum(plainFront) + (Sum(thinFront) - Sum(plainFront)) / 4, $"and barely through the thick one, {thickFront} against {thinFront}");
        UnloadModel(slab);
    }

    [NeedsVulkanFact]
    public void A_Sheet_Lit_From_Behind_By_A_Lamp_No_Field_Measures_Lets_It_Through_By_Its_Materials_Thickness()
    {
        // A sheet facing the camera with a lamp behind it and no scene's field to measure how thick
        // it is toward the lamp, so no light comes through it, until its material says it is five
        // centimeters thick, as an ear is: its front read (39, 39, 39), the ambient light alone,
        // and (136, 120, 109) with the thickness, as this test measured on an RTX 4070.
        Open();
        CreatePointLight(new Vector3(0, 0, -1.5f), Color.White, 0.6f, range: 10);
        SetAmbientLight(Color.White, 0.02f);
        var sheet = GenMeshPlane(1.5f, 1.5f, 1, 1);
        var camera = new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45);
        Color Front(float thickness)
        {
            var material = new ModelMaterial(Color.White) { SubsurfaceRadius = 0.3f, SubsurfaceColor = new Color(255, 90, 60), SubsurfaceThickness = thickness };
            var path = Path.Combine(_folder.Path, $"{_captures++}.png");
            for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawMesh(sheet, material, Matrix4x4.CreateRotationX(MathF.PI / 2));
                EndMode3D();
                if (frame == 3) TakeScreenshot(path);
                EndDrawing();
            }
            File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
            var image = LoadImage(path);
            var p = GetWorldToScreen(Vector3.Zero, camera);
            return GetImageColor(image, (int)p.X, (int)p.Y);
        }

        var (measured, given) = (Front(0), Front(0.05f));
        Sum(given).Should().BeGreaterThan(Sum(measured) + 120, $"the lamp's light comes through the five centimeters the material gives, {given} against {measured}");
        (given.R - given.B).Should().BeGreaterThan(15, $"red, which travels farthest, most, {given}");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong");
        UnloadMesh(sheet);
    }
}
