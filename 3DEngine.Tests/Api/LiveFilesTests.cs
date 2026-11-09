using System.Diagnostics;
using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// The files the flat API loaded by name, watched where the program is live and loaded again once
/// a change has been quiet, with no window, each test's files in a folder of its own.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LiveFilesTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-live-test-");

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void A_File_Saved_While_Live_Is_Loaded_Again_Once_After_It_Has_Been_Quiet()
    {
        var path = _folder.File("level.txt");
        File.WriteAllText(path, "first");
        using var live = new LiveFiles(on: true, project: null);
        var read = new List<string>();
        live.Watch("text", 1, path, file => { read.Add(File.ReadAllText(file)); return null; });

        // Two writes in a row, as an editor saves in more than one, are read as one change.
        File.WriteAllText(path, "second");
        File.WriteAllText(path, "third");
        RunUntil(live, () => read.Count > 0);
        Pump(live, LiveFiles.Quiet * 3);

        read.Should().Equal(["third"], "the file is read again once, after its last write");
        var entry = live.Entries.Should().ContainSingle().Subject;
        (entry.Reloads, entry.LastError).Should().Be((1, null));
        entry.LastFrame.Should().NotBeNull();
    }

    [Fact]
    public void A_Reload_That_Fails_Is_Kept_With_Its_Reason_Until_One_Does_Not()
    {
        var path = _folder.File("shader.slang");
        File.WriteAllText(path, "fine");
        using var live = new LiveFiles(on: true, project: null);
        live.Watch("shader", 3, path, file => File.ReadAllText(file) == "broken" ? "it does not compile" : null);

        File.WriteAllText(path, "broken");
        RunUntil(live, () => live.Entries[0].Reloads == 1);
        live.Entries[0].LastError.Should().Be("it does not compile");

        File.WriteAllText(path, "fine again");
        RunUntil(live, () => live.Entries[0].Reloads == 2);
        live.Entries[0].LastError.Should().BeNull("the reload after the mend took");
    }

    [Fact]
    public void A_Program_That_Is_Not_Live_Loads_Nothing_Again_Until_It_Is_Turned_On()
    {
        var path = _folder.File("texture.txt");
        File.WriteAllText(path, "first");
        using var live = new LiveFiles(on: false, project: null);
        var reloads = 0;
        live.Watch("texture", 2, path, _ => { reloads++; return null; });

        File.WriteAllText(path, "second");
        Pump(live, LiveFiles.Quiet * 4);
        reloads.Should().Be(0, "a program that is not live watches nothing");

        live.Turn(true);
        File.WriteAllText(path, "third");
        RunUntil(live, () => reloads > 0);
        reloads.Should().Be(1);

        live.Forget("texture", 2);
        File.WriteAllText(path, "fourth");
        Pump(live, LiveFiles.Quiet * 4);
        (reloads, live.Entries.Count).Should().Be((1, 0), "a file forgotten, as an unload forgets it, is not loaded again");
    }

    [Fact]
    public void A_File_The_Build_Copied_Is_Watched_Where_The_Project_Has_It()
    {
        // A project's build folder, bin/Debug/net10.0, holding a copy of the project's texture and a
        // file of its own.
        var project = _folder.Path;
        var program = Path.Combine(project, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(Path.Combine(program, "textures"));
        Directory.CreateDirectory(Path.Combine(project, "textures"));
        File.WriteAllText(Path.Combine(project, "textures", "wall.png"), "the project's");
        File.WriteAllText(Path.Combine(program, "textures", "wall.png"), "the build's copy");
        File.WriteAllText(Path.Combine(program, "made.png"), "the program's own");
        using var live = new LiveFiles(on: false, project, program);

        live.Watched(Path.Combine(program, "textures", "wall.png")).Should().Be(Path.Combine(project, "textures", "wall.png"));
        live.Watched(Path.Combine(program, "made.png")).Should().Be(Path.Combine(program, "made.png"), "the project has no copy of it");
        live.Watched(Path.Combine(project, "textures", "wall.png")).Should().Be(Path.Combine(project, "textures", "wall.png"));
    }

    [Fact]
    public void The_Command_Says_Each_File_With_Its_Last_Reload()
    {
        var path = _folder.File("model.txt");
        File.WriteAllText(path, "first");
        using var live = new LiveFiles(on: true, project: null);
        live.Watch("model", 4, path, _ => "it has no meshes");
        live.Watch("scene", 5, _folder.File("level.json"), _ => null);

        File.WriteAllText(path, "second");
        RunUntil(live, () => live.Entries.Any(e => e.Reloads > 0));

        using var world = new World();
        var lines = LiveCommands.Say(live, world).Split('\n');
        lines[0].Should().Be("live");
        lines.Should().Contain(line => line.StartsWith($"model 4 {Path.GetFullPath(path)} reloads 1 frame ") && line.EndsWith(" failed it has no meshes"));
        lines.Should().Contain($"scene 5 {Path.GetFullPath(_folder.File("level.json"))}", "a file not loaded again since says no reload");
    }

    // Runs the frame's reloads until the condition holds, a few seconds at most, as a watch's
    // events reach a test after the write that made them.
    private static void RunUntil(LiveFiles live, Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        ulong frame = 0;
        while (!done())
        {
            watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "a saved file is loaded again within moments");
            live.Run(++frame);
            Thread.Sleep(20);
        }
    }

    // Runs the frame's reloads for a while, for a test that asserts nothing more was loaded.
    private static void Pump(LiveFiles live, TimeSpan time)
    {
        var watch = Stopwatch.StartNew();
        ulong frame = 1000;
        while (watch.Elapsed < time)
        {
            live.Run(++frame);
            Thread.Sleep(20);
        }
    }
}
