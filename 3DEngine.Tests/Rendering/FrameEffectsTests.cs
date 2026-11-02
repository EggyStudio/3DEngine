using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The effects over the frame, each set alone and read from chosen pixels of a frame drawn
/// offscreen, exposure, the curves, grading, the vignette, FXAA and temporal antialiasing.
/// </summary>
/// <remarks>
/// The effects are drawn under the validation layer where the machine has it, as CI draws them on
/// lavapipe, and an error it reports fails the test that drew the frame. A driver may let such an
/// error pass and draw the frame right, so a run on the GPU without the layer does not see it. To
/// run them as CI does, name <c>build/tools/vulkan-layer/share/vulkan/explicit_layer.d</c>, which
/// <c>build/fetch-validation-layer.sh</c> fills, in <c>VK_LAYER_PATH</c>, and lavapipe's manifest in
/// <c>VK_DRIVER_FILES</c>.
/// </remarks>
[Collection("E3D")]
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

    [NeedsVulkanFact]
    public void Center_Weighted_Metering_Reads_The_Middle_And_Its_Compensation_Moves_It_By_Stops()
    {
        Open();
        // A gray cube in the middle of a white background, which the whole picture's mean is pulled
        // up by and the middle's is not.
        var scene = Cube(new Color(60, 60, 60), Color.White);
        SetAutoExposure(true, min: 0.05f, max: 20, speed: 10000);
        var average = GetImageColor(Capture(scene), 80, 60);
        SetAutoExposureMetering(ExposureMetering.CenterWeighted);
        var center = GetImageColor(Capture(scene), 80, 60);
        SetAutoExposureMetering(ExposureMetering.CenterWeighted, 1);
        var brighter = GetImageColor(Capture(scene), 80, 60);

        ((int)center.R).Should().BeGreaterThan(average.R + 15, $"the middle's metering exposes the cube for itself ({average} to {center})");
        ((int)brighter.R).Should().BeGreaterThan(center.R + 15, $"a stop of compensation brightens it ({center} to {brighter})");
        GetAutoExposureMetering().Should().Be((ExposureMetering.CenterWeighted, 1f));
    }

    [NeedsVulkanFact]
    public void A_Histogram_Of_The_Darkest_Quarter_Exposes_A_Gray_Cube_On_White_For_The_Cube()
    {
        Open();
        // The white background is most of the picture's weighted light, so measuring the darkest
        // quarter alone measures the cube.
        var scene = Cube(new Color(60, 60, 60), Color.White);
        SetAutoExposure(true, min: 0.05f, max: 20, speed: 10000);
        var whole = GetImageColor(Capture(scene), 80, 60);
        SetAutoExposureHistogram(0, 0.25f);
        var darkest = GetImageColor(Capture(scene), 80, 60);

        ((int)darkest.R).Should().BeGreaterThan(whole.R + 15, $"the darkest quarter's metering exposes the cube for itself ({whole} to {darkest})");
        GetAutoExposureHistogram().Should().Be((0f, 0.25f));
        SetAutoExposureHistogram(0.9f, 0.1f);
        GetAutoExposureHistogram().Should().Be((0f, 1f), "shares out of order measure the whole picture");
    }

    [NeedsVulkanFact]
    public void The_Exposure_Follows_A_Brightening_And_A_Darkening_Scene_At_Speeds_Of_Their_Own()
    {
        Open();
        var dark = Cube(new Color(40, 40, 40), new Color(10, 10, 10));
        var bright = Cube(new Color(200, 200, 200), Color.White);
        SetAutoExposure(true, speed: 10000);
        var brightFollowed = GetImageColor(Capture(bright), 80, 60);
        var darkFollowed = GetImageColor(Capture(dark), 80, 60);

        // Brightening held where it is, darkening followed at once.
        SetAutoExposureSpeeds(brightening: 0, darkening: 10000);
        Capture(dark);
        var brightHeld = GetImageColor(Capture(bright), 80, 60);
        var darkAgain = GetImageColor(Capture(dark), 80, 60);

        ((int)brightHeld.R).Should().BeGreaterThan(brightFollowed.R + 30, $"the bright scene keeps the dark one's exposure and is washed out ({brightHeld} against {brightFollowed})");
        ((int)darkAgain.R).Should().BeInRange(darkFollowed.R - 3, darkFollowed.R + 3, "the dark scene is followed at once");
        GetAutoExposureSpeeds().Should().Be((0f, 10000f));
        SetAutoExposure(true, speed: 2);
        GetAutoExposureSpeeds().Should().Be((2f, 2f), "one speed sets both");
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
    public void Per_Object_Motion_Blur_Smears_A_Mesh_Entity_Along_Its_Own_Path_Under_A_Still_Camera()
    {
        Open();
        var ecs = GetApp().World.Resource<EcsWorld>();
        // A white square entity sliding three tenths of a unit a frame, about seven pixels, past a
        // camera that stays where it is, drawn through the camera of BeginMode3D.
        var square = ecs.Spawn();
        ecs.Add(square, new Mesh([new(-0.75f, -0.75f, 0), new(0.75f, -0.75f, 0), new(0.75f, 0.75f, 0), new(-0.75f, -0.75f, 0), new(0.75f, 0.75f, 0), new(-0.75f, 0.75f, 0)]));
        ecs.Add(square, new Material(Vector4.One));
        ecs.Add(square, new Transform(Vector3.Zero));
        Action scene = () =>
        {
            ecs.GetRef<Transform>(square).Position.X += 0.3f;
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            EndMode3D();
        };
        // The same frames each time, the capture taken as the square passes the middle.
        Image Slide()
        {
            ecs.GetRef<Transform>(square).Position = new Vector3(-1.5f, 0, 0);
            for (int i = 0; i < 3; i++)
            {
                BeginDrawing();
                scene();
                EndDrawing();
            }
            return Capture(scene);
        }

        SetMotionBlur(1);
        var camera = Slide();
        SetMotionBlur(1, objects: true);
        var own = Slide();

        Soft(camera, 60, 0, 160).Should().BeLessThanOrEqualTo(4, "with the camera still, the camera's movement blurs nothing");
        Soft(own, 60, 0, 160).Should().BeGreaterThan(6, "the square's own movement smears its sides along its path");
        Soft(own, 20, 0, 160).Should().Be(0, "above the square there is nothing to smear");
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

    [Fact]
    public void Temporal_Jitter_Covers_The_Pixel_And_Averages_To_Its_Middle()
    {
        var moves = Enumerable.Range(0, TemporalJitter.Count).Select(i => TemporalJitter.Pixels((ulong)i)).ToList();

        moves.Should().OnlyContain(m => m.X > -0.5f && m.X < 0.5f && m.Y > -0.5f && m.Y < 0.5f, "each move stays inside the pixel");
        moves.Distinct().Should().HaveCount(TemporalJitter.Count, "the eight frames sample eight places");
        var mean = moves.Aggregate(Vector2.Zero, (sum, m) => sum + m) / moves.Count;
        mean.Length().Should().BeLessThan(0.1f, "the places lie about the pixel's middle, so a still edge does not move");
        TemporalJitter.Pixels(3).Should().Be(TemporalJitter.Pixels(3 + TemporalJitter.Count), "the moves come round again");
    }

    // A white triangle turned so its edges cross the pixels at a slant, as the FXAA test draws it,
    // at a place across.
    private static Action Slanted(float across = 0) => () =>
    {
        var turned = Matrix4x4.CreateRotationZ(0.3f) * Matrix4x4.CreateTranslation(across, 0, 0);
        ClearBackground(Color.Black);
        BeginMode3D(new Camera3D(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45));
        DrawTriangle3D(Vector3.Transform(new Vector3(-2, -1.5f, 0), turned), Vector3.Transform(new Vector3(2, -1.5f, 0), turned),
            Vector3.Transform(new Vector3(0, 1.5f, 0), turned), Color.White);
        EndMode3D();
    };

    // Draws frames of draw before the one captured, so the history holds them.
    private Image CaptureAfter(int frames, Action draw)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            BeginDrawing();
            draw();
            EndDrawing();
        }
        return Capture(draw);
    }

    private static float Linear(byte c) => BloomRenderer.SrgbToLinear(c / 255f);

    [NeedsVulkanFact]
    public void Temporal_Antialiasing_Smooths_An_Edge_Over_Frames()
    {
        // One sample, so a pixel is the triangle or the background in any one frame, and only the
        // frames together cover it in part.
        Open(samples: 1);
        int Between(Image image)
        {
            var count = 0;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (GetImageColor(image, x, y).G is > 20 and < 235) count++;
            return count;
        }

        var hard = Between(CaptureAfter(16, Slanted()));
        SetTemporalAntialiasing(true);
        var smoothed = Between(CaptureAfter(16, Slanted()));

        hard.Should().BeLessThan(10, "with one sample and no temporal antialiasing a pixel is the triangle or the background");
        smoothed.Should().BeGreaterThan(hard + 40, "the frames, each moved by a part of a pixel, cover the pixels along the edges in part");
    }

    [NeedsVulkanFact]
    public void Temporal_Antialiasing_Holds_A_Still_View()
    {
        Open(samples: 1);
        SetTemporalAntialiasing(true);
        var before = CaptureAfter(24, Slanted());
        var after = Capture(Slanted());

        // A pixel on the edge sees the triangle in some of the eight places and not in the others.
        // Without the history it would go from one side of the step to the other, and with an
        // eighth of each frame blended in it would move by up to an eighth of the step, 0.125.
        // A pixel that holds still takes less of each frame the longer it does, down to a
        // thirty-second, which reads 0.026 on lavapipe and on an RTX 4070.
        var most = 0f;
        for (int y = 0; y < before.Height; y++)
            for (int x = 0; x < before.Width; x++)
                most = Math.Max(most, Math.Abs(Linear(GetImageColor(after, x, y).G) - Linear(GetImageColor(before, x, y).G)));
        most.Should().BeLessThan(0.08f, "a still view's edges settle, each frame taking less of the blend the longer they hold");
    }

    [NeedsVulkanFact]
    public void Temporal_Antialiasing_Leaves_No_Trail_Behind_What_Moved()
    {
        Open(samples: 1);
        SetTemporalAntialiasing(true);
        CaptureAfter(24, Slanted(-1.5f));
        var moved = Capture(Slanted(1.5f));
        var settled = CaptureAfter(24, Slanted(1.5f));

        // The left of the frame, where the triangle was and is no longer, held to the black around
        // it this frame though the history saw the triangle there.
        var most = 0f;
        for (int y = 0; y < moved.Height; y++)
            for (int x = 0; x < 40; x++)
                most = Math.Max(most, Math.Abs(Linear(GetImageColor(moved, x, y).G) - Linear(GetImageColor(settled, x, y).G)));
        most.Should().BeLessThan(0.05f, "the history is held to the colors around each pixel this frame, so what moved away leaves nothing behind");
    }
}
