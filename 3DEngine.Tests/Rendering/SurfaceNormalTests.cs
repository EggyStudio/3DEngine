using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The normal a program's shader hands the pass's lighting kept as given on a single-sided surface,
/// and the back of a double-sided one still lit as its front, read from a wall drawn offscreen.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class SurfaceNormalTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-surface-normal-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("surface normal test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
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

    // A wall four units square from the ground up, facing +Z, seen from a camera at the given side's
    // height of two units.
    private Image Wall(Model wall, Camera3D camera) => Capture(() =>
    {
        ClearBackground(Color.Black);
        BeginMode3D(camera);
        DrawModelEx(wall, new Vector3(0, 2, 0), Vector3.UnitX, 90, Vector3.One, Color.White);
        EndMode3D();
    });

    private static int Red(Image image, Camera3D camera, Vector3 at)
    {
        var pixel = GetWorldToScreen(at, camera);
        return GetImageColor(image, (int)pixel.X, (int)pixel.Y).R;
    }

    [NeedsVulkanFact]
    public void A_Programs_Sample_With_An_Upward_Normal_Reads_The_Same_Light_Above_And_Below_The_Eye()
    {
        // A sun straight down, and a wall facing the camera whose shader lights each point as a face
        // turned up would be lit there, as the voxel game samples the light for its mobs. Above the
        // eye's height the way to the eye points down, against the upward normal, which the pass
        // turned over as a double-sided face's back is, lighting the sample as a face turned down.
        Open();
        CreateDirectionalLight(new Vector3(0, -1, 0), Color.White, 1);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                ModelVertexOutput up = input;
                up.normal = float3(0, 1, 0);
                return float4(lit(float3(0.5, 0.5, 0.5), up), 1);
            }
            """, "upward.slang");
        var wall = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        wall.Materials[0] = new ModelMaterial(Color.White) { Shader = shader, Roughness = 1 };
        var camera = new Camera3D(new Vector3(0, 2, 6), new Vector3(0, 2, 0), Vector3.UnitY, 45);

        var image = Wall(wall, camera);
        var above = Red(image, camera, new Vector3(0, 3.5f, 0));
        var below = Red(image, camera, new Vector3(0, 0.5f, 0));
        below.Should().BeGreaterThan(60, "the sun lights a face turned up");
        above.Should().BeCloseTo(below, 8, "the sample above the eye keeps the normal it was given, as the one below does");
        UnloadModel(wall);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Double_Sided_Walls_Back_Seen_And_Lit_From_Behind_Is_Lit_As_Its_Front()
    {
        Open();
        var wall = LoadModelFromMesh(GenMeshPlane(4, 4, 1, 1));
        wall.Materials[0] = new ModelMaterial(Color.White) { DoubleSided = true, Roughness = 1 };
        // From the side given, +Z its front and -Z its back, lit from that side.
        int Seen(float side)
        {
            var light = CreateDirectionalLight(new Vector3(0, -0.3f, -side), Color.White, 1);
            var camera = new Camera3D(new Vector3(0, 2, 6 * side), new Vector3(0, 2, 0), Vector3.UnitY, 45);
            var red = Red(Wall(wall, camera), camera, new Vector3(0, 2, 0));
            UnloadLight(light);
            return red;
        }

        var front = Seen(1);
        front.Should().BeGreaterThan(60, "the light falls on the front");
        Seen(-1).Should().BeCloseTo(front, 4, "the back's normal is turned toward the eye and the light behind it");
        UnloadModel(wall);
    }
}
