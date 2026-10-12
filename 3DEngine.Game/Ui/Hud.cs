using System.Numerics;
using static Engine.E3D;

namespace Engine.Game;

/// <summary>What is drawn over the world: the crosshair, the hotbar and the outline of the block looked at.</summary>
public static class Hud
{
    private const int Slot = 44;
    private const int Gap = 4;

    /// <summary>
    /// Outlines a block a hair larger than it, so its edges are not lost in its faces' depth, a block
    /// smaller than its cell by the box around its pieces. Called inside <c>BeginMode3D</c>.
    /// </summary>
    public static void DrawOutline(BlockHit hit, BlockShape? shape)
    {
        var (min, max) = shape is not null ? (shape.Min, shape.Max) : (Vector3.Zero, Vector3.One);
        var size = max - min + new Vector3(0.004f);
        DrawCubeWires(new Vector3(hit.X, hit.Y, hit.Z) + (min + max) / 2, size.X, size.Y, size.Z, new Color(0, 0, 0, 220));
    }

    /// <summary>
    /// Darkens a block being broken in survival over its outline, more the further it is broken, as
    /// Minecraft's cracks spread. Called inside <c>BeginMode3D</c>.
    /// </summary>
    public static void DrawBreaking(BlockHit hit, BlockShape? shape, float progress)
    {
        var (min, max) = shape is not null ? (shape.Min, shape.Max) : (Vector3.Zero, Vector3.One);
        var size = max - min + new Vector3(0.006f);
        DrawCube(new Vector3(hit.X, hit.Y, hit.Z) + (min + max) / 2, size.X, size.Y, size.Z, new Color(20, 16, 12, (byte)(15 + 95 * Math.Clamp(progress, 0, 1))));
    }

    public static void DrawCrosshair()
    {
        int cx = GetScreenWidth() / 2, cy = GetScreenHeight() / 2;
        var shadow = new Color(0, 0, 0, 120);
        DrawRectangle(cx - 10, cy - 2, 20, 4, shadow);
        DrawRectangle(cx - 2, cy - 10, 4, 20, shadow);
        DrawRectangle(cx - 9, cy - 1, 18, 2, Color.White);
        DrawRectangle(cx - 1, cy - 9, 2, 18, Color.White);
    }

    /// <summary>The hotbar's slots, and over them the name of what the chosen one holds, lifted by so many pixels above the hearts and food in survival.</summary>
    public static void DrawHotbar(Hotbar hotbar, int lift = 0)
    {
        var width = Hotbar.Size * Slot + (Hotbar.Size - 1) * Gap;
        int x0 = (GetScreenWidth() - width) / 2, y = GetScreenHeight() - Slot - 14;
        for (int i = 0; i < Hotbar.Size; i++)
        {
            var x = x0 + i * (Slot + Gap);
            DrawRectangle(x, y, Slot, Slot, new Color(0, 0, 0, 140));
            if (hotbar.Inventory.Slots[i] is { } stack)
            {
                // Opaque, so glass and ice show their color rather than the slot through it, and a
                // tool or a stick drawn by its icon over the slot.
                var color = stack.Item.Color;
                var icon = ItemIcon.Of(stack.Item);
                if (icon.Length == 0) DrawRectangle(x + 7, y + 7, Slot - 14, Slot - 14, new Color(color.R, color.G, color.B, 255));
                foreach (var stroke in icon)
                    DrawLineEx(new Vector2(x, y) + stroke.From * Slot, new Vector2(x, y) + stroke.To * Slot, stroke.Width * Slot, stroke.Color);
                if (stack.Wear > 0 && stack.Item.Durability > 0)
                {
                    var left = 1 - (float)stack.Wear / stack.Item.Durability;
                    DrawRectangle(x + 5, y + Slot - 7, Slot - 10, 3, Color.Black);
                    DrawRectangle(x + 5, y + Slot - 7, (int)((Slot - 10) * left), 3, new Color((byte)(255 * (1 - left)), (byte)(255 * left), 30));
                }
                // A block that gives off light is ringed in its light's color.
                if (stack.Item.Block is { } block && Blocks.Get(block).Emits) DrawRectangleLines(x + 5, y + 5, Slot - 10, Slot - 10, Surfaces.All[Blocks.Get(block).Top].Emissive);
                if (stack.Count > 1)
                {
                    var text = stack.Count.ToString();
                    var tx = x + Slot - 4 - MeasureText(text, 20);
                    DrawText(text, tx + 2, y + Slot - 20, 20, new Color(0, 0, 0, 180));
                    DrawText(text, tx, y + Slot - 22, 20, Color.White);
                }
            }
            if (i == hotbar.Selected) DrawRectangleLinesEx(new Rectangle(x - 2, y - 2, Slot + 4, Slot + 4), 3, Color.White);
        }

        if (hotbar.Current is not { } held) return;
        var name = held.Item.Name;
        var nameX = (GetScreenWidth() - MeasureText(name, 20)) / 2;
        DrawText(name, nameX + 2, y - 30 - lift, 20, new Color(0, 0, 0, 160));
        DrawText(name, nameX, y - 32 - lift, 20, Color.White);
    }

