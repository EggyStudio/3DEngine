// raylib's text_writing_anim example, Copyright (c) 2016-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextWritingAnim
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] writing anim");

        const string message = "This sample illustrates a text writing\nanimation effect! Check it out! ;)";

        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Space)) framesCounter += 8;
            else framesCounter++;

            if (IsKeyPressed(Key.Return)) framesCounter = 0;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // raylib's TextSubtext is C#'s range, held to the message's length as raylib holds it.
                DrawText(message[..Math.Min(framesCounter/10, message.Length)], 210, 160, 20, Color.Maroon);

                DrawText("PRESS [ENTER] to RESTART!", 240, 260, 20, Color.LightGray);
                DrawText("HOLD [SPACE] to SPEED UP!", 239, 300, 20, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
