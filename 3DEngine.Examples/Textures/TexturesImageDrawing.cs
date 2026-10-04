using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageDrawing
{
    public static void Run()
    {
        InitWindow(800, 450, "[textures] image drawing");

        // An image is pixels in memory. It is edited on the CPU and uploaded once as a texture.
        var image = GenImageGradientLinear(400, 300, 45, Color.DarkBlue, Color.SkyBlue);
        ImageDrawRectangleLines(ref image, new Rectangle(0, 0, 400, 300), 4, Color.RayWhite);
        ImageDrawCircle(ref image, 300, 90, 50, Color.Gold);
        ImageDrawCircleLines(ref image, 300, 90, 60, Color.Orange);
        ImageDrawRectangle(ref image, 40, 200, 140, 60, Color.Maroon);
        for (int i = 0; i < 10; i++)
            ImageDrawLine(ref image, 20, 20 + i * 12, 220, 120 - i * 8, Color.Lime);

        // Another image, made smaller, turned and tinted, drawn into the first with its alpha.
        var logo = LoadImage("resources/logo.png");
        ImageResize(ref logo, 96, 96);
        ImageRotateCW(ref logo);
        ImageColorTint(ref logo, Color.RayWhite.Fade(0.85f));
        ImageDraw(ref image, logo, new Rectangle(0, 0, logo.Width, logo.Height), new Rectangle(230, 170, 120, 120), Color.White);

        // The same picture, cropped, mirrored and adjusted, beside it.
        var detail = ImageFromImage(image, new Rectangle(200, 20, 200, 160));
        ImageFlipHorizontal(ref detail);
        ImageColorGrayscale(ref detail);
        ImageColorContrast(ref detail, 40);
        ImageResizeNN(ref detail, 300, 240);

        var texture = LoadTextureFromImage(image);
        var detailTexture = LoadTextureFromImage(detail);
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);
            DrawTexture(texture, 40, 90, Color.White);
            DrawTexture(detailTexture, 470, 120, Color.White);
            DrawText("Drawn into images on the CPU, then uploaded as textures.", 40, 40, 20, Color.DarkGray);
            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadTexture(detailTexture);
        CloseWindow();
    }
}
