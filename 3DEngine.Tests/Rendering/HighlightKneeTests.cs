using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The engine's curve with its highlights let go toward white past the knee, read on a ramp of an
/// orange light through the window's frame and on an emissive block in a render texture.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class HighlightKneeTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-highlight-knee-");
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // An orange light at six strengths, the first three with every channel under the knee of 0.8.
    private static readonly float[] Strengths = [0.2f, 0.5f, 0.75f, 1.5f, 3, 6];

    // A window of six bands, with a light in its scene, since a scene with none is drawn as raylib
    // draws one, its colors clamped, and the engine's curve bends a scene's light where light past
    // white may come about.
    private static void Open()
    {
        var config = Config.Default.WithWindow("highlight knee test", 48, 8) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(-Vector3.UnitY, Color.White, 1);
    }

    // The window of the six bands, eight pixels wide each, as the window's scene shows them.
    private Image Bands()
    {
        var bands = LoadShaderFromMemory("""
            import engine;
            import color;

            static const float strengths[6] = { 0.2, 0.5, 0.75, 1.5, 3.0, 6.0 };

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(linearToSrgbPastWhite(float3(1.0, 0.5, 0.2) * strengths[min(int(input.position.x) / 8, 5)]), 1.0);
            }
            """, "orange-bands.slang");
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            BeginShaderMode(bands);
            DrawCube(new Vector3(0, 0, -50), 20000, 20000, 1, Color.White);
            EndShaderMode();
            EndMode3D();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        UnloadShader(bands);
        return LoadImage(path);
    }

    private static Color Band(Image frame, int band) => GetImageColor(frame, band * 8 + 4, 4);

    private static int Spread(Color c) => Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));

    [NeedsVulkanFact]
    public void Past_The_Knee_A_Bright_Light_Goes_Toward_White_And_Under_It_Is_Shown_As_It_Was()
    {
        Open();
        GetHighlightKnee().Should().Be((0.8f, 1f), "the knee is on unless set");
        var on = Bands();
        SetHighlightKnee(0.8f, 0);
        var off = Bands();

        for (int band = 0; band < 3; band++)
            Band(on, band).Should().Be(Band(off, band), $"a light of {Strengths[band]} has every channel under the knee");
        for (int band = 3; band < 6; band++)
            Spread(Band(on, band)).Should().BeLessThan(Spread(Band(off, band)) - 20, $"a light of {Strengths[band]} lets its hue go toward white");
        Spread(Band(on, 5)).Should().BeLessThan(12, "the brightest goes nearly white, as a reference clipping at white shows it");
        Spread(Band(off, 5)).Should().BeGreaterThan(100, "where the curve alone keeps it orange");
    }

    [NeedsVulkanFact]
    public void A_Render_Texture_Takes_The_Knee_As_The_Window_Does()
    {
        Open();
        var target = LoadRenderTexture(16, 16);
        var block = LoadModelFromMesh(GenMeshCube(10, 10, 1));
        // An emission of 8, 2.8 and 1.5 in linear light, every channel past the knee.
        block.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 160, 120), EmissiveIntensity = 8 };
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        Color Drawn(float strength)
        {
            SetHighlightKnee(0.8f, strength);
            var path = Path.Combine(_folder.Path, $"{_captures++}.png");
            for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
            {
                BeginTextureMode(target);
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(block, Vector3.Zero, 1, Color.White);
                EndMode3D();
                EndTextureMode();
                BeginDrawing();
                ClearBackground(Color.Black);
                DrawTexture(target.Texture, 0, 0, Color.White);
                if (frame == 0) TakeScreenshot(path);
                EndDrawing();
            }
            return GetImageColor(LoadImage(path), 8, 4);
        }

        var on = Drawn(1);
        var off = Drawn(0);
        Spread(off).Should().BeGreaterThan(100, "the curve alone keeps the block orange");
        Spread(on).Should().BeLessThan(12, "the knee takes it toward white in a render texture too");
        UnloadModel(block);
        UnloadRenderTexture(target);
    }
}
