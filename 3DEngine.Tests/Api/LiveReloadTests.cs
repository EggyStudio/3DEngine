using System.Diagnostics;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// The flat API's loads by name loaded again in place as their files are saved, in a live program
/// drawn offscreen, each test's files in a folder of its own.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class LiveReloadTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-live-reload-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsVulkanFact]
    public void A_Shader_Saved_While_Live_Draws_From_Then_On_And_One_That_Does_Not_Compile_Keeps_It()
    {
        Open();
        SetTonemap(Tonemap.None);
        var (flatFile, modelFile) = (_folder.File("flat.slang"), _folder.File("model.slang"));
        File.WriteAllText(flatFile, Fragment("1.0, 0.0, 0.0", "engine", "VertexOutput"));
        File.WriteAllText(modelFile, Fragment("1.0, 0.0, 0.0", "modelpass", "ModelVertexOutput"));
        var flat = LoadShader(flatFile);
        var model = LoadShader(modelFile);
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        cube.Materials[0].Shader = model;
        Drawn(flat, cube).Should().Be(("red", "red"));

        // Both saved green, each compiled again into its handle, the immediate pass's and the model
        // pass's pipelines made again from the new program.
        File.WriteAllText(flatFile, Fragment("0.0, 1.0, 0.0", "engine", "VertexOutput"));
        File.WriteAllText(modelFile, Fragment("0.0, 1.0, 0.0", "modelpass", "ModelVertexOutput"));
        FramesUntil(() => Live().Entries.All(e => e.Reloads == 1));
        Live().Entries.Should().OnlyContain(e => e.Kind == "shader" && e.LastError == null);
        Drawn(flat, cube).Should().Be(("green", "green"), "a saved shader draws from the frames after it is compiled again");

        // A save that does not compile keeps what the shader was.
        File.WriteAllText(flatFile, Fragment("undefinedColor", "engine", "VertexOutput"));
        FramesUntil(() => Live().Entries.Single(e => e.Id == flat.Id).Reloads == 2);
        Live().Entries.Single(e => e.Id == flat.Id).LastError.Should().NotBeNullOrEmpty("slangc's reason is kept for the live command and the log");
        Drawn(flat, cube).Should().Be(("green", "green"), "a shader that does not compile keeps the one it was");

        UnloadShader(flat);
        Live().Entries.Should().ContainSingle(e => e.Id == model.Id, "an unloaded shader's file is no longer watched");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
    }

    private static void Open() =>
        UseApp(new App(Config.Default.WithWindow("live test", 32, 32) with { Headless = true, Offscreen = true, Samples = 1, Live = true })
            .AddPlugin(new DefaultPlugins()));

    private static LiveFiles Live() => GetApp().World.Resource<LiveFiles>();

    // A shader whose fragment stage is one color, over the module and the input of its pass.
    private static string Fragment(string color, string module, string input) => $$"""
        import {{module}};

        [shader("fragment")]
        float4 fragmentMain({{input}} input) : SV_Target
        {
            return float4({{color}}, 1.0);
        }
        """;

    // The color a rectangle drawn with the flat shader shows, then a cube's face drawn with the
    // model's, each by the channel that is brightest.
    private (string Flat, string Model) Drawn(Shader flat, Model cube)
    {
        var rectangle = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginShaderMode(flat);
            DrawRectangle(0, 0, 32, 32, Color.White);
            EndShaderMode();
        });
        var face = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 0, 1.5f), Vector3.Zero, Vector3.UnitY, 45));
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        });
        return (Brightest(GetImageColor(rectangle, 16, 16)), Brightest(GetImageColor(face, 16, 16)));
    }

    private static string Brightest(Color c) =>
        c.R > 128 && c.G < 64 && c.B < 64 ? "red" : c.G > 128 && c.R < 64 && c.B < 64 ? "green" : $"{c.R} {c.G} {c.B}";

    // Draws frames until the capture asked for in the first has been written.
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
        return LoadImage(path);
    }

    // Draws frames, the live files read again at the start of each, until the condition holds, a
    // few seconds at most, as the system's watch tells of a save after the write that made it.
    private static void FramesUntil(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20), "a saved file is loaded again within moments");
            BeginDrawing();
            ClearBackground(Color.Black);
            EndDrawing();
            Thread.Sleep(20);
        }
    }
}
