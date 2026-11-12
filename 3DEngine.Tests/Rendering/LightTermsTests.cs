using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// light.terms reading a pixel's light by the model pass's terms, each drawn alone in turn, their
/// sum with the light past the surface the light drawn with every term.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed partial class LightTermsTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [GeneratedRegex(@"^(?<what>[^:]+): (?<r>-?[0-9.]+) (?<g>-?[0-9.]+) (?<b>-?[0-9.]+)", RegexOptions.Multiline)]
    private static partial Regex Line();

    [NeedsVulkanFact]
    public void A_Sunlit_Pixels_Light_Is_The_Sum_Of_Its_Terms_The_Suns_The_Largest()
    {
        var config = Config.Default.WithWindow("light terms", 160, 90) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f);
        SetGlobalIllumination(GlobalIllumination.Low);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.4f)), Color.White, 1.5f);
        SetAmbientLight(new Color(150, 180, 255), 0.2f);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var view = new Camera3D(new Vector3(0, 3, 6), new Vector3(0, 0, 0), Vector3.UnitY, 60);
        void Draw()
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(Vector3.Zero);
            DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(8, 1, 8), new Color(200, 190, 170));
            DrawModelEx(block, new Vector3(0, 1, -2), Vector3.UnitY, 0, new Vector3(8, 3, 1), new Color(180, 120, 90));
            EndMode3D();
            EndDrawing();
        }
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 20; frame++) Draw();

        // A pixel of the sunlit floor before the wall.
        Func<string?> later;
        using (ConsoleHost.Lend(GetApp().World, GetApp()))
        {
            ConsoleCommands.Run("light.terms 80 70").Should().StartWith("reading the light of the pixel 80 70");
            later = ConsoleHost.Pending!;
        }
        string? answer = null;
        for (int frame = 0; frame < 60 && answer is null; frame++)
        {
            Draw();
            using (ConsoleHost.Lend(GetApp().World, GetApp()))
                answer = later();
        }
        answer.Should().NotBeNull("each of the terms is drawn and read within two frames");
        var lines = Line().Matches(answer!).ToDictionary(m => m.Groups["what"].Value.Trim(),
            m => new Vector3(float.Parse(m.Groups["r"].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture), float.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture)));
        var summed = lines["the terms and that"];
        var drawn = lines["drawn with every term"];
        drawn.X.Should().BeGreaterThan(0.05f, "the floor is lit");
        for (int c = 0; c < 3; c++)
            summed[c].Should().BeApproximately(drawn[c], 0.05f * drawn[c] + 0.002f, $"channel {c} of the terms' sum is the light drawn with every term:\n{answer}");
        var sun = lines.Single(l => l.Key.EndsWith("the lights, scattered")).Value;
        lines.Where(l => char.IsDigit(l.Key.TrimStart()[0])).Should().OnlyContain(l => l.Value.Y <= sun.Y, $"the sun's scattered light is the most of a sunlit floor's:\n{answer}");
        UnloadModel(block);
    }
}
