// raylib's shaders_texture_outline example, Copyright (c) 2021-2025 Serenity Skiff (@GoldenThumbs) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersTextureOutline
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] texture outline");

        Texture2D texture = LoadTexture("resources/fudesumi.png");

        // raylib's outline.fs, written in Slang
        Shader shdrOutline = LoadShader("resources/shaders/slang/outline.slang");

        float outlineSize = 2.0f;
        Vector4 outlineColor = new(1.0f, 0.0f, 0.0f, 1.0f);     // Red, from 0 to 1
        Vector2 textureSize = new((float)texture.Width, (float)texture.Height);

        int outlineSizeLoc = GetShaderLocation(shdrOutline, "outlineSize");
        int outlineColorLoc = GetShaderLocation(shdrOutline, "outlineColor");
        int textureSizeLoc = GetShaderLocation(shdrOutline, "textureSize");

        // raylib's SetShaderValue with a uniform type is a typed overload here.
        SetShaderValue(shdrOutline, outlineSizeLoc, outlineSize);
        SetShaderValue(shdrOutline, outlineColorLoc, outlineColor);
        SetShaderValue(shdrOutline, textureSizeLoc, textureSize);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            outlineSize += GetMouseWheelMove();
            if (outlineSize < 1.0f) outlineSize = 1.0f;

            SetShaderValue(shdrOutline, outlineSizeLoc, outlineSize);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginShaderMode(shdrOutline);

                    DrawTexture(texture, GetScreenWidth()/2 - texture.Width/2, -30, Color.White);

                EndShaderMode();

                DrawText("Shader-based\ntexture\noutline", 10, 10, 20, Color.Gray);
                DrawText("Scroll mouse wheel to\nchange outline size", 10, 72, 20, Color.Gray);
                DrawText($"Outline size: {(int)outlineSize} px", 10, 120, 20, Color.Maroon);

                DrawFPS(710, 10);

            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadShader(shdrOutline);

        CloseWindow();
    }
}
