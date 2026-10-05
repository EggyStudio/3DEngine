using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Assets;

/// <summary>
/// Each loader lets go of its file once it has read it, which only a Windows run could see by
/// chance before, a delete there failing on a file still open.
/// </summary>
/// <remarks>
/// A file read and left open is a file a game cannot save over while it runs, and on Windows one a
/// child process started meanwhile inherits. Linux deletes an open file without complaint, so the
/// test asks the system which files are open, under <c>/proc/self/fd</c>, and on Windows opens each
/// for writing with no sharing, which fails while anything holds it.
/// </remarks>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class FileHandleTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-handles-");
    // Where the asset server reads from, the program's source folder.
    private readonly TestFolder _source = TestFolder.At(Path.Combine(AppContext.BaseDirectory, "source", "handles-" + Guid.NewGuid().ToString("N")[..8]));
    private readonly App _app = new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins());

    public FileHandleTests() => UseApp(_app);

    public void Dispose()
    {
        UseApp(null);
        _app.Shutdown();
        _folder.Dispose();
        _source.Dispose();
    }

    // Each loader, the files it reads from the examples' resources, and whether what it gave back
    // was loaded, so a load that failed early does not pass for one that let go.
    public static TheoryData<string> Loaders => new() { "model", "model glTF", "model by the asset server", "texture", "image", "wave", "music, unloaded", "font", "scene" };

    [NeedsOpenFilesTheory]
    [MemberData(nameof(Loaders))]
    public void A_Loader_Holds_No_File_Open_Once_It_Has_Read_It(string loader)
    {
        var files = loader switch
        {
            "model" => Load(["torus.obj", "torus.mtl", "checker.png"], f => LoadModel(f[0]).Meshes.Length > 0),
            "model glTF" => Load(["arm.gltf"], f => IsModelValid(LoadModel(f[0]))),
            "model by the asset server" => LoadByServer(),
            "texture" => Load(["logo.png"], f => IsTextureValid(LoadTexture(f[0]))),
            "image" => Load(["checker.png"], f => IsImageValid(LoadImage(f[0]))),
            "wave" => Load(["coin.wav", "drone.ogg"], f => IsWaveValid(LoadWave(f[0])) && IsWaveValid(LoadWave(f[1]))),
            // Music is read as it plays, as raylib streams it, so its file is open until it is
            // unloaded, and closed after.
            "music, unloaded" => Load(["drone.ogg"], f => LoadMusicStream(f[0]) is var music && IsMusicValid(music) && Unload(music)),
            "font" => Load(["fonts/Lato-Regular.ttf"], f => IsFontValid(LoadFont(f[0]))),
            "scene" => Scene(),
            _ => throw new ArgumentOutOfRangeException(nameof(loader)),
        };

        files.Where(Held).Should().BeEmpty("{0} read these and should have closed them", loader);
    }

    [NeedsOpenFilesFact(slang: true)]
    public void A_Shader_Holds_No_File_Open_Once_It_Is_Compiled()
    {
        var files = Load(["shaders/grayscale.slang"], f => IsShaderValid(LoadShader(f[0])));

        files.Where(Held).Should().BeEmpty();
    }

    [NeedsOpenFilesFact]
    public void A_File_Left_Open_Is_Found_Held_And_Not_Once_Closed()
    {
        // The check itself, which would pass every loader if it found nothing.
        var file = _folder.File("open.bin");
        File.WriteAllBytes(file, [1]);
        using (File.OpenRead(file))
            Held(file).Should().BeTrue();
        Held(file).Should().BeFalse();
    }

    // Copies the named files of the examples' resources into the folder, loads them through the
    // flat API, and returns the copies' paths.
    private string[] Load(string[] names, Func<string[], bool> load)
    {
        var copies = names.Select(name =>
        {
            var copy = _folder.File(Path.GetFileName(name));
            File.Copy(Path.Combine(Api.CheatsheetTests.RepoRoot(), "3DEngine.Examples", "resources", name), copy);
            return copy;
        }).ToArray();
        load(copies).Should().BeTrue("the files are ones that load");
        return copies;
    }

    private static bool Unload(Music music)
    {
        UnloadMusicStream(music);
        return true;
    }

    // An OBJ and its library loaded as a level places a model, through the asset server, which
    // reads from its source folder on a worker.
    private string[] LoadByServer()
    {
        var copies = new[] { "torus.obj", "torus.mtl" }.Select(name =>
        {
            var copy = _source.File(name);
            File.Copy(Path.Combine(Api.CheatsheetTests.RepoRoot(), "3DEngine.Examples", "resources", name), copy);
            return copy;
        }).ToArray();
        var server = _app.World.Resource<AssetServer>();
        var handle = server.Load<SceneAsset>(Path.GetFileName(_source.Path) + "/torus.obj");
        for (int frame = 0; frame < 500 && server.GetLoadState(handle) is LoadState.Loading or LoadState.NotLoaded; frame++)
        {
            _app.BeginFrame();
            _app.EndFrame();
            // The file is read on the asset server's worker, outside the frame loop.
            Thread.Sleep(2);
        }
        server.GetLoadState(handle).Should().Be(LoadState.Loaded);
        return copies;
    }

    private string[] Scene()
    {
        var file = _folder.File("level.json");
        File.WriteAllText(file, """{ "format": "3dengine-scene", "version": 1, "entities": [ { "components": { "Transform": { "Position": [1, 2, 3] } } } ] }""");
        LoadScene(file).Should().ContainSingle();
        return [file];
    }

    // Whether this process has the file open, which Linux says by a descriptor under
    // /proc/self/fd naming it and Windows by refusing to open it alone.
    private bool Held(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var alone = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        // A descriptor's target is the real path, which may differ from the folder's by a link
        // above it, as /home is on some systems, so it is matched by the folder's unique name.
        var tail = $"/{Path.GetFileName(Path.GetDirectoryName(file))}/{Path.GetFileName(file)}";
        foreach (var descriptor in Directory.EnumerateFileSystemEntries("/proc/self/fd"))
        {
            string? target;
            try
            {
                target = new FileInfo(descriptor).LinkTarget;
            }
            catch (IOException)
            {
                // Closed between being listed and being read.
                continue;
            }
            if (target is not null && target.EndsWith(tail, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
