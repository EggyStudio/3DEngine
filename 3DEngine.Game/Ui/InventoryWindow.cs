using System.Numerics;
using ImGuiNET;

namespace Engine.Game;

/// <summary>
/// The window E opens: in survival the inventory's 36 slots, in creative every item to put in the
/// hotbar's chosen slot.
/// </summary>
/// <remarks>
/// The survival window lays the slots out as Minecraft's does, three rows of nine and the hotbar's
/// below them. A left click takes up a slot's stack, puts the stack held down, joins it to a stack
/// of its item or swaps it with another, and a right click takes up half a stack or puts down one
/// of the held stack, as Minecraft's clicks do. What is held when the window closes goes back into
/// the inventory.
/// </remarks>
public sealed class InventoryWindow
{
    private const float Slot = 40;

    /// <summary>The stack taken up and following the mouse, or null.</summary>
    public ItemStack? Held { get; private set; }

    /// <summary>Draws the inventory's slots, the chosen hotbar slot outlined.</summary>
    /// <returns>Whether the window stays open.</returns>
    public bool DrawSurvival(Inventory inventory, int selected)
    {
        Center();
        var open = true;
        if (ImGui.Begin("Inventory", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < Hotbar.Size; column++)
                {
                    if (column > 0) ImGui.SameLine();
                    SlotButton(inventory, Hotbar.Size + row * Hotbar.Size + column, false);
                }
            }
            ImGui.Separator();
            for (int column = 0; column < Hotbar.Size; column++)
            {
                if (column > 0) ImGui.SameLine();
                SlotButton(inventory, column, column == selected);
            }
            if (Held is { } held)
            {
                var at = ImGui.GetIO().MousePos - new Vector2(Slot / 2);
                var list = ImGui.GetForegroundDrawList();
                list.AddRectFilled(at, at + new Vector2(Slot), ImGui.GetColorU32(ToVector(held.Item.Color)));
                Count(list, at, held.Count);
            }
        }
        ImGui.End();
        return open;
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
                if (ImGui.ColorButton(item.Key, ToVector(item.Color), ImGuiColorEditFlags.NoTooltip, new Vector2(Slot)))
                    hotbar.Inventory.Slots[hotbar.Selected] = new ItemStack(item, 1);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(item.Name);
                if (++column % 8 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
            ImGui.TextUnformatted($"Chosen slot: {hotbar.Selected + 1}, {hotbar.Current?.Item.Name ?? "empty"}");
        }
        ImGui.End();
        return open;
    }

    /// <summary>Puts the stack held back into the inventory as the window closes.</summary>
    /// <returns>What did not fit, which the caller drops, or null.</returns>
    public ItemStack? Close(Inventory inventory)
    {
        if (Held is not { } held) return null;
        Held = null;
        var left = inventory.Add(held.Item, held.Count);
        return left > 0 ? held with { Count = left } : null;
    }

    private static void Center() =>
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

    private void SlotButton(Inventory inventory, int slot, bool chosen)
    {
        ImGui.PushID(slot);
        var stack = inventory.Slots[slot];
        var color = stack is { } s ? ToVector(s.Item.Color) : new Vector4(0.16f, 0.16f, 0.18f, 1);
        ImGui.ColorButton("##slot", color, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoDragDrop, new Vector2(Slot));
        var left = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var right = ImGui.IsItemClicked(ImGuiMouseButton.Right);
        var list = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        if (stack is { } shown)
        {
            Count(list, min, shown.Count);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(shown.Item.Name);
        }
        if (chosen) list.AddRect(min - Vector2.One, ImGui.GetItemRectMax() + Vector2.One, 0xFFFFFFFF, 0, ImDrawFlags.None, 2);
        if (left) Click(inventory, slot);
        else if (right) RightClick(inventory, slot);
        ImGui.PopID();
    }

    // Takes up a stack, puts the held one down, joins it to a stack of its item, or swaps the two.
    private void Click(Inventory inventory, int slot)
    {
        var there = inventory.Slots[slot];
        if (Held is { } held && there is { } stack && stack.Item == held.Item)
        {
            var moved = Math.Min(held.Count, held.Item.MostInStack - stack.Count);
            inventory.Slots[slot] = stack with { Count = stack.Count + moved };
            Held = held.Count > moved ? held with { Count = held.Count - moved } : null;
            return;
        }
        (Held, inventory.Slots[slot]) = (there, Held);
    }

    // Takes up half a stack, the larger half, or puts down one of the held stack.
    private void RightClick(Inventory inventory, int slot)
    {
        var there = inventory.Slots[slot];
        if (Held is not { } held)
        {
            if (there is not { } stack) return;
            var half = (stack.Count + 1) / 2;
            Held = stack with { Count = half };
            inventory.Slots[slot] = stack.Count > half ? stack with { Count = stack.Count - half } : null;
            return;
        }
        if (there is { } other && (other.Item != held.Item || other.Count >= other.Item.MostInStack)) return;
        inventory.Slots[slot] = new ItemStack(held.Item, (there?.Count ?? 0) + 1);
        Held = held.Count > 1 ? held with { Count = held.Count - 1 } : null;
    }

    // A stack's count at the bottom right of its slot, where it holds more than one.
    private static void Count(ImDrawListPtr list, Vector2 min, int count)
    {
        if (count <= 1) return;
        var text = count.ToString();
        var at = min + new Vector2(Slot - 4, Slot - 2) - ImGui.CalcTextSize(text);
        list.AddText(at + Vector2.One, 0xC0000000, text);
        list.AddText(at, 0xFFFFFFFF, text);
    }

    private static Vector4 ToVector(Color color) => new Vector4(color.R, color.G, color.B, 255) / 255;
}
