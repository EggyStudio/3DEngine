using FluentAssertions;
using Engine.Files.Compiler;

namespace Engine.Tests.Behaviors;

/// <summary>The directory of scripts apps compile from, watched once however many apps watch it at the same time.</summary>
[Trait("Category", "Integration")]
public sealed class ScriptDirectoryWatchTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-script-watch-");

    public void Dispose() => _folder.Dispose();

    private App Open() => new App(Config.Default with { Headless = true }).AddPlugin(new BehaviorsPlugin { ScriptsDirectory = _folder.Path });

    [Fact]
    public void A_Second_App_On_The_Same_Directory_Makes_No_Second_Watcher_And_The_Last_To_Close_Lets_It_Go()
    {
        var first = Open();
        var watchers = DirectoryWatches.Of(_folder.Path).Watchers;
        watchers.Should().BePositive("an app with behaviors watches its scripts");

        var second = Open();
        DirectoryWatches.Of(_folder.Path).Should().Be((watchers, 2), "the second app is told by the watchers the first made, as a hundred apps would be, where each made its own");

        first.Shutdown();
        DirectoryWatches.Of(_folder.Path).Should().Be((watchers, 1), "the watchers stay for the app still watching");
        second.Shutdown();
        DirectoryWatches.Of(_folder.Path).Should().Be((0, 0), "and go with the last");
    }
}
