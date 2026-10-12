using System.Numerics;
using ImGuiNET;

namespace Engine.Game;

/// <summary>
/// The window E opens: in survival the inventory's 36 slots under a crafting grid of two by two, in
/// creative every item to put in the hotbar's chosen slot, and at a crafting table the inventory
/// under a grid of three by three in either mode.
/// </summary>
/// <remarks>
/// The slots are laid out as Minecraft's are, three rows of nine and the hotbar's below them. A left
/// click takes up a slot's stack, puts the stack held down, joins it to a stack of its item or swaps
/// it with another, and a right click takes up half a stack or puts down one of the held stack, as
/// Minecraft's clicks do, in the inventory and in the grid alike. A click on what the grid makes
/// takes it up, using one item of each of the grid's stacks, onto the held stack where it is of the
/// same item and has room. What is held and what the grid holds go back into the inventory when the
/// window closes.
/// </remarks>
public sealed class InventoryWindow
{
    private const float Slot = 40;

    /// <summary>The inventory's own crafting grid, two by two.</summary>
    public CraftingGrid Small { get; } = new(2);

    /// <summary>A crafting table's grid, three by three.</summary>
    public CraftingGrid Table { get; } = new(3);

    /// <summary>The stack taken up and following the mouse, or null.</summary>
    public ItemStack? Held { get; private set; }

    /// <summary>Draws the inventory's slots under a crafting grid, the chosen hotbar slot outlined.</summary>
    /// <returns>Whether the window stays open.</returns>
    public bool DrawInventory(Inventory inventory, int selected, CraftingGrid grid)
    {
        Center();
        var open = true;
        if (ImGui.Begin(grid.Size == 3 ? "Crafting Table" : "Inventory", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            DrawGrid(grid);
            ImGui.Separator();
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < Hotbar.Size; column++)
                {
                    if (column > 0) ImGui.SameLine();
                    SlotButton(inventory.Slots, Hotbar.Size + row * Hotbar.Size + column, false);
                }
            ImGui.Separator();
            for (int column = 0; column < Hotbar.Size; column++)
            {
                if (column > 0) ImGui.SameLine();
                SlotButton(inventory.Slots, column, column == selected);
            }
            if (Held is { } held) DrawStack(ImGui.GetForegroundDrawList(), ImGui.GetIO().MousePos - new Vector2(Slot / 2), held);
        }
        ImGui.End();
        return open;
    }

