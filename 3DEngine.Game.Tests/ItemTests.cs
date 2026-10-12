using System.Numerics;
using Xunit;

namespace Engine.Game.Tests;

public sealed class ItemTests : IDisposable
{
    // A folder of this test's own, removed with it, so no run reads another's save.
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "3DEngine.Game.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    // A floor of stone with its top at 11.
    private static VoxelWorld Floor() => Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Stone));

    private static Item Of(BlockId block) => Items.Of(block);

    [Fact]
    public void Every_Block_But_Air_Has_An_Item_That_Places_The_Block_It_Is_Held_As()
    {
        foreach (var block in Blocks.All.Where(b => b.Id != BlockId.Air))
            Assert.Equal(block.Item, Of(block.Id).Block);
        Assert.Equal(Items.All.Count, Items.All.Select(i => i.Key).Distinct().Count());
        Assert.All(Items.All, item => Assert.Equal(item, Items.All[item.Id]));
    }

    [Fact]
    public void Items_Go_Onto_Stacks_Of_Their_Kind_Then_Into_Empty_Slots_And_What_Does_Not_Fit_Is_Given_Back()
    {
        var inventory = new Inventory();
        inventory.Slots[4] = new ItemStack(Of(BlockId.Dirt), 60);
        inventory.Slots[0] = new ItemStack(Of(BlockId.Stone), 1);

        Assert.Equal(0, inventory.Add(Of(BlockId.Dirt), 10));
        Assert.Equal(64, inventory.Slots[4]!.Value.Count);
        Assert.Equal(new ItemStack(Of(BlockId.Dirt), 6), inventory.Slots[1]);

        for (int i = 0; i < Inventory.Size; i++) inventory.Slots[i] ??= new ItemStack(Items.Feather, 64);
        Assert.Equal(7, inventory.Add(Of(BlockId.Dirt), 65));
        Assert.Equal(64, inventory.Slots[1]!.Value.Count);

        Assert.Equal(1, inventory.Take(0, 5));
        Assert.Null(inventory.Slots[0]);
    }

    [Fact]
    public void An_Inventory_Saves_Its_Slots_And_Reads_Them_Back_Leaving_An_Unknown_Item_Out()
    {
        var inventory = new Inventory();
        inventory.Slots[0] = new ItemStack(Of(BlockId.Torch), 12);
        inventory.Slots[35] = new ItemStack(Items.RawBeef, 3);
        var saved = inventory.Save();
        saved[7] = "no_such_item 5";

        var back = new Inventory();
        back.Load(saved);

        Assert.Equal(inventory.Slots, back.Slots);
    }

    [Theory]
    [InlineData(BlockId.Grass, BlockId.Dirt)]
    [InlineData(BlockId.Stone, BlockId.Cobblestone)]
    [InlineData(BlockId.WallTorchEast, BlockId.Torch)]
    [InlineData(BlockId.StoneSlabTop, BlockId.StoneSlab)]
    [InlineData(BlockId.OakDoorWestTopOpen, BlockId.OakDoor)]
    [InlineData(BlockId.Glowstone, BlockId.Glowstone)]
    public void A_Block_Broken_Drops_The_Item_It_Leaves(BlockId broken, BlockId left)
    {
        var drop = Assert.Single(Loot.Of(broken, new Random(1)));
        Assert.Equal((Of(left), 1), drop);
    }

    [Fact]
    public void Leaves_Glass_And_Ice_Drop_Nothing_And_Wheat_Its_Wheat_And_Seeds()
    {
        Assert.Empty(Loot.Of(BlockId.OakLeaves, new Random(1)));
        Assert.Empty(Loot.Of(BlockId.Glass, new Random(1)));
        Assert.Empty(Loot.Of(BlockId.Ice, new Random(1)));
        var wheat = Loot.Of(BlockId.Wheat, new Random(1)).ToList();
        Assert.Contains((Items.Wheat, 1), wheat);
        Assert.Contains(wheat, d => d.Item == Of(BlockId.Wheat) && d.Count >= 1);
    }

    [Fact]
    public void A_Sheep_Drops_Wool_Of_Its_Coat_And_Mutton()
    {
        var sheep = new Mob(1, new MobRecord(MobKinds.Sheep, 3, Vector3.Zero, 0, 8, null), 1);

        var drops = Loot.Of(sheep, new Random(1)).ToList();

        Assert.Contains((Of(BlockId.BlackWool), 1), drops);
        Assert.Contains(drops, d => d.Item == Items.RawMutton);
    }

    [Fact]
    public void A_Stack_On_The_Ground_Is_Picked_Up_After_Its_Delay_As_Far_As_The_Inventory_Has_Room()
    {
        var world = Floor();
        var player = new Body { Position = new Vector3(4.5f, 11, 4.5f) };
        var inventory = new Inventory();
        for (int i = 1; i < Inventory.Size; i++) inventory.Slots[i] = new ItemStack(Items.Feather, 64);
        inventory.Slots[0] = new ItemStack(Of(BlockId.Sand), 60);
        var drops = new ItemDrops();
        drops.Spawn(Of(BlockId.Sand), 10, new Vector3(5.5f, 11.5f, 4.5f), Vector3.Zero);

        Assert.Equal(0, drops.Update(world, player, inventory, 0.3f));
        Assert.Equal(4, drops.Update(world, player, inventory, 0.3f));

        Assert.Equal(64, inventory.Slots[0]!.Value.Count);
        var left = Assert.Single(drops.All);
        Assert.Equal(6, left.Count);
        Assert.Equal(11, left.Body.Position.Y, 2);
    }

    [Fact]
    public void Two_Stacks_Of_One_Item_Lying_Together_Join()
    {
        var world = Floor();
        var drops = new ItemDrops();
        drops.Spawn(Items.Leather, 2, new Vector3(4.5f, 11, 4.5f), Vector3.Zero);
        drops.Spawn(Items.Leather, 3, new Vector3(4.7f, 11, 4.5f), Vector3.Zero);
        drops.Spawn(Items.Feather, 1, new Vector3(4.6f, 11, 4.5f), Vector3.Zero);

        drops.Update(world, new Body { Position = new Vector3(20.5f, 11, 20.5f) }, new Inventory(), 0.1f);

        Assert.Equal(2, drops.All.Count);
        Assert.Equal(5, drops.All.Single(d => d.Item == Items.Leather).Count);
    }

    [Fact]
    public void A_Torch_That_Falls_With_Its_Wall_Is_Listed_For_Its_Drop()
    {
        var world = Floor();
        world.SetBlock(5, 11, 5, BlockId.Stone);
        world.SetBlock(6, 11, 5, BlockId.WallTorchEast);
        world.Fallen.Clear();

        world.SetBlock(5, 11, 5, BlockId.Air);

        Assert.Equal([(6, 11, 5, BlockId.WallTorchEast)], world.Fallen);
        Assert.Equal(BlockId.Air, world.GetBlock(6, 11, 5));
    }

    [Fact]
    public void A_Save_Keeps_The_Mode_And_The_Inventory_And_One_That_Names_No_Mode_Opens_In_Creative()
    {
        var save = new WorldSave(_folder);
        var inventory = new Inventory();
        inventory.Slots[2] = new ItemStack(Of(BlockId.OakLog), 7);
        save.WriteInfo(new WorldInfo("overworld", 1, 0, 0, 0, 0, 0, false, 10, [.. Blocks.All.Select(b => b.Key)], "survival", inventory.Save(), 2));

        var read = save.ReadInfo()!;
        Assert.Equal("survival", read.Mode);
        Assert.Equal(2, read.Selected);
        var back = new Inventory();
        back.Load(read.Inventory!);
        Assert.Equal(inventory.Slots, back.Slots);

        File.WriteAllText(Path.Combine(_folder, "world.json"), """
            { "Kind": "flat", "Seed": 2, "X": 0, "Y": 4, "Z": 0, "Heading": 0, "Pitch": 0, "Flying": false, "Hour": 10, "Blocks": ["air"] }
            """);
        var old = save.ReadInfo()!;
        Assert.Equal("creative", old.Mode);
        Assert.Null(old.Inventory);
    }
}
