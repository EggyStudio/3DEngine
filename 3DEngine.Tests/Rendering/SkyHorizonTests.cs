using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The sky below the horizon drawn in a color <see cref="E3D.SetSkyHorizon"/> sets, blended into
/// the map's image over the degrees above it. The color is kept through a new map and taken away by
/// a clear one, the sky's draw carries it on the null device, and on the device the frame read down
/// across the horizon has no edge where the image's dark lower half met its sky, the color is drawn
/// where asked, and the scene's light from the map stays as it was.
/// </summary>
[Collection("E3D")]
public sealed class SkyHorizonTests : IDisposable
{
    private static readonly Color Fog = new(200, 210, 225), Above = new(80, 130, 210), Below = new(40, 40, 45);

    private readonly TestFolder _folder = new("engine-sky-horizon-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // A sky of one color above the horizon and a darker one below, with a sharp edge between, wide
    // enough that the cube it is drawn from keeps the edge.
    private static Image Sky()
    {
        var image = GenImageColor(1024, 512, Below);
        ImageDrawRectangle(ref image, 0, 0, 1024, 256, Above);
        return image;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Horizon_Is_Kept_As_Set_Through_A_New_Map_And_A_Clear_Color_Takes_It_Away()
    {
        UseApp(new App());
        GetApp().World.TryGetResource<SkyHorizon>(out _).Should().BeFalse("the image is drawn as it is until a horizon is set");

        SetSkyHorizon(Fog, 6);
        SetEnvironmentMap(Sky());
        var horizon = GetApp().World.Resource<SkyHorizon>();
        (horizon.Color, horizon.BlendDegrees).Should().Be((Fog, 6f), "a new map keeps the horizon set for the sky");

        SetSkyHorizon(Fog, -3);
        horizon.BlendDegrees.Should().Be(0, "a blend below none meets the image at a sharp edge");
        SetSkyHorizon(Fog, float.NaN);
        horizon.BlendDegrees.Should().Be(0);
        SetSkyHorizon(Fog, 200);
        horizon.BlendDegrees.Should().Be(90, "the blend reaches the top of the sky at most");

        SetSkyHorizon(Color.Blank, 6);
        GetApp().World.TryGetResource<SkyHorizon>(out _).Should().BeFalse("a clear color draws the image as it is again");
    }

    [NeedsSlangFact]
    [Trait("Category", "Integration")]
    public void The_Skys_Draw_Carries_The_Horizon_And_None_Once_It_Is_Taken_Away()
    {
        UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));
        SetEnvironmentMap(Sky());

        // The uniforms the sky's draw carries, the horizon's color and its blend, read by the names
        // sky.slang gives them.
        (Vector4 Color, float Blend) Drawn()
        {
            BeginDrawing();
            BeginMode3D(new Camera3D(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, 60));
            var draws = GetApp().World.Resource<ModelDrawList>().Draws;
            var before = draws.Count;
            DrawSkybox();
            draws.Count.Should().Be(before + 1, "the sky is one draw");
            var draw = draws[^1];
            EndMode3D();
            EndDrawing();

            var program = GetApp().World.Resource<ShaderStore>().Get(draw.Shader)!;
            var color = program.Uniforms.Single(uniform => uniform.Name == "horizonColor").Offset;
            var blend = program.Uniforms.Single(uniform => uniform.Name == "horizonBlend").Offset;
            draw.Uniforms.Should().NotBeNull("the sky's draw carries its uniforms");
            float At(int offset) => BitConverter.ToSingle(draw.Uniforms!, offset);
            return (new Vector4(At(color), At(color + 4), At(color + 8), At(color + 12)), At(blend));
        }

        Drawn().Should().Be((Vector4.Zero, 0f), "with no horizon set the image is drawn as it is");
        SetSkyHorizon(Fog, 6);
        Drawn().Should().Be((new Vector4(200 / 255f, 210 / 255f, 225 / 255f, 1), 6f), "the horizon's color as the program gave it, and its blend in degrees");
        SetSkyHorizon(Color.Blank, 6);
        Drawn().Should().Be((Vector4.Zero, 0f));
    }

