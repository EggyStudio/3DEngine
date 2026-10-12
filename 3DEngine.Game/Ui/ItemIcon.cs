using System.Numerics;

namespace Engine.Game;

/// <summary>
/// The lines a tool or a stick is drawn with over its slot, in a square from 0 to 1 with y down, so
/// a wooden pickaxe is told from planks, which are a square of the same color.
/// </summary>
public static class ItemIcon
{
    /// <summary>A stroke of an icon: from one point to another, its width as a share of the slot, and its color.</summary>
    public readonly record struct Stroke(Vector2 From, Vector2 To, float Width, Color Color);

    private static readonly Color Handle = new(104, 76, 44);

    /// <summary>The strokes an item is drawn with, or none for an item shown as a square of its color.</summary>
    public static Stroke[] Of(Item item)
    {
        static Stroke S(float x0, float y0, float x1, float y1, float width, Color color) => new(new Vector2(x0, y0), new Vector2(x1, y1), width, color);
        var head = item.Color;
        return item.Tool switch
        {
            ToolKind.Pickaxe => [S(0.25f, 0.8f, 0.65f, 0.35f, 0.1f, Handle), S(0.3f, 0.2f, 0.6f, 0.2f, 0.12f, head), S(0.6f, 0.2f, 0.8f, 0.4f, 0.12f, head), S(0.8f, 0.4f, 0.8f, 0.7f, 0.12f, head)],
            ToolKind.Axe => [S(0.25f, 0.8f, 0.7f, 0.3f, 0.1f, Handle), S(0.45f, 0.2f, 0.75f, 0.5f, 0.22f, head)],
            ToolKind.Shovel => [S(0.25f, 0.8f, 0.6f, 0.4f, 0.1f, Handle), S(0.6f, 0.4f, 0.78f, 0.22f, 0.24f, head)],
            ToolKind.Sword => [S(0.4f, 0.6f, 0.82f, 0.18f, 0.12f, head), S(0.25f, 0.5f, 0.5f, 0.75f, 0.08f, Handle), S(0.18f, 0.82f, 0.35f, 0.65f, 0.1f, Handle)],
            _ when item == Items.Stick => [S(0.25f, 0.8f, 0.75f, 0.2f, 0.12f, Handle)],
            _ => [],
        };
    }
}
