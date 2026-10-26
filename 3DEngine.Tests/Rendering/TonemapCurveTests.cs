using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The curve set by its numbers, as BSL's shader draws it, against the levels the voxel game read
/// from a modpack's shader, and a shader of the program's own drawn in the curve's place.
/// </summary>
/// <remarks>
/// The frame is seven bands of light, 0.05 to 4, each eight pixels wide, which a shader of the test's
/// own writes with no light, exposure, grading or dither in the way, as TonemapTests writes its ramp.
/// </remarks>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class TonemapCurveTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-tonemap-curve-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // The ask's seven lights, and what the curve shows for each at the settings the voxel game's
    // entry in ASKS.md reads them at from the modpack's shader.
    private static readonly float[] Lights = [0.05f, 0.1f, 0.25f, 0.5f, 1, 2, 4];
    private static readonly double[] Modpacks = [0.230, 0.317, 0.475, 0.620, 0.763, 0.874, 0.941];

    // BSL's curve as its shader works it out, the light times the exposure, then the white curve,
    // then the power mixed from the lower curve to the upper by the square root, then the gamma of
    // 1/2.2, which gives the value it shows.
    private static double Bsl(double light, double exposure, double white, double lower, double upper)
    {
        var c = light * exposure;
        c /= Math.Pow(Math.Pow(c, white) + 1, 1 / white);
        c = Math.Pow(c, lower + (upper - lower) * Math.Sqrt(c));
        return Math.Pow(c, 1 / 2.2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Curve_Set_By_Its_Numbers_Gives_The_Modpacks_Levels_And_Is_Read_Back_As_Set()
    {
        for (int i = 0; i < Lights.Length; i++)
            Bsl(Lights[i], 0.9, 1.3, 1.05, 1.0).Should().BeApproximately(Modpacks[i], 0.001, $"the modpack's settings show a light of {Lights[i]} so");

        UseApp(new App());
        GetTonemapCurve().Should().Be((1.4f, 2f, 1.2f, 1.5f), "the shader's defaults stand until the curve is set");
        GetTonemap().Should().Be(Tonemap.Engine);
        SetTonemapCurve(0.9f, 1.3f, 1.05f, 1.0f);
        GetTonemap().Should().Be(Tonemap.Curve, "setting the numbers chooses the curve");
        GetTonemapCurve().Should().Be((0.9f, 1.3f, 1.05f, 1.0f));
        SetTonemapCurve(-1, 0, 0, -2);
        GetTonemapCurve().Should().Be((0f, 0.01f, 0.01f, 0.01f), "the exposure is held to 0 and up, and the three that divide and raise to 0.01 and up");
        SetTonemap(Tonemap.Aces);
        GetTonemapCurve().Should().Be((0f, 0.01f, 0.01f, 0.01f), "choosing another curve keeps the numbers");
    }

    // A window of the seven bands, drawn as the window's scene through whatever the frame's curve
    // or tonemap shader is.
    private Image Bands()
    {
        var bands = LoadShaderFromMemory("""
            import engine;
            import color;

            static const float lights[7] = { 0.05, 0.1, 0.25, 0.5, 1.0, 2.0, 4.0 };

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(linearToSrgbPastWhite(lights[min(int(input.position.x) / 8, 6)].xxx), 1.0);
            }
            """, "bands.slang");
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            // A face far wider than the view, drawn inside BeginMode3D so it is the window's scene.
            BeginMode3D(camera);
            BeginShaderMode(bands);
            DrawCube(new Vector3(0, 0, -50), 20000, 20000, 1, Color.White);
            EndShaderMode();
            EndMode3D();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        UnloadShader(bands);
        return LoadImage(path);
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("tonemap curve test", 56, 8) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // The middle of each band as the window shows it, against what each band's light should show,
    // the most any channel is apart in levels of 255.
    private static (int Apart, int Band) Worst(Image frame, Func<float, Vector3> shown)
    {
        var worst = (Apart: 0, Band: 0);
        for (int band = 0; band < Lights.Length; band++)
        {
            var got = GetImageColor(frame, band * 8 + 4, 4);
            var expected = shown(Lights[band]) * 255;
            var apart = (int)MathF.Round(MathF.Max(MathF.Max(MathF.Abs(got.R - expected.X), MathF.Abs(got.G - expected.Y)), MathF.Abs(got.B - expected.Z)));
            if (apart > worst.Apart) worst = (apart, band);
        }
        return worst;
    }

    [NeedsVulkanFact]
    public void The_Curve_Set_By_Its_Numbers_Shows_Each_Light_As_The_Modpacks_Shader_Does()
    {
        Open();
        SetTonemapCurve(0.9f, 1.3f, 1.05f, 1.0f);
        var worst = Worst(Bands(), light => new Vector3((float)Bsl(light, 0.9, 1.3, 1.05, 1.0)));
        worst.Apart.Should().BeLessThanOrEqualTo(2, $"each band shows the curve's value, at worst at a light of {Lights[worst.Band]}");
    }

    [NeedsVulkanFact]
    public void A_Programs_Shader_Drawn_In_The_Curves_Place_Reads_The_Light_And_Its_Own_Values_And_Gives_The_Curve_Back_When_Taken_Away()
    {
        Open();
        // Reinhard's curve times a strength of its own and a texture of its own, read at its middle.
        var shader = LoadShaderFromMemory("""
            import frame;

            uniform float strength;
            Sampler2D tint;

            [shader("fragment")]
            float4 fragmentMain(FullscreenOutput input) : SV_Target
            {
                float3 light = frameLight(input.uv);
                return frameShown(light / (1.0 + light) * strength * tint.SampleLevel(float2(0.5, 0.5), 0).rgb, input.uv);
            }
            """, "own_curve.slang");
        var tint = LoadTextureFromImage(GenImageColor(1, 1, new Color(255, 128, 64)));
        SetShaderValue(shader, GetShaderLocation(shader, "strength"), 0.5f);
        SetTonemapShader(shader);
        GetTonemapShader().Should().Be(shader);
        // Set after the shader is chosen, so it reaches the frames drawn after it.
        SetShaderValueTexture(shader, GetShaderLocation(shader, "tint"), tint);

        var shaded = Bands();
        var colors = new Vector3(1, 128 / 255f, 64 / 255f);
        var worst = Worst(shaded, light => Encoded(light / (1 + light) * 0.5f * colors));
        worst.Apart.Should().BeLessThanOrEqualTo(2, $"the shader's curve, strength and texture show each band, at worst at a light of {Lights[worst.Band]}");

        SetTonemapCurve(0.9f, 1.3f, 1.05f, 1.0f);
        SetTonemapShader(default);
        worst = Worst(Bands(), light => new Vector3((float)Bsl(light, 0.9, 1.3, 1.05, 1.0)));
        worst.Apart.Should().BeLessThanOrEqualTo(2, "with the shader taken away the chosen curve shows the frame again");

        SetTonemapShader(shader);
        UnloadShader(shader);
        GetTonemapShader().IsValid.Should().BeFalse("an unloaded shader is no longer drawn in the curve's place");
        UnloadTexture(tint);
    }

    // A linear color as the window's eight bits hold it, sRGB-encoded.
    private static Vector3 Encoded(Vector3 c)
    {
        static float One(float v) => v <= 0.0031308f ? v * 12.92f : 1.055f * MathF.Pow(v, 1 / 2.4f) - 0.055f;
        return new Vector3(One(c.X), One(c.Y), One(c.Z));
    }
}
