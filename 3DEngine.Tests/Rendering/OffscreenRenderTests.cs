using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws through the flat API into offscreen frames on a real Vulkan device and reads the pixels
/// back, so the renderer is covered by a test.
/// </summary>
/// <remarks>
/// Skipped, with the reason, on a machine with no Vulkan device or no Slang compiler. A software
/// device such as lavapipe is enough to run them.
/// </remarks>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed partial class OffscreenRenderTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-offscreen-");

    // Errors the validation layer had reported before this test, so the test fails on its own.
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open(int width, int height, int samples = 4)
    {
        var config = Config.Default.WithWindow("offscreen test", width, height) with { Headless = true, Offscreen = true, Samples = samples };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        RunMode.HasRenderer(GetApp().World).Should().BeTrue("the probe started a Vulkan device, so the app's renderer starts too");
    }

    // Draws frames until the capture asked for in the first has been written.
    private Image Capture(Action draw, string name = "frame")
    {
        var path = Path.Combine(_folder.Path, name + ".png");
        // An earlier capture of the same name would otherwise be read back in place of this one.
        File.Delete(path);
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
    public void Shapes_Drawn_In_2D_Land_On_The_Pixels_They_Cover()
    {
        Open(64, 32);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawRectangle(0, 0, 32, 32, new Color(255, 0, 0));
            DrawRectangle(32, 0, 32, 32, new Color(0, 0, 255));
        });

        (image.Width, image.Height).Should().Be((64, 32));
        GetImageColor(image, 8, 16).Should().Be(new Color(255, 0, 0));
        GetImageColor(image, 56, 16).Should().Be(new Color(0, 0, 255));
    }

    // raylib's OpenGL takes the row below a line on the boundary between two rows, and the column
    // left of one between two columns, which a grid drawn at whole coordinates shows, and fills a
    // row whose middle is on a shape's lower edge and not one whose middle is on its upper edge.
    [NeedsVulkanFact]
    public void A_Tie_Between_Two_Rows_Of_Pixels_Is_Broken_As_Raylib_Breaks_It()
    {
        Open(32, 32, samples: 1);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawLine(0, 10, 16, 10, Color.White);
            DrawLine(20, 0, 20, 32, Color.White);
            DrawRectangleRec(new Rectangle(2, 20.5f, 8, 4), Color.Red);
        });

        GetImageColor(image, 5, 10).Should().Be(Color.White, "the row below the line is drawn");
        GetImageColor(image, 5, 9).Should().Be(Color.Black, "the row above it is not");
        GetImageColor(image, 19, 25).Should().Be(Color.White, "the column left of an upright line is drawn");
        GetImageColor(image, 20, 25).Should().Be(Color.Black);
        GetImageColor(image, 5, 20).Should().Be(Color.Black, "a row whose middle is on the upper edge is left");
        GetImageColor(image, 5, 24).Should().Be(Color.Red, "a row whose middle is on the lower edge is filled");
    }

    [NeedsVulkanFact]
    public void Dashes_Thick_Circles_Ellipses_And_Blended_Triangles_Cover_What_They_Should()
    {
        Open(96, 48);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawLineDashed(new Vector2(0, 4.5f), new Vector2(40, 4.5f), 4, 4, Color.White);
            DrawCircleLinesEx(new Vector2(16, 28), 12, 3, Color.Red);
            DrawEllipseV(new Vector2(52, 28), 10, 4, Color.Lime);
            DrawTriangleGradient(new Vector2(72, 4), new Vector2(72, 44), new Vector2(94, 4), Color.Red, Color.Red, Color.Blue);
        });

        GetImageColor(image, 1, 4).Should().Be(Color.White, "a dash");
        GetImageColor(image, 5, 4).Should().Be(Color.Black, "the gap after it");
        GetImageColor(image, 9, 4).Should().Be(Color.White, "the next dash");
        GetImageColor(image, 27, 28).R.Should().BeGreaterThan(200, "the circle's thick edge");
        GetImageColor(image, 16, 28).Should().Be(Color.Black, "its middle is open");
        GetImageColor(image, 60, 28).G.Should().BeGreaterThan(140, "the ellipse reaches its long radius");
        GetImageColor(image, 52, 21).Should().Be(Color.Black, "and not past its short one");
        GetImageColor(image, 73, 6).R.Should().BeGreaterThan(200, "the triangle is red at its red corners");
        GetImageColor(image, 91, 5).B.Should().BeGreaterThan(150, "and blue toward its blue one");
    }

    [NeedsVulkanFact]
    public void Gradients_Thick_Outlines_Rings_Turned_And_Rounded_Rectangles_Cover_What_They_Should()
    {
        Open(128, 64);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawRectangleGradientH(0, 0, 32, 32, new Color(255, 0, 0), new Color(0, 0, 255));
            DrawRectangleLinesEx(new Rectangle(32, 0, 32, 32), 4, Color.White);
            DrawRing(new Vector2(80, 16), 6, 14, 0, 360, 0, new Color(0, 255, 0));
            DrawRectanglePro(new Rectangle(112, 16, 16, 16), new Vector2(8, 8), 45, Color.White);
            DrawRectangleRounded(new Rectangle(0, 32, 32, 32), 1, 0, Color.White);
        });

        var middle = GetImageColor(image, 16, 16);
        (middle.R is > 90 and < 170 && middle.B is > 90 and < 170).Should().BeTrue($"the gradient's middle is half red and half blue, not {middle}");
        GetImageColor(image, 34, 16).Should().Be(Color.White, "the outline is four pixels thick inside the edge");
        GetImageColor(image, 48, 16).Should().Be(Color.Black, "and the rectangle's inside is empty");
        GetImageColor(image, 80, 16).Should().Be(Color.Black, "the ring has a hole");
        GetImageColor(image, 90, 16).Should().Be(new Color(0, 255, 0), "and is green between its radii");
        GetImageColor(image, 112, 8).Should().Be(Color.White, "turned 45 degrees, the square's corner points up");
        GetImageColor(image, 105, 9).Should().Be(Color.Black, "and its old corner is empty");
        GetImageColor(image, 1, 33).Should().Be(Color.Black, "a fully rounded square leaves its corners empty");
        GetImageColor(image, 16, 48).Should().Be(Color.White);
    }

    [NeedsVulkanFact]
    public void Cylinders_And_Capsules_Draw_Between_Their_Ends()
    {
        Open(64, 32);
        var camera = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY, 45);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCylinderEx(new Vector3(-3, -1.5f, 0), new Vector3(-3, 1.5f, 0), 0.8f, 0.8f, 16, new Color(255, 0, 0));
            DrawCapsule(new Vector3(2, -1, 0), new Vector3(4, 1, 0), 0.6f, 12, 6, new Color(0, 255, 0));
            EndMode3D();
        });

        GetImageColor(image, 20, 16).Should().Be(new Color(255, 0, 0), "the cylinder stands three units left of the middle");
        GetImageColor(image, 45, 16).Should().Be(new Color(0, 255, 0), "the capsule leans three units right of it");
        GetImageColor(image, 32, 16).Should().Be(Color.Black, "with nothing between");
    }

    [NeedsVulkanFact]
    public void A_Cube_Is_Lit_Through_The_Camera_And_The_Background_Is_Cleared()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        var image = Capture(() =>
        {
            ClearBackground(new Color(0, 255, 0));
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        });

        GetImageColor(image, 2, 2).Should().Be(new Color(0, 255, 0));
        var center = GetImageColor(image, 32, 32);
        (center.R == center.G && center.G == center.B).Should().BeTrue("a white cube is shaded gray, not tinted");
        center.R.Should().BeInRange(60, 254, "the face toward the camera is lit by the fixed light, not black and not full white");
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Point_Light_Entity_Lights_The_Faces_Turned_Toward_It()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 4)));
        var square = ecs.Spawn();
        ecs.Add(square, new Mesh([new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, -1, 0), new(1, 1, 0), new(-1, 1, 0)]));
        ecs.Add(square, new Material(Vector4.One));
        ecs.Add(square, new Transform(Vector3.Zero));
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Point(Vector3.One, 4f));
        ecs.Add(lamp, new Transform(new Vector3(0, 0, 2)));

        var front = Capture(() => ClearBackground(Color.Black), "front");
        ecs.GetRef<Transform>(lamp).Position = new Vector3(0, 0, -2);
        var behind = Capture(() => ClearBackground(Color.Black), "behind");

        GetImageColor(front, 32, 32).R.Should().BeGreaterThan(200, "the light is 2 units in front of the square, facing it");
        GetImageColor(behind, 32, 32).R.Should().BeLessThan(10, "a face turned away from the only light gets none");
    }

    [NeedsVulkanFact]
    public void A_Camera_Entity_With_A_Render_Texture_Draws_Into_It_Beside_The_Window_Camera()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        Vector3[] square = [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, -1, 0), new(1, 1, 0), new(-1, 1, 0)];
        var main = ecs.Spawn();
        ecs.Add(main, new Camera(45f));
        ecs.Add(main, new Transform(new Vector3(0, 0, 4)));
        var red = ecs.Spawn();
        ecs.Add(red, new Mesh(square));
        ecs.Add(red, new Material(new Vector4(1, 0, 0, 1)) { EmissiveFactor = new Vector3(1, 0, 0) });
        ecs.Add(red, new Transform(Vector3.Zero));

        var view = LoadRenderTexture(32, 32);
        var side = ecs.Spawn();
        ecs.Add(side, new Camera(45f, target: view) { Background = Color.Blue });
        ecs.Add(side, new Transform(new Vector3(20, 0, 4)));
        var green = ecs.Spawn();
        ecs.Add(green, new Mesh(square));
        ecs.Add(green, new Material(new Vector4(0, 1, 0, 1)) { EmissiveFactor = new Vector3(0, 1, 0) });
        ecs.Add(green, new Transform(new Vector3(20, 0, 0)));

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTexture(view.Texture, 0, 0, Color.White);
        }, "cameras");

        var corner = GetImageColor(image, 16, 16);
        corner.G.Should().BeGreaterThan(200, "the side camera drew the green square into its texture");
        corner.R.Should().BeLessThan(60);
        GetImageColor(image, 1, 1).B.Should().BeGreaterThan(200, "its texture is cleared to the camera's background");
        GetImageColor(image, 48, 48).R.Should().BeGreaterThan(200, "the window camera drew the red square");
        UnloadRenderTexture(view);
    }

    [NeedsVulkanFact]
    public void A_Model_Shader_Reads_Its_Uniforms_By_Name_As_Each_Draw_Set_Them()
    {
        Open(96, 48);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            uniform float4 paint;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return paint;
            }
            """, "paint.slang");
        var paint = GetShaderLocation(shader, "paint");
        paint.Should().BeGreaterThanOrEqualTo(0);
        GetShaderLocation(shader, "missing").Should().Be(-1);

        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].Shader = shader;
        // From 3 units away a 96 by 48 frame shows 2.5 units either side, so the cubes' centers
        // land 23 pixels either side of the middle.
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            SetShaderValue(shader, paint, new Vector4(1, 0, 0, 1));
            DrawModel(cube, new Vector3(-1.2f, 0, 0), 1, Color.White);
            SetShaderValue(shader, paint, new Vector4(0, 0, 1, 1));
            DrawModel(cube, new Vector3(1.2f, 0, 0), 1, Color.White);
            EndMode3D();
        });

        GetImageColor(image, 24, 24).Should().Be(new Color(255, 0, 0), "the left cube was drawn while paint was red");
        GetImageColor(image, 72, 24).Should().Be(new Color(0, 0, 255), "the right cube was drawn after paint turned blue");
        UnloadModel(cube);
        UnloadShader(shader);
    }

    // How many pixels of a rectangle of the image pass a test.
    private static int Count(Image image, int x0, int y0, int x1, int y1, Func<Color, bool> test)
    {
        var count = 0;
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
            if (test(GetImageColor(image, x, y))) count++;
        return count;
    }

    [NeedsVulkanFact]
    public void With_Culling_On_Every_Shape_And_Text_Is_Drawn_As_Raylib_Draws_Them_And_A_Clockwise_Triangle_Is_Not()
    {
        Open(256, 128);
        var white = LoadTextureFromImage(GenImageColor(8, 8, Color.White));
        var patch = new NPatchInfo(new Rectangle(0, 0, 8, 8), 2, 2, 2, 2);
        // Each shape in a cell 32 pixels square, eight to a row, in the order drawn.
        Action<Vector2>[] shapes =
        [
            at => DrawRectangleV(at + new Vector2(4, 4), new Vector2(24, 24), Color.White),
            at => DrawRectanglePro(new Rectangle(at.X + 16, at.Y + 16, 20, 20), new Vector2(10, 10), 30, Color.White),
            at => DrawRectangleRounded(new Rectangle(at.X + 4, at.Y + 4, 24, 24), 0.5f, 4, Color.White),
            at => DrawRectangleGradientEx(new Rectangle(at.X + 4, at.Y + 4, 24, 24), Color.White, Color.White, Color.White, Color.White),
            at => DrawRectangleLinesEx(new Rectangle(at.X + 4, at.Y + 4, 24, 24), 4, Color.White),
            at => DrawRectangleRoundedLinesEx(new Rectangle(at.X + 8, at.Y + 8, 16, 16), 0.5f, 4, 3, Color.White),
            at => DrawCircleV(at + new Vector2(16, 16), 12, Color.White),
            at => DrawCircleGradient(at + new Vector2(16, 16), 12, Color.White, Color.White),
            at => DrawCircleSector(at + new Vector2(16, 16), 12, 0, 270, 8, Color.White),
            at => DrawCircleSector(at + new Vector2(16, 16), 12, 270, 0, 8, Color.White),
            at => DrawCircleLinesEx(at + new Vector2(16, 16), 12, 4, Color.White),
            at => DrawEllipseV(at + new Vector2(16, 16), 14, 8, Color.White),
            at => DrawEllipseLinesEx(at + new Vector2(16, 16), 14, 8, 3, Color.White),
            at => DrawRing(at + new Vector2(16, 16), 6, 13, 0, 360, 0, Color.White),
            at => DrawRingLinesEx(at + new Vector2(16, 16), 6, 13, 0, 270, 0, 3, Color.White),
            at => DrawPoly(at + new Vector2(16, 16), 6, 12, 0, Color.White),
            at => DrawPolyLinesEx(at + new Vector2(16, 16), 6, 12, 0, 4, Color.White),
            at => DrawLineEx(at + new Vector2(28, 4), at + new Vector2(4, 28), 6, Color.White),
            at => DrawLineBezier(at + new Vector2(4, 4), at + new Vector2(28, 28), 4, Color.White),
            at => DrawSplineCatmullRom([at + new Vector2(2, 4), at + new Vector2(10, 28), at + new Vector2(22, 4), at + new Vector2(30, 28)], 4, Color.White),
            at => DrawTriangle(at + new Vector2(16, 4), at + new Vector2(4, 28), at + new Vector2(28, 28), Color.White),
            at => DrawTriangleFan([at + new Vector2(16, 16), at + new Vector2(28, 16), at + new Vector2(16, 4), at + new Vector2(4, 16), at + new Vector2(16, 28)], Color.White),
            at => DrawTriangleStrip([at + new Vector2(4, 4), at + new Vector2(4, 28), at + new Vector2(16, 4), at + new Vector2(16, 28), at + new Vector2(28, 4), at + new Vector2(28, 28)], Color.White),
            at => DrawTexturePro(white, new Rectangle(0, 0, 8, 8), new Rectangle(at.X + 4, at.Y + 4, 24, 24), Vector2.Zero, 0, Color.White),
            at => DrawTexturePro(white, new Rectangle(0, 0, -8, -8), new Rectangle(at.X + 16, at.Y + 16, 24, 24), new Vector2(12, 12), 45, Color.White),
            at => DrawTextureNPatch(white, patch, new Rectangle(at.X + 4, at.Y + 4, 24, 24), Vector2.Zero, 0, Color.White),
            at => DrawText("Hi", (int)at.X + 4, (int)at.Y + 6, 20, Color.White),
            at => DrawTextPro(GetFontDefault(), "Hi", at + new Vector2(16, 16), new Vector2(8, 10), 90, 20, 2, Color.White),
        ];

        static Vector2 Cell(int i) => new(i % 8 * 32, i / 8 * 32);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            rlEnableBackfaceCulling();
            for (int i = 0; i < shapes.Length; i++) shapes[i](Cell(i));
            // Given clockwise, which raylib leaves out with culling on.
            DrawTriangle(Cell(28) + new Vector2(16, 4), Cell(28) + new Vector2(28, 28), Cell(28) + new Vector2(4, 28), Color.White);
            // Culling the front faces leaves out a rectangle, and with culling off the clockwise
            // triangle is drawn.
            rlSetCullFace(RlCullFace.Front);
            DrawRectangleV(Cell(29) + new Vector2(4, 4), new Vector2(24, 24), Color.White);
            rlSetCullFace(RlCullFace.Back);
            rlDisableBackfaceCulling();
            DrawTriangle(Cell(30) + new Vector2(16, 4), Cell(30) + new Vector2(28, 28), Cell(30) + new Vector2(4, 28), Color.White);
        });

        int Lit(int i) => Count(image, (int)Cell(i).X, (int)Cell(i).Y, (int)Cell(i).X + 32, (int)Cell(i).Y + 32, c => c.R > 128);
        for (int i = 0; i < shapes.Length; i++)
            Lit(i).Should().BeGreaterThan(30, $"the shape in cell {i} is counterclockwise as raylib's is, so culling keeps it");
        Lit(28).Should().Be(0, "the clockwise triangle faces away");
        Lit(29).Should().Be(0, "culling front faces leaves out the rectangle");
        Lit(30).Should().BeGreaterThan(30, "with culling off the clockwise triangle is drawn");
        UnloadTexture(white);
    }

    [NeedsVulkanFact]
    public void With_Culling_On_Solids_Show_Their_Outsides_And_Billboards_Face_The_Camera()
    {
        Open(160, 64);
        var camera = new Camera3D(new Vector3(0, 3, 10), Vector3.Zero, Vector3.UnitY, 45);
        var white = LoadTextureFromImage(GenImageColor(8, 8, Color.White));
        (Color Color, Action<Color> Draw)[] solids =
        [
            (new Color(255, 0, 0), c => DrawCube(new Vector3(-4, 0, 0), 1.2f, 1.2f, 1.2f, c)),
            (new Color(0, 255, 0), c => DrawSphere(new Vector3(-2, 0, 0), 0.7f, c)),
            (new Color(0, 0, 255), c => DrawCylinder(new Vector3(0, -0.6f, 0), 0.5f, 0.7f, 1.2f, 12, c)),
            (new Color(255, 255, 0), c => DrawCapsule(new Vector3(2, -0.5f, 0), new Vector3(2, 0.5f, 0), 0.5f, 12, 6, c)),
            (new Color(0, 255, 255), c => DrawBillboard(camera, white, new Vector3(4, 0, 0), 1.2f, c)),
            (new Color(255, 0, 255), c => DrawPlane(new Vector3(0, -1.5f, 0), new Vector2(10, 2), c)),
        ];

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            rlEnableBackfaceCulling();
            BeginMode3D(camera);
            foreach (var (color, draw) in solids) draw(color);
            EndMode3D();
        });

        foreach (var (color, _) in solids)
            Count(image, 0, 0, 160, 64, c => c == color).Should().BeGreaterThan(20, $"the solid in {color} faces the camera from outside, so culling keeps the side seen");
        UnloadTexture(white);
    }

    // A cube drawn at a camera's own place, as raylib's split screen draws each player, is hollow
    // seen from within, since 3D shapes leave out their back faces as rlgl's do from the start,
    // where a 2D triangle given clockwise still shows until culling is turned on.
    [NeedsVulkanFact]
    public void A_Cube_Around_The_Camera_Is_Hollow_Until_Culling_Is_Off_And_2D_Draws_Both_Faces()
    {
        Open(32, 32);
        var camera = new Camera3D(new Vector3(0, 1, 0), new Vector3(0, 1, 5), Vector3.UnitY, 60);
        void Scene()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(camera.Position, 1, 1, 1, Color.Red);
            EndMode3D();
            DrawTriangle(new Vector2(0, 0), new Vector2(8, 0), new Vector2(0, 8), Color.Blue);
        }

        var hollow = Capture(Scene, "hollow");
        GetImageColor(hollow, 16, 16).Should().Be(Color.Black, "the cube's faces seen from within are its back faces, left out");
        GetImageColor(hollow, 2, 2).Should().Be(Color.Blue, "a 2D triangle given clockwise on the screen draws both faces");

        rlDisableBackfaceCulling();
        var filled = Capture(Scene, "filled");
        GetImageColor(filled, 16, 16).Should().Be(Color.Red, "with culling off the cube's insides are drawn");
    }

    [NeedsVulkanFact]
    public void Custom_Blend_Factors_Combine_Color_And_Alpha_As_Rlgl_Sets_Them_And_A_Clear_Texel_Is_Blended_In_2D()
    {
        Open(64, 16);
        var target = LoadRenderTexture(16, 16);
        var image = Capture(() =>
        {
            // The larger of each channel, rlgl's equation leaving the factors out
            ClearBackground(Color.Black);
            DrawRectangle(0, 0, 16, 16, new Color(100, 20, 0));
            rlSetBlendFactors(RlBlendFactor.One, RlBlendFactor.One, RlBlendEquation.Max);
            BeginBlendMode(BlendMode.Custom);
            DrawRectangle(0, 0, 16, 16, new Color(50, 200, 0));
            EndBlendMode();

            // The color kept and the alpha replaced, as textures_magnifying_glass masks its view
            BeginTextureMode(target);
            ClearBackground(new Color(0, 0, 255));
            BeginBlendMode(BlendMode.CustomSeparate);
            rlSetBlendFactorsSeparate(RlBlendFactor.Zero, RlBlendFactor.One, RlBlendFactor.One, RlBlendFactor.Zero, RlBlendEquation.FuncAdd, RlBlendEquation.FuncAdd);
            DrawRectangle(0, 0, 8, 16, new Color(255, 0, 0, 0));
            EndBlendMode();
            EndTextureMode();
            DrawRectangle(16, 0, 16, 16, new Color(0, 255, 0));
            DrawTexture(target.Texture, 16, 0, Color.White);

            // A texel with no alpha adds its color in 2D, as raylib's does, where the 3D pass discards it
            BeginBlendMode(BlendMode.AddColors);
            DrawRectangle(32, 0, 16, 16, new Color(0, 100, 0, 0));
            EndBlendMode();
        });

        GetImageColor(image, 8, 8).Should().Be(new Color(100, 200, 0), "the maximum of each channel");
        GetImageColor(image, 20, 8).Should().Be(new Color(0, 255, 0), "the target's left half kept its blue and took the clear alpha, so the green shows through");
        GetImageColor(image, 28, 8).Should().Be(new Color(0, 0, 255), "its right half is the blue it was cleared to");
        GetImageColor(image, 40, 8).Should().Be(new Color(0, 100, 0), "the clear texel's color is added");
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void A_Shader_Writes_The_Depth_2D_Is_Tested_Against_Once_The_Test_Is_On_And_The_Mask_Keeps_It_From_Being_Written()
    {
        Open(48, 16);
        // The depth of each pixel one less its red, so a red shape is nearest and a black one farthest
        var shader = LoadShaderFromMemory("""
            import engine;

            struct Output { float4 color : SV_Target; float depth : SV_Depth; };

            [shader("fragment")]
            Output fragmentMain(VertexOutput input)
            {
                Output output;
                output.color = input.color;
                output.depth = 1.0 - input.color.r;
                return output;
            }
            """, "depth.slang");

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            rlEnableDepthTest();
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 16, 16, new Color(255, 0, 0));
            DrawRectangle(16, 0, 16, 16, new Color(0, 0, 255));
            rlDisableDepthMask();
            DrawRectangle(32, 0, 16, 16, new Color(255, 0, 0));
            rlEnableDepthMask();
            EndShaderMode();
            // Shapes in 2D lie halfway into the depth
            DrawRectangle(0, 0, 48, 16, Color.White);
            rlDisableDepthTest();
        });

        GetImageColor(image, 8, 8).Should().Be(new Color(255, 0, 0), "the red square's depth is nearer than the white one's, which the test leaves out");
        GetImageColor(image, 24, 8).Should().Be(Color.White, "the blue square's depth is the farthest, so the white one passes");
        GetImageColor(image, 40, 8).Should().Be(Color.White, "with the mask off the red square drew without writing its depth");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void Text_Lands_Inside_The_Box_MeasureText_Gives_It()
    {
        Open(96, 48);
        var red = new Color(255, 0, 0);
        var width = 0;

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawText("HH", 8, 8, 20, red);
            width = MeasureText("HH", 20);
        });

        // Glyph edges blend into the background, so a stroke pixel is one that is mostly red.
        Count(image, 8, 8, 8 + width, 28, c => c.R > 150 && c.G < 60 && c.B < 60).Should().BeGreaterThan(40, "the strokes of two H's are drawn there");
        Count(image, 8 + width + 4, 0, 96, 48, c => c == Color.Black).Should().Be((96 - 8 - width - 4) * 48, "nothing is drawn past the text");
        Count(image, 0, 32, 96, 48, c => c == Color.Black).Should().Be(96 * 16, "nothing is drawn below it");
    }

    [NeedsVulkanFact]
    public void A_Distance_Field_Font_Keeps_Its_Edges_Sharp_Drawn_Far_Past_Its_Bake()
    {
        Open(96, 128);
        var lato = Engine.Tests.Api.FontTests.Lato();
        var plain = LoadFontEx(lato, 16, ['I']);
        var sdf = LoadFontEx(lato, 16, ['I'], FontType.Sdf);
        sdf.Type.Should().Be(FontType.Sdf);

        // Pixels on an edge, between the background and the stroke, and the stroke's ink in all.
        (int Edge, double Ink) Strokes(Font font)
        {
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                DrawTextEx(font, "I", new Vector2(16, 0), 128, 0, Color.White);
            }, $"I {font.Type}");
            var ink = 0.0;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 96; x++)
                    ink += GetImageColor(image, x, y).R / 255.0;
            return (Count(image, 0, 0, 96, 128, c => c.R is > 30 and < 225), ink);
        }

        // The coverage bake as it is, which a font loaded from a file is not drawn from this large,
        // since it is baked again at the size.
        var scaled = new Font(plain.Texture, plain.BaseSize, plain.LineHeight, plain.Glyphs.ToDictionary(), plain.Atlas);
        var blurred = Strokes(scaled);
        var sharp = Strokes(sdf);
        var rebaked = Strokes(plain);
        sharp.Ink.Should().BeGreaterThan(400, "the stroke is drawn");
        sharp.Ink.Should().BeApproximately(blurred.Ink, blurred.Ink * 0.3, "both draw the same stroke");
        sharp.Edge.Should().BeLessThan(blurred.Edge / 2, $"the distance field smooths over a pixel where a coverage bake scaled eight times smooths over eight, {sharp.Edge} against {blurred.Edge}");
        rebaked.Edge.Should().BeLessThan(blurred.Edge / 2, "the coverage font baked again at 128 pixels is as sharp");
        UnloadFont(plain);
        UnloadFont(sdf);
    }
}
