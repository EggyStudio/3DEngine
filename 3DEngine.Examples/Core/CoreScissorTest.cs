// raylib's core_scissor_test example, Copyright (c) 2019-2025 Chris Dill (@MysteriousSpace), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreScissorTest
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] scissor test");

        Rectangle scissorArea = new(0, 0, 300, 300);
        bool scissorMode = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.S)) scissorMode = !scissorMode;

            scissorArea = scissorArea with { X = GetMouseX() - scissorArea.Width/2, Y = GetMouseY() - scissorArea.Height/2 };

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (scissorMode) BeginScissorMode((int)scissorArea.X, (int)scissorArea.Y, (int)scissorArea.Width, (int)scissorArea.Height);

                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.Red);
                DrawText("Move the mouse around to reveal this text!", 190, 200, 20, Color.LightGray);

                if (scissorMode) EndScissorMode();

                DrawRectangleLinesEx(scissorArea, 1, Color.Black);
                DrawText("Press S to toggle scissor test", 10, 10, 20, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
