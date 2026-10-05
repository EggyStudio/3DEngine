// raylib's textures_tiled_drawing example, Copyright (c) 2020-2025 Vlad Adrian (@demizdor) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesTiledDrawing
{
    private const int OPT_WIDTH = 220;      // The options' width at most
    private const int MARGIN_SIZE = 8;
    private const int COLOR_SIZE = 16;      // The color buttons' size

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.WindowResizable);
        InitWindow(screenWidth, screenHeight, "[textures] tiled drawing");

        Texture2D texPattern = LoadTexture("resources/patterns.png");
        SetTextureFilter(texPattern, TextureFilter.Bilinear);

        // Where each pattern lies in the texture
        Rectangle[] recPattern =
        [
            new(3, 3, 66, 66),
            new(75, 3, 100, 100),
            new(3, 75, 66, 66),
            new(7, 156, 50, 50),
            new(85, 106, 90, 45),
            new(75, 154, 100, 60),
        ];

        Color[] colors = [Color.Black, Color.Maroon, Color.Orange, Color.Blue, Color.Purple, Color.Beige, Color.Lime, Color.Red, Color.DarkGray, Color.SkyBlue];
        int MAX_COLORS = colors.Length;
        Rectangle[] colorRec = new Rectangle[MAX_COLORS];

        // Each color's button
        for (int i = 0, x = 0, y = 0; i < MAX_COLORS; i++)
        {
            colorRec[i] = new Rectangle(2.0f + MARGIN_SIZE + x, 22.0f + 256.0f + MARGIN_SIZE + y, COLOR_SIZE*2.0f, (float)COLOR_SIZE);

            if (i == (MAX_COLORS/2 - 1))
            {
                x = 0;
                y += COLOR_SIZE + MARGIN_SIZE;
            }
            else x += (COLOR_SIZE*2 + MARGIN_SIZE);
        }

        int activePattern = 0, activeCol = 0;
        float scale = 1.0f, rotation = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonPressed(MouseButton.Left))
            {
                Vector2 mouse = GetMousePosition();

                // The pattern clicked becomes the one drawn,
                for (int i = 0; i < recPattern.Length; i++)
                {
                    if (CheckCollisionPointRec(mouse, new Rectangle(2 + MARGIN_SIZE + recPattern[i].X, 40 + MARGIN_SIZE + recPattern[i].Y, recPattern[i].Width, recPattern[i].Height)))
                    {
                        activePattern = i;
                        break;
                    }
                }

                // and the color clicked its tint.
                for (int i = 0; i < MAX_COLORS; i++)
                {
                    if (CheckCollisionPointRec(mouse, colorRec[i]))
                    {
                        activeCol = i;
                        break;
                    }
                }
            }

            if (IsKeyPressed(Key.Up)) scale += 0.25f;
            if (IsKeyPressed(Key.Down)) scale -= 0.25f;
            if (scale > 10.0f) scale = 10.0f;
            else if (scale <= 0.0f) scale = 0.25f;

            if (IsKeyPressed(Key.Left)) rotation -= 25.0f;
            if (IsKeyPressed(Key.Right)) rotation += 25.0f;

            if (IsKeyPressed(Key.Space)) { rotation = 0.0f; scale = 1.0f; }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // The tiled area
                DrawTextureTiled(texPattern, recPattern[activePattern], new Rectangle((float)OPT_WIDTH + MARGIN_SIZE, (float)MARGIN_SIZE, GetScreenWidth() - OPT_WIDTH - 2.0f*MARGIN_SIZE, GetScreenHeight() - 2.0f*MARGIN_SIZE),
                    new Vector2(0.0f, 0.0f), rotation, scale, colors[activeCol]);

                // The options
                DrawRectangle(MARGIN_SIZE, MARGIN_SIZE, OPT_WIDTH - MARGIN_SIZE, GetScreenHeight() - 2*MARGIN_SIZE, ColorAlpha(Color.LightGray, 0.5f));

                DrawText("Select Pattern", 2 + MARGIN_SIZE, 30 + MARGIN_SIZE, 10, Color.Black);
                DrawTexture(texPattern, 2 + MARGIN_SIZE, 40 + MARGIN_SIZE, Color.Black);
                DrawRectangle(2 + MARGIN_SIZE + (int)recPattern[activePattern].X, 40 + MARGIN_SIZE + (int)recPattern[activePattern].Y, (int)recPattern[activePattern].Width, (int)recPattern[activePattern].Height, ColorAlpha(Color.DarkBlue, 0.3f));

                DrawText("Select Color", 2 + MARGIN_SIZE, 10 + 256 + MARGIN_SIZE, 10, Color.Black);
                for (int i = 0; i < MAX_COLORS; i++)
                {
                    DrawRectangleRec(colorRec[i], colors[i]);
                    if (activeCol == i) DrawRectangleLinesEx(colorRec[i], 3, ColorAlpha(Color.White, 0.5f));
                }

                DrawText("Scale (UP/DOWN to change)", 2 + MARGIN_SIZE, 80 + 256 + MARGIN_SIZE, 10, Color.Black);
                DrawText($"{scale:0.00}x", 2 + MARGIN_SIZE, 92 + 256 + MARGIN_SIZE, 20, Color.Black);

                DrawText("Rotation (LEFT/RIGHT to change)", 2 + MARGIN_SIZE, 122 + 256 + MARGIN_SIZE, 10, Color.Black);
                DrawText($"{rotation:0} degrees", 2 + MARGIN_SIZE, 134 + 256 + MARGIN_SIZE, 20, Color.Black);

                DrawText("Press [SPACE] to reset", 2 + MARGIN_SIZE, 164 + 256 + MARGIN_SIZE, 10, Color.DarkBlue);

                DrawText($"{GetFPS()} FPS", 2 + MARGIN_SIZE, 2 + MARGIN_SIZE, 20, Color.Black);

            EndDrawing();
        }

        UnloadTexture(texPattern);

        CloseWindow();
    }

    // Part of a texture, turned and scaled, tiled across a rectangle, the tiles at its right and
    // bottom edges cut to fit. raylib's example has this as a function of its own.
    private static void DrawTextureTiled(Texture2D texture, Rectangle source, Rectangle dest, Vector2 origin, float rotation, float scale, Color tint)
    {
        // A scale of zero would tile forever.
        if (!texture.IsValid || (scale <= 0.0f)) return;
        if ((source.Width == 0) || (source.Height == 0)) return;

        int tileWidth = (int)(source.Width*scale), tileHeight = (int)(source.Height*scale);
        if ((dest.Width < tileWidth) && (dest.Height < tileHeight))
        {
            // One tile, cut on both sides
            DrawTexturePro(texture, new Rectangle(source.X, source.Y, ((float)dest.Width/tileWidth)*source.Width, ((float)dest.Height/tileHeight)*source.Height),
                new Rectangle(dest.X, dest.Y, dest.Width, dest.Height), origin, rotation, tint);
        }
        else if (dest.Width <= tileWidth)
        {
            // One column
            int dy = 0;
            for (; dy + tileHeight < dest.Height; dy += tileHeight)
            {
                DrawTexturePro(texture, new Rectangle(source.X, source.Y, ((float)dest.Width/tileWidth)*source.Width, source.Height), new Rectangle(dest.X, dest.Y + dy, dest.Width, (float)tileHeight), origin, rotation, tint);
            }

            if (dy < dest.Height)
            {
                DrawTexturePro(texture, new Rectangle(source.X, source.Y, ((float)dest.Width/tileWidth)*source.Width, ((float)(dest.Height - dy)/tileHeight)*source.Height),
                    new Rectangle(dest.X, dest.Y + dy, dest.Width, dest.Height - dy), origin, rotation, tint);
            }
        }
        else if (dest.Height <= tileHeight)
        {
            // One row
            int dx = 0;
            for (; dx + tileWidth < dest.Width; dx += tileWidth)
            {
                DrawTexturePro(texture, new Rectangle(source.X, source.Y, source.Width, ((float)dest.Height/tileHeight)*source.Height), new Rectangle(dest.X + dx, dest.Y, (float)tileWidth, dest.Height), origin, rotation, tint);
            }

            if (dx < dest.Width)
            {
                DrawTexturePro(texture, new Rectangle(source.X, source.Y, ((float)(dest.Width - dx)/tileWidth)*source.Width, ((float)dest.Height/tileHeight)*source.Height),
                    new Rectangle(dest.X + dx, dest.Y, dest.Width - dx, dest.Height), origin, rotation, tint);
            }
        }
        else
        {
            // Rows and columns
            int dx = 0;
            for (; dx + tileWidth < dest.Width; dx += tileWidth)
            {
                int dy = 0;
                for (; dy + tileHeight < dest.Height; dy += tileHeight)
                {
                    DrawTexturePro(texture, source, new Rectangle(dest.X + dx, dest.Y + dy, (float)tileWidth, (float)tileHeight), origin, rotation, tint);
                }

                if (dy < dest.Height)
                {
                    DrawTexturePro(texture, new Rectangle(source.X, source.Y, source.Width, ((float)(dest.Height - dy)/tileHeight)*source.Height),
                        new Rectangle(dest.X + dx, dest.Y + dy, (float)tileWidth, dest.Height - dy), origin, rotation, tint);
                }
            }

            // The last column, cut to fit
            if (dx < dest.Width)
            {
                int dy = 0;
                for (; dy + tileHeight < dest.Height; dy += tileHeight)
                {
                    DrawTexturePro(texture, new Rectangle(source.X, source.Y, ((float)(dest.Width - dx)/tileWidth)*source.Width, source.Height),
                        new Rectangle(dest.X + dx, dest.Y + dy, dest.Width - dx, (float)tileHeight), origin, rotation, tint);
                }

                // and the corner at the bottom right.
                if (dy < dest.Height)
                {
                    DrawTexturePro(texture, new Rectangle(source.X, source.Y, ((float)(dest.Width - dx)/tileWidth)*source.Width, ((float)(dest.Height - dy)/tileHeight)*source.Height),
                        new Rectangle(dest.X + dx, dest.Y + dy, dest.Width - dx, dest.Height - dy), origin, rotation, tint);
                }
            }
        }
    }
}
