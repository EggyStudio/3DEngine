using Xunit;

namespace Engine.Game.Tests;

public class CraftingTests
{
    private static Item Of(BlockId block) => Items.Of(block);

    private static Item Tool(string key) => Items.Find(key)!;

    // A grid of a size from rows of letters, each letter an item, a space an empty slot.
    private static CraftingGrid Grid(int size, string[] rows, params (char Key, Item Item)[] keys)
    {
        var grid = new CraftingGrid(size);
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] != ' ') grid.Slots[y * size + x] = new ItemStack(keys.First(k => k.Key == rows[y][x]).Item, 2);
        return grid;
    }

    [Fact]
    public void A_Log_Anywhere_In_The_Small_Grid_Makes_Four_Planks_Of_Its_Wood()
    {
        var grid = Grid(2, ["  ", " L"], ('L', Of(BlockId.SpruceLog)));

        var made = grid.Craft();

        Assert.Equal(new ItemStack(Of(BlockId.SprucePlanks), 4), made);
        Assert.Equal(1, grid.Slots[3]!.Value.Count);
    }

    [Fact]
    public void Two_Planks_Stacked_Make_Sticks_And_Four_Make_A_Crafting_Table()
    {
        Assert.Equal(Items.Stick, Grid(2, ["P ", "P "], ('P', Of(BlockId.BirchPlanks))).Recipe?.Result);
        Assert.Equal(Of(BlockId.CraftingTable), Grid(2, ["PP", "PB"], ('P', Of(BlockId.OakPlanks)), ('B', Of(BlockId.BirchPlanks))).Recipe?.Result);
        Assert.Null(Grid(2, ["PP", "P "], ('P', Of(BlockId.OakPlanks))).Recipe);
    }

    [Fact]
    public void A_Pickaxe_Needs_The_Table_And_An_Axe_Faces_Either_Way()
    {
        Assert.Equal(Tool("stone_pickaxe"), Grid(3, ["CCC", " S ", " S "], ('C', Of(BlockId.Cobblestone)), ('S', Items.Stick)).Recipe?.Result);
        Assert.Equal(Tool("wooden_axe"), Grid(3, ["PP ", "PS ", " S "], ('P', Of(BlockId.OakPlanks)), ('S', Items.Stick)).Recipe?.Result);
        Assert.Equal(Tool("wooden_axe"), Grid(3, [" PP", " SP", " S "], ('P', Of(BlockId.OakPlanks)), ('S', Items.Stick)).Recipe?.Result);
        Assert.Null(Grid(3, ["CCC", " S ", " C "], ('C', Of(BlockId.Cobblestone)), ('S', Items.Stick)).Recipe);
    }

    [Fact]
    public void The_Inventory_Crafts_As_A_Recipe_Book_Taking_Out_What_It_Uses()
    {
        var inventory = new Inventory();
        inventory.Add(Of(BlockId.OakPlanks), 1);
        inventory.Add(Of(BlockId.SprucePlanks), 2);
        inventory.Add(Items.Stick, 3);

        Assert.Equal(Tool("wooden_pickaxe"), Recipes.CraftFrom(inventory, Tool("wooden_pickaxe"))?.Result);
        Assert.Equal(0, inventory.Count(Of(BlockId.OakPlanks)) + inventory.Count(Of(BlockId.SprucePlanks)));
        Assert.Equal(1, inventory.Count(Items.Stick));
        Assert.Null(Recipes.CraftFrom(inventory, Tool("wooden_sword")));
    }

    [Fact]
    public void Stone_Asks_For_A_Pickaxe_To_Drop_Its_Cobblestone_And_Breaks_Faster_With_A_Better_One()
    {
        var random = new Random(1);
        Assert.Empty(Loot.Of(BlockId.Stone, random));
        Assert.Empty(Loot.Of(BlockId.CoalOre, random, Tool("wooden_axe")));
        Assert.Equal((Of(BlockId.Cobblestone), 1), Assert.Single(Loot.Of(BlockId.Stone, random, Tool("wooden_pickaxe"))));
        Assert.Equal((Items.Coal, 1), Assert.Single(Loot.Of(BlockId.CoalOre, random, Tool("stone_pickaxe"))));

        Assert.Equal(7.5f, Loot.BreakSeconds(BlockId.Stone), 3);
        Assert.Equal(1.125f, Loot.BreakSeconds(BlockId.Stone, Tool("wooden_pickaxe")), 3);
        Assert.Equal(0.5625f, Loot.BreakSeconds(BlockId.Stone, Tool("stone_pickaxe")), 3);
        // A log needs no tool, and an axe of the wrong kind for stone breaks it no faster than a hand.
        Assert.Equal(3f, Loot.BreakSeconds(BlockId.OakLog), 3);
        Assert.Equal(1.5f, Loot.BreakSeconds(BlockId.OakLog, Tool("wooden_axe")), 3);
        Assert.Equal(7.5f, Loot.BreakSeconds(BlockId.Stone, Tool("stone_axe")), 3);
    }

    [Fact]
    public void A_Worn_Tool_Keeps_Its_Wear_Through_A_Save()
    {
        var inventory = new Inventory();
        inventory.Slots[4] = new ItemStack(Tool("stone_sword"), 1, 40);

        var back = new Inventory();
        back.Load(inventory.Save());

        Assert.Equal(new ItemStack(Tool("stone_sword"), 1, 40), back.Slots[4]);
        Assert.Equal(1, Tool("stone_sword").MostInStack);
        Assert.Equal(6, Tool("stone_sword").Damage);
    }

    [Fact]
    public void The_Stone_Holds_Veins_Of_Coal()
    {
        var land = new Overworld(1);
        var coal = 0;
        for (int cz = -2; cz <= 2; cz++)
            for (int cx = -2; cx <= 2; cx++)
            {
                var column = land.Generate(cx, cz);
                for (int y = 0; y < ChunkColumn.Height; y++)
                    for (int z = 0; z < Section.Size; z++)
                        for (int x = 0; x < Section.Size; x++)
                        {
                            if (column.Get(x, y, z) == BlockId.CoalOre) coal++;
                        }
            }
        Assert.True(coal > 100, $"{coal} coal ore in 25 columns");
    }
}
