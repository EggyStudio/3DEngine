// raylib's textures_blend_modes example, Copyright (c) 2020-2025 Karlo Licudine (@accidentalrebel), under
// the zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesBlendModes
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] blend modes");

        Image bgImage = LoadImage("resources/cyberpunk_street_background.png");
        Texture2D bgTexture = LoadTextureFromImage(bgImage);

        Image fgImage = LoadImage("resources/cyberpunk_street_foreground.png");
        Texture2D fgTexture = LoadTextureFromImage(fgImage);

        UnloadImage(bgImage);
        UnloadImage(fgImage);

        const int blendCountMax = 4;
        BlendMode blendMode = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space))
            {
                if ((int)blendMode >= (blendCountMax - 1)) blendMode = 0;
                else blendMode++;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(bgTexture, screenWidth/2 - bgTexture.Width/2, screenHeight/2 - bgTexture.Height/2, Color.White);

                // The foreground drawn in the blend mode
                BeginBlendMode(blendMode);
                    DrawTexture(fgTexture, screenWidth/2 - fgTexture.Width/2, screenHeight/2 - fgTexture.Height/2, Color.White);
                EndBlendMode();

                DrawText("Press SPACE to change blend modes.", 310, 350, 10, Color.Gray);

                switch (blendMode)
                {
                    case BlendMode.Alpha: DrawText("Current: BLEND_ALPHA", (screenWidth/2) - 60, 370, 10, Color.Gray); break;
                    case BlendMode.Additive: DrawText("Current: BLEND_ADDITIVE", (screenWidth/2) - 60, 370, 10, Color.Gray); break;
                    case BlendMode.Multiplied: DrawText("Current: BLEND_MULTIPLIED", (screenWidth/2) - 60, 370, 10, Color.Gray); break;
                    case BlendMode.AddColors: DrawText("Current: BLEND_ADD_COLORS", (screenWidth/2) - 60, 370, 10, Color.Gray); break;
                    default: break;
                }

                DrawText("(c) Cyberpunk Street Environment by Luis Zuno (@ansimuz)", screenWidth - 330, screenHeight - 20, 10, Color.Gray);

            EndDrawing();
        }

        UnloadTexture(fgTexture);
        UnloadTexture(bgTexture);

        CloseWindow();
    }
}