    // The grid's slots, an arrow, and the slot of what they make.
    private void DrawGrid(CraftingGrid grid)
    {
        ImGui.BeginGroup();
        for (int row = 0; row < grid.Size; row++)
            for (int column = 0; column < grid.Size; column++)
            {
                if (column > 0) ImGui.SameLine();
                ImGui.PushID(1000 + grid.Size * 100);
                SlotButton(grid.Slots, row * grid.Size + column, false);
                ImGui.PopID();
            }
        ImGui.EndGroup();
        ImGui.SameLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (grid.Size * (Slot + ImGui.GetStyle().ItemSpacing.Y) - Slot) / 2);
        ImGui.TextUnformatted("  ->  ");
        ImGui.SameLine();
        var made = grid.Recipe is { } recipe ? new ItemStack(recipe.Result, recipe.Count) : (ItemStack?)null;
        ImGui.PushID("made");
        ImGui.ColorButton("##made", made is { } m ? Swatch(m.Item) : Empty, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop, new Vector2(Slot));
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        if (made is { } shown)
        {
            DrawStack(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), shown);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(shown.Item.Name);
        }
        ImGui.PopID();
        if (!clicked || made is not { } result) return;
        if (Held is { } held && (held.Item != result.Item || held.Count + result.Count > held.Item.MostInStack)) return;
        grid.Craft();
        Held = new ItemStack(result.Item, (Held?.Count ?? 0) + result.Count);
    }

    /// <summary>Draws every item as a swatch, which puts one of it into the hotbar's chosen slot when clicked.</summary>
    /// <returns>Whether the window stays open.</returns>
    public bool DrawCreative(Hotbar hotbar)
    {
        Center();
        var open = true;
        if (ImGui.Begin("Items", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var column = 0;
            foreach (var item in Items.All)
            {
                if (ImGui.ColorButton(item.Key, Swatch(item), ImGuiColorEditFlags.NoTooltip, new Vector2(Slot)))
                    hotbar.Inventory.Slots[hotbar.Selected] = new ItemStack(item, 1);
                Icon(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), item);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(item.Name);
                if (++column % 10 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
            ImGui.TextUnformatted($"Chosen slot: {hotbar.Selected + 1}, {hotbar.Current?.Item.Name ?? "empty"}");
        }
        ImGui.End();
        return open;
    }

    /// <summary>Puts the stack held and the grids' stacks back into the inventory as the window closes.</summary>
    /// <returns>What did not fit, which the caller drops.</returns>
    public List<ItemStack> Close(Inventory inventory)
    {
        var back = Small.Empty();
        back.AddRange(Table.Empty());
        if (Held is { } held) back.Add(held);
        Held = null;
        var left = new List<ItemStack>();
        foreach (var stack in back)
            if (inventory.Add(stack.Item, stack.Count, stack.Wear) is > 0 and var over) left.Add(stack with { Count = over });
        return left;
    }

    private static readonly Vector4 Empty = new(0.16f, 0.16f, 0.18f, 1);

    private static void Center() =>
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

    private void SlotButton(ItemStack?[] slots, int slot, bool chosen)
    {
        ImGui.PushID(slot);
        var stack = slots[slot];
        ImGui.ColorButton("##slot", stack is { } s ? Swatch(s.Item) : Empty, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop, new Vector2(Slot));
        var left = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var list = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        if (stack is { } shown)
        {
            DrawStack(list, min, shown);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(shown.Item.Durability > 0 ? $"{shown.Item.Name}, {shown.Item.Durability - shown.Wear} uses left" : shown.Item.Name);
        }
        if (chosen) list.AddRect(min - Vector2.One, ImGui.GetItemRectMax() + Vector2.One, 0xFFFFFFFF, 0, ImDrawFlags.None, 2);
        if (left) Click(slots, slot);
        else if (right) RightClick(slots, slot);
        ImGui.PopID();
    }

    // Takes up a stack, puts the held one down, joins it to a stack of its item, or swaps the two.
    private void Click(ItemStack?[] slots, int slot)
    {
        var there = slots[slot];
        if (Held is { } held && there is { } stack && stack.Item == held.Item && stack.Count < stack.Item.MostInStack)
        {
            var moved = Math.Min(held.Count, held.Item.MostInStack - stack.Count);
            slots[slot] = stack with { Count = stack.Count + moved };
            Held = held.Count > moved ? held with { Count = held.Count - moved } : null;
            return;
        }
        (Held, slots[slot]) = (there, Held);
    }

    // Takes up half a stack, the larger half, or puts down one of the held stack.
    private void RightClick(ItemStack?[] slots, int slot)
    {
        var there = slots[slot];
        if (Held is not { } held)
        {
            if (there is not { } stack) return;
            var half = (stack.Count + 1) / 2;
            Held = stack with { Count = half };
            slots[slot] = stack.Count > half ? stack with { Count = stack.Count - half } : null;
            return;
        }
        if (there is { } other && (other.Item != held.Item || other.Count >= other.Item.MostInStack)) return;
        slots[slot] = held with { Count = (there?.Count ?? 0) + 1 };
        Held = held.Count > 1 ? held with { Count = held.Count - 1 } : null;
    }

    // A stack drawn over its slot: a tool's or a stick's icon, a worn tool's bar of uses left, and
    // the count where it is more than one.
    private static void DrawStack(ImDrawListPtr list, Vector2 min, ItemStack stack)
    {
        if (ItemIcon.Of(stack.Item).Length == 0) list.AddRectFilled(min, min + new Vector2(Slot), ImGui.GetColorU32(Swatch(stack.Item)));
        else
        {
            list.AddRectFilled(min, min + new Vector2(Slot), ImGui.GetColorU32(Empty));
            Icon(list, min, stack.Item);
        }
        if (stack.Wear > 0 && stack.Item.Durability > 0)
        {
            var left = 1 - (float)stack.Wear / stack.Item.Durability;
            list.AddRectFilled(min + new Vector2(4, Slot - 6), min + new Vector2(Slot - 4, Slot - 3), 0xFF000000);
            list.AddRectFilled(min + new Vector2(4, Slot - 6), min + new Vector2(4 + (Slot - 8) * left, Slot - 3), ImGui.GetColorU32(new Vector4(1 - left, left, 0.1f, 1)));
        }
        if (stack.Count <= 1) return;
        var text = stack.Count.ToString();
        var at = min + new Vector2(Slot - 4, Slot - 2) - ImGui.CalcTextSize(text);
        list.AddText(at + Vector2.One, 0xC0000000, text);
        list.AddText(at, 0xFFFFFFFF, text);
    }

    private static void Icon(ImDrawListPtr list, Vector2 min, Item item)
    {
        foreach (var stroke in ItemIcon.Of(item))
            list.AddLine(min + stroke.From * Slot, min + stroke.To * Slot, ImGui.GetColorU32(ToVector(stroke.Color)), stroke.Width * Slot);
    }

    // A slot's color, the dark of an empty slot under a tool's or a stick's icon.
    private static Vector4 Swatch(Item item) => ItemIcon.Of(item).Length > 0 ? Empty : ToVector(item.Color);

    private static Vector4 ToVector(Color color) => new Vector4(color.R, color.G, color.B, 255) / 255;
}
