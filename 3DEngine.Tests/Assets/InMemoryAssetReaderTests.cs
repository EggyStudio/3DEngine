using System.Collections.Concurrent;
using FluentAssertions;
using Xunit;

namespace Engine.Tests.Assets;

/// <summary>
/// Tests for <see cref="InMemoryAssetReader"/> - process-wide synthetic asset
/// store that backs the <c>__embedded__/</c> path scheme used by the glTF and
/// future USDZ readers when surfacing archive-internal resources to the
/// extension-dispatched loader pipeline.
/// </summary>
[Trait("Category", "Unit")]
public class InMemoryAssetReaderTests
{
    private static InMemoryAssetReader NewIsolated() =>
        new(new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal));

    [Fact]
    public async Task Set_Then_Exists_Then_Read_Roundtrips_Bytes()
    {
        var reader = NewIsolated();
        var path = new AssetPath("foo/bar.png");
        var bytes = new byte[] { 1, 2, 3, 4 };

        reader.Set(path, bytes);

        reader.Exists(path).Should().BeTrue();
        reader.Count.Should().Be(1);

        using var stream = await reader.ReadAsync(path);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task Set_Replaces_Existing_Bytes()
    {
        var reader = NewIsolated();
        var p = new AssetPath("a.bin");
        reader.Set(p, new byte[] { 1 });
        reader.Set(p, new byte[] { 9, 9 });

        reader.Count.Should().Be(1);
        using var stream = await reader.ReadAsync(p);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.ToArray().Should().BeEquivalentTo(new byte[] { 9, 9 });
    }

    [Fact]
    public void Exists_Returns_False_For_Unknown_Path()
    {
        NewIsolated().Exists(new AssetPath("nope.bin")).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_Throws_FileNotFound_For_Unknown_Path()
    {
        var reader = NewIsolated();
        var act = () => reader.ReadAsync(new AssetPath("missing.bin"));
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void CreateWatcher_Returns_Null()
    {
        // In-memory assets don't change without an explicit Set; no watcher needed.
        NewIsolated().CreateWatcher().Should().BeNull();
    }

    [Fact]
    public void Set_Throws_On_Null_Bytes()
    {
        var reader = NewIsolated();
        var act = () => reader.Set(new AssetPath("x"), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Publish_Writes_Through_The_Process_Wide_Store()
    {
        // Use a unique key so concurrent test runs don't collide.
        var key = new AssetPath($"__embedded__/test/inmem_publish_{Guid.NewGuid():N}.bin");
        var bytes = new byte[] { 7, 7, 7 };

        InMemoryAssetReader.Publish(key, bytes);

        var reader = new InMemoryAssetReader();
        reader.Exists(key).Should().BeTrue();
        InMemoryAssetReader.ProcessWideStore.ContainsKey(key.Path).Should().BeTrue();

        // Cleanup so we don't leak between test cases.
        InMemoryAssetReader.ProcessWideStore.TryRemove(key.Path, out _);
    }

    [Fact]
    public void Constructor_Throws_On_Null_Store()
    {
        var act = () => new InMemoryAssetReader(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
