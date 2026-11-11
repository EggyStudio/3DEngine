using FluentAssertions;

namespace Engine.Tests.Core;

/// <summary>The engine's folder in the user's cache, each kind of it kept by the engine's version (<see cref="UserCache"/>).</summary>
[Collection("E3D")]
public sealed class UserCacheTests
{
    [Fact]
    public void Another_Versions_Folder_No_Run_Used_For_The_Time_Given_Is_Deleted_And_This_Versions_Kept()
    {
        using var cache = new TestFolder("engine-versions-");
        UserCache.Folder = cache.Path;
        try
        {
            var longAgo = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string Kept(string version, string file, DateTime written)
            {
                var path = Path.Combine(cache.Path, "pipelines", version, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, [1, 2, 3]);
                File.SetLastWriteTimeUtc(path, written);
                return Path.GetDirectoryName(path)!;
            }
            var old = Kept("6.2.17", "Game.bin", longAgo);
            // A folder with one file written within the time is kept whole, as a package run lately.
            var recent = Kept("6.2.30", "Game.bin", longAgo);
            Kept("6.2.30", "Other.bin", new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var own = Kept(UserCache.Version, "Game.bin", longAgo);
            var empty = Path.Combine(cache.Path, "pipelines", "6.2.1");
            Directory.CreateDirectory(empty);

            UserCache.SweepVersions("pipelines", TimeSpan.FromDays(14));

            Directory.Exists(old).Should().BeFalse("no run of that version wrote its folder for longer than the time given");
            Directory.Exists(empty).Should().BeFalse("a version's folder holding nothing is left by no run");
            Directory.Exists(recent).Should().BeTrue("a file of that version's was written within the time given");
            File.Exists(Path.Combine(own, "Game.bin")).Should().BeTrue("this version's own folder is never swept, however old its file");
        }
        finally
        {
            UserCache.Folder = null!;
        }
    }
}
