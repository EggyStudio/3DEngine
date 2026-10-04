using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextFontSdf
{
    public static void Run()
    {
        InitWindow(800, 450, "[text] font sdf");

        // The same font baked at 32 pixels twice, once as coverage and once as a distance field.
        const string Message = "Signed Distance Fields";
        var plain = LoadFontEx("resources/fonts/Lato-Regular.ttf", 32);
        var sdf = LoadFontEx("resources/fonts/Lato-Regular.ttf", 32, null, FontType.Sdf);
        var fontSize = 96f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            fontSize = Math.Clamp(fontSize + GetMouseWheelMove() * 8, 8, 240);
            var useSdf = !IsKeyDown(Key.Space);
            var font = useSdf ? sdf : plain;
            var size = MeasureTextEx(font, Message, fontSize, 0);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            DrawTextEx(font, Message, new Vector2((GetScreenWidth() - size.X) / 2, (GetScreenHeight() - size.Y) / 2 - 40), fontSize, 0, Color.Black);
            DrawTextEx(sdf, "Both fonts are baked at 32 pixels", new Vector2(20, 330), 20, 0, Color.DarkGray);
            DrawTextEx(useSdf ? sdf : plain, useSdf ? "Distance field, drawn sharp" : "Coverage, scaled and blurred", new Vector2(20, 360), 32, 0,
                useSdf ? Color.DarkGreen : Color.Maroon);
            DrawText($"Size {fontSize:0}, mouse wheel to change, hold space for the coverage font", 20, 410, 20, Color.Gray);

            EndDrawing();
        }

        UnloadFont(plain);
        UnloadFont(sdf);
        CloseWindow();
    }
}
