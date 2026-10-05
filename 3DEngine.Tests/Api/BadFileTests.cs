using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Every loader given a file that is missing, empty, cut short or random bytes, which answers as
/// DESIGN.md §6 says a failed load does: a warning naming the file, a resource that reports itself
/// invalid or empty, and no exception. A loader added later is a row of <see cref="Loaders"/>.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class BadFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-bad-files-").FullName;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    private static string Repo(string path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3DEngine.slnx"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, path);
    }

    // Each loader, the extension its files have, a good file of its kind to cut short, whether what
    // it gave back is usable, and the bad files it refuses. A file cut short may still give something,
    // and the file functions read an empty or random file as the bytes it is.
    private static readonly string[] Refused = ["missing", "empty", "random"];

    // A font other than the default one, which a font that fails to load falls back to.
    private static bool OwnFont(Font font) => IsFontValid(font) && font.Texture.Id != GetFontDefault().Texture.Id;

    private static readonly (string Name, string Extension, string Good, Func<string, bool> LoadsUsable, string[] Refuses)[] Loaders =
    [
        ("LoadImage", ".png", "3DEngine.Examples/resources/checker.png", f => IsImageValid(LoadImage(f)), Refused),
        ("LoadImageAnim", ".png", "3DEngine.Examples/resources/checker.png", f => IsImageValid(LoadImageAnim(f, out _)), Refused),
        ("LoadTexture", ".png", "3DEngine.Examples/resources/logo.png", f => IsTextureValid(LoadTexture(f)), Refused),
        ("LoadModel", ".obj", "3DEngine.Examples/resources/torus.obj", f => LoadModel(f) is var m && IsModelValid(m) && m.Meshes.Length > 0, Refused),
        ("LoadModel glTF", ".gltf", "3DEngine.Examples/resources/arm.gltf", f => LoadModel(f) is var m && IsModelValid(m) && m.Meshes.Length > 0, Refused),
        ("LoadModelAnimations", ".gltf", "3DEngine.Examples/resources/arm.gltf", f => LoadModelAnimations(f).Length > 0, Refused),
        ("LoadMaterials", ".obj", "3DEngine.Examples/resources/torus.obj", f => LoadMaterials(f).Length > 0, Refused),
        ("LoadFont", ".ttf", "3DEngine.Examples/resources/fonts/Lato-Regular.ttf", f => OwnFont(LoadFont(f)), Refused),
        ("LoadFontEx", ".ttf", "3DEngine.Examples/resources/fonts/Lato-Regular.ttf", f => OwnFont(LoadFontEx(f, 24)), Refused),
        ("LoadFontEx codepoints", ".ttf", "3DEngine.Examples/resources/fonts/Lato-Regular.ttf", f => OwnFont(LoadFontEx(f, 24, [65, 66, 0x1F600])), Refused),
        ("LoadFontEx distance field", ".ttf", "3DEngine.Examples/resources/fonts/Lato-Regular.ttf", f => OwnFont(LoadFontEx(f, 24, null, FontType.Sdf)), Refused),
        ("LoadSound", ".wav", "3DEngine.Examples/resources/coin.wav", f => IsSoundValid(LoadSound(f)), Refused),
        ("LoadSound Ogg", ".ogg", "3DEngine.Examples/resources/drone.ogg", f => IsSoundValid(LoadSound(f)), Refused),
        ("LoadWave", ".wav", "3DEngine.Examples/resources/coin.wav", f => IsWaveValid(LoadWave(f)), Refused),
        ("LoadMusicStream", ".ogg", "3DEngine.Examples/resources/drone.ogg", f => IsMusicValid(LoadMusicStream(f)), Refused),
        ("LoadShader", ".slang", "3DEngine.Examples/resources/shaders/grayscale.slang", f => IsShaderValid(LoadShader(f)), Refused),
        ("LoadComputeShader", ".slang", "3DEngine.Examples/resources/shaders/plasma.slang", f => IsShaderValid(LoadComputeShader(f)), Refused),
        ("LoadScene", ".json", "games/Summit/resources/level.json", f => LoadScene(f).Count > 0, Refused),
        ("LoadFileText", ".txt", "3DEngine.Examples/resources/torus.mtl", f => LoadFileText(f) is not null, ["missing"]),
        ("LoadFileData", ".bin", "3DEngine.Examples/resources/coin.wav", f => LoadFileData(f) is not null, ["missing"]),
    ];

    // The four bad files of a kind: none, nothing in it, the first third of a good one, and bytes
    // with no meaning, the same each run.
    private IEnumerable<(string Kind, string Path)> BadFiles(string extension, string good)
    {
        var bytes = File.ReadAllBytes(Repo(good));
        var random = new byte[4096];
        new Random(7).NextBytes(random);
        var files = new (string Kind, byte[]? Bytes)[] { ("missing", null), ("empty", []), ("cut short", bytes[..(bytes.Length / 3)]), ("random", random) };
        foreach (var (kind, content) in files)
        {
            var path = Path.Combine(_directory, $"{kind.Replace(' ', '-')}-{Guid.NewGuid():N}{extension}");
            if (content is not null) File.WriteAllBytes(path, content);
            yield return (kind, path);
        }
    }

    // The readers under the ECS, each by the asset type a file of its extension loads as.
    private static readonly (string Name, string Extension, string Good, Func<AssetServer, string, Func<LoadState>> Load)[] Readers =
    [
        ("texture", ".png", "3DEngine.Examples/resources/checker.png", (server, path) => { var h = server.Load<Texture>(path); return () => server.GetLoadState(h); }),
        ("model OBJ", ".obj", "3DEngine.Examples/resources/torus.obj", (server, path) => { var h = server.Load<SceneAsset>(path); return () => server.GetLoadState(h); }),
        ("model glTF", ".gltf", "3DEngine.Examples/resources/arm.gltf", (server, path) => { var h = server.Load<SceneAsset>(path); return () => server.GetLoadState(h); }),
        // Sounds load through the same decoders LoadSound does, whose row above covers them, and
        // their asset loader is there only once InitAudioDevice has added it.
        ("shader", ".slang", "3DEngine.Examples/resources/shaders/grayscale.slang", (server, path) => { var h = server.Load<ShaderProgram>(path); return () => server.GetLoadState(h); }),
    ];

    [NeedsVulkanFact]
    public void Every_Reader_Under_The_Ecs_Marks_A_Bad_File_Failed_With_A_Message()
    {
        var config = Config.Default.WithWindow("bad files", 160, 120) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var server = GetApp().World.Resource<AssetServer>();
        var ecs = GetApp().World.Resource<EcsWorld>();
        // The files go where the asset server reads from, the program's source folder.
        var folder = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "source", "bad-files")).FullName;
        var wrong = new List<string>();
        try
        {
            foreach (var (name, extension, good, load) in Readers)
                foreach (var (kind, path) in BadFiles(extension, good))
                {
                    var relative = Path.Combine("bad-files", Path.GetFileName(path));
                    if (File.Exists(path)) File.Copy(path, Path.Combine(folder, Path.GetFileName(path)));
                    var before = ConsoleLog.Written;
                    try
                    {
                        var state = load(server, relative);
                        for (int frame = 0; frame < 120 && state() is LoadState.Loading or LoadState.NotLoaded; frame++)
                        {
                            BeginDrawing();
                            EndDrawing();
                            Thread.Sleep(5);
                        }
                        if (state() == LoadState.Loaded && kind != "cut short") wrong.Add($"{name}, {kind} file: loaded");
                        if (state() != LoadState.Loaded && !Named(before, Path.GetFileName(path))) wrong.Add($"{name}, {kind} file: no message naming the file");
                    }
                    catch (Exception error)
                    {
                        wrong.Add($"{name}, {kind} file: threw {error.GetType().Name}: {error.Message}");
                    }
                }

            // A scene file and a model a level places, each bad four ways.
            foreach (var (name, extension, good, component) in new (string, string, string, Func<string, object>)[]
            {
                ("SceneRef", ".json", "games/Summit/resources/level.json", p => new SceneRef { Path = p }),
                ("ModelRef", ".obj", "3DEngine.Examples/resources/torus.obj", p => new ModelRef { Path = p }),
            })
                foreach (var (kind, path) in BadFiles(extension, good))
                {
                    var before = ConsoleLog.Written;
                    try
                    {
                        var entity = ecs.Spawn();
                        ecs.Add(entity, new Transform(System.Numerics.Vector3.Zero));
                        if (component(path) is SceneRef scene) ecs.Add(entity, scene);
                        else ecs.Add(entity, (ModelRef)component(path));
                        for (int frame = 0; frame < 60 && !Named(before, Path.GetFileName(path)); frame++)
                        {
                            BeginDrawing();
                            EndDrawing();
                            Thread.Sleep(5);
                        }
                        if (!Named(before, Path.GetFileName(path)) && kind != "cut short") wrong.Add($"{name}, {kind} file: no message naming the file");
                        ecs.DespawnRecursive(entity);
                    }
                    catch (Exception error)
                    {
                        wrong.Add($"{name}, {kind} file: threw {error.GetType().Name}: {error.Message}");
                    }
                }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        string.Join("\n", wrong).Should().BeEmpty();
    }

    // Whether a warning or worse logged since the count was taken names the file.
    private static bool Named(int before, string file) =>
        ConsoleLog.All().Skip(Math.Max(0, ConsoleLog.All().Length - (ConsoleLog.Written - before)))
            .Any(l => l.Level >= LogLevel.Warning && l.Text.Contains(file));

    [NeedsVulkanFact]
    public void Every_Loader_Answers_A_Bad_File_With_A_Warning_And_An_Unusable_Resource()
    {
        var config = Config.Default.WithWindow("bad files", 160, 120) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        BeginDrawing();

        var wrong = new List<string>();
        foreach (var (name, extension, good, loadsUsable, refuses) in Loaders)
        {
            try
            {
                if (!loadsUsable(Repo(good))) wrong.Add($"{name}: does not load a good file, so its row is no test");
            }
            catch (Exception error)
            {
                wrong.Add($"{name}: threw on a good file, {error.GetType().Name}: {error.Message}");
            }
            foreach (var (kind, path) in BadFiles(extension, good))
            {
                var before = ConsoleLog.Written;
                bool usable;
                try
                {
                    usable = loadsUsable(path);
                }
                catch (Exception error)
                {
                    wrong.Add($"{name}, {kind} file: threw {error.GetType().Name}: {error.Message}");
                    continue;
                }
                if (usable && refuses.Contains(kind)) wrong.Add($"{name}, {kind} file: gave back something usable");
                var named = ConsoleLog.All().Skip(Math.Max(0, ConsoleLog.All().Length - (ConsoleLog.Written - before)))
                    .Any(l => l.Level >= LogLevel.Warning && l.Text.Contains(Path.GetFileName(path)));
                if (!usable && !named) wrong.Add($"{name}, {kind} file: no warning naming the file");
            }
        }
        EndDrawing();

        string.Join("\n", wrong).Should().BeEmpty();
    }
}
