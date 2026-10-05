// raylib's core_input_gamepad example, Copyright (c) 2013-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputGamepad
{
    private const string XboxAlias1 = "xbox";
    private const string XboxAlias2 = "x-box";
    private const string PsAlias1 = "playstation";
    private const string PsAlias2 = "sony";

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

                    if (GetGamepadName(gamepad).ToLowerInvariant().Contains(XboxAlias1) ||
                        GetGamepadName(gamepad).ToLowerInvariant().Contains(XboxAlias2))
                    {
                        DrawTexture(texXboxPad, 0, 0, Color.DarkGray);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.Guide)) DrawCircle(394, 89, 19, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.Start)) DrawCircle(436, 150, 9, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.Back)) DrawCircle(352, 150, 9, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.West)) DrawCircle(501, 151, 15, Color.Blue);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.South)) DrawCircle(536, 187, 15, Color.Lime);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.East)) DrawCircle(572, 151, 15, Color.Maroon);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.North)) DrawCircle(536, 115, 15, Color.Gold);

                        DrawRectangle(317, 202, 19, 71, Color.Black);
                        DrawRectangle(293, 228, 69, 19, Color.Black);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadUp)) DrawRectangle(317, 202, 19, 26, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadDown)) DrawRectangle(317, 202 + 45, 19, 26, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadLeft)) DrawRectangle(292, 228, 25, 19, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadRight)) DrawRectangle(292 + 44, 228, 26, 19, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftShoulder)) DrawCircle(259, 61, 20, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightShoulder)) DrawCircle(536, 61, 20, Color.Red);

                        Color leftGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftStick)) leftGamepadColor = Color.Red;
                        DrawCircle(259, 152, 39, Color.Black);
                        DrawCircle(259, 152, 34, Color.LightGray);
                        DrawCircle(259 + (int)(leftStickX*20), 152 + (int)(leftStickY*20), 25, leftGamepadColor);

                        Color rightGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightStick)) rightGamepadColor = Color.Red;
                        DrawCircle(461, 237, 38, Color.Black);
                        DrawCircle(461, 237, 33, Color.LightGray);
                        DrawCircle(461 + (int)(rightStickX*20), 237 + (int)(rightStickY*20), 25, rightGamepadColor);

                        DrawRectangle(170, 30, 15, 70, Color.Gray);
                        DrawRectangle(604, 30, 15, 70, Color.Gray);
                        DrawRectangle(170, 30, 15, (int)(((1 + leftTrigger)/2)*70), Color.Red);
                        DrawRectangle(604, 30, 15, (int)(((1 + rightTrigger)/2)*70), Color.Red);
                    }
                    else if (GetGamepadName(gamepad).ToLowerInvariant().Contains(PsAlias1) ||
                             GetGamepadName(gamepad).ToLowerInvariant().Contains(PsAlias2))
                    {
                        DrawTexture(texPs3Pad, 0, 0, Color.DarkGray);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.Guide)) DrawCircle(396, 222, 13, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.Back)) DrawRectangle(328, 170, 32, 13, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.Start)) DrawTriangle(new Vector2(436, 168), new Vector2(436, 185), new Vector2(464, 177), Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.North)) DrawCircle(557, 144, 13, Color.Lime);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.East)) DrawCircle(586, 173, 13, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.South)) DrawCircle(557, 203, 13, Color.Violet);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.West)) DrawCircle(527, 173, 13, Color.Pink);

                        DrawRectangle(225, 132, 24, 84, Color.Black);
                        DrawRectangle(195, 161, 84, 25, Color.Black);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadUp)) DrawRectangle(225, 132, 24, 29, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadDown)) DrawRectangle(225, 132 + 54, 24, 30, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadLeft)) DrawRectangle(195, 161, 30, 25, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadRight)) DrawRectangle(195 + 54, 161, 30, 25, Color.Red);

                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftShoulder)) DrawCircle(239, 82, 20, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightShoulder)) DrawCircle(557, 82, 20, Color.Red);

                        Color leftGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftStick)) leftGamepadColor = Color.Red;
                        DrawCircle(319, 255, 35, Color.Black);
                        DrawCircle(319, 255, 31, Color.LightGray);
                        DrawCircle(319 + (int)(leftStickX*20), 255 + (int)(leftStickY*20), 25, leftGamepadColor);

                        Color rightGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightStick)) rightGamepadColor = Color.Red;
                        DrawCircle(475, 255, 35, Color.Black);
                        DrawCircle(475, 255, 31, Color.LightGray);
                        DrawCircle(475 + (int)(rightStickX*20), 255 + (int)(rightStickY*20), 25, rightGamepadColor);

                        DrawRectangle(169, 48, 15, 70, Color.Gray);
                        DrawRectangle(611, 48, 15, 70, Color.Gray);
                        DrawRectangle(169, 48, 15, (int)(((1 + leftTrigger)/2)*70), Color.Red);
                        DrawRectangle(611, 48, 15, (int)(((1 + rightTrigger)/2)*70), Color.Red);
                    }
                    else
                    {
                        DrawRectangleRounded(new Rectangle(175, 110, 460, 220), 0.3f, 16, Color.DarkGray);

                        DrawCircle(365, 170, 12, Color.RayWhite);
                        DrawCircle(405, 170, 12, Color.RayWhite);
                        DrawCircle(445, 170, 12, Color.RayWhite);
                        DrawCircle(516, 191, 17, Color.RayWhite);
                        DrawCircle(551, 227, 17, Color.RayWhite);
                        DrawCircle(587, 191, 17, Color.RayWhite);
                        DrawCircle(551, 155, 17, Color.RayWhite);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.Back)) DrawCircle(365, 170, 10, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.Guide)) DrawCircle(405, 170, 10, Color.Green);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.Start)) DrawCircle(445, 170, 10, Color.Blue);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.West)) DrawCircle(516, 191, 15, Color.Gold);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.South)) DrawCircle(551, 227, 15, Color.Blue);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.East)) DrawCircle(587, 191, 15, Color.Green);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.North)) DrawCircle(551, 155, 15, Color.Red);

                        DrawRectangle(245, 145, 28, 88, Color.RayWhite);
                        DrawRectangle(215, 174, 88, 29, Color.RayWhite);
                        DrawRectangle(247, 147, 24, 84, Color.Black);
                        DrawRectangle(217, 176, 84, 25, Color.Black);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadUp)) DrawRectangle(247, 147, 24, 29, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadDown)) DrawRectangle(247, 147 + 54, 24, 30, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadLeft)) DrawRectangle(217, 176, 30, 25, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.DpadRight)) DrawRectangle(217 + 54, 176, 30, 25, Color.Red);

                        DrawRectangleRounded(new Rectangle(215, 98, 100, 10), 0.5f, 16, Color.DarkGray);
                        DrawRectangleRounded(new Rectangle(495, 98, 100, 10), 0.5f, 16, Color.DarkGray);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftShoulder)) DrawRectangleRounded(new Rectangle(215, 98, 100, 10), 0.5f, 16, Color.Red);
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightShoulder)) DrawRectangleRounded(new Rectangle(495, 98, 100, 10), 0.5f, 16, Color.Red);

                        Color leftGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.LeftStick)) leftGamepadColor = Color.Red;
                        DrawCircle(345, 260, 40, Color.Black);
                        DrawCircle(345, 260, 35, Color.LightGray);
                        DrawCircle(345 + (int)(leftStickX*20), 260 + (int)(leftStickY*20), 25, leftGamepadColor);

                        Color rightGamepadColor = Color.Black;
                        if (IsGamepadButtonDown(gamepad, GamepadButton.RightStick)) rightGamepadColor = Color.Red;
                        DrawCircle(465, 260, 40, Color.Black);
                        DrawCircle(465, 260, 35, Color.LightGray);
                        DrawCircle(465 + (int)(rightStickX*20), 260 + (int)(rightStickY*20), 25, rightGamepadColor);

                        DrawRectangle(151, 110, 15, 70, Color.Gray);
                        DrawRectangle(644, 110, 15, 70, Color.Gray);
                        DrawRectangle(151, 110, 15, (int)(((1 + leftTrigger)/2)*70), Color.Red);
                        DrawRectangle(644, 110, 15, (int)(((1 + rightTrigger)/2)*70), Color.Red);
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