    // A heart and a drumstick as pixels seven wide and six high, a row a string.
    private static readonly string[] Heart = [".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..."];
    private static readonly string[] Drumstick = ["...XXX.", "..XXXXX", "..XXXXX", ".XXXXX.", "XX.....", "X......"];

    /// <summary>
    /// Ten hearts of health over the hotbar's left half and ten drumsticks of food over its right,
    /// each worth two points and half drawn for one, as Minecraft's are, and while the eye is under
    /// water, bubbles of breath over the food.
    /// </summary>
    public static void DrawVitals(Vitals vitals)
    {
        var width = Hotbar.Size * Slot + (Hotbar.Size - 1) * Gap;
        int x0 = (GetScreenWidth() - width) / 2, y = GetScreenHeight() - Slot - 14 - 22;
        const int scale = 2, step = 18;
        for (int i = 0; i < 10; i++)
        {
            // A heart worth one point is its left half, and a drumstick its meat's half on the right.
            var health = Math.Clamp(vitals.Health - i * 2, 0, 2);
            Outline(Heart, x0 + i * step, y, scale);
            Pixels(Heart, x0 + i * step, y, scale, new Color(40, 0, 0, 200), 0, 7);
            Pixels(Heart, x0 + i * step, y, scale, new Color(220, 30, 40), 0, health == 2 ? 7 : health == 1 ? 4 : 0);
            // Food from the right, as Minecraft's is drawn.
            var fx = x0 + width - 14 - i * step;
            var food = Math.Clamp(vitals.Food - i * 2, 0, 2);
            Outline(Drumstick, fx, y, scale);
            Pixels(Drumstick, fx, y, scale, new Color(40, 24, 10, 200), 0, 7);
            Pixels(Drumstick, fx, y, scale, new Color(196, 120, 58), food == 2 ? 0 : 3, food == 0 ? 0 : 7);
            if (vitals.Air < Vitals.Breath && i < (int)MathF.Ceiling(vitals.Air / Vitals.Breath * 10))
                DrawCircleLines(fx + 7, y - 12, 6, new Color(170, 220, 255));
        }
    }

    // A dark edge a pixel wide around a pattern, so it reads over any color behind it, as Minecraft's
    // icons are outlined.
    private static void Outline(string[] pattern, int x, int y, int scale)
    {
        var edge = new Color(16, 10, 8, 230);
        foreach (var (dx, dy) in (ReadOnlySpan<(int, int)>)[(-1, 0), (1, 0), (0, -1), (0, 1)])
            Pixels(pattern, x + dx, y + dy, scale, edge, 0, 7);
    }

    // Draws a pattern's set pixels from one column up to another in a color, each at its own place.
    private static void Pixels(string[] pattern, int x, int y, int scale, Color color, int from, int to)
    {
        for (int row = 0; row < pattern.Length; row++)
            for (int column = from; column < Math.Min(pattern[row].Length, to); column++)
                if (pattern[row][column] == 'X') DrawRectangle(x + column * scale, y + row * scale, scale, scale, color);
    }

    /// <summary>A bar under the crosshair, filling as the food held is eaten.</summary>
    public static void DrawEating(float progress)
    {
        int cx = GetScreenWidth() / 2, cy = GetScreenHeight() / 2 + 16;
        DrawRectangle(cx - 20, cy, 40, 4, new Color(0, 0, 0, 140));
        DrawRectangle(cx - 20, cy, (int)(40 * Math.Clamp(progress, 0, 1)), 4, new Color(196, 120, 58));
    }

    /// <summary>A line of help at the top left, while the settings window is closed.</summary>
    public static void DrawHelp(GlobalIllumination light, GameMode mode)
    {
        var text = $"F3 settings   E {(mode == GameMode.Survival ? "inventory" : "items")}   Q throw   G light: {light}   F1 hide   {mode}";
        DrawText(text, 11, 11, 10, new Color(0, 0, 0, 160));
        DrawText(text, 10, 10, 10, Color.White);
    }
}
