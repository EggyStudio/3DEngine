// raylib's core_input_virtual_controls example, Copyright (c) 2024-2025 GreenSnakeLinux (@GreenSnakeLinux)
// and Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputVirtualControls
{
    private enum PadButton
    {
        BUTTON_NONE = -1,
        BUTTON_UP,
        BUTTON_LEFT,
        BUTTON_RIGHT,
        BUTTON_DOWN,
        BUTTON_MAX,
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input virtual controls");

        Vector2 padPosition = new(100, 350);
        float buttonRadius = 30;

        Vector2[] buttonPositions =
        [
            new(padPosition.X, padPosition.Y - buttonRadius*1.5f),
            new(padPosition.X - buttonRadius*1.5f, padPosition.Y),
            new(padPosition.X + buttonRadius*1.5f, padPosition.Y),
            new(padPosition.X, padPosition.Y + buttonRadius*1.5f),
        ];

        Vector2[][] arrowTris =
        [
            [
                new(buttonPositions[0].X,     buttonPositions[0].Y - 12),
                new(buttonPositions[0].X - 9, buttonPositions[0].Y + 9),
                new(buttonPositions[0].X + 9, buttonPositions[0].Y + 9),
            ],
            [
                new(buttonPositions[1].X + 9,  buttonPositions[1].Y - 9),
                new(buttonPositions[1].X - 12, buttonPositions[1].Y),
                new(buttonPositions[1].X + 9,  buttonPositions[1].Y + 9),
            ],
            [
                new(buttonPositions[2].X + 12, buttonPositions[2].Y),
                new(buttonPositions[2].X - 9,  buttonPositions[2].Y - 9),
                new(buttonPositions[2].X - 9,  buttonPositions[2].Y + 9),
            ],
            [
                new(buttonPositions[3].X - 9, buttonPositions[3].Y - 9),
                new(buttonPositions[3].X,     buttonPositions[3].Y + 12),
                new(buttonPositions[3].X + 9, buttonPositions[3].Y - 9),
            ],
        ];

        Color[] buttonLabelColors =
        [
            Color.Yellow,
            Color.Blue,
            Color.Red,
            Color.Green,
        ];

        PadButton pressedButton = PadButton.BUTTON_NONE;
        Vector2 inputPosition = new(0, 0);

        Vector2 playerPosition = new((float)screenWidth/2, (float)screenHeight/2);
        float playerSpeed = 75;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if ((GetTouchPointCount() > 0)) inputPosition = GetTouchPosition(0);
            else inputPosition = GetMousePosition();

            pressedButton = PadButton.BUTTON_NONE;

            if ((GetTouchPointCount() > 0) ||
                ((GetTouchPointCount() == 0) && IsMouseButtonDown(MouseButton.Left)))
            {
                for (int i = 0; i < (int)PadButton.BUTTON_MAX; i++)
                {
                    float distX = MathF.Abs(buttonPositions[i].X - inputPosition.X);
                    float distY = MathF.Abs(buttonPositions[i].Y - inputPosition.Y);

                    if ((distX + distY < buttonRadius))
                    {
                        pressedButton = (PadButton)i;
                        break;
                    }
                }
            }

            switch (pressedButton)
            {
                case PadButton.BUTTON_UP: playerPosition.Y -= playerSpeed*GetFrameTime(); break;
                case PadButton.BUTTON_LEFT: playerPosition.X -= playerSpeed*GetFrameTime(); break;
                case PadButton.BUTTON_RIGHT: playerPosition.X += playerSpeed*GetFrameTime(); break;
                case PadButton.BUTTON_DOWN: playerPosition.Y += playerSpeed*GetFrameTime(); break;
                default: break;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawCircleV(playerPosition, 50, Color.Maroon);

                for (int i = 0; i < (int)PadButton.BUTTON_MAX; i++)
                {
                    DrawCircleV(buttonPositions[i], buttonRadius, (i == (int)pressedButton) ? Color.DarkGray : Color.Black);

                    DrawTriangle(
                        arrowTris[i][0],
                        arrowTris[i][1],
                        arrowTris[i][2],
                        buttonLabelColors[i]
                    );
                }

                DrawText("move the player with D-Pad buttons", 10, 10, 20, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
