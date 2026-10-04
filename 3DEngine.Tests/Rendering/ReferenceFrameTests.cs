using System.Numerics;
using System.Runtime.CompilerServices;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws whole scenes offscreen and compares each frame with a reference image checked in beside
/// this file, so a fault anywhere in a frame fails a test, where the other render tests read
/// chosen pixels.
/// </summary>
/// <remarks>
/// <para>
/// Devices rasterize and filter a little differently, so a frame matches when few of its pixels
/// differ from the reference by more than a small step in any channel. The references are drawn
/// on a desktop GPU and hold on lavapipe, which CI draws with.
/// </para>
/// <para>
/// A missing reference is written from the frame and the test fails, asking for it to be looked
/// at and committed. Setting <c>E3D_WRITE_REFERENCES=1</c> writes every reference again after a
/// change meant to alter frames. A frame that does not match is written with its difference into
/// <c>reference-failures</c> beside the test assembly.
/// </para>
/// </remarks>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class ReferenceFrameTests : IDisposable
{
    // A channel further than this from the reference marks its pixel as differing.
    private const int Step = 24;

    // The share of pixels that may differ, which edges, text and filtering between devices take.
    private const double DifferingShare = 0.02;

    private readonly string _directory = Directory.CreateTempSubdirectory("engine-reference-").FullName;
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    private static void Open(int width, int height)
    {
        var config = Config.Default.WithWindow("reference test", width, height) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the first one's capture has been written, as OffscreenRenderTests does.
    private Image Capture(Action draw)
    {
        var path = Path.Combine(_directory, "frame.png");
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

    // Compares a frame with the reference of its name, writing the reference when there is none.
    private static void Matches(Image frame, string name, [CallerFilePath] string source = "")
    {
        var reference = Path.Combine(Path.GetDirectoryName(source)!, "References", name + ".png");
        if (!File.Exists(reference) || Environment.GetEnvironmentVariable("E3D_WRITE_REFERENCES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reference)!);
            ExportImage(frame, reference);
            Assert.Fail($"The reference {reference} was written from this frame. Look at it and commit it.");
        }

        var expected = LoadImage(reference);
        (frame.Width, frame.Height).Should().Be((expected.Width, expected.Height), "the frame is drawn at the reference's size");
        var difference = GenImageColor(frame.Width, frame.Height, Color.Black);
        var differing = 0;
        for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++)
            {
                Color a = GetImageColor(frame, x, y), b = GetImageColor(expected, x, y);
                var most = Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Abs(a.B - b.B));
                if (most <= Step) continue;
                differing++;
                ImageDrawPixel(ref difference, x, y, new Color((byte)Math.Min(255, most * 2), 0, 0));
            }

        var share = (double)differing / (frame.Width * frame.Height);
        if (share <= DifferingShare) return;
        var failures = Path.Combine(AppContext.BaseDirectory, "reference-failures");
        Directory.CreateDirectory(failures);
        ExportImage(frame, Path.Combine(failures, name + ".png"));
        ExportImage(difference, Path.Combine(failures, name + ".difference.png"));
        Assert.Fail($"{share:P1} of the pixels differ from {reference}, more than {DifferingShare:P0}. The frame and its difference are in {failures}.");
    }

    [NeedsVulkanFact]
    public void Shapes_And_Text_In_2D_Match_Their_Reference()
    {
        Open(256, 160);
        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawRectangle(10, 10, 60, 40, Color.Red);
            DrawRectangleLinesEx(new Rectangle(80, 10, 60, 40), 4, Color.DarkBlue);
            DrawRectangleRounded(new Rectangle(150, 10, 90, 40), 0.5f, 8, Color.Gold);
            DrawRectangleGradientH(10, 60, 110, 30, Color.Lime, Color.Purple);
            DrawCircle(160, 75, 18, Color.SkyBlue);
            DrawCircleGradient(210, 75, 18, Color.White, Color.Maroon);
            DrawTriangle(new Vector2(20, 150), new Vector2(60, 150), new Vector2(40, 110), Color.Orange);
            DrawPoly(new Vector2(95, 130), 6, 18, 30, Color.Violet);
            DrawLineEx(new Vector2(130, 110), new Vector2(240, 150), 3, Color.Black);
            DrawText("Reference 123", 130, 98, 10, Color.DarkGray);
        });
        Matches(frame, "shapes_and_text");
    }

    [NeedsVulkanFact]
    public void A_Lit_Shadowed_Scene_Matches_Its_Reference()
    {
        Open(256, 160);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, 0.6f)), Color.White, 1, castsShadows: true);
        CreatePointLight(new Vector3(2, 1.5f, 2), new Color(255, 160, 80), 4, 8);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1.5f, 1.5f, 1.5f));
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 24, 24));
        var camera = new Camera3D(new Vector3(5, 4, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(30, 34, 46));
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(200, 200, 190));
            DrawModel(cube, new Vector3(-1, 0.75f, 0), 1, Color.Red);
            DrawModelEx(cube, new Vector3(1.5f, 0.5f, -1.5f), Vector3.UnitY, 30, new Vector3(0.7f), Color.Green);
            DrawModel(sphere, new Vector3(1, 0.8f, 1), 1, Color.SkyBlue);
            EndMode3D();
        });
        Matches(frame, "lit_shadowed_scene");
        UnloadModel(ground);
        UnloadModel(cube);
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_Drawn_Into_The_Window_Matches_Its_Reference()
    {
        Open(256, 160);
        var target = LoadRenderTexture(128, 96);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(2, 2, 3), Vector3.Zero, Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            BeginTextureMode(target);
            ClearBackground(Color.DarkBlue);
            BeginMode3D(camera);
            DrawModel(cube, Vector3.Zero, 1, Color.Orange);
            DrawCubeWires(Vector3.Zero, 1.2f, 1.2f, 1.2f, Color.White);
            EndMode3D();
            DrawText("target", 4, 4, 10, Color.White);
            EndTextureMode();

            ClearBackground(Color.Black);
            DrawTextureRec(target.Texture, new Rectangle(0, 0, 128, 96), new Vector2(8, 8), Color.White);
            DrawTexturePro(target.Texture, new Rectangle(0, 0, 128, 96), new Rectangle(144, 40, 104, 78), Vector2.Zero, 0, Color.White);
            DrawRectangleLines(144, 40, 104, 78, Color.Yellow);
        });
        Matches(frame, "render_texture");
        UnloadModel(cube);
        UnloadRenderTexture(target);
    }

    [NeedsVulkanFact]
    public void An_ImGui_Window_Matches_Its_Reference()
    {
        Open(256, 160);
        var value = 0.4f;
        var on = true;
        var frame = Capture(() =>
        {
            ClearBackground(new Color(40, 44, 52));
            DrawRectangle(0, 120, 256, 40, Color.DarkGreen);
            ImGuiNET.ImGui.SetNextWindowPos(new Vector2(16, 12), ImGuiNET.ImGuiCond.Always);
            ImGuiNET.ImGui.SetNextWindowSize(new Vector2(200, 110), ImGuiNET.ImGuiCond.Always);
            ImGuiNET.ImGui.Begin("Reference");
            ImGuiNET.ImGui.Text("Frames compared whole");
            ImGuiNET.ImGui.Button("Button");
            ImGuiNET.ImGui.SliderFloat("Value", ref value, 0, 1);
            ImGuiNET.ImGui.Checkbox("On", ref on);
            ImGuiNET.ImGui.End();
        });
        Matches(frame, "imgui_window");
    }

    [NeedsVulkanFact]
    public void Materials_With_Maps_And_A_Model_Shader_Match_Their_Reference()
    {
        Open(256, 160);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.6f)), Color.White, 1);
        // A checker for color, ridges across for the normal map, a stripe of light given off, and
        // rough and smooth halves, each made here so the scene needs no file.
        var checker = LoadTextureFromImage(GenImageChecked(64, 64, 8, 8, Color.White, new Color(90, 90, 90)));
        var ridges = GenImageColor(64, 64, new Color(128, 128, 255));
        for (int x = 0; x < 64; x++)
            ImageDrawLine(ref ridges, x, 0, x, 63, (x / 4) % 2 == 0 ? new Color(200, 128, 230) : new Color(56, 128, 230));
        var bumps = LoadTextureFromImage(ridges);
        var glow = LoadTextureFromImage(GenImageGradientLinear(64, 64, 0, Color.Black, Color.Orange));
        var halves = GenImageColor(64, 64, new Color(0, 40, 255));
        ImageDrawRectangle(ref halves, 0, 0, 32, 64, new Color(0, 230, 0));
        var roughness = LoadTextureFromImage(halves);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                float3 n = normalize(input.normal);
                return float4(toDisplay(abs(n) * baseColor(input).rgb), 1.0);
            }
            """, "normals.slang");

        var cube = LoadModelFromMesh(GenMeshCube(1.4f, 1.4f, 1.4f));
        var plane = LoadModelFromMesh(GenMeshPlane(6, 6, 1, 1));
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 32, 32));
        var shaded = LoadModelFromMesh(GenMeshTorus(0.7f, 0.25f, 24, 32));
        plane.Materials[0] = new ModelMaterial(Color.White, checker) { NormalMap = bumps };
        cube.Materials[0] = new ModelMaterial(Color.White, checker) { EmissiveMap = glow, Emissive = Color.White };
        sphere.Materials[0] = new ModelMaterial(new Color(220, 180, 120)) { Metallic = 1, Roughness = 1, MetallicRoughnessMap = roughness };
        shaded.Materials[0] = new ModelMaterial(Color.White) { Shader = shader };
        var camera = new Camera3D(new Vector3(0, 4, 6), new Vector3(0, 0.4f, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(30, 34, 46));
            BeginMode3D(camera);
            DrawModel(plane, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(-1.8f, 0.7f, 0), 1, Color.White);
            DrawModel(sphere, new Vector3(0, 0.8f, 0.6f), 1, Color.White);
            DrawModel(shaded, new Vector3(1.9f, 0.6f, 0), 1, Color.White);
            EndMode3D();
        });
        Matches(frame, "materials_and_shader");
        UnloadModel(cube);
        UnloadModel(plane);
        UnloadModel(sphere);
        UnloadModel(shaded);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Skinned_Model_Posed_Mid_Clip_Matches_Its_Reference()
    {
        Open(160, 160);
        var arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
        var model = LoadModel(arm);
        var bend = LoadModelAnimations(arm)[0];
        UpdateModelAnimation(model, bend, bend.FrameCount / 2);
        var camera = new Camera3D(new Vector3(2, 2, 5), new Vector3(0, 1, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(30, 34, 46));
            BeginMode3D(camera);
            DrawGrid(10, 0.5f);
            DrawModel(model, Vector3.Zero, 1, new Color(230, 160, 60));
            DrawModelWires(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
        });
        Matches(frame, "skinned_arm");
        UnloadModel(model);
    }
}
