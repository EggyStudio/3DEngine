// raylib's shapes_ball_physics example, Copyright (c) 2025 David Buzatto (@davidbuzatto), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesBallPhysics
{
    private const int MAX_BALLS = 5000;

    private struct Ball
    {
        public Vector2 position;
        public Vector2 speed;
        public Vector2 prevPosition;
        public float radius;
        public float friction;
        public float elasticity;
        public Color color;
        public bool grabbed;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] ball physics");

        Ball[] balls = new Ball[MAX_BALLS];

        balls[0] = new Ball
        {
            position = new Vector2(GetScreenWidth()/2.0f, GetScreenHeight()/2.0f),
            speed = new Vector2(200, 200),
            prevPosition = Vector2.Zero,
            radius = 40,
            friction = 0.99f,
            elasticity = 0.9f,
            color = Color.Blue,
            grabbed = false,
        };

        int ballCount = 1;
        int grabbedBall = -1;
        Vector2 pressOffset = Vector2.Zero;

        float gravity = 100;

        Vector2 windowPosition = GetWindowPosition();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float delta = GetFrameTime();
            Vector2 mousePos = GetMousePosition();

            if (IsMouseButtonPressed(MouseButton.Left))
            {
                for (int i = ballCount - 1; i >= 0; i--)
                {
                    ref Ball ball = ref balls[i];
                    pressOffset.X = mousePos.X - ball.position.X;
                    pressOffset.Y = mousePos.Y - ball.position.Y;

                    if (MathF.Sqrt(pressOffset.X*pressOffset.X + pressOffset.Y*pressOffset.Y) <= ball.radius)
                    {
                        ball.grabbed = true;
                        grabbedBall = i;
                        break;
                    }
                }
            }

            if (IsMouseButtonReleased(MouseButton.Left))
            {
                if (grabbedBall >= 0)
                {
                    balls[grabbedBall].grabbed = false;
                    grabbedBall = -1;
                }
            }

            if (IsMouseButtonPressed(MouseButton.Right) || (IsKeyDown(Key.LeftControl) && IsMouseButtonDown(MouseButton.Right)))
            {
                if (ballCount < MAX_BALLS)
                {
                    balls[ballCount++] = new Ball
                    {
                        position = mousePos,
                        speed = new Vector2((float)GetRandomValue(-300, 300), (float)GetRandomValue(-300, 300)),
                        prevPosition = Vector2.Zero,
                        radius = 20.0f + (float)GetRandomValue(0, 30),
                        friction = 0.99f,
                        elasticity = 0.9f,
                        color = new Color((byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), 255),
                        grabbed = false,
                    };
                }
            }

            Vector2 windowPositionDelta = windowPosition - GetWindowPosition();

            if (windowPositionDelta.Length() > 5.0f)
            {
                for (int i = 0; i < ballCount; i++)
                {
                    if (!balls[i].grabbed) balls[i].speed += windowPositionDelta*10.0f;
                }
            }

            if (IsMouseButtonPressed(MouseButton.Middle))
            {
                for (int i = 0; i < ballCount; i++)
                {
                    if (!balls[i].grabbed) balls[i].speed = new Vector2((float)GetRandomValue(-2000, 2000), (float)GetRandomValue(-2000, 2000));
                }
            }

            gravity += GetMouseWheelMove()*5;

            for (int i = 0; i < ballCount; i++)
            {
                ref Ball ball = ref balls[i];

                if (!ball.grabbed)
                {
                    ball.position.X += ball.speed.X*delta;
                    ball.position.Y += ball.speed.Y*delta;

                    if ((ball.position.X + ball.radius) >= screenWidth)
                    {
                        ball.position.X = screenWidth - ball.radius;
                        ball.speed.X = -ball.speed.X*ball.elasticity;
                    }
                    else if ((ball.position.X - ball.radius) <= 0)
                    {
                        ball.position.X = ball.radius;
                        ball.speed.X = -ball.speed.X*ball.elasticity;
                    }

                    if ((ball.position.Y + ball.radius) >= screenHeight)
                    {
                        ball.position.Y = screenHeight - ball.radius;
                        ball.speed.Y = -ball.speed.Y*ball.elasticity;
                    }
                    else if ((ball.position.Y - ball.radius) <= 0)
                    {
                        ball.position.Y = ball.radius;
                        ball.speed.Y = -ball.speed.Y*ball.elasticity;
                    }

                    ball.speed.X = ball.speed.X*ball.friction;
                    ball.speed.Y = ball.speed.Y*ball.friction + gravity;
                }
                else
                {
                    ball.position.X = mousePos.X - pressOffset.X;
                    ball.position.Y = mousePos.Y - pressOffset.Y;

                    ball.speed.X = (ball.position.X - ball.prevPosition.X)/delta;
                    ball.speed.Y = (ball.position.Y - ball.prevPosition.Y)/delta;
                    ball.prevPosition = ball.position;
                }
            }

            windowPosition = GetWindowPosition();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < ballCount; i++)
                {
                    DrawCircleV(balls[i].position, balls[i].radius, balls[i].color);
                    DrawCircleLinesV(balls[i].position, balls[i].radius, Color.Black);
                }

                DrawText("grab a ball by pressing with the mouse and throw it by releasing", 10, 10, 10, Color.DarkGray);
                DrawText("right click to create new balls (keep left control pressed to create a lot)", 10, 30, 10, Color.DarkGray);
                DrawText("use mouse wheel to change gravity", 10, 50, 10, Color.DarkGray);
                DrawText("middle click to shake", 10, 70, 10, Color.DarkGray);
                DrawText($"BALL COUNT: {ballCount}", 10, GetScreenHeight() - 70, 20, Color.Black);
                DrawText($"GRAVITY: {gravity:0.00}", 10, GetScreenHeight() - 40, 20, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
