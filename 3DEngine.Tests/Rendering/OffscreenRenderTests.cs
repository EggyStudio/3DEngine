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
public sealed class OffscreenRenderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-offscreen-").FullName;

    // Errors the validation layer had reported before this test, so the test fails on its own.
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
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
        var path = Path.Combine(_directory, name + ".png");
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

        var blurred = Strokes(plain);
        var sharp = Strokes(sdf);
        sharp.Ink.Should().BeGreaterThan(400, "the stroke is drawn");
        sharp.Ink.Should().BeApproximately(blurred.Ink, blurred.Ink * 0.3, "both draw the same stroke");
        sharp.Edge.Should().BeLessThan(blurred.Edge / 2, $"the distance field smooths over a pixel where a coverage bake scaled eight times smooths over eight, {sharp.Edge} against {blurred.Edge}");
        UnloadFont(plain);
        UnloadFont(sdf);
    }

    // A cube drawn into a target, and that target's depth drawn over the window, at the given samples.
    private Image TargetDepth(int samples)
    {
        Open(64, 64, samples);
        var target = LoadRenderTexture(32, 32);
        // A unit from the cube's front face, where the depth is 0.95 with the near plane at 0.05,
        // and wide enough to see past its edges.
        var camera = new Camera3D(new Vector3(0, 0, 1.5f), Vector3.Zero, Vector3.UnitY, 90);

        var image = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(Vector3.Zero, 1, 1, 1, Color.Red);
            EndMode3D();
            EndTextureMode();

            ClearBackground(Color.Blue);
            DrawTexture(target.Depth, 0, 0, Color.White);
        }, $"depth {samples}");
        UnloadRenderTexture(target);
        return image;
    }

    [NeedsVulkanFact]
    public void A_Render_Target_Has_Its_Depth_To_Sample()
    {
        var image = TargetDepth(4);

        var cube = GetImageColor(image, 16, 16);
        cube.R.Should().BeInRange(236, 248, "the cube's face is a unit from the camera, at a depth of 0.95");
        (cube.G, cube.B).Should().Be(((byte)0, (byte)0), "a depth is sampled into red alone");
        GetImageColor(image, 1, 1).R.Should().Be(255, "where nothing was drawn the depth is cleared to the far plane");
        GetImageColor(image, 48, 48).Should().Be(Color.Blue, "the depth texture is the target's size");
    }

    [NeedsVulkanFact]
    public void A_Render_Target_Drawn_With_One_Sample_Has_Its_Depth_To_Sample()
    {
        var image = TargetDepth(1);

        GetImageColor(image, 16, 16).R.Should().BeInRange(236, 248);
        GetImageColor(image, 1, 1).R.Should().Be(255);
    }

    [NeedsVulkanFact]
    public void A_Render_Target_Holds_What_Was_Drawn_Into_It_And_Draws_As_A_Texture()
    {
        Open(64, 32);
        var target = LoadRenderTexture(16, 16);
        var green = new Color(0, 255, 0);

        var image = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(green);
            DrawRectangle(0, 0, 8, 16, Color.Blue);
            EndTextureMode();

            ClearBackground(Color.Black);
            DrawTexture(target.Texture, 32, 8, Color.White);
        });

        GetImageColor(image, 36, 16).Should().Be(Color.Blue, "the target's left half was drawn blue");
        GetImageColor(image, 44, 16).Should().Be(green, "its right half kept the clear color");
        GetImageColor(image, 16, 16).Should().Be(Color.Black, "the window around it is the window's own clear");
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_Reads_Its_Slot_And_Applies_Only_Inside_Its_Mode()
    {
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return param(0);
            }
            """, "slot.slang");
        SetShaderValue(shader, 0, new Vector4(1, 0, 1, 1));

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 32, 32, Color.White);
            EndShaderMode();
            DrawRectangle(32, 0, 32, 32, Color.White);
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(255, 0, 255), "the shader returns slot 0");
        GetImageColor(image, 48, 16).Should().Be(Color.White, "after EndShaderMode the engine's own shader draws");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_Reads_Named_Uniforms_As_They_Were_When_Each_Shape_Was_Drawn()
    {
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            uniform float4 tint;
            uniform float strength;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(tint.rgb * strength, 1.0);
            }
            """, "named.slang");
        var tint = GetShaderLocation(shader, "tint");
        var strength = GetShaderLocation(shader, "strength");
        tint.Should().BeGreaterThanOrEqualTo(0);
        SetShaderValue(shader, strength, 1f);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            SetShaderValue(shader, tint, new Vector4(1, 0, 0, 1));
            DrawRectangle(0, 0, 32, 32, Color.White);
            SetShaderValue(shader, tint, new Vector4(0, 0, 1, 1));
            DrawRectangle(32, 0, 32, 32, Color.White);
            EndShaderMode();
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(255, 0, 0), "the left square was drawn while the tint was red");
        GetImageColor(image, 48, 16).Should().Be(new Color(0, 0, 255), "and the right one after it was set to blue");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_Mixes_Its_Own_Texture_With_The_One_Drawn()
    {
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import engine;

            Sampler2D detail;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return boundTexture.Sample(input.uv) * detail.Sample(input.uv);
            }
            """, "detail.slang");
        var yellow = LoadTextureFromImage(GenImageColor(2, 2, new Color(255, 255, 0)));
        var cyan = LoadTextureFromImage(GenImageColor(2, 2, new Color(0, 255, 255)));
        var detail = GetShaderLocation(shader, "detail");
        detail.Should().BeGreaterThanOrEqualTo(0);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            SetShaderValueTexture(shader, detail, cyan);
            DrawTexturePro(yellow, new Rectangle(0, 0, 2, 2), new Rectangle(0, 0, 32, 32), Vector2.Zero, 0, Color.White);
            EndShaderMode();
        }, "immediate detail");

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0), "yellow times cyan is green");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void An_Immediate_Shader_With_Uniforms_And_A_Texture_Of_Its_Own_Reads_Both()
    {
        Open(32, 32);
        // With a uniform, Slang puts the uniform buffer at binding 0 and the texture after the pass's.
        var shader = LoadShaderFromMemory("""
            import engine;

            uniform float4 tint;
            Sampler2D detail;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return detail.Sample(input.uv) * tint;
            }
            """, "tinteddetail.slang");
        var cyan = LoadTextureFromImage(GenImageColor(2, 2, new Color(0, 255, 255)));
        SetShaderValueTexture(shader, GetShaderLocation(shader, "detail"), cyan);
        SetShaderValue(shader, GetShaderLocation(shader, "tint"), new Vector4(0, 1, 0, 1));

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(shader);
            DrawRectangle(0, 0, 32, 32, Color.White);
            EndShaderMode();
        }, "tinted detail");

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0), "cyan times green is green");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Frame_Draws_More_Models_With_Their_Own_Uniforms_Than_One_Descriptor_Pool_Holds()
    {
        Open(32, 32);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            uniform float4 tint;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return tint;
            }
            """, "tinted.slang");
        var tint = GetShaderLocation(shader, "tint");
        var cube = LoadModelFromMesh(GenMeshCube(0.01f, 0.01f, 0.01f));
        cube.Materials[0] = new ModelMaterial(Color.White) { Shader = shader };
        var big = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        big.Materials[0] = new ModelMaterial(Color.White) { Shader = shader };

        // Each draw with uniforms of its own takes a set, and a pool holds 4096.
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45));
            for (int i = 0; i < 5000; i++)
            {
                SetShaderValue(shader, tint, new Vector4(i / 5000f, 0, 0, 1));
                DrawModel(cube, new Vector3(10, 0, -i), 1, Color.White);
            }
            SetShaderValue(shader, tint, new Vector4(0, 1, 0, 1));
            DrawModel(big, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }, "many sets");

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0, 255), "the last draw, past the first pool's sets, has its own uniforms");
        UnloadModel(cube);
        UnloadModel(big);
    }

    [NeedsVulkanFact]
    public void A_Model_Shader_Mixes_Its_Own_Texture_With_The_Base_Color()
    {
        Open(32, 32);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            Sampler2D detail;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(toDisplay(baseColor(input).rgb) * detail.Sample(input.uv).rgb, 1.0);
            }
            """, "modeldetail.slang");
        var cyan = LoadTextureFromImage(GenImageColor(2, 2, new Color(0, 255, 255)));
        SetShaderValueTexture(shader, GetShaderLocation(shader, "detail"), cyan);
        var cube = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        cube.Materials[0] = new ModelMaterial(new Color(255, 255, 0)) { Shader = shader };

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45));
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }, "model detail");

        // Green, a little under full where the pass's tonemapping bends the brightest light.
        var middle = GetImageColor(image, 16, 16);
        (middle.R < 10 && middle.G > 230 && middle.B < 10).Should().BeTrue($"the yellow base color times cyan is green, not {middle}");
        UnloadModel(cube);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void ImGui_Draws_Over_The_Frame_Where_It_Is_Told()
    {
        Open(64, 32);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawRectangle(0, 0, 64, 32, Color.Blue);
            // ImGui colors are packed as ABGR, so this is opaque green.
            ImGuiNET.ImGui.GetForegroundDrawList().AddRectFilled(new Vector2(8, 8), new Vector2(24, 24), 0xFF00FF00);
        });

        GetImageColor(image, 16, 16).Should().Be(new Color(0, 255, 0), "ImGui draws after the immediate shapes, over them");
        GetImageColor(image, 48, 16).Should().Be(Color.Blue, "the rest of the frame is the shapes beneath");
    }

    [NeedsVulkanFact]
    public void A_Spot_Lights_Inside_Its_Cone_And_A_Range_Ends_A_Light()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 4)));
        var wall = ecs.Spawn();
        ecs.Add(wall, new Mesh([new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, -2, 0), new(2, 2, 0), new(-2, 2, 0)]));
        ecs.Add(wall, new Material(Vector4.One));
        ecs.Add(wall, new Transform(Vector3.Zero));

        // A narrow spot 2 units in front of the wall, pointing at it along -Z.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Spot(Vector3.One, 8f, innerAngle: 5f, outerAngle: 8f));
        ecs.Add(lamp, new Transform(new Vector3(0, 0, 2)));
        var spot = Capture(() => ClearBackground(Color.Black), "spot");

        // The same light as a point that stops 1 unit short of the wall.
        ecs.GetRef<Light>(lamp) = Light.Point(Vector3.One, 8f, range: 1f);
        var ranged = Capture(() => ClearBackground(Color.Black), "ranged");

        GetImageColor(spot, 32, 32).R.Should().BeGreaterThan(200, "the middle of the wall is inside the cone");
        GetImageColor(spot, 52, 32).R.Should().BeLessThan(10, "the wall's edge is far outside an 8 degree cone");
        GetImageColor(ranged, 32, 32).R.Should().BeLessThan(10, "the wall is past the light's range");
    }

    // A byte of a captured frame as the linear light it encodes, since the model pass lights in
    // linear space and stores sRGB.
    private static float Linear(byte value)
    {
        var c = value / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    // A white wall facing a camera 4 units away down +Z, filling the frame.
    private static int SpawnWallAndCamera(EcsWorld ecs)
    {
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 4)));
        var wall = ecs.Spawn();
        ecs.Add(wall, new Mesh([new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, -2, 0), new(2, 2, 0), new(-2, 2, 0)]));
        ecs.Add(wall, new Material(Vector4.One));
        ecs.Add(wall, new Transform(Vector3.Zero));
        return wall;
    }

    [NeedsVulkanFact]
    public void A_Smooth_Surface_Mirrors_A_Light_A_Rough_One_Scatters()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var wall = SpawnWallAndCamera(ecs);

        // Pointing straight at the wall the way the camera looks, so the middle of the wall
        // mirrors it into the camera, and its diffuse light equals an ambient light's.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Directional(Vector3.One, 0.5f));
        ecs.Add(lamp, new Transform(Vector3.Zero));
        ecs.GetRef<Material>(wall).RoughnessFactor = 0.25f;
        var smooth = Capture(() => ClearBackground(Color.Black), "smooth");
        ecs.GetRef<Material>(wall).RoughnessFactor = 0.9f;
        var rough = Capture(() => ClearBackground(Color.Black), "rough");
        ecs.GetRef<Light>(lamp) = Light.Ambient(Vector3.One, 0.5f);
        var ambient = Capture(() => ClearBackground(Color.Black), "ambient");

        var diffuse = Linear(GetImageColor(ambient, 32, 32).R);
        diffuse.Should().BeInRange(0.48f, 0.58f, "half a white light on a white wall returns about half the light, stored as sRGB 188");
        Linear(GetImageColor(rough, 32, 32).R).Should().BeApproximately(diffuse, 0.06f, "a rough surface spreads its highlight too thin to see");
        Linear(GetImageColor(smooth, 32, 32).R).Should().BeGreaterThan(diffuse + 0.3f, "a smooth one gathers it where the wall mirrors the light");
    }

    [NeedsVulkanFact]
    public void A_Metal_Reflects_In_Its_Own_Color_And_Scatters_No_Diffuse_Light()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var wall = SpawnWallAndCamera(ecs);
        ref var material = ref ecs.GetRef<Material>(wall);
        material.Albedo = new Vector4(1, 0.2f, 0.2f, 1);
        material.RoughnessFactor = 0.3f;

        // From the side, so the middle of the wall shows its diffuse light and no highlight.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Directional(Vector3.One, 1f));
        ecs.Add(lamp, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3), Vector3.One));
        var plastic = Capture(() => ClearBackground(Color.Black), "plastic");
        ecs.GetRef<Material>(wall).MetallicFactor = 1;
        var metal = Capture(() => ClearBackground(Color.Black), "metal");

        Linear(GetImageColor(plastic, 32, 32).R).Should().BeGreaterThan(0.3f, "red plastic scatters the light that falls on it");
        Linear(GetImageColor(metal, 32, 32).R).Should().BeLessThan(0.05f, "a metal scatters none, and this light is not mirrored toward the camera");
    }

    [NeedsVulkanFact]
    public void A_Normal_Map_Turns_A_Flat_Surface_Toward_A_Light()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 4, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        var ecs = GetApp().World.Resource<EcsWorld>();
        var sun = ecs.Spawn();
        // From +X, 20 degrees over the plane, so a surface tilted toward +X faces it. The light's
        // -Z turned a quarter about Y points down -X, and then 20 degrees about Z points it down.
        ecs.Add(sun, Light.Directional(Vector3.One, 1f));
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.Concatenate(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.35f)), Vector3.One));

        // A plane facing up whose texture coordinates run along X, and a normal map leaning every
        // normal 45 degrees toward +u.
        var plane = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        plane.Materials[0] = plane.Materials[0] with { Roughness = 1 };
        var flat = Capture(Draw, "flat");
        var leaning = LoadTextureFromImage(GenImageColor(4, 4, new Color(218, 128, 218)));
        plane.Materials[0] = plane.Materials[0] with { NormalMap = leaning };
        var mapped = Capture(Draw, "mapped");

        ((int)GetImageColor(mapped, 32, 32).R).Should().BeGreaterThan(GetImageColor(flat, 32, 32).R + 40, "the map turns the surface toward the low light");

        // Up in the map is toward the top of the image, falling v, which on this plane is -Z. A
        // map leaning that way faces a sun low in -Z and turns from one low in +Z.
        var up = LoadTextureFromImage(GenImageColor(4, 4, new Color(128, 218, 218)));
        plane.Materials[0] = plane.Materials[0] with { NormalMap = up };
        ecs.GetRef<Transform>(sun).Rotation = Quaternion.Concatenate(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.35f));
        var facing = Capture(Draw, "facing");
        ecs.GetRef<Transform>(sun).Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.35f);
        var away = Capture(Draw, "away");
        ((int)GetImageColor(facing, 32, 32).R).Should().BeGreaterThan(GetImageColor(away, 32, 32).R + 40, "the map's up leans toward -Z");

        UnloadModel(plane);
        UnloadTexture(leaning);
        UnloadTexture(up);

        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(plane, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }
    }

    [NeedsVulkanFact]
    public void Light_Summed_Past_One_Keeps_Its_Hue_Instead_Of_Clamping_To_White()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        SpawnWallAndCamera(ecs);

        // An orange light four times over: (4, 2, 1), which a plain clamp turns white.
        var lamp = ecs.Spawn();
        ecs.Add(lamp, Light.Ambient(new Vector3(1f, 0.5f, 0.25f), 4f));
        var image = Capture(() => ClearBackground(Color.Black), "orange");

        var color = GetImageColor(image, 32, 32);
        color.R.Should().BeGreaterThan(245, "the brightest channel comes close to full");
        Linear(color.G).Should().BeApproximately(0.5f * Linear(color.R), 0.04f, "green keeps half of red's light");
        Linear(color.B).Should().BeApproximately(0.25f * Linear(color.R), 0.03f, "blue keeps a quarter, so the light reads as the same orange");
    }

    [NeedsVulkanFact]
    public void A_Directional_Light_Casts_A_Shadow_Only_When_Asked()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        SpawnWallAndCamera(ecs);

        // A square a unit in front of the wall, right of the middle, and a sun pointing down -X and
        // -Z at 45 degrees, which throws the square's shadow a unit to its left on the wall. That
        // puts it over the wall's middle, where the square itself does not hide it from the camera.
        var square = ecs.Spawn();
        ecs.Add(square, new Mesh([new(0.25f, -0.5f, 1), new(1.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, 0.5f, 1)]));
        ecs.Add(square, new Material(Vector4.One));
        ecs.Add(square, new Transform(Vector3.Zero));

        var sun = ecs.Spawn();
        ecs.Add(sun, Light.Directional(Vector3.One, 1f) with { CastsShadows = true });
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4), Vector3.One));
        var shadowed = Capture(() => ClearBackground(Color.Black), "shadowed");

        ecs.GetRef<Light>(sun).CastsShadows = false;
        var unshadowed = Capture(() => ClearBackground(Color.Black), "unshadowed");

        // Pixel 27 across is the wall a quarter unit left of the middle, in the shadow. Pixel 9
        // down from the top is the wall above the square's reach.
        GetImageColor(shadowed, 27, 32).R.Should().BeLessThan(10, "the square stands between the sun and this part of the wall");
        GetImageColor(shadowed, 27, 9).R.Should().BeGreaterThan(120, "the wall above the square's shadow is lit");
        GetImageColor(unshadowed, 27, 32).R.Should().BeGreaterThan(120, "a light that does not cast shadows lights the wall behind the square");
    }

    [NeedsVulkanFact]
    public void A_Shadow_Fades_Out_Toward_The_Shadow_Distance_Rather_Than_Stopping_At_A_Line()
    {
        Open(64, 256);
        // A long wall left of the ground the camera looks down along, under a low sun from the
        // left, so its shadow covers that ground as far as shadows are drawn.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -0.35f, 0)), Color.White, 1, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(800, 800, 1, 1));
        var wall = LoadModelFromMesh(GenMeshCube(2, 80, 800));
        var camera = new Camera3D(new Vector3(20, 30, 0), new Vector3(20, 0, -200), Vector3.UnitY, 40);

        var image = Capture(() =>
        {
            ClearBackground(Color.Blue);
            BeginMode3D(camera);
            DrawModel(ground, new Vector3(0, 0, -390), 1, Color.White);
            DrawModel(wall, new Vector3(-10, 40, -390), 1, Color.White);
            EndMode3D();
        }, "fade");

        // Up the middle column from the camera's feet toward the horizon, the ground goes from
        // shadowed to lit where shadows end.
        // The ground is gray, and the sky above the horizon blue.
        var column = Enumerable.Range(0, 256).Select(y => GetImageColor(image, 32, 255 - y)).ToArray();
        var ground0 = column.TakeWhile(c => c.B <= c.G + 8).Select(c => (int)c.G).ToArray();
        var (dark, lit) = (ground0[..10].Average(), ground0[^10..].Average());
        lit.Should().BeGreaterThan(dark + 40, "the ground near the camera is shadowed and the far ground is lit");
        var between = ground0.Count(g => g > dark + (lit - dark) * 0.2 && g < lit - (lit - dark) * 0.2);
        between.Should().BeGreaterThan(2, "the shadow fades over a band of rows, where it used to step from dark to lit in one");
        UnloadModel(ground);
        UnloadModel(wall);
    }

    [NeedsVulkanFact]
    public void A_Point_Light_Casts_Shadows_On_Every_Side()
    {
        Open(64, 64);
        // A low light in the middle of the ground, a cube to its +X and one to its -Z, seen from
        // straight above with -Z up the image.
        var lamp = CreatePointLight(new Vector3(0, 1.5f, 0), Color.White, 30, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 15, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        Image Draw(string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(2, 0.5f, 0), 1, Color.White);
            DrawModel(cube, new Vector3(0, 0.5f, -2), 1, Color.White);
            EndMode3D();
        }, name);

        var shadowed = Draw("point shadows");
        SetLightCastsShadows(lamp, false);
        var open = Draw("point open");

        // The ground 3.5 units out each way, at (px, py) for x and z of ±3.5.
        int behindX = GetImageColor(shadowed, 50, 32).R, clearX = GetImageColor(shadowed, 14, 32).R;
        int behindZ = GetImageColor(shadowed, 32, 14).R, clearZ = GetImageColor(shadowed, 32, 50).R;
        behindX.Should().BeLessThan(clearX / 3, $"the cube at +X shadows the ground past it ({behindX} against {clearX})");
        // (3.5, 0, 2) is on the +X face too, past the cube's edge, so that face lights it.
        GetImageColor(shadowed, 50, 42).R.Should().BeGreaterThan((byte)(clearX / 2), "the +X face lights the ground the cube does not hide");
        behindZ.Should().BeLessThan(clearZ / 3, $"the cube at -Z shadows the ground past it ({behindZ} against {clearZ})");
        ((int)GetImageColor(open, 50, 32).R).Should().BeGreaterThan(clearX * 8 / 10, "without shadows the light reaches past the cube");
        UnloadModel(ground);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Shadow_Eighty_Units_Away_Falls_In_A_Far_Cascade()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();

        // The scene of the test above twenty times over, seen from eighty units, past the forty
        // the single map reached.
        var big = new Vector3(20);
        var camera = ecs.Spawn();
        ecs.Add(camera, new Camera(45f));
        ecs.Add(camera, new Transform(new Vector3(0, 0, 80)));
        var wall = ecs.Spawn();
        ecs.Add(wall, new Mesh([new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, -2, 0), new(2, 2, 0), new(-2, 2, 0)]));
        ecs.Add(wall, new Material(Vector4.One));
        ecs.Add(wall, new Transform(Vector3.Zero, Quaternion.Identity, big));
        var square = ecs.Spawn();
        ecs.Add(square, new Mesh([new(0.25f, -0.5f, 1), new(1.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, -0.5f, 1), new(1.25f, 0.5f, 1), new(0.25f, 0.5f, 1)]));
        ecs.Add(square, new Material(Vector4.One));
        ecs.Add(square, new Transform(Vector3.Zero, Quaternion.Identity, big));
        var sun = ecs.Spawn();
        ecs.Add(sun, Light.Directional(Vector3.One, 1f) with { CastsShadows = true });
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4), Vector3.One));

        var image = Capture(() => ClearBackground(Color.Black), "far");

        GetImageColor(image, 27, 32).R.Should().BeLessThan(10, "the square shadows the wall eighty units from the camera");
        GetImageColor(image, 27, 9).R.Should().BeGreaterThan(120, "the wall above the shadow is lit");

        SetShadowDistance(40);
        var near = Capture(() => ClearBackground(Color.Black), "near");
        GetImageColor(near, 27, 32).R.Should().BeGreaterThan(120, "with shadows reaching forty units, the wall at eighty has none");
    }

    [NeedsVulkanFact]
    public void A_Spot_Light_Casts_A_Shadow_In_Its_Tile()
    {
        Open(64, 64);
        var ecs = GetApp().World.Resource<EcsWorld>();
        SpawnWallAndCamera(ecs);

        // A spot at (-2, 0, 2) aimed at the wall's middle, and a strip a unit in front of the wall
        // from x -1 to -0.5, whose shadow falls from x 0 to 1 on the wall, right of the middle,
        // where the strip itself does not hide it from the camera.
        var strip = ecs.Spawn();
        ecs.Add(strip, new Mesh([new(-1, -0.5f, 1), new(-0.5f, -0.5f, 1), new(-0.5f, 0.5f, 1), new(-1, -0.5f, 1), new(-0.5f, 0.5f, 1), new(-1, 0.5f, 1)]));
        ecs.Add(strip, new Material(Vector4.One));
        ecs.Add(strip, new Transform(Vector3.Zero));
        var spot = ecs.Spawn();
        ecs.Add(spot, Light.Spot(Vector3.One, 30f, 35, 45) with { CastsShadows = true });
        ecs.Add(spot, new Transform(new Vector3(-2, 0, 2), Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 4), Vector3.One));
        var shadowed = Capture(() => ClearBackground(Color.Black), "spot");

        ecs.GetRef<Light>(spot).CastsShadows = false;
        var unshadowed = Capture(() => ClearBackground(Color.Black), "spot unshadowed");

        // Pixel 41 across is the wall half a unit right of the middle, in the shadow, and pixel 26
        // the wall a little left of it, lit and clear of the strip.
        var lit = GetImageColor(shadowed, 26, 32).R;
        lit.Should().BeGreaterThan(60, "the spot lights the wall beside the strip");
        GetImageColor(shadowed, 41, 32).R.Should().BeLessThan((byte)(lit / 4), "the strip stands between the spot and this part of the wall");
        GetImageColor(unshadowed, 41, 32).R.Should().BeGreaterThan(60, "a spot that does not cast shadows lights it");
    }

    [NeedsVulkanFact]
    public void Two_Spot_Lights_Cast_Shadows_At_Once()
    {
        Open(64, 64);
        // A red spot left and a blue one right, each low and aimed past a cube toward the middle,
        // so each cube's shadow falls on the ground between it and the middle, which only the other
        // spot then lights. Seen from straight above, with -Z up the image.
        var red = CreateSpotLight(new Vector3(-4, 2, 0), Vector3.Normalize(new Vector3(3, -2, 0)), new Color(255, 0, 0), 40, 30, 35, castsShadows: true);
        var blue = CreateSpotLight(new Vector3(4, 2, 0), Vector3.Normalize(new Vector3(-3, -2, 0)), new Color(0, 0, 255), 40, 30, 35, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 12, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        Image Draw(string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(-2.5f, 0.5f, 0), 1, Color.White);
            DrawModel(cube, new Vector3(2.5f, 0.5f, 0), 1, Color.White);
            EndMode3D();
        }, name);

        var both = Draw("two spots");
        SetLightCastsShadows(red, false);
        SetLightCastsShadows(blue, false);
        var none = Draw("two spots unshadowed");

        // The ground a unit left of the middle is in the red cube's shadow, and a unit right in the blue one's.
        var left = GetImageColor(both, 25, 32);
        var right = GetImageColor(both, 38, 32);
        left.R.Should().BeLessThan((byte)(GetImageColor(none, 25, 32).R / 4), $"the red cube shadows the ground left of the middle, {left}");
        left.B.Should().BeGreaterThan(40, "which the blue spot still lights");
        right.B.Should().BeLessThan((byte)(GetImageColor(none, 38, 32).B / 4), $"the blue cube shadows the ground right of it, {right}");
        right.R.Should().BeGreaterThan(40, "which the red spot still lights");
        UnloadModel(ground);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Masked_Surface_Casts_The_Shadow_Of_Its_Cut_Out()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 4), Vector3.Zero, Vector3.UnitY, 45);
        var wall = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        // A unit square a unit in front of the wall, from x 0.25 to 1.25, cut out on its left half
        // and solid on its right. The sun along -X and -Z throws its shadow a unit to the left.
        var square = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
        var texture = LoadTextureFromImage(new Image([255, 255, 255, 40, 255, 255, 255, 230], 2, 1));
        SetTextureFilter(texture, TextureFilter.Point);
        square.Materials[0] = new ModelMaterial(Color.White, texture) { AlphaMode = MaterialAlphaMode.Mask };
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, 0, -1)), Color.White, 1, castsShadows: true);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(wall, Vector3.Zero, Vector3.UnitX, 90, Vector3.One, Color.White);
            DrawModelEx(square, new Vector3(0.75f, 0, 1), Vector3.UnitX, 90, Vector3.One, Color.White);
            EndMode3D();
        });

        // The wall half a unit left of the middle is behind the cut-out half, and the middle behind the solid one.
        var hole = GetImageColor(image, 22, 32).R;
        var solid = GetImageColor(image, 32, 32).R;
        hole.Should().BeGreaterThan(60, "light passes through the cut-out half");
        solid.Should().BeLessThan((byte)(hole / 3), "the solid half shadows the wall");
        UnloadModel(square);
        UnloadModel(wall);
    }

    // CI installs the layer and sets E3D_REQUIRE_VALIDATION, so a missing layer there fails here
    // instead of letting every frame pass unchecked.
    [NeedsVulkanFact]
    public void The_Validation_Layer_Runs_Where_The_Build_Requires_It()
    {
        Open(16, 16);
        Capture(() => ClearBackground(Color.Black));
        if (Environment.GetEnvironmentVariable("E3D_REQUIRE_VALIDATION") == "1")
            GraphicsDevice.ValidationActive.Should().BeTrue("E3D_REQUIRE_VALIDATION is set, and a Debug build enables the layer when it is installed");
    }

    [NeedsVulkanFact]
    public void A_Posed_Model_Draws_Where_Its_Bones_Moved_It()
    {
        Open(64, 64);
        var arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
        var model = LoadModel(arm);
        var bend = LoadModelAnimations(arm)[0];
        var camera = new Camera3D(new Vector3(0, 1, 5), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        UpdateModelAnimation(model, bend, 0);
        var rest = Capture(Draw, "rest");
        UpdateModelAnimation(model, bend, bend.FrameCount - 1);
        var bent = Capture(Draw, "bent");

        // Pixel (32, 24) is half a unit above the elbow, and (23, 32) six tenths of a unit left of it.
        GetImageColor(rest, 32, 24).R.Should().BeGreaterThan(40, "at rest the upper arm stands above the elbow");
        GetImageColor(rest, 23, 32).R.Should().BeLessThan(10, "and nothing is beside it");
        GetImageColor(bent, 32, 24).R.Should().BeLessThan(10, "bent, the upper arm has left the space above the elbow");
        GetImageColor(bent, 23, 32).R.Should().BeGreaterThan(40, "for the space to its left");
        GetApp().World.Resource<MeshStore>().TryGetData(model.Meshes[0].Id, out var vertices, out _);
        vertices.Max(v => v.Position.Y).Should().BeGreaterThan(1.99f, "the GPU posed it, and the mesh's own vertices stay at rest");

        // Posed every frame, the vertices go round a ring of buffers, and each frame draws its own pose.
        for (int i = 0; i < 8; i++)
        {
            var atRest = i % 2 == 0;
            UpdateModelAnimation(model, bend, atRest ? 0 : bend.FrameCount - 1);
            var pose = Capture(Draw, $"pose{i}");
            (GetImageColor(pose, 32, 24).R > 40).Should().Be(atRest, $"frame {i} drew the pose given that frame");
        }
        UnloadModel(model);
    }

    [NeedsVulkanFact]
    public void A_Texture_Is_Decoded_From_SRGB_As_A_Color_Is()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var gray = LoadTextureFromImage(GenImageColor(4, 4, new Color(128, 128, 128)));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        // Gray as the material's color, then white with a gray texture. Both are sRGB 128, a
        // fifth of white's light, and a texture read as its bytes would light as half of it.
        cube.Materials[0] = new ModelMaterial(new Color(128, 128, 128));
        var colored = Capture(Draw, "colored");
        cube.Materials[0] = new ModelMaterial(Color.White, gray);
        var textured = Capture(Draw, "textured");

        ((int)GetImageColor(textured, 32, 32).R).Should().BeInRange(GetImageColor(colored, 32, 32).R - 2, GetImageColor(colored, 32, 32).R + 2);
        UnloadModel(cube);
        UnloadTexture(gray);

        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }
    }

    [NeedsVulkanFact]
    public void Emission_Shows_With_No_Light_On_The_Surface()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 0, 0), EmissiveIntensity = 0.5f };
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        // Under the fixed light, and then with one light entity pointing away from the camera's
        // side of the cube, so only the emission reaches it.
        var fixedLight = Capture(Draw, "fixed");
        var ecs = GetApp().World.Resource<EcsWorld>();
        var away = ecs.Spawn();
        ecs.Add(away, Light.Directional(Vector3.One, 1));
        ecs.Add(away, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), Vector3.One));
        var dark = Capture(Draw, "dark");

        foreach (var image in new[] { fixedLight, dark })
        {
            Linear(GetImageColor(image, 32, 32).R).Should().BeApproximately(0.5f, 0.03f, "a black cube gives off half of red's light");
            GetImageColor(image, 32, 32).G.Should().BeLessThan(5);
        }
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void Occlusion_Darkens_The_Light_From_All_Around_And_Not_A_Lamp()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var crease = LoadTextureFromImage(GenImageColor(4, 4, new Color(64, 64, 64)));
        var ecs = GetApp().World.Resource<EcsWorld>();
        var light = ecs.Spawn();
        ecs.Add(light, Light.Ambient(Vector3.One, 0.5f));
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        cube.Materials[0] = new ModelMaterial(Color.White) { Roughness = 1 };
        var open = Capture(Draw, "open");
        cube.Materials[0] = cube.Materials[0] with { OcclusionMap = crease };
        var occluded = Capture(Draw, "occluded");

        // The same under a lamp facing the cube, where occlusion leaves the light alone.
        ecs.GetRef<Light>(light) = Light.Directional(Vector3.One, 0.5f);
        var lamp = Capture(Draw, "lamp");
        cube.Materials[0] = cube.Materials[0] with { OcclusionMap = default };
        var lampOpen = Capture(Draw, "lamp-open");

        // The map's red, 64, is a quarter. An occlusion map is data rather than a color, so it is
        // read as it is and not decoded from sRGB.
        Linear(GetImageColor(occluded, 32, 32).R).Should().BeApproximately(Linear(GetImageColor(open, 32, 32).R) * 0.25f, 0.02f);
        ((int)GetImageColor(lamp, 32, 32).R).Should().BeInRange(GetImageColor(lampOpen, 32, 32).R - 2, GetImageColor(lampOpen, 32, 32).R + 2);
        UnloadModel(cube);
        UnloadTexture(crease);
    }

    [NeedsVulkanFact]
    public void The_Sky_Is_Drawn_Behind_Everything_In_The_Direction_Looked()
    {
        Open(64, 64);
        // A sky red above the horizon and blue below, with a sharp edge between, wide enough that
        // its cube keeps the edge.
        var image = GenImageColor(1024, 512, new Color(0, 0, 255));
        ImageDrawRectangle(ref image, 0, 0, 1024, 256, new Color(255, 0, 0));
        SetEnvironmentMap(image);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        Image Look(Vector3 target, string name, bool skybox = true) => Capture(() =>
        {
            ClearBackground(Color.Green);
            BeginMode3D(new Camera3D(Vector3.Zero, target, Vector3.UnitY + Vector3.UnitZ * 0.01f, 60));
            DrawModel(cube, target * 3, 1, Color.White);
            if (skybox) DrawSkybox();
            EndMode3D();
        }, name);

        var up = Look(Vector3.UnitY, "up");
        var down = Look(-Vector3.UnitY, "down");
        var bare = Look(Vector3.UnitY, "bare", skybox: false);

        var sky = GetImageColor(up, 2, 2);
        (sky.R > 200 && sky.B < 40).Should().BeTrue($"looking up shows the red sky, not {sky}");
        var ground = GetImageColor(down, 2, 2);
        (ground.B > 200 && ground.R < 40).Should().BeTrue($"looking down shows the blue below, not {ground}");
        GetImageColor(up, 32, 32).Should().NotBe(sky, "the cube hides the sky behind it");
        GetImageColor(bare, 2, 2).Should().Be(Color.Green, "with no DrawSkybox the clear color shows");
        UnloadModel(cube);
        UnloadEnvironmentMap();
    }

    [NeedsVulkanFact]
    public void A_Smooth_Metal_Reflects_The_Environment_Where_A_Rough_Surface_Scatters_It()
    {
        Open(64, 64);
        // A sky red above the horizon and blue below, and no light entities.
        var sky = GenImageColor(64, 32, new Color(0, 0, 255));
        ImageDrawRectangle(ref sky, 0, 0, 64, 16, new Color(255, 0, 0));
        SetEnvironmentMap(sky);

        // A plane facing up, seen from straight above, so it mirrors the sky overhead.
        var camera = new Camera3D(new Vector3(0, 4, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        var plane = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(plane, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        plane.Materials[0] = new ModelMaterial(Color.White) { Metallic = 1, Roughness = 0.05f };
        var mirror = Capture(Draw, "mirror");
        plane.Materials[0] = new ModelMaterial(Color.White) { Metallic = 0, Roughness = 1 };
        var chalk = Capture(Draw, "chalk");

        var m = GetImageColor(mirror, 32, 32);
        Linear(m.R).Should().BeGreaterThan(0.6f, "a mirror facing up shows the red sky above it");
        Linear(m.B).Should().BeLessThan(0.05f, "and none of the blue below the horizon");
        var c = GetImageColor(chalk, 32, 32);
        c.B.Should().BeGreaterThan(20, "a rough white surface gathers light from both halves");
        UnloadModel(plane);
        UnloadEnvironmentMap();
    }

    [NeedsVulkanFact]
    public void Draws_Differing_Only_In_Color_Share_One_Set_And_Keep_Their_Colors()
    {
        Open(64, 32);
        var camera = new Camera3D(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        // A row of cubes, each a shade of its own, and the two ends red and blue.
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            for (int i = 0; i < 300; i++)
            {
                var color = i == 0 ? new Color(255, 0, 0) : i == 299 ? new Color(0, 0, 255) : new Color((byte)i, (byte)(i / 2), 0);
                // The ends 10 units away, 3 either side of the middle, and the rest far behind.
                var at = i == 0 ? new Vector3(-3, 0, 0) : i == 299 ? new Vector3(3, 0, 0) : new Vector3(0, 0, -20 - i);
                DrawModel(cube, at, 1, color);
            }
            EndMode3D();
        });

        var renderer = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>();
        renderer.MaterialSetCount.Should().Be(1, "every cube has the same maps, and its factors reach it in its instance");
        renderer.DrawCalls.Should().Be(1, "the cubes share a mesh and its maps, so one instanced draw draws them all");
        var left = GetImageColor(image, 20, 16);
        var right = GetImageColor(image, 44, 16);
        (left.R > 100 && left.B < 20).Should().BeTrue($"the leftmost cube is red, not {left}");
        (right.B > 100 && right.R < 20).Should().BeTrue($"the rightmost cube is blue, not {right}");
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_Translucent_Draw_Blends_With_What_Was_Drawn_Before_It_Though_Its_Mesh_Came_First()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var small = LoadModelFromMesh(GenMeshCube(1, 1, 0.1f));
        var wall = LoadModelFromMesh(GenMeshCube(4, 4, 0.1f));

        // An opaque quad of the small mesh off to the side, then a red wall behind, then a half
        // clear blue quad of the small mesh in front of the wall's middle.
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(small, new Vector3(1.5f, 1.5f, 0), 1, Color.White);
            DrawModel(wall, new Vector3(0, 0, -1), 1, new Color(255, 0, 0));
            DrawModel(small, new Vector3(0, 0, 0.5f), 1, new Color(0, 0, 255, 128));
            EndMode3D();
        });

        var middle = GetImageColor(image, 32, 32);
        middle.R.Should().BeGreaterThan(40, $"the red wall shows through the glass, not {middle}");
        middle.B.Should().BeGreaterThan(40, $"and the glass tints it blue, not {middle}");
        UnloadModel(wall);
        UnloadModel(small);
    }

    // A red wall, then a blue quad of the given texture and material in front of its middle, in a
    // 64 by 64 frame opened before the texture was loaded.
    private Image WallBehind(Texture2D texture, MaterialAlphaMode mode)
    {
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var wall = LoadModelFromMesh(GenMeshCube(4, 4, 0.1f));
        // One face, since a box's back face maps the texture mirrored behind its front.
        var front = LoadModelFromMesh(GenMeshPlane(1.5f, 1.5f, 1, 1));
        front.Materials[0] = new ModelMaterial(new Color(0, 0, 255), texture) { AlphaMode = mode };
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(front, new Vector3(0, 0, 0.5f), Vector3.UnitX, 90, Vector3.One, Color.White);
            DrawModel(wall, new Vector3(0, 0, -1), 1, new Color(255, 0, 0));
            EndMode3D();
        });
        UnloadModel(front);
        UnloadModel(wall);
        return image;
    }

    [NeedsVulkanFact]
    public void A_Masked_Surface_Shows_What_Is_Behind_Its_Holes_And_Hides_It_Elsewhere()
    {
        Open(64, 64);
        // The texture's left column below the cutoff and its right column above it, but neither solid.
        var texture = LoadTextureFromImage(new Image([255, 255, 255, 60, 255, 255, 255, 200], 2, 1));
        SetTextureFilter(texture, TextureFilter.Point);

        var image = WallBehind(texture, MaterialAlphaMode.Mask);

        var hole = GetImageColor(image, 26, 32);
        var solid = GetImageColor(image, 38, 32);
        (hole.R > 40 && hole.B < 10).Should().BeTrue($"the wall shows through the cut-out half unmixed, not {hole}");
        (solid.B > 40 && solid.R < 10).Should().BeTrue($"the masked half is solid blue, not {solid}");
    }

    [NeedsVulkanFact]
    public void A_Blended_Surface_Mixes_A_Textures_Alpha_With_What_Is_Behind()
    {
        Open(64, 64);
        var texture = LoadTextureFromImage(new Image([255, 255, 255, 128], 1, 1));

        var image = WallBehind(texture, MaterialAlphaMode.Blend);

        var middle = GetImageColor(image, 32, 32);
        (middle.R > 40 && middle.B > 40).Should().BeTrue($"the half-clear texture mixes blue with the red behind, not {middle}");
    }

    // A triangle around the origin wound counterclockwise seen from +Z, glTF's front, with a
    // material that is single-sided or not.
    private static string SidedTriangle(bool doubleSided)
    {
        var bytes = new List<byte>();
        foreach (var f in new float[] { -1, -1, 0, 1, -1, 0, 0, 1, 0 }) bytes.AddRange(BitConverter.GetBytes(f));
        var json = $$$"""
            {"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],
             "materials":[{"doubleSided":{{{(doubleSided ? "true" : "false")}}}}],
             "meshes":[{"primitives":[{"attributes":{"POSITION":0},"material":0}]}],
             "buffers":[{"byteLength":36,"uri":"data:application/octet-stream;base64,{{{Convert.ToBase64String(bytes.ToArray())}}}"}],
             "bufferViews":[{"buffer":0,"byteLength":36}],
             "accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[-1,-1,0],"max":[1,1,0]}]}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"sided-{Guid.NewGuid():N}.gltf");
        File.WriteAllText(path, json);
        return path;
    }

    [NeedsVulkanFact]
    public void A_Single_Sided_Material_Hides_Its_Back_Faces_And_A_Double_Sided_One_Does_Not()
    {
        Open(32, 32);
        var single = LoadModel(SidedTriangle(doubleSided: false));
        var both = LoadModel(SidedTriangle(doubleSided: true));
        single.Materials[0].DoubleSided.Should().BeFalse("the file says so");
        both.Materials[0].DoubleSided.Should().BeTrue("the file says so");
        byte Middle(Model model, float z) => GetImageColor(Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, z), Vector3.Zero, Vector3.UnitY, 60));
            DrawModel(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }), 16, 18).R;

        Middle(single, 3).Should().BeGreaterThan(20, "its front faces the camera");
        Middle(single, -3).Should().Be(0, "from behind, its back face is left out");
        Middle(both, -3).Should().BeGreaterThan(0, "a double-sided one is drawn from behind too");
        UnloadModel(single);
        UnloadModel(both);
    }

    [NeedsVulkanFact]
    public void Anisotropic_Filtering_Keeps_A_Slanted_Checkerboard_Sharper_Than_Bilinear()
    {
        Open(64, 64);
        var checker = LoadTextureFromImage(GenImageChecked(256, 256, 8, 8, Color.Black, Color.White));
        GenTextureMipmaps(ref checker);
        var floor = LoadModelFromMesh(GenMeshPlane(40, 40, 1, 1));
        floor.Materials[0] = new ModelMaterial(Color.White, checker);
        // Low over the floor, looking along it, so the far squares are squashed many times over.
        var camera = new Camera3D(new Vector3(0, 0.6f, 10), new Vector3(0, 0, -20), Vector3.UnitY, 45);

        // How much the far rows of the floor still vary, from a gray smear to sharp squares.
        double Contrast(TextureFilter filter)
        {
            SetTextureFilter(checker, filter);
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(floor, Vector3.Zero, 1, Color.White);
                EndMode3D();
            }, $"slant {filter}");
            var values = new List<double>();
            for (int y = 30; y < 34; y++)
                for (int x = 8; x < 56; x++)
                    values.Add(GetImageColor(image, x, y).R);
            var mean = values.Average();
            return Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
        }

        var bilinear = Contrast(TextureFilter.Bilinear);
        var anisotropic = Contrast(TextureFilter.Anisotropic16x);
        anisotropic.Should().BeGreaterThan(bilinear * 1.2, $"16 samples along the slant keep the squares apart, {anisotropic:0.0} against {bilinear:0.0}");
        UnloadModel(floor);
        UnloadTexture(checker);
    }

    [NeedsVulkanFact]
    public void A_Metal_Reflects_An_HDR_Sky_Brighter_Than_White_Could_Be()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 4, 0), Vector3.Zero, -Vector3.UnitZ, 45);
        var plane = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        // A dark mirror, which returns a tenth of what it reflects.
        plane.Materials[0] = new ModelMaterial(new Color(89, 89, 89)) { Metallic = 1, Roughness = 0.05f };
        void Draw()
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(plane, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        Vector3[] Sky(float overhead) =>
            Enumerable.Range(0, 32 * 16).Select(i => i / 32 < 8 ? new Vector3(overhead) : Vector3.Zero).ToArray();

        GetApp().World.InsertResource(EnvironmentMap.FromLinear(Sky(1), 32, 16));
        var white = Capture(Draw, "white-sky");
        GetApp().World.InsertResource(EnvironmentMap.FromLinear(Sky(8), 32, 16));
        var bright = Capture(Draw, "bright-sky");

        Linear(GetImageColor(white, 32, 32).R).Should().BeApproximately(0.1f, 0.03f, "a white sky gives a tenth of white");
        Linear(GetImageColor(bright, 32, 32).R).Should().BeGreaterThan(0.6f, "a sky eight times white gives eight tenths, which no eight-bit sky can");
        UnloadModel(plane);
        UnloadEnvironmentMap();
    }

    // How many pixels along a white triangle's diagonal edge on black are neither black nor white,
    // drawn into the window and into a render target drawn beside it.
    private (int Window, int Target) EdgeShades(int samples)
    {
        Open(128, 32, samples);
        var target = LoadRenderTexture(64, 32);
        var image = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.Black);
            DrawTriangle(new Vector2(0, 0), new Vector2(0, 32), new Vector2(64, 32), Color.White);
            EndTextureMode();

            ClearBackground(Color.Black);
            DrawTriangle(new Vector2(0, 0), new Vector2(0, 32), new Vector2(64, 32), Color.White);
            DrawTexture(target.Texture, 64, 0, Color.White);
        }, $"edges-{samples}");
        UnloadRenderTexture(target);
        CloseWindow();
        UseApp(null);

        bool Between(Color c) => c.R is > 30 and < 225;
        return (Count(image, 0, 0, 64, 32, Between), Count(image, 64, 0, 128, 32, Between));
    }

    [NeedsVulkanFact]
    public void Multisampling_Shades_The_Pixels_An_Edge_Crosses_In_The_Window_And_A_Target()
    {
        var single = EdgeShades(1);
        var four = EdgeShades(4);

        single.Window.Should().Be(0, "with one sample a pixel is in the triangle or not");
        single.Target.Should().Be(0);
        four.Window.Should().BeGreaterThan(20, "with four a pixel the edge crosses is partly covered");
        four.Target.Should().BeGreaterThan(20, "and a render target is multisampled as the window is");
    }

    [NeedsVulkanFact]
    public void A_Flat_Point_Light_Lights_A_Model_In_Its_Color()
    {
        Open(64, 64);
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0] = new ModelMaterial(Color.White) { Roughness = 1 };
        var lamp = CreatePointLight(new Vector3(0, 0, 1.5f), new Color(255, 0, 0), 2);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        });

        var c = GetImageColor(image, 32, 32);
        c.R.Should().BeGreaterThan(150, "the red lamp a unit in front of the face lights it");
        c.G.Should().BeLessThan(20, "and in red only, since it is the only light");
        UnloadLight(lamp);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void A_2D_Camera_Draws_The_World_Around_Its_Target()
    {
        Open(64, 32);
        var camera = new Camera2D(new Vector2(32, 16), new Vector2(1000, 500), Zoom: 2);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode2D(camera);
            DrawRectangle(1000, 500, 4, 4, new Color(255, 0, 0));
            EndMode2D();
            DrawRectangle(0, 0, 4, 4, new Color(0, 0, 255));
        });

        GetImageColor(image, 34, 18).Should().Be(new Color(255, 0, 0), "the target's square is at the offset, twice its size");
        GetImageColor(image, 2, 2).Should().Be(new Color(0, 0, 255), "after EndMode2D drawing is in screen pixels again");
    }
}
