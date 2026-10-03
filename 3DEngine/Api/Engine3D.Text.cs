using System.Numerics;
using ImGuiNET;

namespace Engine;

public static partial class Engine3D
{
    // -- Text. Drawn with ImGui's font into ImGui's foreground layer, so text is always above
    // shapes and windows and needs a frame begun by BeginDrawing.

    /// <summary>Draws <paramref name="text"/> with its top left corner at (<paramref name="x"/>, <paramref name="y"/>), <paramref name="fontSize"/> pixels high.</summary>
    public static void DrawText(string text, int x, int y, int fontSize, Color color) =>
        ImGui.GetForegroundDrawList().AddText(ImGui.GetFont(), fontSize, new Vector2(x, y), color.ToImGui(), text);

    /// <summary>The width in pixels <see cref="DrawText"/> would draw <paramref name="text"/> at.</summary>
    public static int MeasureText(string text, int fontSize) =>
        (int)MathF.Ceiling(ImGui.CalcTextSize(text).X * fontSize / ImGui.GetFontSize());

    /// <summary>Draws the frame rate at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void DrawFPS(int x, int y) => DrawText($"{GetFPS()} FPS", x, y, 20, Color.Lime);
}
