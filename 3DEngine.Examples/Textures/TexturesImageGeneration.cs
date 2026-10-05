// raylib's textures_image_generation example, Copyright (c) 2017-2025 Wilhem Barbier (@nounoursheureux) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageGeneration
{
    // Eight ways of making an image, two of them used twice
    private const int NUM_TEXTURES = 9;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] image generation");

        Image verticalGradient = GenImageGradientLinear(screenWidth, screenHeight, 0, Color.Red, Color.Blue);
        Image horizontalGradient = GenImageGradientLinear(screenWidth, screenHeight, 90, Color.Red, Color.Blue);
        Image diagonalGradient = GenImageGradientLinear(screenWidth, screenHeight, 45, Color.Red, Color.Blue);
        Image radialGradient = GenImageGradientRadial(screenWidth, screenHeight, 0.0f, Color.White, Color.Black);
        Image squareGradient = GenImageGradientSquare(screenWidth, screenHeight, 0.0f, Color.White, Color.Black);
        Image @checked = GenImageChecked(screenWidth, screenHeight, 32, 32, Color.Red, Color.Blue);
        Image whiteNoise = GenImageWhiteNoise(screenWidth, screenHeight, 0.5f);
        Image perlinNoise = GenImagePerlinNoise(screenWidth, screenHeight, 50, 50, 4.0f);
        Image cellular = GenImageCellular(screenWidth, screenHeight, 32);

        Texture2D[] textures =
        [
            LoadTextureFromImage(verticalGradient),
            LoadTextureFromImage(horizontalGradient),
            LoadTextureFromImage(diagonalGradient),
            LoadTextureFromImage(radialGradient),
            LoadTextureFromImage(squareGradient),
            LoadTextureFromImage(@checked),
            LoadTextureFromImage(whiteNoise),
            LoadTextureFromImage(perlinNoise),
            LoadTextureFromImage(cellular),
        ];

        UnloadImage(verticalGradient);
        UnloadImage(horizontalGradient);
        UnloadImage(diagonalGradient);
        UnloadImage(radialGradient);
        UnloadImage(squareGradient);
        UnloadImage(@checked);
        UnloadImage(whiteNoise);
        UnloadImage(perlinNoise);
        UnloadImage(cellular);

        int currentTexture = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonPressed(MouseButton.Left) || IsKeyPressed(Key.Right))
            {
                currentTexture = (currentTexture + 1)%NUM_TEXTURES;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(textures[currentTexture], 0, 0, Color.White);

                DrawRectangle(30, 400, 325, 30, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(30, 400, 325, 30, Fade(Color.White, 0.5f));
                DrawText("MOUSE LEFT BUTTON to CYCLE PROCEDURAL TEXTURES", 40, 410, 10, Color.White);

                switch (currentTexture)
                {
                    case 0: DrawText("VERTICAL GRADIENT", 560, 10, 20, Color.RayWhite); break;
                    case 1: DrawText("HORIZONTAL GRADIENT", 540, 10, 20, Color.RayWhite); break;
                    case 2: DrawText("DIAGONAL GRADIENT", 540, 10, 20, Color.RayWhite); break;
                    case 3: DrawText("RADIAL GRADIENT", 580, 10, 20, Color.LightGray); break;
                    case 4: DrawText("SQUARE GRADIENT", 580, 10, 20, Color.LightGray); break;
                    case 5: DrawText("CHECKED", 680, 10, 20, Color.RayWhite); break;
                    case 6: DrawText("WHITE NOISE", 640, 10, 20, Color.Red); break;
                    case 7: DrawText("PERLIN NOISE", 640, 10, 20, Color.Red); break;
                    case 8: DrawText("CELLULAR", 670, 10, 20, Color.RayWhite); break;
                    default: break;
                }

            EndDrawing();
        }

        for (int i = 0; i < NUM_TEXTURES; i++) UnloadTexture(textures[i]);

        CloseWindow();
    }
}
