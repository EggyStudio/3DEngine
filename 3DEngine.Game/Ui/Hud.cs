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

    public static void DrawHotbar(Hotbar hotbar)
    {
        var width = Hotbar.Size * Slot + (Hotbar.Size - 1) * Gap;
        int x0 = (GetScreenWidth() - width) / 2, y = GetScreenHeight() - Slot - 14;
        for (int i = 0; i < Hotbar.Size; i++)
        {
            var x = x0 + i * (Slot + Gap);
            DrawRectangle(x, y, Slot, Slot, new Color(0, 0, 0, 140));
            if (hotbar.Inventory.Slots[i] is { } stack)
            {
                // Opaque, so glass and ice show their color rather than the slot through it.
                var color = stack.Item.Color;
                DrawRectangle(x + 7, y + 7, Slot - 14, Slot - 14, new Color(color.R, color.G, color.B, 255));
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
        DrawText(name, nameX + 2, y - 30, 20, new Color(0, 0, 0, 160));
        DrawText(name, nameX, y - 32, 20, Color.White);
    }

    /// <summary>A line of help at the top left, while the settings window is closed.</summary>
    public static void DrawHelp(GlobalIllumination light, GameMode mode)
    {
        var text = $"F3 settings   E {(mode == GameMode.Survival ? "inventory" : "items")}   Q throw   G light: {light}   F1 hide   {mode}";
        DrawText(text, 11, 11, 10, new Color(0, 0, 0, 160));
        DrawText(text, 10, 10, 10, Color.White);
    }
}
