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

    private readonly TestFolder _folder = new("engine-reference-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open(int width, int height)
    {
        var config = Config.Default.WithWindow("reference test", width, height) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the first one's capture has been written, as OffscreenRenderTests does,
    // after settle frames for what takes a frame or more to appear, as a probe's capture or a
    // scene's models do.
    private Image Capture(Action draw, int settle = 0, Func<bool>? ready = null)
    {
        // A capture taken earlier in the test is written over, rather than mistaken for this one.
        var path = Path.Combine(_folder.Path, "frame.png");
        File.Delete(path);
        for (int frame = 0; frame < 120 && ready is not null && !ready(); frame++)
        {
            BeginDrawing();
            draw();
            EndDrawing();
        }
        for (int frame = 0; frame < settle; frame++)
        {
            BeginDrawing();
            draw();
            EndDrawing();
        }
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
            DrawCircleGradient(new Vector2(210, 75), 18, Color.White, Color.Maroon);
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
        // A ring of 0.7 and a tube of 0.25, standing as raylib's does, laid flat as it is drawn.
        var shaded = LoadModelFromMesh(GenMeshTorus(0.25f / 0.7f, 1.4f, 24, 32));
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
            DrawModelEx(shaded, new Vector3(1.9f, 0.6f, 0), Vector3.UnitX, 90, Vector3.One, Color.White);
            EndMode3D();
        });
        Matches(frame, "materials_and_shader");
        UnloadModel(cube);
        UnloadModel(plane);
        UnloadModel(sphere);
        UnloadModel(shaded);
        UnloadShader(shader);
        // A model unloads its meshes and not the textures its materials name, as raylib's does.
        foreach (var texture in new[] { checker, bumps, glow, roughness }) UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Skinned_Model_Posed_Mid_Clip_Matches_Its_Reference()
    {
        Open(160, 160);
        var arm = Path.Combine(AppContext.BaseDirectory, "resources", "arm.gltf");
        var model = LoadModel(arm);
        var bend = LoadModelAnimations(arm)[0];
        UpdateModelAnimation(model, bend, bend.KeyframeCount / 2);
        var camera = new Camera3D(new Vector3(2, 2, 5), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        // Lit from above over a floor of light from all around, so the bend shows in the shading
        // where a model drawn with no light is flat.
        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 0.65f);
        SetAmbientLight(Color.White, 0.35f);

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

    [NeedsVulkanFact]
    public void Point_And_Spot_Shadows_Match_Their_Reference()
    {
        Open(256, 160);
        CreatePointLight(new Vector3(-1.5f, 1.6f, 0), new Color(255, 220, 180), 12, 0, castsShadows: true);
        CreateSpotLight(new Vector3(3, 4, 2), Vector3.Normalize(new Vector3(-0.6f, -1, -0.4f)), new Color(160, 200, 255), 30, 20, 30, castsShadows: true);
        var ground = LoadModelFromMesh(GenMeshPlane(14, 14, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(0.8f, 1.2f, 0.8f));
        var camera = new Camera3D(new Vector3(2, 7, 7), new Vector3(0, 0, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.White);
            DrawModel(cube, new Vector3(0, 0.6f, 0), 1, Color.White);
            DrawModel(cube, new Vector3(-3, 0.6f, -1), 1, Color.White);
            DrawModel(cube, new Vector3(1.5f, 0.6f, 1.5f), 1, Color.White);
            EndMode3D();
        });
        Matches(frame, "point_and_spot_shadows");
        UnloadModel(ground);
        UnloadModel(cube);
    }

    [NeedsVulkanFact]
    public void An_Environment_Map_And_Its_Sky_Match_Their_Reference()
    {
        Open(256, 160);
        var sky = GenImageColor(256, 128, Color.Blank);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(40, 90, 170), new Color(190, 215, 235)),
            0, 0, Color.White);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(95, 105, 80), new Color(45, 50, 40)),
            0, 64, Color.White);
        ImageDrawCircle(ref sky, 70, 35, 6, new Color(255, 250, 225));
        SetEnvironmentMap(sky);
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 32, 32));
        var camera = new Camera3D(new Vector3(0, 1.2f, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 50);

        var frame = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            for (int i = 0; i < 4; i++)
            {
                sphere.Materials[0] = new ModelMaterial(new Color(230, 230, 235)) { Metallic = i < 2 ? 1 : 0, Roughness = 0.05f + i * 0.3f };
                DrawModel(sphere, new Vector3(-3 + i * 2, 0.5f, 0), 1, Color.White);
            }
            EndMode3D();
        });
        Matches(frame, "environment_and_sky");
        UnloadModel(sphere);
        UnloadEnvironmentMap();
    }

    [NeedsVulkanFact]
    public void Bloom_Matches_Its_Reference()
    {
        Open(256, 160);
        SetBloom(0.8f);
        CreatePointLight(new Vector3(0, 2, 2), new Color(255, 210, 160), 4, 10);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var orb = LoadModelFromMesh(GenMeshSphere(0.4f, 24, 24));
        var camera = new Camera3D(new Vector3(0, 3, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(12, 12, 18));
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(70, 70, 80));
            DrawModel(cube, new Vector3(0, 0.5f, 0), 1, Color.White);
            orb.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(60, 200, 255), EmissiveIntensity = 4 };
            DrawModel(orb, new Vector3(-1.8f, 1, 0), 1, Color.White);
            orb.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 120, 40), EmissiveIntensity = 4 };
            DrawModel(orb, new Vector3(1.8f, 1, 0), 1, Color.White);
            DrawCubeWires(new Vector3(0, 0.5f, 0), 1.2f, 1.2f, 1.2f, Color.Gold);
            EndMode3D();
            DrawText("Bloom", 8, 8, 20, Color.RayWhite);
        });
        Matches(frame, "bloom");
        UnloadModel(ground);
        UnloadModel(cube);
        UnloadModel(orb);
    }

    [NeedsVulkanFact]
    public void Ambient_Occlusion_Matches_Its_Reference()
    {
        Open(256, 160);
        SetAmbientLight(new Color(200, 210, 230), 0.7f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, -0.3f)), Color.White, 0.5f);
        SetAmbientOcclusion(1);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var ball = LoadModelFromMesh(GenMeshSphere(0.5f, 24, 24));
        var camera = new Camera3D(new Vector3(2, 3, 5), new Vector3(-1, 0.5f, -1), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(12, 12, 18));
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(180, 180, 175));
            DrawModelEx(cube, new Vector3(-1.5f, 1, -1.5f), Vector3.UnitY, 0, new Vector3(5, 2, 0.2f), new Color(200, 190, 170));
            DrawModelEx(cube, new Vector3(-3.9f, 1, 0.9f), Vector3.UnitY, 0, new Vector3(0.2f, 2, 5), new Color(200, 190, 170));
            DrawModel(cube, new Vector3(0, 0.5f, 0), 1, new Color(170, 120, 90));
            DrawModel(ball, new Vector3(-2.6f, 0.5f, -0.6f), 1, new Color(120, 160, 200));
            DrawModelEx(cube, new Vector3(-1.2f, 0.4f, -1.05f), Vector3.UnitY, 20, new Vector3(0.8f), new Color(150, 170, 120));
            EndMode3D();
            DrawText("Ambient occlusion", 8, 8, 20, Color.RayWhite);
        });
        Matches(frame, "ambient_occlusion");
        UnloadModel(ground);
        UnloadModel(cube);
        UnloadModel(ball);
    }

    [NeedsVulkanFact]
    public void The_Effects_Over_The_Frame_Match_Their_Reference()
    {
        Open(256, 160);
        SetExposure(1.3f);
        SetTonemap(Tonemap.Aces);
        SetColorGrading(1.1f, 0.8f, new Color(255, 236, 215));
        SetVignette(0.5f);
        SetFxaa(true);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.6f, -1, -0.3f)), Color.White, 1.4f, castsShadows: true);
        SetAmbientLight(new Color(150, 170, 200), 0.3f);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1.5f, 1.5f, 1.5f));
        var camera = new Camera3D(new Vector3(5, 4, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(90, 120, 160));
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(200, 200, 190));
            DrawModelEx(cube, new Vector3(0, 0.75f, 0), Vector3.UnitY, 30, Vector3.One, Color.Red);
            DrawLine3D(new Vector3(-3, 0.02f, 2), new Vector3(3, 0.02f, -1), Color.Black);
            EndMode3D();
            DrawText("Effects", 8, 8, 20, Color.White);
        });
        Matches(frame, "frame_effects");
        UnloadModel(ground);
        UnloadModel(cube);
    }

    // A cube of six faces, two triangles each, with their normals, as a mesh entity holds one.
    private static (Vector3[] Positions, Vector3[] Normals) CubeTriangles(float size)
    {
        var half = size / 2;
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        foreach (var normal in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
        {
            // Two axes across the face, turned so its corners go round counterclockwise seen from outside.
            var across = MathF.Abs(normal.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
            var up = Vector3.Cross(normal, across);
            Vector3 Corner(float a, float b) => (normal + across * a + up * b) * half;
            foreach (var corner in new[] { Corner(-1, -1), Corner(1, -1), Corner(1, 1), Corner(-1, -1), Corner(1, 1), Corner(-1, 1) })
            {
                positions.Add(corner);
                normals.Add(normal);
            }
        }
        return ([.. positions], [.. normals]);
    }

    [NeedsVulkanFact]
    public void Per_Object_Motion_Blur_Matches_Its_Reference()
    {
        // A red cube entity sliding a quarter of a unit a frame past a still camera, smeared along
        // its path, beside a blue one standing still and a floor drawn with DrawModel, both sharp.
        Open(256, 160);
        SetMotionBlur(1, objects: true);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.5f, -1, -0.4f)), Color.White, 1.2f);
        SetAmbientLight(new Color(150, 170, 200), 0.35f);
        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var ecs = GetApp().World.Resource<EcsWorld>();
        var (positions, normals) = CubeTriangles(1.2f);
        var moving = ecs.Spawn();
        ecs.Add(moving, new Mesh(positions, normals));
        ecs.Add(moving, new Material(new Vector4(0.85f, 0.15f, 0.1f, 1)));
        ecs.Add(moving, new Transform(new Vector3(-1.75f, 0.6f, 0.5f)));
        var still = ecs.Spawn();
        ecs.Add(still, new Mesh(positions, normals));
        ecs.Add(still, new Material(new Vector4(0.15f, 0.3f, 0.85f, 1)));
        ecs.Add(still, new Transform(new Vector3(1.5f, 0.6f, -1)));
        var camera = new Camera3D(new Vector3(0, 3, 7), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ecs.GetRef<Transform>(moving).Position.X += 0.25f;
            ClearBackground(new Color(90, 120, 160));
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(200, 200, 190));
            EndMode3D();
        }, settle: 3);
        Matches(frame, "motion_blur");
        UnloadModel(ground);
    }

    [NeedsVulkanFact]
    public void Particles_Match_Their_Reference()
    {
        // Two still clouds given off at once over a lit floor, through bloom: an unlit one that
        // glows past white and adds its light, and a lit one laid over by alpha that takes the
        // lamp's. Still, so the frame is the same however long its frames took.
        Open(256, 160);
        SetBloom(0.6f);
        CreatePointLight(new Vector3(0, 2.5f, 1.5f), new Color(255, 200, 150), 4, range: 10);
        ParticleEmitter Still(Color color) => ParticleEmitter.Default with
        {
            MaxParticles = 150, Emitting = false, Burst = 150, Life = 60, Velocity = Vector3.Zero, Gravity = Vector3.Zero,
            Radius = 0.7f, StartSize = 0.25f, EndSize = 0.25f, StartColor = color, EndColor = color,
        };
        CreateParticleEmitter(new Vector3(-1.1f, 1, 0), Still(new Color(255, 120, 40)) with { Intensity = 3 });
        CreateParticleEmitter(new Vector3(1.1f, 1, 0), Still(new Color(200, 200, 210, 200)) with { Lit = true, Blend = ParticleBlend.Alpha });
        var floor = LoadModelFromMesh(GenMeshPlane(10, 10, 1, 1));
        var camera = new Camera3D(new Vector3(0, 2, 5), new Vector3(0, 0.8f, 0), Vector3.UnitY, 45);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(10, 12, 20));
            BeginMode3D(camera);
            DrawModel(floor, Vector3.Zero, 1, new Color(120, 120, 120));
            EndMode3D();
        }, settle: 2);
        Matches(frame, "particles");
        UnloadModel(floor);
    }

    [NeedsVulkanFact]
    public void A_Reflection_Probe_Matches_Its_Reference()
    {
        Open(256, 160);
        CreatePointLight(new Vector3(0, 2.5f, 0), new Color(255, 230, 200), 6, range: 10);
        var room = LoadModelFromMesh(GenMeshCube(8, 4, 8));
        room.Materials[0] = new ModelMaterial(new Color(200, 120, 90)) { DoubleSided = true };
        var ball = LoadModelFromMesh(GenMeshSphere(0.8f, 32, 32));
        ball.Materials[0] = new ModelMaterial(new Color(230, 230, 235)) { Metallic = 1, Roughness = 0.1f };
        var pillar = LoadModelFromMesh(GenMeshCube(0.6f, 3, 0.6f));
        var probe = CreateReflectionProbe(new Vector3(0, 2, 0), new Vector3(8, 4, 8));
        var camera = new Camera3D(new Vector3(2.5f, 1.8f, 3.2f), new Vector3(0, 0.9f, 0), Vector3.UnitY, 60);

        var frame = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(room, new Vector3(0, 2, 0), 1, Color.White);
            DrawModel(pillar, new Vector3(-2, 1.5f, -2), 1, new Color(60, 140, 220));
            DrawModel(ball, new Vector3(0, 0.8f, 0), 1, Color.White);
            EndMode3D();
        }, settle: 12, ready: () => IsReflectionProbeReady(probe));
        IsReflectionProbeReady(probe).Should().BeTrue("the probe has captured the room");
        Matches(frame, "reflection_probe");
        UnloadModel(room);
        UnloadModel(ball);
        UnloadModel(pillar);
    }

    [NeedsVulkanFact]
    public void Many_Shadowed_Lights_Match_Their_Reference()
    {
        Open(256, 160);
        var floor = LoadModelFromMesh(GenMeshPlane(30, 12, 1, 1));
        var block = LoadModelFromMesh(GenMeshCube(0.6f, 1.2f, 0.6f));
        for (int i = 0; i < 6; i++)
        {
            var x = (i - 2.5f) * 4;
            CreatePointLight(new Vector3(x - 0.8f, 1.4f, -1.5f), new Color(255, 200, 150), 3, range: 3, castsShadows: true);
            CreateSpotLight(new Vector3(x - 0.8f, 1.4f, 1.5f), new Vector3(0.6f, -1, 0), new Color(150, 200, 255), 3,
                innerAngle: 40, outerAngle: 50, range: 3.5f, castsShadows: true);
        }
        var camera = new Camera3D(new Vector3(0, 14, 9), Vector3.Zero, Vector3.UnitY, 55);

        var frame = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(floor, Vector3.Zero, 1, Color.White);
            for (int i = 0; i < 6; i++)
            {
                DrawModel(block, new Vector3((i - 2.5f) * 4, 0.6f, -1.5f), 1, Color.White);
                DrawModel(block, new Vector3((i - 2.5f) * 4, 0.6f, 1.5f), 1, Color.White);
            }
            EndMode3D();
        });
        Matches(frame, "many_shadows");
        UnloadModel(floor);
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    public void A_Morph_Target_And_A_Clip_On_Part_Of_A_Skeleton_Match_Their_Reference()
    {
        Open(256, 160);
        var strip = LoadModel(Path.Combine(AppContext.BaseDirectory, "resources", "morph.gltf"));
        SetModelMorphWeight(strip, "Raise", 0.6f);
        var heroFile = Path.Combine(AppContext.BaseDirectory, "resources", "hero.gltf");
        var hero = LoadModel(heroFile);
        var clips = LoadModelAnimations(heroFile);
        UpdateModelAnimationLayer(hero, clips.Single(c => c.Name == "run"), 0.15f, clips.Single(c => c.Name == "jump"), 0, "ArmL");
        var camera = new Camera3D(new Vector3(0, 1.4f, 5), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        // Lit from above over a floor of light from all around, so the raised part and the arm
        // show in the shading where a model drawn with no light is flat.
        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 0.65f);
        SetAmbientLight(Color.White, 0.35f);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(30, 34, 46));
            BeginMode3D(camera);
            DrawModel(strip, new Vector3(-1.6f, 0, 0), 1, new Color(240, 200, 80));
            DrawModel(hero, new Vector3(1.2f, 0, 0), 1, Color.White);
            EndMode3D();
        });
        Matches(frame, "morph_and_layer");
        UnloadModel(strip);
        UnloadModel(hero);
    }

    [NeedsVulkanFact]
    public void Text_In_A_Font_From_A_File_Matches_Its_Reference()
    {
        Open(256, 160);
        var font = LoadFontEx(Engine.Tests.Api.FontTests.Lato(), 28);

        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawTextEx(font, "Lato from a file", new Vector2(10, 20), 28, 1, Color.DarkBlue);
            DrawTextEx(font, "Small and spaced", new Vector2(10, 70), 18, 3, Color.Maroon);
            DrawTextEx(font, "0123456789 ÆØÅ éü", new Vector2(10, 110), 22, 0, Color.Black);
        });
        Matches(frame, "font_from_file");
        UnloadFont(font);
    }

    [NeedsVulkanFact]
    public void Color_Text_Matches_Its_Reference()
    {
        // The test fonts' color glyphs drawn as text: paints of a gradient and a moved square
        // (COLR version 1), a sequence the bitmap font joins into its yellow glyph beside the sun
        // alone, and layers with a letter of no color, tinted.
        Open(256, 96);
        string Font(string name) => Path.Combine(AppContext.BaseDirectory, "Api", name);
        var paints = LoadFontEx(Font("paints.ttf"), 48, [0x1F600]);
        var bitmaps = LoadFontEx(Font("bitmaps.ttf"), 32, LoadCodepoints("\U0001F600\u200D\u2600 "));
        var layers = LoadFontEx(Font("layers.ttf"), 40, ['A', 0x1F600]);

        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawTextEx(paints, "\U0001F600", new Vector2(8, 8), 48, 0, Color.White);
            DrawTextEx(bitmaps, "\U0001F600\u200D\u2600 \u2600", new Vector2(72, 16), 32, 2, Color.White);
            DrawTextEx(layers, "A\U0001F600", new Vector2(170, 12), 40, 2, Color.DarkBlue);
        });
        Matches(frame, "color_text");
        UnloadFont(paints);
        UnloadFont(bitmaps);
        UnloadFont(layers);
    }

    [NeedsVulkanFact]
    public void A_Texture_A_Compute_Shader_Wrote_Matches_Its_Reference()
    {
        Open(256, 160);
        var texture = LoadTextureFromImage(GenImageColor(64, 64, Color.Black));
        Capture(() => ClearBackground(Color.Black));

        var paint = LoadComputeShaderFromMemory("""
            RWTexture2D<float4> image;

            [shader("compute")]
            [numthreads(8, 8, 1)]
            void computeMain(uint3 id : SV_DispatchThreadID)
            {
                float2 p = (float2(id.xy) + 0.5) / 64.0 - 0.5;
                float ring = step(0.5, frac(length(p) * 6.0));
                image[id.xy] = float4(p.x + 0.5, ring, p.y + 0.5, 1);
            }
            """, "rings.slang");
        SetShaderValueTexture(paint, GetShaderLocation(paint, "image"), texture);
        ComputeShaderDispatch(paint, 8, 8, 1);

        var frame = Capture(() =>
        {
            ClearBackground(Color.Black);
            DrawTextureEx(texture, new Vector2(64, 16), 0, 2, Color.White);
        });
        Matches(frame, "compute_texture");
        UnloadShader(paint);
        UnloadTexture(texture);
    }

    [NeedsVulkanFact]
    public void A_Frame_Of_Summit_Matches_Its_Reference()
    {
        // The level of games/Summit, as the game draws its island with the steps and the house
        // beyond, through its own light, sky and bloom.
        Open(320, 180);
        Tests.Scenes.SummitComponents.Register();
        LoadScene("resources/level.json");
        CreateDirectionalLight(new Vector3(-0.5f, -1, -0.35f), new Color(255, 244, 225), 1.6f, castsShadows: true);
        CreatePointLight(new Vector3(12, 8.6f, -38), new Color(255, 200, 150), 2.5f, range: 9, castsShadows: true);
        SetShadowDistance(60);
        var sky = GenImageColor(256, 128, Color.Blank);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(60, 110, 200), new Color(200, 220, 240)),
            0, 0, Color.White);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(150, 160, 140), new Color(70, 80, 70)),
            0, 64, Color.White);
        SetEnvironmentMap(sky, intensity: 0.5f);
        SetBloom(0.7f);
        var orb = LoadModelFromMesh(GenMeshSphere(0.3f, 16, 16));
        orb.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 200, 90), EmissiveIntensity = 5, CastsShadows = false };
        var camera = new Camera3D(new Vector3(0, 4.2f, 15), new Vector3(0, 1, 8), Vector3.UnitY, 55);

        var frame = Capture(() =>
        {
            ClearBackground(new Color(60, 110, 200));
            BeginMode3D(camera);
            DrawSkybox();
            foreach (var at in new[] { new Vector3(-8, 2.2f, -2), new Vector3(7, 1.6f, 5), new Vector3(0, 3.2f, -15.5f) })
                DrawModel(orb, at, 1, Color.White);
            EndMode3D();
        }, settle: 10);
        Matches(frame, "summit");
        UnloadModel(orb);
        UnloadEnvironmentMap();
    }
}
