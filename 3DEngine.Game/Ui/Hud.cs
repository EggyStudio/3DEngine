using System.Numerics;
using static Engine.Engine3D;

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
    public static void DrawOutline(BlockHit hit, BlockId block)
    {
        var (min, max) = Blocks.Get(block).Shape is { } shape ? (shape.Min, shape.Max) : (Vector3.Zero, Vector3.One);
        var size = max - min + new Vector3(0.004f);
        DrawCubeWires(new Vector3(hit.X, hit.Y, hit.Z) + (min + max) / 2, size.X, size.Y, size.Z, new Color(0, 0, 0, 220));
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
            var block = Blocks.Get(hotbar.Slots[i]);
            DrawRectangle(x, y, Slot, Slot, new Color(0, 0, 0, 140));
            DrawRectangle(x + 7, y + 7, Slot - 14, Slot - 14, block.Swatch);
            // A block that gives off light is ringed in its light's color.
            if (block.Emits) DrawRectangleLines(x + 5, y + 5, Slot - 10, Slot - 10, Surfaces.All[block.Top].Emissive);
            if (i == hotbar.Selected) DrawRectangleLinesEx(new Rectangle(x - 2, y - 2, Slot + 4, Slot + 4), 3, Color.White);
        }

        var name = Blocks.Get(hotbar.Current).Name;
        var nameX = (GetScreenWidth() - MeasureText(name, 20)) / 2;
        DrawText(name, nameX + 2, y - 30, 20, new Color(0, 0, 0, 160));
        DrawText(name, nameX, y - 32, 20, Color.White);
    }

    /// <summary>A line of help at the top left, while the settings window is closed.</summary>
    public static void DrawHelp(GlobalIllumination light)
    {
        var text = $"F3 settings   E blocks   G light: {light}   F1 hide";
        DrawText(text, 11, 11, 10, new Color(0, 0, 0, 160));
        DrawText(text, 10, 10, 10, Color.White);
    }
}
