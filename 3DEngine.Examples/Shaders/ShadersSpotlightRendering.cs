// raylib's shaders_spotlight_rendering example, Copyright (c) 2019-2025 Chris Camacho (@chriscamacho) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersSpotlightRendering
{
    private const int MAX_SPOTS = 3;        // As many as the shader holds
    private const int MAX_STARS = 400;

    private struct Spot
    {
        public Vector2 position;
        public Vector2 speed;
        public float inner;
        public float radius;

        // The shader's places for them
        public int positionLoc;
        public int innerLoc;
        public int radiusLoc;
    }

    private struct Star
    {
        public Vector2 position;
        public Vector2 speed;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] spotlight rendering");
        HideCursor();

        Texture2D texRay = LoadTexture("resources/raysan.png");

        Star[] stars = new Star[MAX_STARS];

        for (int n = 0; n < MAX_STARS; n++) ResetStar(ref stars[n]);

        // The stars moved on, so they do not all start in the middle
        for (int m = 0; m < screenWidth/2.0; m++)
        {
            for (int n = 0; n < MAX_STARS; n++) UpdateStar(ref stars[n]);
        }

        int frameCounter = 0;

        // raylib's spotlight.fs, written in Slang, with the engine's own vertex stage
        Shader shdrSpot = LoadShader("resources/shaders/slang/spotlight.slang");

        // Each spot's fields, found by the names GLSL gives them
        Spot[] spots = new Spot[MAX_SPOTS];

        for (int i = 0; i < MAX_SPOTS; i++)
        {
            spots[i].positionLoc = GetShaderLocation(shdrSpot, $"spots[{i}].pos");
            spots[i].innerLoc = GetShaderLocation(shdrSpot, $"spots[{i}].inner");
            spots[i].radiusLoc = GetShaderLocation(shdrSpot, $"spots[{i}].radius");
        }

        // The screen's width, for a dark half and a dim one
        int wLoc = GetShaderLocation(shdrSpot, "screenWidth");
        float sw = (float)GetScreenWidth();
        SetShaderValue(shdrSpot, wLoc, sw);

        // The shader's one value raylib's has no need of, its height, which turns Vulkan's pixel
        // rows to count from the bottom as GLSL's do
        SetShaderValue(shdrSpot, GetShaderLocation(shdrSpot, "screenHeight"), (float)GetScreenHeight());

        // The spots placed and set moving at random
        for (int i = 0; i < MAX_SPOTS; i++)
        {
            spots[i].position.X = (float)GetRandomValue(64, screenWidth - 64);
            spots[i].position.Y = (float)GetRandomValue(64, screenHeight - 64);
            spots[i].speed = Vector2.Zero;

            while ((MathF.Abs(spots[i].speed.X) + MathF.Abs(spots[i].speed.Y)) < 2)
            {
                spots[i].speed.X = GetRandomValue(-400, 40)/25.0f;
                spots[i].speed.Y = GetRandomValue(-400, 40)/25.0f;
            }

            spots[i].inner = 28.0f*(i + 1);
            spots[i].radius = 48.0f*(i + 1);

            SetShaderValue(shdrSpot, spots[i].positionLoc, spots[i].position);
            SetShaderValue(shdrSpot, spots[i].innerLoc, spots[i].inner);
            SetShaderValue(shdrSpot, spots[i].radiusLoc, spots[i].radius);
        }

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            frameCounter++;

            // The stars move on, starting again from the middle when they leave the screen.
            for (int n = 0; n < MAX_STARS; n++) UpdateStar(ref stars[n]);

            // The first spot follows the pointer and the rest bounce, each given to the shader.
            for (int i = 0; i < MAX_SPOTS; i++)
            {
                if (i == 0)
                {
                    Vector2 mp = GetMousePosition();
                    spots[i].position.X = mp.X;
                    spots[i].position.Y = screenHeight - mp.Y;
                }
                else
                {
                    spots[i].position.X += spots[i].speed.X;
                    spots[i].position.Y += spots[i].speed.Y;

                    if (spots[i].position.X < 64) spots[i].speed.X = -spots[i].speed.X;
                    if (spots[i].position.X > (screenWidth - 64)) spots[i].speed.X = -spots[i].speed.X;
                    if (spots[i].position.Y < 64) spots[i].speed.Y = -spots[i].speed.Y;
                    if (spots[i].position.Y > (screenHeight - 64)) spots[i].speed.Y = -spots[i].speed.Y;
                }

                SetShaderValue(shdrSpot, spots[i].positionLoc, spots[i].position);
            }

            BeginDrawing();

                ClearBackground(Color.DarkBlue);

                // The stars, two pixels wide, a pixel being too small to see
                for (int n = 0; n < MAX_STARS; n++)
                {
                    DrawRectangle((int)stars[n].position.X, (int)stars[n].position.Y, 2, 2, Color.White);
                }

                for (int i = 0; i < 16; i++)
                {
                    DrawTexture(texRay,
                        (int)((screenWidth/2.0f) + MathF.Cos((frameCounter + i*8)/51.45f)*(screenWidth/2.2f) - 32),
                        (int)((screenHeight/2.0f) + MathF.Sin((frameCounter + i*8)/17.87f)*(screenHeight/4.2f)), Color.White);
                }

                // The darkness with its spots, drawn over a white rectangle. A render texture of
                // the screen could be drawn here instead, with the shader taking its color.
                BeginShaderMode(shdrSpot);
                    DrawRectangle(0, 0, screenWidth, screenHeight, Color.White);
                EndShaderMode();

                DrawFPS(10, 10);

                DrawText("Move the mouse!", 10, 30, 20, Color.Green);
                DrawText("Pitch Black", (int)(screenWidth*0.2f), screenHeight/2, 20, Color.Green);
                DrawText("Dark", (int)(screenWidth*.66f), screenHeight/2, 20, Color.Green);

            EndDrawing();
        }

        UnloadTexture(texRay);
        UnloadShader(shdrSpot);

        CloseWindow();
    }

    private static void ResetStar(ref Star star)
    {
        star.position = new Vector2(GetScreenWidth()/2.0f, GetScreenHeight()/2.0f);

        star.speed.X = (float)GetRandomValue(-1000, 1000)/100.0f;
        star.speed.Y = (float)GetRandomValue(-1000, 1000)/100.0f;

        // raylib's test adds |x| to whether |y| is above 1, so it tries again only while x is 0
        // and y is small, which this keeps.
        while (!((MathF.Abs(star.speed.X) + (MathF.Abs(star.speed.Y) > 1 ? 1 : 0)) != 0))
        {
            star.speed.X = (float)GetRandomValue(-1000, 1000)/100.0f;
            star.speed.Y = (float)GetRandomValue(-1000, 1000)/100.0f;
        }

        star.position += star.speed*new Vector2(8.0f, 8.0f);
    }

    private static void UpdateStar(ref Star star)
    {
        star.position += star.speed;

        if ((star.position.X < 0) || (star.position.X > GetScreenWidth()) ||
            (star.position.Y < 0) || (star.position.Y > GetScreenHeight())) ResetStar(ref star);
    }
}
