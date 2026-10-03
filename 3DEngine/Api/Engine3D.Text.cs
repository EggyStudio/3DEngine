using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- Text in the default font, drawn in the draw list like any shape, so it keeps its place
    // among shapes and reaches render targets.

    /// <summary>Draws <paramref name="text"/> with its top left corner at (<paramref name="x"/>, <paramref name="y"/>), <paramref name="fontSize"/> pixels high, in the default font.</summary>
    public static void DrawText(string text, int x, int y, int fontSize, Color color) =>
        DrawTextEx(GetFontDefault(fontSize), text, new Vector2(x, y), fontSize, 0, color);

    /// <summary>The width in pixels <see cref="DrawText"/> would draw <paramref name="text"/> at.</summary>
    public static int MeasureText(string text, int fontSize) =>
        (int)MathF.Ceiling(MeasureTextEx(GetFontDefault(fontSize), text, fontSize, 0).X);

    /// <summary>Draws the frame rate at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void DrawFPS(int x, int y) => DrawText($"{GetFPS()} FPS", x, y, 20, Color.Lime);
}
