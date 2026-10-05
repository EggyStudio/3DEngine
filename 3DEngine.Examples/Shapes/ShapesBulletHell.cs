// raylib's shapes_bullet_hell example, Copyright (c) 2025 Zero (@zerohorsepower), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesBulletHell
{
    private const int MAX_BULLETS = 500000;

    private struct Bullet
    {
        public Vector2 position;
        public Vector2 acceleration;
        public bool disabled;
        public Color color;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] bullet hell");

        Bullet[] bullets = new Bullet[MAX_BULLETS];
        int bulletCount = 0;
        int bulletDisabledCount = 0;
        int bulletRadius = 10;
        float bulletSpeed = 3.0f;
        int bulletRows = 6;
        Color[] bulletColor = { Color.Red, Color.Blue };

        float baseDirection = 0;
        int angleIncrement = 5;
        float spawnCooldown = 2;
        float spawnCooldownTimer = spawnCooldown;

        float magicCircleRotation = 0;

        RenderTexture2D bulletTexture = LoadRenderTexture(24, 24);

        BeginTextureMode(bulletTexture);
            DrawCircle(12, 12, (float)bulletRadius, Color.White);
            DrawCircleLines(12, 12, (float)bulletRadius, Color.Black);
        EndTextureMode();

        bool drawInPerformanceMode = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (bulletCount >= MAX_BULLETS)
            {
                bulletCount = 0;
                bulletDisabledCount = 0;
            }

            spawnCooldownTimer--;
            if (spawnCooldownTimer < 0)
            {
                spawnCooldownTimer = spawnCooldown;

                float degreesPerRow = 360.0f/bulletRows;
                for (int row = 0; row < bulletRows; row++)
                {
                    if (bulletCount < MAX_BULLETS)
                    {
                        bullets[bulletCount].position = new Vector2((float) screenWidth/2, (float) screenHeight/2);
                        bullets[bulletCount].disabled = false;
                        bullets[bulletCount].color = bulletColor[row%2];

                        float bulletDirection = baseDirection + (degreesPerRow*row);

                        bullets[bulletCount].acceleration = new Vector2(bulletSpeed*MathF.Cos(bulletDirection*(MathF.PI/180)),
                            bulletSpeed*MathF.Sin(bulletDirection*(MathF.PI/180)));

                        bulletCount++;
                    }
                }

                baseDirection += angleIncrement;
            }

            for (int i = 0; i < bulletCount; i++)
            {
                if (!bullets[i].disabled)
                {
                    bullets[i].position.X += bullets[i].acceleration.X;
                    bullets[i].position.Y += bullets[i].acceleration.Y;

                    if ((bullets[i].position.X < -bulletRadius*2) ||
                        (bullets[i].position.X > screenWidth + bulletRadius*2) ||
                        (bullets[i].position.Y < -bulletRadius*2) ||
                        (bullets[i].position.Y > screenHeight + bulletRadius*2))
                    {
                        bullets[i].disabled = true;
                        bulletDisabledCount++;
                    }
                }
            }

            if ((IsKeyPressed(Key.Right) || IsKeyPressed(Key.D)) && (bulletRows < 359)) bulletRows++;
            if ((IsKeyPressed(Key.Left) || IsKeyPressed(Key.A)) && (bulletRows > 1)) bulletRows--;
            if (IsKeyPressed(Key.Up) || IsKeyPressed(Key.W)) bulletSpeed += 0.25f;
            if ((IsKeyPressed(Key.Down) || IsKeyPressed(Key.S)) && (bulletSpeed > 0.50f)) bulletSpeed -= 0.25f;
            if (IsKeyPressed(Key.Z) && (spawnCooldown > 1)) spawnCooldown--;
            if (IsKeyPressed(Key.X)) spawnCooldown++;
            if (IsKeyPressed(Key.Enter)) drawInPerformanceMode = !drawInPerformanceMode;

            if (IsKeyDown(Key.Space))
            {
                angleIncrement += 1;
                angleIncrement %= 360;
            }

            if (IsKeyPressed(Key.C))
            {
                bulletCount = 0;
                bulletDisabledCount = 0;
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                magicCircleRotation++;
                DrawRectanglePro(new Rectangle((float)screenWidth/2, (float)screenHeight/2, 120, 120),
                    new Vector2(60.0f, 60.0f), magicCircleRotation, Color.Purple);
                DrawRectanglePro(new Rectangle((float)screenWidth/2, (float)screenHeight/2, 120, 120),
                    new Vector2(60.0f, 60.0f), magicCircleRotation + 45, Color.Purple);
                DrawCircleLines(screenWidth/2, screenHeight/2, 70, Color.Black);
                DrawCircleLines(screenWidth/2, screenHeight/2, 50, Color.Black);
                DrawCircleLines(screenWidth/2, screenHeight/2, 30, Color.Black);

                if (drawInPerformanceMode)
                {
                    for (int i = 0; i < bulletCount; i++)
                    {
                        if (!bullets[i].disabled)
                        {
                            DrawTexture(bulletTexture.Texture,
                                (int)(bullets[i].position.X - bulletTexture.Texture.Width*0.5f),
                                (int)(bullets[i].position.Y - bulletTexture.Texture.Height*0.5f),
                                bullets[i].color);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < bulletCount; i++)
                    {
                        if (!bullets[i].disabled)
                        {
                            DrawCircleV(bullets[i].position, (float)bulletRadius, bullets[i].color);
                            DrawCircleLinesV(bullets[i].position, (float)bulletRadius, Color.Black);
                        }
                    }
                }

                DrawRectangle(10, 10, 280, 150, new Color(0, 0, 0, 200));
                DrawText("Controls:", 20, 20, 10, Color.LightGray);
                DrawText("- Right/Left or A/D: Change rows number", 40, 40, 10, Color.LightGray);
                DrawText("- Up/Down or W/S: Change bullet speed", 40, 60, 10, Color.LightGray);
                DrawText("- Z or X: Change spawn cooldown", 40, 80, 10, Color.LightGray);
                DrawText("- Space (Hold): Change the angle increment", 40, 100, 10, Color.LightGray);
                DrawText("- Enter: Switch draw method (Performance)", 40, 120, 10, Color.LightGray);
                DrawText("- C: Clear bullets", 40, 140, 10, Color.LightGray);

                DrawRectangle(610, 10, 170, 30, new Color(0, 0, 0, 200));
                if (drawInPerformanceMode) DrawText("Draw method: DrawTexture(*)", 620, 20, 10, Color.Green);
                else DrawText("Draw method: DrawCircle(*)", 620, 20, 10, Color.Red);

                DrawRectangle(135, 410, 530, 30, new Color(0, 0, 0, 200));
                DrawText($"[ FPS: {GetFPS()}, Bullets: {bulletCount - bulletDisabledCount}, Rows: {bulletRows}, Bullet speed: {bulletSpeed:0.00}, Angle increment per frame: {angleIncrement}, Cooldown: {spawnCooldown:0} ]",
                    155, 420, 10, Color.Green);

            EndDrawing();
        }

        UnloadRenderTexture(bulletTexture);


        CloseWindow();
    }
}
