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

    [NeedsVulkanFact]
    public void A_Texture_Saved_While_Live_Is_Drawn_From_Then_On_At_Its_New_Size()
    {
        Open();
        var file = _folder.File("wall.png");
        ExportImage(GenImageColor(2, 2, Color.Red), file);
        var wall = LoadTexture(file);
        Shown(wall).Should().Be("red");

        // Saved green at twice the size, read again into its handle, the store taking the new size
        // and the handle the program holds keeping the one it was given.
        ExportImage(GenImageColor(4, 4, Color.Green), file);
        FramesUntil(() => Live().Entries.Single().Reloads == 1);
        Live().Entries.Single().LastError.Should().BeNull();
        GetApp().World.Resource<TextureStore>().SizeOf(wall.Id).Should().Be((4, 4));
        (wall.Width, wall.Height).Should().Be((2, 2));
        Shown(wall).Should().Be("green", "a saved texture draws from the frames after it is read again");

        // A save that is no picture keeps what the texture was.
        File.WriteAllText(file, "not a picture");
        FramesUntil(() => Live().Entries.Single().Reloads == 2);
        Live().Entries.Single().LastError.Should().NotBeNullOrEmpty();
        Shown(wall).Should().Be("green", "a file that cannot be decoded keeps the texture it was");

        UnloadTexture(wall);
        Live().Entries.Should().BeEmpty("an unloaded texture's file is no longer watched");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
    }

    [NeedsVulkanFact]
    public void A_Model_Saved_While_Live_Is_Read_Again_Into_The_Model_The_Program_Holds()
    {
        Open();
        var file = _folder.File("ramp.obj");
        File.WriteAllText(file, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");
        var ramp = LoadModel(file);
        ramp.Meshes.Should().ContainSingle().Which.TriangleCount.Should().Be(1);
        var paint = LoadShaderFromMemory(Fragment("1.0, 0.0, 0.0", "modelpass", "ModelVertexOutput"), "paint.slang");
        ramp.Materials[0].Shader = paint;
        var meshBefore = ramp.Meshes[0].Id;

        // Saved as a quad, read again into the same model, its mesh made anew and the shader the
        // program set on its material kept.
        File.WriteAllText(file, "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nf 1 2 3\nf 1 3 4\n");
        FramesUntil(() => Live().Entries.Single(e => e.Kind == "model").Reloads == 1);
        Live().Entries.Single(e => e.Kind == "model").LastError.Should().BeNull();
        ramp.Meshes.Should().ContainSingle().Which.TriangleCount.Should().Be(2, "the model the program holds is the file as it is now");
        ramp.Meshes[0].Id.Should().NotBe(meshBefore);
        ramp.Materials[0].Shader.Should().Be(paint, "a shader the program set on a material stays on it");
        GetApp().World.Resource<MeshStore>().Contains(meshBefore).Should().BeFalse("the mesh the file made before is unloaded");
        Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0.5f, 0.5f, 2), new Vector3(0.5f, 0.5f, 0), Vector3.UnitY, 45));
            DrawModel(ramp, Vector3.Zero, 1, Color.White);
            EndMode3D();
        });

        UnloadModel(ramp);
        Live().Entries.Should().NotContain(e => e.Kind == "model", "an unloaded model's file is no longer watched");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
    }

    [NeedsVulkanFact]
    public void A_Scene_Saved_While_Live_Is_Spawned_Again_And_One_That_Cannot_Be_Read_Keeps_The_Level()
    {
        Open();
        var file = _folder.File("level.json");
        File.WriteAllText(file, Scene("door", "lamp"));
        var level = LoadScene(file);
        var ecs = GetApp().World.Resource<EcsWorld>();
        level.Should().HaveCount(2).And.OnlyContain(e => ecs.IsAlive(e));

        File.WriteAllText(file, Scene("door", "lamp", "chest"));
        FramesUntil(() => Live().Entries.Single().Reloads == 1);
        Live().Entries.Single().LastError.Should().BeNull();
        level.Should().OnlyContain(e => !ecs.IsAlive(e), "the entities spawned before are despawned once the file is spawned again");
        ecs.FindByName("chest").Should().NotBe(0, "the file as it is now is in the world");

        // A save that is not a scene file leaves the level as it was.
        var chest = ecs.FindByName("chest");
        File.WriteAllText(file, "{ not json");
        FramesUntil(() => Live().Entries.Single().Reloads == 2);
        Live().Entries.Single().LastError.Should().NotBeNullOrEmpty();
        ecs.IsAlive(chest).Should().BeTrue("a scene file that cannot be read keeps the level it spawned");
    }

    // A scene file of entities with the names given and nothing else.
    private static string Scene(params string[] names) =>
        $$"""{ "format": "{{SceneFile.Format}}", "version": {{SceneFile.Version}}, "entities": [{{string.Join(", ", names.Select(n => $"{{ \"name\": \"{n}\" }}"))}}] }""";

    // The color a texture shows drawn over the whole window, by its brightest channel.
    private string Shown(Texture2D texture) => Brightest(GetImageColor(Capture(() =>
    {
        ClearBackground(Color.Black);
        DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height), new Rectangle(0, 0, 32, 32), Vector2.Zero, 0, Color.White);
    }), 16, 16));

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
