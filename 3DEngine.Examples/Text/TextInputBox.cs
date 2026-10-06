// raylib's text_input_box example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextInputBox
{
    private const int MAX_INPUT_CHARS = 9;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] input box");

        string name = "";
        int letterCount = 0;

        Rectangle textBox = new(screenWidth/2.0f - 100, 180, 225, 50);
        bool mouseOnText = false;

        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (CheckCollisionPointRec(GetMousePosition(), textBox)) mouseOnText = true;
            else mouseOnText = false;

            if (mouseOnText)
            {
                SetMouseCursor(MouseCursor.IBeam);

                // Every character typed this frame, in order, those from space to '}' kept
                int key = GetCharPressed();
                while (key > 0)
                {
                    if ((key >= 32) && (key <= 125) && (letterCount < MAX_INPUT_CHARS))
                    {
                        name += (char)key;
                        letterCount++;
                    }

                    key = GetCharPressed();
                }

                if (IsKeyPressed(Key.Backspace))
                {
                    letterCount--;
                    if (letterCount < 0) letterCount = 0;
                    name = name[..letterCount];
                }
            }
            else SetMouseCursor(MouseCursor.Default);

            if (mouseOnText) framesCounter++;
            else framesCounter = 0;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("PLACE MOUSE OVER INPUT BOX!", 240, 140, 20, Color.Gray);

                DrawRectangleRec(textBox, Color.LightGray);
                if (mouseOnText) DrawRectangleLines((int)textBox.X, (int)textBox.Y, (int)textBox.Width, (int)textBox.Height, Color.Red);
                else DrawRectangleLines((int)textBox.X, (int)textBox.Y, (int)textBox.Width, (int)textBox.Height, Color.DarkGray);

                DrawText(name, (int)textBox.X + 5, (int)textBox.Y + 8, 40, Color.Maroon);

                DrawText($"INPUT CHARS: {letterCount}/{MAX_INPUT_CHARS}", 315, 250, 20, Color.DarkGray);

                if (mouseOnText)
                {
                    if (letterCount < MAX_INPUT_CHARS)
                    {
                        // A blinking underscore
                        if (((framesCounter/20)%2) == 0) DrawText("_", (int)textBox.X + 8 + MeasureText(name, 40), (int)textBox.Y + 12, 40, Color.Maroon);
                    }
                    else DrawText("Press BACKSPACE to delete chars...", 230, 300, 20, Color.Gray);
                }

            EndDrawing();
        }

        CloseWindow();
    }
}
