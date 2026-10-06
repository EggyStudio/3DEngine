// raylib's core_window_flags example, Copyright (c) 2020-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreWindowFlags
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        // Possible window flags
        /*
        ConfigFlags.VsyncHint
        ConfigFlags.FullscreenMode    -> not working properly -> wrong scaling
        ConfigFlags.WindowResizable
        ConfigFlags.WindowUndecorated
        ConfigFlags.WindowTransparent
        ConfigFlags.WindowHidden
        ConfigFlags.WindowMinimized   -> Not supported on window creation
        ConfigFlags.WindowMaximized   -> Not supported on window creation
        ConfigFlags.WindowUnfocused
        ConfigFlags.WindowTopmost
        ConfigFlags.WindowHighdpi     -> errors after minimize-resize, fb size is recalculated
        ConfigFlags.WindowAlwaysRun
        ConfigFlags.Msaa4xHint
        */

        // Set configuration flags for window creation
        //SetConfigFlags(ConfigFlags.VsyncHint | ConfigFlags.Msaa4xHint | ConfigFlags.WindowHighdpi);// | ConfigFlags.WindowTransparent);
        InitWindow(screenWidth, screenHeight, "[core] window flags");

        Vector2 ballPosition = new(GetScreenWidth()/2.0f, GetScreenHeight()/2.0f);
        Vector2 ballSpeed = new(5.0f, 4.0f);
        float ballRadius = 20;

        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.F)) ToggleFullscreen();  // modifies window size when scaling

            if (IsKeyPressed(Key.R))
            {
                if (IsWindowState(ConfigFlags.WindowResizable)) ClearWindowState(ConfigFlags.WindowResizable);
                else SetWindowState(ConfigFlags.WindowResizable);
            }

            if (IsKeyPressed(Key.D))
            {
                if (IsWindowState(ConfigFlags.WindowUndecorated)) ClearWindowState(ConfigFlags.WindowUndecorated);
                else SetWindowState(ConfigFlags.WindowUndecorated);
            }

            if (IsKeyPressed(Key.H))
            {
                if (!IsWindowState(ConfigFlags.WindowHidden)) SetWindowState(ConfigFlags.WindowHidden);

                framesCounter = 0;
            }

            if (IsWindowState(ConfigFlags.WindowHidden))
            {
                framesCounter++;
                if (framesCounter >= 240) ClearWindowState(ConfigFlags.WindowHidden); // Show window after 3 seconds
            }

            if (IsKeyPressed(Key.N))
            {
                if (!IsWindowState(ConfigFlags.WindowMinimized)) MinimizeWindow();

                framesCounter = 0;
            }

            if (IsWindowState(ConfigFlags.WindowMinimized))
            {
                framesCounter++;
                if (framesCounter >= 240)
                {
                    RestoreWindow(); // Restore window after 3 seconds
                    framesCounter = 0;
                }
            }

            if (IsKeyPressed(Key.M))
            {
                // NOTE: Requires ConfigFlags.WindowResizable enabled
                if (IsWindowState(ConfigFlags.WindowMaximized)) RestoreWindow();
                else MaximizeWindow();
            }

            if (IsKeyPressed(Key.U))
            {
                if (IsWindowState(ConfigFlags.WindowUnfocused)) ClearWindowState(ConfigFlags.WindowUnfocused);
                else SetWindowState(ConfigFlags.WindowUnfocused);
            }

            if (IsKeyPressed(Key.T))
            {
                if (IsWindowState(ConfigFlags.WindowTopmost)) ClearWindowState(ConfigFlags.WindowTopmost);
                else SetWindowState(ConfigFlags.WindowTopmost);
            }

            if (IsKeyPressed(Key.A))
            {
                if (IsWindowState(ConfigFlags.WindowAlwaysRun)) ClearWindowState(ConfigFlags.WindowAlwaysRun);
                else SetWindowState(ConfigFlags.WindowAlwaysRun);
            }

            if (IsKeyPressed(Key.V))
            {
                if (IsWindowState(ConfigFlags.VsyncHint)) ClearWindowState(ConfigFlags.VsyncHint);
                else SetWindowState(ConfigFlags.VsyncHint);
            }

            if (IsKeyPressed(Key.B)) ToggleBorderlessWindowed();

            // Bouncing ball logic
            ballPosition.X += ballSpeed.X;
            ballPosition.Y += ballSpeed.Y;
            if ((ballPosition.X >= (GetScreenWidth() - ballRadius)) || (ballPosition.X <= ballRadius)) ballSpeed.X *= -1.0f;
            if ((ballPosition.Y >= (GetScreenHeight() - ballRadius)) || (ballPosition.Y <= ballRadius)) ballSpeed.Y *= -1.0f;

            BeginDrawing();

            if (IsWindowState(ConfigFlags.WindowTransparent)) ClearBackground(Color.Blank);
            else ClearBackground(Color.RayWhite);

            DrawCircleV(ballPosition, ballRadius, Color.Maroon);
            DrawRectangleLinesEx(new Rectangle(0, 0, (float)GetScreenWidth(), (float)GetScreenHeight()), 4, Color.RayWhite);

            DrawCircleV(GetMousePosition(), 10, Color.DarkBlue);

            DrawFPS(10, 10);

            DrawText($"Screen Size: [{GetScreenWidth()}, {GetScreenHeight()}]", 10, 40, 10, Color.Green);

            // Draw window state info
            DrawText("Following flags can be set after window creation:", 10, 60, 10, Color.Gray);
            if (IsWindowState(ConfigFlags.FullscreenMode)) DrawText("[F] FLAG_FULLSCREEN_MODE: on", 10, 80, 10, Color.Lime);
            else DrawText("[F] FLAG_FULLSCREEN_MODE: off", 10, 80, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowResizable)) DrawText("[R] FLAG_WINDOW_RESIZABLE: on", 10, 100, 10, Color.Lime);
            else DrawText("[R] FLAG_WINDOW_RESIZABLE: off", 10, 100, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowUndecorated)) DrawText("[D] FLAG_WINDOW_UNDECORATED: on", 10, 120, 10, Color.Lime);
            else DrawText("[D] FLAG_WINDOW_UNDECORATED: off", 10, 120, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowHidden)) DrawText("[H] FLAG_WINDOW_HIDDEN: on", 10, 140, 10, Color.Lime);
            else DrawText("[H] FLAG_WINDOW_HIDDEN: off (hides for 3 seconds)", 10, 140, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowMinimized)) DrawText("[N] FLAG_WINDOW_MINIMIZED: on", 10, 160, 10, Color.Lime);
            else DrawText("[N] FLAG_WINDOW_MINIMIZED: off (restores after 3 seconds)", 10, 160, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowMaximized)) DrawText("[M] FLAG_WINDOW_MAXIMIZED: on", 10, 180, 10, Color.Lime);
            else DrawText("[M] FLAG_WINDOW_MAXIMIZED: off", 10, 180, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowUnfocused)) DrawText("[G] FLAG_WINDOW_UNFOCUSED: on", 10, 200, 10, Color.Lime);
            else DrawText("[U] FLAG_WINDOW_UNFOCUSED: off", 10, 200, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowTopmost)) DrawText("[T] FLAG_WINDOW_TOPMOST: on", 10, 220, 10, Color.Lime);
            else DrawText("[T] FLAG_WINDOW_TOPMOST: off", 10, 220, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowAlwaysRun)) DrawText("[A] FLAG_WINDOW_ALWAYS_RUN: on", 10, 240, 10, Color.Lime);
            else DrawText("[A] FLAG_WINDOW_ALWAYS_RUN: off", 10, 240, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.VsyncHint)) DrawText("[V] FLAG_VSYNC_HINT: on", 10, 260, 10, Color.Lime);
            else DrawText("[V] FLAG_VSYNC_HINT: off", 10, 260, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.BorderlessWindowedMode)) DrawText("[B] FLAG_BORDERLESS_WINDOWED_MODE: on", 10, 280, 10, Color.Lime);
            else DrawText("[B] FLAG_BORDERLESS_WINDOWED_MODE: off", 10, 280, 10, Color.Maroon);

            DrawText("Following flags can only be set before window creation:", 10, 320, 10, Color.Gray);
            if (IsWindowState(ConfigFlags.WindowHighdpi)) DrawText("FLAG_WINDOW_HIGHDPI: on", 10, 340, 10, Color.Lime);
            else DrawText("FLAG_WINDOW_HIGHDPI: off", 10, 340, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.WindowTransparent)) DrawText("FLAG_WINDOW_TRANSPARENT: on", 10, 360, 10, Color.Lime);
            else DrawText("FLAG_WINDOW_TRANSPARENT: off", 10, 360, 10, Color.Maroon);
            if (IsWindowState(ConfigFlags.Msaa4xHint)) DrawText("FLAG_MSAA_4X_HINT: on", 10, 380, 10, Color.Lime);
            else DrawText("FLAG_MSAA_4X_HINT: off", 10, 380, 10, Color.Maroon);

            EndDrawing();
        }

        CloseWindow();
    }
}
