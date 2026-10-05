// raylib's shaders_ascii_rendering example, Copyright (c) 2025 Maicon Santana (@maiconpintoabreu), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersAsciiRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] ascii rendering");

        // A picture that stays,
        Texture2D fudesumi = LoadTexture("resources/fudesumi.png");

        // and one that moves
        Texture2D raysan = LoadTexture("resources/raysan.png");

        // raylib's ascii.fs, written in Slang, which the scene is drawn through afterward
        Shader shader = LoadShader("resources/shaders/slang/ascii.slang");

        int resolutionLoc = GetShaderLocation(shader, "resolution");
        int fontSizeLoc = GetShaderLocation(shader, "fontSize");

        // The characters' size, 9 at least
        float fontSize = 9.0f;

        Vector2 resolution = new((float)screenWidth, (float)screenHeight);
        SetShaderValue(shader, resolutionLoc, resolution);

        Vector2 circlePos = new(40.0f, (float)screenHeight*0.5f);
        float circleSpeed = 1.0f;

        // The scene, drawn here before the shader draws it again
        RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            circlePos.X += circleSpeed;

            if ((circlePos.X > 200.0f) || (circlePos.X < 40.0f)) circleSpeed *= -1;

            if (IsKeyPressed(Key.Left) && (fontSize > 9.0)) fontSize -= 1;
            if (IsKeyPressed(Key.Right) && (fontSize < 15.0)) fontSize += 1;

            SetShaderValue(shader, fontSizeLoc, fontSize);

            BeginTextureMode(target);

                ClearBackground(Color.White);

                DrawTexture(fudesumi, 500, -30, Color.White);
                DrawTextureV(raysan, circlePos, Color.White);

            EndTextureMode();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // The scene through the shader, every pixel of it. A target is stored the right way
                // up here, so it is drawn with its height as it is, and the shader counts its
                // characters' rows as raylib's turned target has them.
                BeginShaderMode(shader);
                    DrawTextureRec(target.Texture, new Rectangle(0, 0, (float)target.Texture.Width, (float)target.Texture.Height), Vector2.Zero, Color.White);
                EndShaderMode();

                DrawRectangle(0, 0, screenWidth, 40, Color.Black);
                DrawText($"Ascii effect - FontSize:{fontSize,2:0} - [Left] -1 [Right] +1 ", 120, 10, 20, Color.LightGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadRenderTexture(target);
        UnloadShader(shader);

        UnloadTexture(fudesumi);
        UnloadTexture(raysan);

        CloseWindow();
    }
}
