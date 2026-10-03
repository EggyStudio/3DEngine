using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputGamepad
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] gamepad input");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);

            if (!IsGamepadAvailable(0))
            {
                DrawText("Connect a gamepad, or drive one with: e3d command input.button 0 South 30", 40, 200, 20, Color.Gray);
                EndDrawing();
                continue;
            }

            DrawText($"Gamepad 0: {GetGamepadName(0)}", 20, 20, 20, Color.DarkGray);

            // The face buttons, lit while held.
            Button(new Vector2(560, 260), GamepadButton.South, Color.Lime);
            Button(new Vector2(600, 220), GamepadButton.East, Color.Red);
            Button(new Vector2(520, 220), GamepadButton.West, Color.Blue);
            Button(new Vector2(560, 180), GamepadButton.North, Color.Gold);
            Button(new Vector2(240, 180), GamepadButton.DpadUp, Color.DarkGray);
            Button(new Vector2(240, 260), GamepadButton.DpadDown, Color.DarkGray);
            Button(new Vector2(200, 220), GamepadButton.DpadLeft, Color.DarkGray);
            Button(new Vector2(280, 220), GamepadButton.DpadRight, Color.DarkGray);
            Button(new Vector2(360, 200), GamepadButton.Back, Color.DarkGray);
            Button(new Vector2(440, 200), GamepadButton.Start, Color.DarkGray);

            // The sticks, as a dot inside a ring.
            Stick(new Vector2(320, 330), GamepadAxis.LeftX, GamepadAxis.LeftY);
            Stick(new Vector2(480, 330), GamepadAxis.RightX, GamepadAxis.RightY);

            // The triggers, as bars that fill.
            Trigger(new Vector2(200, 100), GamepadAxis.LeftTrigger);
            Trigger(new Vector2(560, 100), GamepadAxis.RightTrigger);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void Button(Vector2 at, GamepadButton button, Color lit)
    {
        DrawCircleV(at, 18, IsGamepadButtonDown(0, button) ? lit : Color.LightGray);
        DrawCircleLines((int)at.X, (int)at.Y, 18, Color.Gray);
    }

    private static void Stick(Vector2 at, GamepadAxis x, GamepadAxis y)
    {
        DrawCircleLines((int)at.X, (int)at.Y, 40, Color.Gray);
        DrawCircleV(at + new Vector2(GetGamepadAxisMovement(0, x), GetGamepadAxisMovement(0, y)) * 30, 12, Color.Maroon);
    }

    private static void Trigger(Vector2 at, GamepadAxis axis)
    {
        DrawRectangle((int)at.X, (int)at.Y, 40, 60, Color.LightGray);
        var fill = GetGamepadAxisMovement(0, axis);
        DrawRectangle((int)at.X, (int)(at.Y + 60 * (1 - fill)), 40, (int)(60 * fill), Color.Orange);
        DrawRectangleLines((int)at.X, (int)at.Y, 40, 60, Color.Gray);
    }
}
