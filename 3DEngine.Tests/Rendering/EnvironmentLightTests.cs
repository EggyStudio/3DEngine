using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The environment map's light on the scene set apart from the sky the map draws, its share and
/// tint kept through a new map and taken by the model pass, the drawn sky left as it was.
/// </summary>
[Collection("E3D")]
public sealed class EnvironmentLightTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-environment-light-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Share_And_Tint_Are_Read_Back_As_Set_Kept_Through_A_New_Map_And_Reach_The_Render_World_Linear()
    {
        UseApp(new App());
        GetEnvironmentLight().Should().Be((1f, Color.White), "the map's whole light, untinted, until set");
        SetEnvironmentLight(-1, Color.Red);
        GetEnvironmentLight().Should().Be((0f, Color.Red), "a share below none is none");

        SetEnvironmentLight(0.5f, new Color(255, 128, 0));
        SetEnvironmentMap(GenImageColor(8, 4, Color.Gray));
        GetEnvironmentLight().Should().Be((0.5f, new Color(255, 128, 0)), "a new map keeps the share and tint set for the scene");

        var renderWorld = new RenderWorld();
        EnvironmentLight.In(renderWorld).Should().Be(Vector3.One, "a frame with nothing set takes the map's whole light");
        new LightExtract().Run(GetApp().World, renderWorld);
        var linear = EnvironmentLight.In(renderWorld);
        linear.X.Should().BeApproximately(0.5f, 1e-6f);
        linear.Y.Should().BeApproximately(0.5f * 0.2158605f, 1e-5f, "the tint is decoded to linear light, 128 being 0.216 of the way");
        linear.Z.Should().Be(0);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Wall_Lit_By_The_Map_Takes_The_Share_And_Tint_And_The_Drawn_Sky_Stays_As_It_Was()
    {
        var config = Config.Default.WithWindow("environment light test", 64, 48) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        // A gray sky all around and a white wall facing the camera, lit by the sky alone.
        SetEnvironmentMap(GenImageColor(64, 32, new Color(160, 160, 160)));
        var wall = LoadModelFromMesh(GenMeshCube(2, 2, 0.2f));
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var captures = 0;
        Image Frame()
        {
            // A few frames first, so the map uploaded and filtered on the GPU lights the one read.
            var path = Path.Combine(_folder.Path, $"{captures++}.png");
            for (int frame = 0; frame < 14 && !File.Exists(path); frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawSkybox();
                DrawModel(wall, Vector3.Zero, 1, Color.White);
                EndMode3D();
                if (frame == 4) TakeScreenshot(path);
                EndDrawing();
            }
            File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
            GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
            return LoadImage(path);
        }
        static float Linear(byte level) => BloomRenderer.SrgbToLinear(level / 255f);

        var whole = Frame();
        SetEnvironmentLight(0.5f, Color.White);
        var half = Frame();
        // Red alone, where raylib's Color.Red carries some green and blue.
        SetEnvironmentLight(1, new Color(255, 0, 0));
        var red = Frame();

        // The wall in the middle and the sky in a corner.
        Color Wall(Image frame) => GetImageColor(frame, 32, 24);
        Color Sky(Image frame) => GetImageColor(frame, 3, 3);
        Linear(Wall(whole).G).Should().BeInRange(0.1f, 0.85f, "the map lights the wall below the curve's bend, so its light reads back as it was");
        Linear(Wall(half).G).Should().BeApproximately(Linear(Wall(whole).G) / 2, 0.02f, $"half the share lights the wall at half the light, {Wall(whole)} then {Wall(half)}");
        Wall(red).R.Should().BeCloseTo(Wall(whole).R, 2, "a red tint keeps red as it was");
        Wall(red).G.Should().BeLessThan(3, $"and takes the green and blue away, {Wall(red)}");
        Wall(red).B.Should().BeLessThan(3);
        Sky(half).Should().Be(Sky(whole), "the sky drawn from the map is not the scene's light");
        Sky(red).Should().Be(Sky(whole));
        UnloadModel(wall);
    }
}
