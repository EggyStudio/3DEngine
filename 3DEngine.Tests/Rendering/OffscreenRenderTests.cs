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
        ecs.Add(lamp, new Light { Type = LightType.Sphere, Color = Vector3.One, Intensity = 4f });
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
}
