// raylib's textures_background_scrolling example, Copyright (c) 2019-2025 Ramon Santamaria (@raysan5),
// under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesBackgroundScrolling
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] background scrolling");

        // Each layer is at least as wide as the screen, so two copies of it cover the scroll.
        Texture2D background = LoadTexture("resources/cyberpunk_street_background.png");
        Texture2D midground = LoadTexture("resources/cyberpunk_street_midground.png");
        Texture2D foreground = LoadTexture("resources/cyberpunk_street_foreground.png");

        // Pixel art drawn at twice its size, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(background, TextureFilter.Point);
        SetTextureFilter(midground, TextureFilter.Point);
        SetTextureFilter(foreground, TextureFilter.Point);

        float scrollingBack = 0.0f;
        float scrollingMid = 0.0f;
        float scrollingFore = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            scrollingBack -= 0.1f;
            scrollingMid -= 0.5f;
            scrollingFore -= 1.0f;

            // Drawn at twice their size, so they scroll twice their width.
            if (scrollingBack <= -background.Width*2) scrollingBack = 0;
            if (scrollingMid <= -midground.Width*2) scrollingMid = 0;
            if (scrollingFore <= -foreground.Width*2) scrollingFore = 0;

            BeginDrawing();

                ClearBackground(GetColor(0x052c46ff));

                DrawTextureEx(background, new Vector2(scrollingBack, 20), 0.0f, 2.0f, Color.White);
                DrawTextureEx(background, new Vector2(background.Width*2 + scrollingBack, 20), 0.0f, 2.0f, Color.White);

                DrawTextureEx(midground, new Vector2(scrollingMid, 20), 0.0f, 2.0f, Color.White);
                DrawTextureEx(midground, new Vector2(midground.Width*2 + scrollingMid, 20), 0.0f, 2.0f, Color.White);

                DrawTextureEx(foreground, new Vector2(scrollingFore, 70), 0.0f, 2.0f, Color.White);
                DrawTextureEx(foreground, new Vector2(foreground.Width*2 + scrollingFore, 70), 0.0f, 2.0f, Color.White);

                DrawText("BACKGROUND SCROLLING & PARALLAX", 10, 10, 20, Color.Red);
                DrawText("(c) Cyberpunk Street Environment by Luis Zuno (@ansimuz)", screenWidth - 330, screenHeight - 20, 10, Color.RayWhite);

            EndDrawing();
        }

        UnloadTexture(background);
        UnloadTexture(midground);
        UnloadTexture(foreground);

        CloseWindow();
    }
}