    // Opens a window drawn on the device with no display, one sample a pixel so neighbors are read
    // as they were shaded.
    private static void Open(int width, int height)
    {
        var config = Config.Default.WithWindow("sky horizon test", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the capture asked for in a few frames' time has been written, so the map is
    // uploaded and filtered on the GPU before it.
    private Image Capture(Action draw, string name)
    {
        var path = Path.Combine(_folder.Path, name + ".png");
        File.Delete(path);
        for (int frame = 0; frame < 14 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == 4) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Read_Down_Across_The_Horizon_The_Sky_Meets_The_Color_With_No_Edge_Where_The_Image_Steps()
    {
        Open(48, 96);
        SetEnvironmentMap(Sky());
        // Looking level, so the horizon crosses the middle of the frame, sixty degrees high over 96
        // pixels, so a pixel is five eighths of a degree.
        var camera = new Camera3D(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY, 60);
        Image Look(string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            EndMode3D();
        }, name);

        // The frame's middle column, read top to bottom, with the largest step between two of its pixels.
        FrameReadCommands.Reading Column(Image frame) => FrameReadCommands.Measure(frame.Data, frame.Width, 24, 0, 1, frame.Height);

        var bare = Look("bare");
        var edge = Column(bare);
        edge.Step.Should().BeGreaterThan(100, $"the image's dark lower half meets its sky at a sharp edge, {edge}");
        edge.StepY.Should().BeInRange(46, 48, "at the horizon, across the middle of the frame");

        SetSkyHorizon(Fog, 8);
        var fogged = Look("fogged");
        var blended = Column(fogged);
        // Eight degrees are thirteen pixels, so the most red changes between neighbors, 120 between
        // the fog and the sky, is about 120 times the smoothstep's steepest, 1.5, over thirteen,
        // 14, and a little more near the sky, where the two mixed as light change faster in sRGB.
        // Lavapipe reads 16, against 131 at the image's own edge.
        blended.Step.Should().BeLessThanOrEqualTo(20, $"the image blends into the fog's color over the degrees above the horizon, {blended}");
        GetImageColor(fogged, 24, 90).Should().Be(Fog, "below the horizon the sky is the fog's color as the program gave it");
        GetImageColor(fogged, 24, 3).Should().Be(GetImageColor(bare, 24, 3), "above the blend the image is drawn as it was");

        SetSkyHorizon(Fog, 0);
        Column(Look("sharp")).Step.Should().BeGreaterThan(100, "a blend of none meets the image at a sharp edge");
        SetSkyHorizon(Color.Blank, 8);
        GetImageColor(Look("cleared"), 24, 90).Should().Be(GetImageColor(bare, 24, 90), "a clear color draws the image's lower half again");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Looking_Down_The_Sky_Is_The_Color_Asked_And_A_Wall_Lit_By_The_Map_Stays_As_It_Was()
    {
        Open(64, 48);
        SetEnvironmentMap(Sky());
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // Looking down at a cube below the eye, the sky's lower half all around it.
        var camera = new Camera3D(Vector3.Zero, -Vector3.UnitY, Vector3.UnitZ, 60);
        Image Look(string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            DrawModel(wall, new Vector3(0, -3, 0), 1, Color.White);
            EndMode3D();
        }, name);

        var bare = Look("bare");
        SetSkyHorizon(Fog, 5);
        var fogged = Look("fogged");

        GetImageColor(fogged, 3, 3).Should().Be(Fog, "the sky below the horizon is drawn in the color asked, with no fog drawn");
        GetImageColor(bare, 3, 3).Should().NotBe(Fog, "where the image's lower half was drawn before");
        GetImageColor(fogged, 32, 24).Should().Be(GetImageColor(bare, 32, 24), "the map lights the cube from below as before, the horizon being the drawn sky's alone");
        UnloadModel(wall);
    }
}
