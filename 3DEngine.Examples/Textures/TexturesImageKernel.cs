// raylib's textures_image_kernel example, Copyright (c) 2015-2025 Karim Salem (@kimo-s), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageKernel
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] image kernel");

        Image image = LoadImage("resources/cat.png");

        float[] gaussiankernel =
        [
            1.0f, 2.0f, 1.0f,
            2.0f, 4.0f, 2.0f,
            1.0f, 2.0f, 1.0f,
        ];

        float[] sobelkernel =
        [
            1.0f, 0.0f, -1.0f,
            2.0f, 0.0f, -2.0f,
            1.0f, 0.0f, -1.0f,
        ];

        float[] sharpenkernel =
        [
            0.0f, -1.0f, 0.0f,
           -1.0f, 5.0f, -1.0f,
            0.0f, -1.0f, 0.0f,
        ];

        NormalizeKernel(gaussiankernel);
        NormalizeKernel(sharpenkernel);
        NormalizeKernel(sobelkernel);

        Image catSharpend = ImageCopy(image);
        ImageKernelConvolution(ref catSharpend, sharpenkernel);

        Image catSobel = ImageCopy(image);
        ImageKernelConvolution(ref catSobel, sobelkernel);

        Image catGaussian = ImageCopy(image);

        for (int i = 0; i < 6; i++)
        {
            ImageKernelConvolution(ref catGaussian, gaussiankernel);
        }

        ImageCrop(ref image, new Rectangle(0, 0, (float)200, (float)450));
        ImageCrop(ref catGaussian, new Rectangle(0, 0, (float)200, (float)450));
        ImageCrop(ref catSobel, new Rectangle(0, 0, (float)200, (float)450));
        ImageCrop(ref catSharpend, new Rectangle(0, 0, (float)200, (float)450));

        Texture2D texture = LoadTextureFromImage(image);
        Texture2D catSharpendTexture = LoadTextureFromImage(catSharpend);
        Texture2D catSobelTexture = LoadTextureFromImage(catSobel);
        Texture2D catGaussianTexture = LoadTextureFromImage(catGaussian);

        UnloadImage(image);
        UnloadImage(catGaussian);
        UnloadImage(catSobel);
        UnloadImage(catSharpend);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(catSharpendTexture, 0, 0, Color.White);
                DrawTexture(catSobelTexture, 200, 0, Color.White);
                DrawTexture(catGaussianTexture, 400, 0, Color.White);
                DrawTexture(texture, 600, 0, Color.White);

            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadTexture(catGaussianTexture);
        UnloadTexture(catSobelTexture);
        UnloadTexture(catSharpendTexture);

        CloseWindow();
    }

    // Divides a kernel by the sum of its weights, where they sum to anything but zero.
    private static void NormalizeKernel(float[] kernel)
    {
        float sum = kernel.Sum();

        if (sum != 0.0f)
        {
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
        }
    }
}
