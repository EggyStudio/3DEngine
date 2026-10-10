using Xunit;

namespace Engine.Game.Tests;

public sealed class SaveTests : IDisposable
{
    // A folder of this test's own, removed with it, so no run reads another's save.
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "3DEngine.Game.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static WorldInfo Info(string[] blocks) => new("overworld", 1, 0, 0, 0, 0, 0, false, 10, blocks);

    [Fact]
    public void A_Kept_Column_Reads_Back_With_Its_Blocks_In_Another_Save_Of_The_Folder()
    {
        var column = new ChunkColumn(-3, 40);
        column.Set(1, 2, 3, BlockId.Glowstone);
        column.Set(15, 127, 0, BlockId.Water);
        column.Changed = true;
        var save = new WorldSave(_folder);
        save.Keep(column);
        save.Flush();
        save.WriteInfo(Info([.. Blocks.All.Select(b => b.Key)]));

        var read = new WorldSave(_folder);
        read.ReadInfo();
        read.ReadRegion(-3, 40);
        var bytes = read.Kept(-3, 40);
        Assert.NotNull(bytes);
        var back = WorldSave.Decode(-3, 40, bytes, read.Renumber);

        Assert.Equal(BlockId.Glowstone, back.Get(1, 2, 3));
        Assert.Equal(BlockId.Water, back.Get(15, 127, 0));
        Assert.Equal(BlockId.Air, back.Get(0, 0, 0));
        Assert.False(column.Changed);
        Assert.Null(read.Kept(-3, 41));
    }

    [Fact]
    public void A_Save_Whose_Blocks_Were_Numbered_Otherwise_Is_Read_By_Their_Keys()
    {
        // A save that numbered stone 1 and grass 3, the other way round from this build.
        var keys = Blocks.All.Select(b => b.Key).ToArray();
        (keys[1], keys[3]) = (keys[3], keys[1]);
        var column = new ChunkColumn(0, 0);
        column.Set(0, 0, 0, (BlockId)1);
        column.Changed = true;
        var save = new WorldSave(_folder);
        save.Keep(column);
        save.Flush();
        save.WriteInfo(Info(keys));

        var read = new WorldSave(_folder);
        read.ReadInfo();
        read.ReadRegion(0, 0);
        var back = WorldSave.Decode(0, 0, read.Kept(0, 0)!, read.Renumber);

        Assert.Equal(BlockId.Stone, back.Get(0, 0, 0));
    }
}
