// raylib's textures_srcrec_dstrec example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesSrcrecDstrec
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] srcrec dstrec");

        Texture2D scarfy = LoadTexture("resources/scarfy.png");

        // Pixel art, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(scarfy, TextureFilter.Point);

        int frameWidth = scarfy.Width/6;
        int frameHeight = scarfy.Height;

        // The part of the texture drawn
        Rectangle sourceRec = new(0.0f, 0.0f, (float)frameWidth, (float)frameHeight);

        // Where on the screen it is drawn, scaled to fit
        Rectangle destRec = new(screenWidth/2.0f, screenHeight/2.0f, frameWidth*2.0f, frameHeight*2.0f);

        // The point it turns and scales about, in the destination's size
        Vector2 origin = new((float)frameWidth, (float)frameHeight);

        int rotation = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            rotation++;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexturePro(scarfy, sourceRec, destRec, origin, (float)rotation, Color.White);

                DrawLine((int)destRec.X, 0, (int)destRec.X, screenHeight, Color.Gray);
                DrawLine(0, (int)destRec.Y, screenWidth, (int)destRec.Y, Color.Gray);

                DrawText("(c) Scarfy sprite by Eiden Marsal", screenWidth - 200, screenHeight - 20, 10, Color.Gray);

            EndDrawing();
        }

        UnloadTexture(scarfy);

        CloseWindow();
    }
}
