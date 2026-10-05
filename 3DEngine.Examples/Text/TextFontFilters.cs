// raylib's text_font_filters example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextFontFilters
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] font filters");

        const string msg = "Loaded Font";

        Font font = LoadFontEx("resources/KAISG.ttf", 96);

        // Mipmaps for trilinear filtering, which drawn in 2D looks as bilinear does. The font's atlas
        // is a property, so its levels are made through a copy of the handle, on the same texture.
        Texture2D atlas = font.Texture;
        GenTextureMipmaps(ref atlas);

        float fontSize = (float)font.BaseSize;
        Vector2 fontPosition = new(40.0f, screenHeight/2.0f - 80.0f);
        Vector2 textSize = Vector2.Zero;

        SetTextureFilter(font.Texture, TextureFilter.Point);
        int currentFontFilter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            fontSize += GetMouseWheelMove()*4.0f;

            if (IsKeyPressed(Key.Alpha1))
            {
                SetTextureFilter(font.Texture, TextureFilter.Point);
                currentFontFilter = 0;
            }
            else if (IsKeyPressed(Key.Alpha2))
            {
                SetTextureFilter(font.Texture, TextureFilter.Bilinear);
                currentFontFilter = 1;
            }
            else if (IsKeyPressed(Key.Alpha3))
            {
                SetTextureFilter(font.Texture, TextureFilter.Trilinear);
                currentFontFilter = 2;
            }

            textSize = MeasureTextEx(font, msg, fontSize, 0);

            if (IsKeyDown(Key.Left)) fontPosition.X -= 10;
            else if (IsKeyDown(Key.Right)) fontPosition.X += 10;

            // A TrueType file dropped on the window is loaded at the size drawn, the first only.
            if (IsFileDropped())
            {
                string[] droppedFiles = LoadDroppedFiles();

                if (Path.GetExtension(droppedFiles[0]).Equals(".ttf", StringComparison.OrdinalIgnoreCase))
                {
                    UnloadFont(font);
                    font = LoadFontEx(droppedFiles[0], (int)fontSize);
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("Use mouse wheel to change font size", 20, 20, 10, Color.Gray);
                DrawText("Use KEY_RIGHT and KEY_LEFT to move text", 20, 40, 10, Color.Gray);
                DrawText("Use 1, 2, 3 to change texture filter", 20, 60, 10, Color.Gray);
                DrawText("Drop a new TTF font for dynamic loading", 20, 80, 10, Color.DarkGray);

                DrawTextEx(font, msg, fontPosition, fontSize, 0, Color.Black);

                DrawRectangle(0, screenHeight - 80, screenWidth, 80, Color.LightGray);
                DrawText($"Font size: {fontSize:0.00}", 20, screenHeight - 50, 10, Color.DarkGray);
                DrawText($"Text size: [{textSize.X:0.00}, {textSize.Y:0.00}]", 20, screenHeight - 30, 10, Color.DarkGray);
                DrawText("CURRENT TEXTURE FILTER:", 250, 400, 20, Color.Gray);

                if (currentFontFilter == 0) DrawText("POINT", 570, 400, 20, Color.Black);
                else if (currentFontFilter == 1) DrawText("BILINEAR", 570, 400, 20, Color.Black);
                else if (currentFontFilter == 2) DrawText("TRILINEAR", 570, 400, 20, Color.Black);

            EndDrawing();
        }

        UnloadFont(font);

        CloseWindow();
    }
}
