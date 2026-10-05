// raylib's textures_clipboard_image example, Copyright (c) 2026 Maicon Santana (@maiconpintoabreu), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesClipboardImage
{
    private const int MAX_TEXTURE_COLLECTION = 20;

    private struct TextureCollection
    {
        public Texture2D texture;
        public Vector2 position;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] clipboard image");

        TextureCollection[] collection = new TextureCollection[MAX_TEXTURE_COLLECTION];
        int currentCollectionIndex = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.R))
            {
                for (int i = 0; i < MAX_TEXTURE_COLLECTION; i++) UnloadTexture(collection[i].texture);

                currentCollectionIndex = 0;
            }

            if (IsKeyDown(Key.LCtrl) && IsKeyPressed(Key.V) && (currentCollectionIndex < MAX_TEXTURE_COLLECTION))
            {
                Image image = GetClipboardImage();

                if (IsImageValid(image))
                {
                    collection[currentCollectionIndex].texture = LoadTextureFromImage(image);
                    collection[currentCollectionIndex].position = GetMousePosition();
                    currentCollectionIndex++;
                    UnloadImage(image);
                }
                else TraceLog(LogLevel.Info, "IMAGE: Could not retrieve image from clipboard");
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < currentCollectionIndex; i++)
                {
                    if (IsTextureValid(collection[i].texture))
                    {
                        DrawTexturePro(collection[i].texture,
                            new Rectangle(0, 0, (float)collection[i].texture.Width, (float)collection[i].texture.Height),
                            new Rectangle(collection[i].position.X, collection[i].position.Y, (float)collection[i].texture.Width, (float)collection[i].texture.Height),
                            new Vector2(collection[i].texture.Width*0.5f, collection[i].texture.Height*0.5f),
                            0.0f, Color.White);
                    }
                }

                DrawRectangle(0, 0, screenWidth, 40, Color.Black);
                DrawText("Clipboard Image - Ctrl+V to Paste and R to Reset ", 120, 10, 20, Color.LightGray);

            EndDrawing();
        }

        for (int i = 0; i < MAX_TEXTURE_COLLECTION; i++) UnloadTexture(collection[i].texture);

        CloseWindow();
    }
}
