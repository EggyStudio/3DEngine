// raylib's core_input_actions example, Copyright (c) 2025 Jett (@JettMonstersGoBoom), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputActions
{
    private const int NO_ACTION = 0;

    private const int ACTION_UP = 1;

    private const int ACTION_DOWN = 2;

    private const int ACTION_LEFT = 3;

    private const int ACTION_RIGHT = 4;

    private const int ACTION_FIRE = 5;

    private const int MAX_ACTION = 6;

    private struct ActionInput
    {
        public Key key;
        public GamepadButton button;
    }

    private static int gamepadIndex = 0;
    private static ActionInput[] actionInputs = new ActionInput[MAX_ACTION];

    private static bool IsActionPressed(int action)
    {
        bool result = false;

        if (action < MAX_ACTION) result = (IsKeyPressed(actionInputs[action].key) || IsGamepadButtonPressed(gamepadIndex, actionInputs[action].button));

        return result;
    }

    private static bool IsActionReleased(int action)
    {
        bool result = false;

        if (action < MAX_ACTION) result = (IsKeyReleased(actionInputs[action].key) || IsGamepadButtonReleased(gamepadIndex, actionInputs[action].button));

        return result;
    }

    private static bool IsActionDown(int action)
    {
        bool result = false;

        if (action < MAX_ACTION) result = (IsKeyDown(actionInputs[action].key) || IsGamepadButtonDown(gamepadIndex, actionInputs[action].button));

        return result;
    }

    private static void SetActionsDefault()
    {
        actionInputs[ACTION_UP].key = Key.W;
        actionInputs[ACTION_DOWN].key = Key.S;
        actionInputs[ACTION_LEFT].key = Key.A;
        actionInputs[ACTION_RIGHT].key = Key.D;
        actionInputs[ACTION_FIRE].key = Key.Space;

        actionInputs[ACTION_UP].button = GamepadButton.LeftFaceUp;
        actionInputs[ACTION_DOWN].button = GamepadButton.LeftFaceDown;
        actionInputs[ACTION_LEFT].button = GamepadButton.LeftFaceLeft;
        actionInputs[ACTION_RIGHT].button = GamepadButton.LeftFaceRight;
        actionInputs[ACTION_FIRE].button = GamepadButton.RightFaceDown;
    }

    private static void SetActionsCursor()
    {
        actionInputs[ACTION_UP].key = Key.Up;
        actionInputs[ACTION_DOWN].key = Key.Down;
        actionInputs[ACTION_LEFT].key = Key.Left;
        actionInputs[ACTION_RIGHT].key = Key.Right;
        actionInputs[ACTION_FIRE].key = Key.Space;

        actionInputs[ACTION_UP].button = GamepadButton.RightFaceUp;
        actionInputs[ACTION_DOWN].button = GamepadButton.RightFaceDown;
        actionInputs[ACTION_LEFT].button = GamepadButton.RightFaceLeft;
        actionInputs[ACTION_RIGHT].button = GamepadButton.RightFaceRight;
        actionInputs[ACTION_FIRE].button = GamepadButton.LeftFaceDown;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input actions");

        bool actionSet = false;
        SetActionsDefault();
        bool releaseAction = false;

        Vector2 position = new Vector2(400.0f, 200.0f);
        Vector2 size = new Vector2(40.0f, 40.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            gamepadIndex = 0;

            if (IsActionDown(ACTION_UP)) position.Y -= 2;
            if (IsActionDown(ACTION_DOWN)) position.Y += 2;
            if (IsActionDown(ACTION_LEFT)) position.X -= 2;
            if (IsActionDown(ACTION_RIGHT)) position.X += 2;
            if (IsActionPressed(ACTION_FIRE))
            {
                position.X = (screenWidth-size.X)/2;
                position.Y = (screenHeight-size.Y)/2;
            }

            releaseAction = false;
            if (IsActionReleased(ACTION_FIRE)) releaseAction = true;

            if (IsKeyPressed(Key.Tab))
            {
                actionSet = !actionSet;
                if (!actionSet) SetActionsDefault();
                else SetActionsCursor();
            }

            BeginDrawing();

                ClearBackground(Color.Gray);

                DrawRectangleV(position, size, releaseAction? Color.Blue : Color.Red);

                DrawText((!actionSet)? "Current input set: WASD (default)" : "Current input set: Arrow keys", 10, 10, 20, Color.White);
                DrawText("Use TAB key to toggles Actions keyset", 10, 50, 20, Color.Green);

            EndDrawing();
        }

        CloseWindow();
    }
}
