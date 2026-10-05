using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- Text in the default font, drawn in the draw list like any shape, so it keeps its place
    // among shapes and reaches render targets.

    /// <summary>Draws <paramref name="text"/> with its top left corner at (<paramref name="x"/>, <paramref name="y"/>), <paramref name="fontSize"/> pixels high, in the default font.</summary>
    /// <remarks>A size below 10 draws at 10, as raylib's does.</remarks>
    public static void DrawText(string text, int x, int y, int fontSize, Color color)
    {
        fontSize = DefaultTextSize(fontSize);
        DrawTextEx(GetFontDefault(fontSize), text, new Vector2(x, y), fontSize, 0, color);
    }

    /// <summary>The width in pixels <see cref="DrawText"/> would draw <paramref name="text"/> at.</summary>
    /// <remarks>A size below 10 is measured at 10, as <see cref="DrawText"/> draws it.</remarks>
    public static int MeasureText(string text, int fontSize)
    {
        fontSize = DefaultTextSize(fontSize);
        return (int)MathF.Ceiling(MeasureTextEx(GetFontDefault(fontSize), text, fontSize, 0).X);
    }

    // raylib's default font is 10 pixels high, and its DrawText, MeasureText and ImageText raise a
    // smaller size to 10. A raylib program asks for 6 and is read at 10, so these do the same
    // rather than bake the font at a size nobody can read.
    private static int DefaultTextSize(int fontSize) => Math.Max(fontSize, 10);

    /// <summary>Draws the frame rate at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <remarks>In lime, orange below 30 frames a second and red below 15, as raylib's does.</remarks>
    public static void DrawFPS(int x, int y)
    {
        int fps = GetFPS();
        DrawText($"{fps,2} FPS", x, y, 20, FpsColor(fps));
    }

    internal static Color FpsColor(int fps) => fps switch
    {
        < 15 => Color.Red,
        < 30 => Color.Orange,
        _ => Color.Lime,
    };
}
