using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// The cache command's listing and clearing of the engine's caches, in a cache folder of the test's
/// own, and the sweep of the compiled shaders no run read for two weeks.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class CacheCommandsTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-cache-");

    public CacheCommandsTests() => UserCache.Folder = _folder.Path;

    public void Dispose()
    {
        UserCache.Folder = null!;
        _folder.Dispose();
    }

    private string Made(string relative, int bytes)
    {
        var path = Path.Combine(_folder.Path, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    [Fact]
    public void The_Caches_Are_Listed_By_Kind_Version_And_Program_And_A_Clear_Leaves_The_Fetched_Packs_Unless_They_Are_Named()
    {
        var shader = Made("shaders/1.0/modelpass.fragmentMain.abc.spv", 2 * 1048576);
        var pipelines = Made("pipelines/1.0/Game.bin", 3 * 1048576);
        var pack = Made("scenes/intel-sponza/sponza.gltf", 1048576);
        ScenePacks.Folder = Path.Combine(_folder.Path, "scenes");
        try
        {
            var listed = CacheCommands.Cache("");
            listed.Should().Contain("compiled shaders, version 1.0: 2.0 MB in 1 files");
            listed.Should().Contain("driver's pipelines, version 1.0: 3.0 MB in 1 files").And.Contain("Game: 3.0 MB");
            listed.Should().Contain("scene pack intel-sponza: 1.0 MB in 1 files");

            var cleared = CacheCommands.Cache("clear");
            cleared.Should().Contain("the compiled shaders cleared, 2.0 MB").And.Contain("the driver's pipelines cleared, 3.0 MB");
            File.Exists(shader).Should().BeFalse();
            File.Exists(pipelines).Should().BeFalse();
            File.Exists(pack).Should().BeTrue("a clear with no part named leaves the packs, which must be fetched again");

            CacheCommands.Cache("clear scenes").Should().Contain("the fetched scene packs cleared, 1.0 MB").And.Contain("scenes fetch");
            File.Exists(pack).Should().BeFalse();
            CacheCommands.Cache("clear all").Should().Contain("cache takes", "there is no part that clears everything at once");
        }
        finally
        {
            ScenePacks.Folder = null!;
        }
    }

    [Fact]
    public void The_Compiled_Shaders_No_Run_Read_For_Two_Weeks_Are_Swept_And_The_Rest_Kept()
    {
        var stale = Made("shaders/1.0/old.fragmentMain.abc.spv", 16);
        var fresh = Made("shaders/1.1/new.fragmentMain.def.spv", 16);
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow - SlangCompiler.SweptAfter - TimeSpan.FromDays(1));
        File.SetLastWriteTimeUtc(fresh, DateTime.UtcNow - SlangCompiler.SweptAfter + TimeSpan.FromDays(1));
        SlangCompiler.Sweep(Path.Combine(_folder.Path, "shaders"));
        File.Exists(stale).Should().BeFalse("no run read it for over two weeks");
        File.Exists(fresh).Should().BeTrue("a run read it within two weeks");
    }
}
