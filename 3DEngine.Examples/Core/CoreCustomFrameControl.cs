// raylib's core_custom_frame_control example, Copyright (c) 2021-2025 Ramon Santamaria (@raysan5),
// under the zlib license, written again for the flat API.
//
// raylib's needs a raylib built with SUPPORT_CUSTOM_FRAME_CONTROL, which leaves polling, the
// buffer swap and the wait to the program. Here EndDrawing presents and the frame polls, as
// raylib's own build does, and PollInputEvents and SwapScreenBuffer have nothing left to do, so
// the program's timing alone decides the frame rate, as it is meant to.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreCustomFrameControl
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] custom frame control");

        // Custom timing variables
        double previousTime = GetTime();    // Previous time measure
        double currentTime = 0.0;           // Current time measure
        double updateDrawTime = 0.0;        // Update + Draw time
        double waitTime = 0.0;              // Wait time (if target fps required)
        float deltaTime = 0.0f;             // Frame time (Update + Draw + Wait time)

        float timeCounter = 0.0f;           // Accumulative time counter (seconds)
        float position = 0.0f;              // Circle position
        bool pause = false;                 // Pause control flag

        int targetFPS = 60;                 // Our initial target fps

        while (!WindowShouldClose())
        {
            PollInputEvents();          // Poll input events (SUPPORT_CUSTOM_FRAME_CONTROL)

            if (IsKeyPressed(Key.Space)) pause = !pause;

            if (IsKeyPressed(Key.Up)) targetFPS += 20;
            else if (IsKeyPressed(Key.Down)) targetFPS -= 20;

            if (targetFPS < 0) targetFPS = 0;

            if (!pause)
            {
                position += 200*deltaTime;  // We move at 200 pixels per second
                if (position >= GetScreenWidth()) position = 0;
                timeCounter += deltaTime;   // We count time (seconds)
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < GetScreenWidth()/200; i++) DrawRectangle(200*i, 0, 1, GetScreenHeight(), Color.SkyBlue);

                DrawCircle((int)position, GetScreenHeight()/2 - 25, 50, Color.Red);

                DrawText($"{timeCounter*1000.0f:000} ms", (int)position - 40, GetScreenHeight()/2 - 100, 20, Color.Maroon);
                DrawText($"PosX: {position:000}", (int)position - 50, GetScreenHeight()/2 + 40, 20, Color.Black);

                DrawText("Circle is moving at a constant 200 pixels/sec,\nindependently of the frame rate.", 10, 10, 20, Color.DarkGray);
                DrawText("PRESS SPACE to PAUSE MOVEMENT", 10, GetScreenHeight() - 60, 20, Color.Gray);
                DrawText("PRESS UP | DOWN to CHANGE TARGET FPS", 10, GetScreenHeight() - 30, 20, Color.Gray);
                DrawText($"TARGET FPS: {targetFPS}", GetScreenWidth() - 220, 10, 20, Color.Lime);
                if (deltaTime != 0)
                {
                    DrawText($"CURRENT FPS: {(int)(1.0f/deltaTime)}", GetScreenWidth() - 220, 40, 20, Color.Green);
                }

            EndDrawing();

            // NOTE: In case raylib is configured to SUPPORT_CUSTOM_FRAME_CONTROL,
            // Events polling, screen buffer swap and frame time control must be managed by the user

            SwapScreenBuffer();         // Flip the back buffer to screen (front buffer)

            currentTime = GetTime();
            updateDrawTime = currentTime - previousTime;

            if (targetFPS > 0)          // A fixed frame rate is asked for
            {
                waitTime = (1.0f/(float)targetFPS) - updateDrawTime;
                if (waitTime > 0.0)
                {
                    WaitTime((float)waitTime);
                    currentTime = GetTime();
                    deltaTime = (float)(currentTime - previousTime);
                }
            }
            else deltaTime = (float)updateDrawTime;    // Framerate could be variable

            previousTime = currentTime;
        }

        CloseWindow();
    }
}
