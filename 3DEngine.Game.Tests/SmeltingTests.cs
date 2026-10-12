using Xunit;

namespace Engine.Game.Tests;

public sealed class SmeltingTests : IDisposable
{
    // A folder of this test's own, removed with it, so no run reads another's save.
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "3DEngine.Game.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Item Of(BlockId block) => Items.Of(block);

    private static void Run(Furnace furnace, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.05f) furnace.Update(0.05f);
    }

    [Fact]
    public void Coal_Smelts_Three_Sand_Into_Glass_Ten_Seconds_Each_And_Burns_On_For_Its_Eighty()
    {
        var furnace = new Furnace();
        furnace.Slots[Furnace.Input] = new ItemStack(Of(BlockId.Sand), 3);
        furnace.Slots[Furnace.Fuel] = new ItemStack(Items.Coal, 2);

        Run(furnace, 31);

        Assert.Null(furnace.Slots[Furnace.Input]);
        Assert.Equal(new ItemStack(Of(BlockId.Glass), 3), furnace.Slots[Furnace.Output]);
        Assert.Equal(new ItemStack(Items.Coal, 1), furnace.Slots[Furnace.Fuel]);
        Assert.True(furnace.Lit);
        Assert.InRange(furnace.Burning, 48, 50);
    }

    [Fact]
    public void A_Furnace_Burns_No_Fuel_On_Nothing_And_Starts_Over_When_Its_Fire_Goes_Out()
    {
        var idle = new Furnace();
        idle.Slots[Furnace.Fuel] = new ItemStack(Items.Coal, 1);
        Run(idle, 5);
        Assert.False(idle.Lit);
        Assert.Equal(1, idle.Slots[Furnace.Fuel]!.Value.Count);

        var short_ = new Furnace();
        short_.Slots[Furnace.Input] = new ItemStack(Of(BlockId.IronOre), 1);
        short_.Slots[Furnace.Fuel] = new ItemStack(Items.Stick, 1);
        Run(short_, 6);
        Assert.False(short_.Lit);
        Assert.Equal(0, short_.Cooked);
        Assert.Null(short_.Slots[Furnace.Output]);
    }

    [Fact]
    public void A_Furnace_Block_Is_Lit_While_It_Burns_And_Gives_Up_What_It_Holds_When_Broken()
    {
        var world = Laid.World(c => Laid.Fill(c, 0, 10, BlockId.Stone));
        world.SetBlock(5, 11, 5, BlockId.Furnace);
        var furnaces = new Furnaces();
        var furnace = furnaces.At(5, 11, 5);
        furnace.Slots[Furnace.Input] = new ItemStack(Items.RawBeef, 1);
        furnace.Slots[Furnace.Fuel] = new ItemStack(Of(BlockId.OakPlanks), 1);

        furnaces.Update(world, 0.05f);
        Assert.Equal(BlockId.FurnaceLit, world.GetBlock(5, 11, 5));
        Assert.Equal(12, world.GetLight(5, 12, 5).Block);

        for (float t = 0; t < 16; t += 0.05f) furnaces.Update(world, 0.05f);
        Assert.Equal(BlockId.Furnace, world.GetBlock(5, 11, 5));
        Assert.Equal(Items.Find("steak"), furnace.Slots[Furnace.Output]!.Value.Item);

        world.SetBlock(5, 11, 5, BlockId.Air);
        var spilled = Assert.Single(furnaces.Update(world, 0.05f));
        Assert.Equal(((5, 11, 5), new ItemStack(Items.Find("steak")!, 1)), spilled);
        Assert.Empty(furnaces.All);
    }

    [Fact]
    public void Iron_Ore_Asks_A_Stone_Pickaxe_And_A_Lit_Furnace_Drops_A_Furnace()
    {
        var random = new Random(1);
        Assert.Empty(Loot.Of(BlockId.IronOre, random, Items.Find("wooden_pickaxe")));
        Assert.Equal((Of(BlockId.IronOre), 1), Assert.Single(Loot.Of(BlockId.IronOre, random, Items.Find("stone_pickaxe"))));
        Assert.Equal((Of(BlockId.Furnace), 1), Assert.Single(Loot.Of(BlockId.FurnaceLit, random, Items.Find("wooden_pickaxe"))));
        Assert.Equal(Items.IronIngot, Smelting.Result(Of(BlockId.IronOre)));
        Assert.Equal(15, Smelting.Burns(Of(BlockId.SpruceLog)));
        Assert.Equal(0, Smelting.Burns(Of(BlockId.Stone)));
    }

    [Fact]
    public void Eight_Cobblestone_In_A_Ring_Make_A_Furnace_And_Ingots_Iron_Tools()
    {
        var inventory = new Inventory();
        inventory.Add(Of(BlockId.Cobblestone), 8);
        inventory.Add(Items.IronIngot, 3);
        inventory.Add(Items.Stick, 2);

        Assert.Equal(Of(BlockId.Furnace), Recipes.CraftFrom(inventory, Of(BlockId.Furnace))?.Result);
        var pickaxe = Recipes.CraftFrom(inventory, Items.Find("iron_pickaxe")!)!.Result;
        Assert.Equal((6f, 250, 3, 5), (pickaxe.Speed, pickaxe.Durability, pickaxe.Tier, pickaxe.Damage));
        Assert.Equal(0, inventory.Count(Of(BlockId.Cobblestone)) + inventory.Count(Items.IronIngot));
    }

    [Fact]
    public void A_Save_Keeps_The_Furnaces_With_Their_Slots_And_Fire()
    {
        var furnaces = new Furnaces();
        var furnace = furnaces.At(-3, 40, 12);
        furnace.Slots[Furnace.Input] = new ItemStack(Of(BlockId.Cobblestone), 7);
        furnace.Slots[Furnace.Output] = new ItemStack(Of(BlockId.Stone), 2);
        (furnace.Burning, furnace.BurnTotal, furnace.Cooked) = (40, 80, 4.5f);
        var save = new WorldSave(_folder);

        save.WriteFurnaces(furnaces);
        var back = Assert.Single(new WorldSave(_folder).ReadFurnaces().All);

        Assert.Equal((-3, 40, 12), back.Key);
        Assert.Equal(furnace.Slots, back.Value.Slots);
        Assert.Equal((40f, 80f, 4.5f), (back.Value.Burning, back.Value.BurnTotal, back.Value.Cooked));
    }

    [Fact]
    public void The_Stone_Holds_Iron_Low_Down()
    {
        var land = new Overworld(1);
        int iron = 0, highest = 0;
        for (int cz = -2; cz <= 2; cz++)
            for (int cx = -2; cx <= 2; cx++)
            {
                var column = land.Generate(cx, cz);
                for (int y = 0; y < ChunkColumn.Height; y++)
                    for (int z = 0; z < Section.Size; z++)
                        for (int x = 0; x < Section.Size; x++)
                            if (column.Get(x, y, z) == BlockId.IronOre) (iron, highest) = (iron + 1, Math.Max(highest, y));
            }
        Assert.True(iron > 50, $"{iron} iron ore in 25 columns");
        Assert.InRange(highest, 30, 70);
    }
}
