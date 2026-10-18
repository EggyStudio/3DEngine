// raylib's core_input_gamepad example, Copyright (c) 2013-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class CoreInputGamepad
{
    // raylib reads the name a pad's system gives it, such as "Sony Interactive Entertainment Wireless
    // Controller", where SDL3 names the same pad "PS4 Controller" or "PS5 Controller", so SDL3's names
    // are read as well. SDL3 gives every other pad an Xbox controller's buttons, sticks and triggers,
    // so each is drawn on raylib's picture of one, where raylib's example drew a pad it could not name
    // with shapes of its own.
    private static readonly string[] PsAliases = ["playstation", "sony", "ps3", "ps4", "ps5", "dualshock", "dualsense"];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);

        InitWindow(screenWidth, screenHeight, "[core] input gamepad");

        Texture2D texPs3Pad = LoadTexture("resources/ps3.png");
        Texture2D texXboxPad = LoadTexture("resources/xbox.png");

        const float leftStickDeadzoneX = 0.1f;
        const float leftStickDeadzoneY = 0.1f;
        const float rightStickDeadzoneX = 0.1f;
        const float rightStickDeadzoneY = 0.1f;
        const float leftTriggerDeadzone = -0.9f;
        const float rightTriggerDeadzone = -0.9f;

        Rectangle vibrateButton = default;

        SetTargetFPS(60);

        int gamepad = 0;

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Left) && gamepad > 0) gamepad--;
            if (IsKeyPressed(Key.Right)) gamepad++;
            Vector2 mousePosition = GetMousePosition();

            vibrateButton = new Rectangle(10, 70.0f + 20*GetGamepadAxisCount(gamepad) + 20, 75, 24);
            if (IsMouseButtonPressed(MouseButton.Left) && CheckCollisionPointRec(mousePosition, vibrateButton)) SetGamepadVibration(gamepad, 1.0f, 1.0f, 1.0f);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (IsGamepadAvailable(gamepad))
                {
                    DrawText($"GP{gamepad}: {GetGamepadName(gamepad)}", 10, 10, 10, Color.Black);

                    float leftStickX = GetGamepadAxisMovement(gamepad, GamepadAxis.LeftX);
                    float leftStickY = GetGamepadAxisMovement(gamepad, GamepadAxis.LeftY);
                    float rightStickX = GetGamepadAxisMovement(gamepad, GamepadAxis.RightX);
                    float rightStickY = GetGamepadAxisMovement(gamepad, GamepadAxis.RightY);
                    // raylib's triggers run from -1 at rest to 1, and these from 0, so they are read in
                    // raylib's range.
                    float leftTrigger = GetGamepadAxisMovement(gamepad, GamepadAxis.LeftTrigger) * 2 - 1;
                    float rightTrigger = GetGamepadAxisMovement(gamepad, GamepadAxis.RightTrigger) * 2 - 1;

                    if (leftStickX > -leftStickDeadzoneX && leftStickX < leftStickDeadzoneX) leftStickX = 0.0f;
                    if (leftStickY > -leftStickDeadzoneY && leftStickY < leftStickDeadzoneY) leftStickY = 0.0f;
                    if (rightStickX > -rightStickDeadzoneX && rightStickX < rightStickDeadzoneX) rightStickX = 0.0f;
                    if (rightStickY > -rightStickDeadzoneY && rightStickY < rightStickDeadzoneY) rightStickY = 0.0f;
                    if (leftTrigger < leftTriggerDeadzone) leftTrigger = -1.0f;
                    if (rightTrigger < rightTriggerDeadzone) rightTrigger = -1.0f;

                    if (!PsAliases.Any(GetGamepadName(gamepad).ToLowerInvariant().Contains))
                    {
                        DrawTexture(texXboxPad, 0, 0, Color.DarkGray);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.Middle)) DrawCircle(394, 89, 19, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.MiddleRight)) DrawCircle(436, 150, 9, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.MiddleLeft)) DrawCircle(352, 150, 9, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceLeft)) DrawCircle(501, 151, 15, Color.Blue);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceDown)) DrawCircle(536, 187, 15, Color.Lime);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceRight)) DrawCircle(572, 151, 15, Color.Maroon);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceUp)) DrawCircle(536, 115, 15, Color.Gold);

                        DrawRectangle(317, 202, 19, 71, Color.Black);
                        DrawRectangle(293, 228, 69, 19, Color.Black);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceUp)) DrawRectangle(317, 202, 19, 26, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceDown)) DrawRectangle(317, 202 + 45, 19, 26, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceLeft)) DrawRectangle(292, 228, 25, 19, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceRight)) DrawRectangle(292 + 44, 228, 26, 19, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftTrigger1)) DrawCircle(259, 61, 20, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightTrigger1)) DrawCircle(536, 61, 20, Color.Red);

                        Color leftGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftThumb)) leftGamepadColor = Color.Red;
                        DrawCircle(259, 152, 39, Color.Black);
                        DrawCircle(259, 152, 34, Color.LightGray);
                        DrawCircle(259 + (int)(leftStickX*20), 152 + (int)(leftStickY*20), 25, leftGamepadColor);

                        Color rightGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightThumb)) rightGamepadColor = Color.Red;
                        DrawCircle(461, 237, 38, Color.Black);
                        DrawCircle(461, 237, 33, Color.LightGray);
                        DrawCircle(461 + (int)(rightStickX*20), 237 + (int)(rightStickY*20), 25, rightGamepadColor);

                        DrawRectangle(170, 30, 15, 70, Color.Gray);
                        DrawRectangle(604, 30, 15, 70, Color.Gray);
                        DrawRectangle(170, 30, 15, (int)(((1 + leftTrigger)/2)*70), Color.Red);
                        DrawRectangle(604, 30, 15, (int)(((1 + rightTrigger)/2)*70), Color.Red);
                    }
                    else
                    {
                        DrawTexture(texPs3Pad, 0, 0, Color.DarkGray);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.Middle)) DrawCircle(396, 222, 13, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.MiddleLeft)) DrawRectangle(328, 170, 32, 13, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.MiddleRight)) DrawTriangle(new Vector2(436, 168), new Vector2(436, 185), new Vector2(464, 177), Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceUp)) DrawCircle(557, 144, 13, Color.Lime);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceRight)) DrawCircle(586, 173, 13, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceDown)) DrawCircle(557, 203, 13, Color.Violet);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightFaceLeft)) DrawCircle(527, 173, 13, Color.Pink);

                        DrawRectangle(225, 132, 24, 84, Color.Black);
                        DrawRectangle(195, 161, 84, 25, Color.Black);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceUp)) DrawRectangle(225, 132, 24, 29, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceDown)) DrawRectangle(225, 132 + 54, 24, 30, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceLeft)) DrawRectangle(195, 161, 30, 25, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftFaceRight)) DrawRectangle(195 + 54, 161, 30, 25, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftTrigger1)) DrawCircle(239, 82, 20, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightTrigger1)) DrawCircle(557, 82, 20, Color.Red);

                        Color leftGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftThumb)) leftGamepadColor = Color.Red;
                        DrawCircle(319, 255, 35, Color.Black);
                        DrawCircle(319, 255, 31, Color.LightGray);
                        DrawCircle(319 + (int)(leftStickX*20), 255 + (int)(leftStickY*20), 25, leftGamepadColor);

                        Color rightGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightThumb)) rightGamepadColor = Color.Red;
                        DrawCircle(475, 255, 35, Color.Black);
                        DrawCircle(475, 255, 31, Color.LightGray);
                        DrawCircle(475 + (int)(rightStickX*20), 255 + (int)(rightStickY*20), 25, rightGamepadColor);

                        DrawRectangle(169, 48, 15, 70, Color.Gray);
                        DrawRectangle(611, 48, 15, 70, Color.Gray);
                        DrawRectangle(169, 48, 15, (int)(((1 + leftTrigger)/2)*70), Color.Red);
                        DrawRectangle(611, 48, 15, (int)(((1 + rightTrigger)/2)*70), Color.Red);
                    }

                    DrawText($"DETECTED AXIS [{GetGamepadAxisCount(gamepad)}]:", 10, 50, 10, Color.Maroon);

                    for (int i = 0; i < GetGamepadAxisCount(gamepad); i++)
                    {
                        DrawText($"AXIS {i}: {GetGamepadAxisMovement(gamepad, (GamepadAxis)i):0.00}", 20, 70 + 20*i, 10, Color.DarkGray);
                    }

                    DrawRectangleRec(vibrateButton, Color.SkyBlue);
                    DrawText("VIBRATE", (int)(vibrateButton.X + 14), (int)(vibrateButton.Y + 1), 10, Color.DarkGray);

                    if (GetGamepadButtonPressed() is { } pressed) DrawText($"DETECTED BUTTON: {pressed}", 10, 430, 10, Color.Red);
                    else DrawText("DETECTED BUTTON: NONE", 10, 430, 10, Color.Gray);
                }
                else
                {
                    DrawText($"GP{gamepad}: NOT DETECTED", 10, 10, 10, Color.Gray);
                    DrawTexture(texXboxPad, 0, 0, Color.LightGray);
                }

            EndDrawing();
        }

        UnloadTexture(texPs3Pad);
        UnloadTexture(texXboxPad);

        CloseWindow();
    }
}
