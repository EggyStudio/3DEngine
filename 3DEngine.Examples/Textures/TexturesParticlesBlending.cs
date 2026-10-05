// raylib's textures_particles_blending example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesParticlesBlending
{
    private const int MAX_PARTICLES = 200;

    private struct Particle
    {
        public Vector2 position;
        public Color color;
        public float alpha;
        public float size;
        public float rotation;
        public bool active;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] particles blending");

        // A pool of particles, used again as they fade
        Particle[] mouseTail = new Particle[MAX_PARTICLES];

        for (int i = 0; i < MAX_PARTICLES; i++)
        {
            mouseTail[i].position = Vector2.Zero;
            mouseTail[i].color = new Color((byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), 255);
            mouseTail[i].alpha = 1.0f;
            mouseTail[i].size = (float)GetRandomValue(1, 30)/20.0f;
            mouseTail[i].rotation = (float)GetRandomValue(0, 360);
            mouseTail[i].active = false;
        }

        float gravity = 3.0f;

        Texture2D smoke = LoadTexture("resources/spark_flame.png");

        BlendMode blending = BlendMode.Alpha;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // A particle a frame starts at the pointer, falls and turns, and is free again once
            // it has faded, after a little over three seconds.
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                if (!mouseTail[i].active)
                {
                    mouseTail[i].active = true;
                    mouseTail[i].alpha = 1.0f;
                    mouseTail[i].position = GetMousePosition();
                    i = MAX_PARTICLES;
                }
            }

            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                if (mouseTail[i].active)
                {
                    mouseTail[i].position.Y += gravity/2;
                    mouseTail[i].alpha -= 0.005f;

                    if (mouseTail[i].alpha <= 0.0f) mouseTail[i].active = false;

                    mouseTail[i].rotation += 2.0f;
                }
            }

            if (IsKeyPressed(Key.Space))
            {
                if (blending == BlendMode.Alpha) blending = BlendMode.Additive;
                else blending = BlendMode.Alpha;
            }

            BeginDrawing();

                ClearBackground(Color.DarkGray);

                BeginBlendMode(blending);

                    for (int i = 0; i < MAX_PARTICLES; i++)
                    {
                        if (mouseTail[i].active) DrawTexturePro(smoke, new Rectangle(0.0f, 0.0f, (float)smoke.Width, (float)smoke.Height),
                                                               new Rectangle(mouseTail[i].position.X, mouseTail[i].position.Y, smoke.Width*mouseTail[i].size, smoke.Height*mouseTail[i].size),
                                                               new Vector2(smoke.Width*mouseTail[i].size/2.0f, smoke.Height*mouseTail[i].size/2.0f), mouseTail[i].rotation,
                                                               Fade(mouseTail[i].color, mouseTail[i].alpha));
                    }

                EndBlendMode();

                DrawText("PRESS SPACE to CHANGE BLENDING MODE", 180, 20, 20, Color.Black);

                if (blending == BlendMode.Alpha) DrawText("ALPHA BLENDING", 290, screenHeight - 40, 20, Color.Black);
                else DrawText("ADDITIVE BLENDING", 280, screenHeight - 40, 20, Color.RayWhite);

            EndDrawing();
        }

        UnloadTexture(smoke);

        CloseWindow();
    }
}
