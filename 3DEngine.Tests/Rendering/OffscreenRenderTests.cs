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

    private static void Open(int width, int height)
    {
        var config = Config.Default.WithWindow("offscreen test", width, height) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        RunMode.HasRenderer(GetApp().World).Should().BeTrue("the probe started a Vulkan device, so the app's renderer starts too");
    }

    // Draws frames until the capture asked for in the first has been written.
    private Image Capture(Action draw, string name = "frame")
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

        int diffuse = GetImageColor(ambient, 32, 32).R;
        diffuse.Should().BeInRange(125, 140, "half a white light on a white wall is about half white, below where the tonemap bends");
        ((int)GetImageColor(rough, 32, 32).R).Should().BeInRange(diffuse - 12, diffuse + 12, "a rough surface spreads its highlight too thin to see");
        ((int)GetImageColor(smooth, 32, 32).R).Should().BeGreaterThan(diffuse + 60, "a smooth one gathers it where the wall mirrors the light");
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

        GetImageColor(plastic, 32, 32).R.Should().BeGreaterThan(100, "red plastic scatters the light that falls on it");
        GetImageColor(metal, 32, 32).R.Should().BeLessThan(40, "a metal scatters none, and this light is not mirrored toward the camera");
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
        ((int)color.G).Should().BeInRange(115, 140, "green keeps half of red");
        ((int)color.B).Should().BeInRange(55, 75, "blue keeps a quarter of red, so the light reads as the same orange");
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
        UnloadModel(model);
    }
}
