using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The effects over the frame, each set alone and read from chosen pixels of a frame drawn
/// offscreen: exposure, the curves, grading, the vignette and FXAA.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class FrameEffectsTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-effects-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open(int samples = 4)
    {
        var config = Config.Default.WithWindow("effects test", 160, 120) with { Headless = true, Offscreen = true, Samples = samples };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the first one's capture has been written, as ReferenceFrameTests does.
    private Image Capture(Action draw)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
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

    private Action Cube(Color color, Color? background = null) => () =>
    {
        ClearBackground(background ?? Color.Black);
        BeginMode3D(_camera);
        DrawCube(Vector3.Zero, 2, 2, 2, color);
        EndMode3D();
    };

    // A sphere giving off white light, so its middle shows what a curve makes of 1.
    private Action WhiteLight(Model sphere) => () =>
    {
        ClearBackground(Color.Black);
        BeginMode3D(_camera);
        sphere.Materials[0] = new ModelMaterial(Color.Black) { Emissive = Color.White };
        DrawModel(sphere, Vector3.Zero, 1, Color.White);
        EndMode3D();
    };

    [NeedsVulkanFact]
    public void Exposure_Brightens_The_Scene()
    {
        Open();
        var before = GetImageColor(Capture(Cube(new Color(80, 80, 80))), 80, 60);
        SetExposure(2);
        var after = GetImageColor(Capture(Cube(new Color(80, 80, 80))), 80, 60);

        before.R.Should().BeInRange(78, 82, "with no effect the shape keeps its color");
        after.R.Should().BeGreaterThan(100, "twice the light is brighter, about 108 once encoded");
    }

    [NeedsVulkanFact]
    public void The_Exposure_That_Follows_The_Scene_Brightens_A_Dark_One_And_Dims_A_Bright_One()
    {
        Open();
        var dark = Cube(new Color(40, 40, 40), new Color(10, 10, 10));
        var bright = Cube(new Color(200, 200, 200), Color.White);
        var darkBefore = GetImageColor(Capture(dark), 80, 60);
        var brightBefore = GetImageColor(Capture(bright), 80, 60);

        SetAutoExposure(true, min: 0.25f, max: 4, speed: 10000);
        var darkAfter = GetImageColor(Capture(dark), 80, 60);
        var brightAfter = GetImageColor(Capture(bright), 80, 60);

        ((int)darkAfter.R).Should().BeGreaterThan(darkBefore.R + 30, $"a dark scene is brought up toward a mid gray ({darkBefore} to {darkAfter})");
        ((int)brightAfter.R).Should().BeLessThan(brightBefore.R - 30, $"a bright one is brought down ({brightBefore} to {brightAfter})");
    }

    [NeedsVulkanFact]
    public void The_Exposure_That_Follows_The_Scene_Moves_At_Its_Speed()
    {
        Open();
        var dark = Cube(new Color(40, 40, 40), new Color(10, 10, 10));
        var bright = Cube(new Color(200, 200, 200), Color.White);
        // At a speed of 0 it keeps where the first frame put it, the dark scene's exposure.
        SetAutoExposure(true, speed: 0);
        Capture(dark);
        var held = GetImageColor(Capture(bright), 80, 60);
        SetAutoExposure(true, speed: 10000);
        var followed = GetImageColor(Capture(bright), 80, 60);

        ((int)held.R).Should().BeGreaterThan(followed.R + 30, $"the bright scene seen with the dark one's exposure is washed out ({held} against {followed})");
        SetAutoExposure(false);
        GetImageColor(Capture(bright), 80, 60).R.Should().BeInRange(198, 202, "with it off the cube keeps its color");
    }

    // How many pixels of a row between two x's are neither near black nor near white, the width
    // of the edges there.
    private static int Soft(Image image, int y, int from, int to)
    {
        var count = 0;
        for (int x = from; x < to; x++)
        {
            var c = GetImageColor(image, x, y);
            if (c.R is > 30 and < 220) count++;
        }
        return count;
    }

    [NeedsVulkanFact]
    public void Depth_Of_Field_Blurs_A_Far_Thing_And_Keeps_The_One_In_Focus_Sharp()
    {
        Open();
        var camera = new Camera3D(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);
        Action scene = () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(new Vector3(-1.2f, 0, 0), 1, 1, 0.2f, Color.White);
            DrawCube(new Vector3(9, 0, -24), 6, 6, 0.2f, Color.White);
            EndMode3D();
        };
        var sharp = Capture(scene);
        SetDepthOfField(6, 2, 0.06f);
        var focused = Capture(scene);

        // The near square spans about x 38 to 62 and the far one about 103 to 125, row 60.
        var (nearBefore, nearAfter) = (Soft(sharp, 60, 30, 70), Soft(focused, 60, 30, 70));
        var (farBefore, farAfter) = (Soft(sharp, 60, 90, 140), Soft(focused, 60, 90, 140));
        farAfter.Should().BeGreaterThan(farBefore + 6, $"the far square's edges spread ({farBefore} soft pixels before, {farAfter} after)");
        nearAfter.Should().BeLessThanOrEqualTo(nearBefore + 2, $"the square in focus stays sharp ({nearBefore} before, {nearAfter} after)");
    }

    [NeedsVulkanFact]
    public void Motion_Blur_Smears_A_Picture_Along_The_Way_The_Camera_Moves()
    {
        Open();
        SetBloom(0.0001f);
        var x = 0f;
        // The camera slides sideways three tenths of a unit a frame past a white square, about
        // seven pixels at this distance.
        Action scene = () =>
        {
            x += 0.3f;
            var camera = new Camera3D(new Vector3(x, 0, 6), new Vector3(x, 0, 0), Vector3.UnitY, 45);
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(Vector3.Zero, 1.5f, 1.5f, 0.2f, Color.White);
            EndMode3D();
        };
        // The same frames each time, the capture taken as the square passes the middle.
        Image Slide()
        {
            x = -1.5f;
            for (int i = 0; i < 3; i++)
            {
                BeginDrawing();
                scene();
                EndDrawing();
            }
            return Capture(scene);
        }

        var still = Slide();
        SetMotionBlur(1);
        var moving = Slide();

        Soft(moving, 60, 0, 160).Should().BeGreaterThan(Soft(still, 60, 0, 160) + 6, "the square's sides smear across the row as the camera moves");
        Soft(moving, 20, 0, 160).Should().Be(0, "above the square there is nothing to smear");
    }

    [NeedsVulkanFact]
    public void Each_Curve_Brings_White_Light_To_Its_Own_Shade()
    {
        Open();
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 16, 16));
        // A light pointing away from the camera's side, so the sphere's light takes the curve lit
        // colors take, where a scene with no light draws its colors as they are.
        CreateDirectionalLight(new Vector3(0, 0, 1), Color.White);
        int Shade(Tonemap curve)
        {
            SetTonemap(curve);
            return GetImageColor(Capture(WhiteLight(sphere)), 80, 60).G;
        }

        Shade(Tonemap.Clamp).Should().BeGreaterThan(252, "1 cut at 1 is white");
        Shade(Tonemap.Engine).Should().BeInRange(245, 253, "the engine's curve bends 1 to about 0.96");
        Shade(Tonemap.Aces).Should().BeInRange(220, 242, "ACES brings 1 to about 0.8");
        Shade(Tonemap.Reinhard).Should().BeInRange(180, 196, "Reinhard halves 1");
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    public void Grading_Takes_Color_Away_And_Tints()
    {
        Open();
        SetColorGrading(contrast: 1, saturation: 0, tint: Color.White);
        var gray = GetImageColor(Capture(Cube(new Color(200, 60, 40))), 80, 60);
        SetColorGrading(contrast: 1, saturation: 1, tint: new Color(255, 0, 0));
        var red = GetImageColor(Capture(Cube(new Color(200, 200, 200))), 80, 60);

        Math.Abs(gray.R - gray.G).Should().BeLessThanOrEqualTo(1, "with no saturation the color is gray");
        Math.Abs(gray.G - gray.B).Should().BeLessThanOrEqualTo(1);
        red.R.Should().BeGreaterThan(180);
        red.G.Should().BeLessThan(5, "a red tint takes the green and blue away");
    }

    [NeedsVulkanFact]
    public void A_Vignette_Darkens_The_Corners()
    {
        Open();
        SetVignette(0.8f);
        var frame = Capture(Cube(new Color(150, 150, 150), background: new Color(150, 150, 150)));

        GetImageColor(frame, 80, 60).R.Should().BeInRange(146, 154, "the middle is left alone");
        GetImageColor(frame, 1, 1).R.Should().BeLessThan(60, "the corner is darkened most");
    }

    [NeedsVulkanFact]
    public void Fxaa_Smooths_An_Edge_Multisampling_Does_Not()
    {
        // One sample, so the edge is a staircase until FXAA blends it.
        Open(samples: 1);
        var turned = Matrix4x4.CreateRotationZ(0.3f);
        Action draw = () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawTriangle3D(Vector3.Transform(new Vector3(-2, -1.5f, 0), turned), Vector3.Transform(new Vector3(2, -1.5f, 0), turned),
                Vector3.Transform(new Vector3(0, 1.5f, 0), turned), Color.White);
            EndMode3D();
        };
        int Between(Image image)
        {
            var count = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (GetImageColor(image, x, y).G is > 20 and < 235) count++;
            return count;
        }

        var hard = Between(Capture(draw));
        SetFxaa(true);
        var smoothed = Between(Capture(draw));

        hard.Should().BeLessThan(10, "with one sample and no FXAA a pixel is the triangle or the background");
        smoothed.Should().BeGreaterThan(hard + 40, "FXAA blends the pixels along the edges");
    }
}
