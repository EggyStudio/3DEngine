using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextInputBox
{
    public static void Run()
    {
        InitWindow(800, 450, "[text] input box");

        const int MaxLength = 24;
        var name = "";
        var framesCounter = 0;
        var lastKey = Key.Unknown;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Every character typed this frame, in order, then Backspace to remove one.
            for (var c = GetCharPressed(); c != 0; c = GetCharPressed())
                if (c >= 32 && name.Length < MaxLength)
                    name += char.ConvertFromUtf32(c);
            if (IsKeyPressed(Key.Backspace) && name.Length > 0)
                name = name[..^1];
            for (var key = GetKeyPressed(); key != Key.Unknown; key = GetKeyPressed())
                lastKey = key;
            framesCounter++;

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            DrawText("Type a name. Backspace removes a letter.", 240, 140, 20, Color.Gray);
            DrawRectangle(200, 180, 400, 50, Color.LightGray);
            DrawRectangleLines(200, 180, 400, 50, Color.Red);
            DrawText(name, 210, 192, 30, Color.Maroon);

            // A blinking caret after the text, while there is room for more.
            if (name.Length < MaxLength && framesCounter / 20 % 2 == 0)
                DrawText("_", 212 + MeasureText(name, 30), 194, 30, Color.Maroon);

            DrawText($"{name.Length}/{MaxLength} characters, last key {lastKey}", 200, 250, 20, Color.DarkGray);
            EndDrawing();
        }

        CloseWindow();
    }
}
