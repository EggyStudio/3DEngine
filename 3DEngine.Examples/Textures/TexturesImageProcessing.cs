// raylib's textures_image_processing example, Copyright (c) 2016-2025 Ramon Santamaria (@raysan5),
// under the zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageProcessing
{
    private const int NUM_PROCESSES = 9;

    private enum ImageProcess
    {
        NONE = 0,
        COLOR_GRAYSCALE,
        COLOR_TINT,
        COLOR_INVERT,
        COLOR_CONTRAST,
        COLOR_BRIGHTNESS,
        GAUSSIAN_BLUR,
        FLIP_VERTICAL,
        FLIP_HORIZONTAL
    }

    private static readonly string[] processText =
    [
        "NO PROCESSING",
        "COLOR GRAYSCALE",
        "COLOR TINT",
        "COLOR INVERT",
        "COLOR CONTRAST",
        "COLOR BRIGHTNESS",
        "GAUSSIAN BLUR",
        "FLIP VERTICAL",
        "FLIP HORIZONTAL"
    ];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] image processing");

        Image imOrigin = LoadImage("resources/parrots.png");   // Loaded in CPU memory (RAM)
        ImageFormat(ref imOrigin, PixelFormat.UncompressedR8G8B8A8);         // Format image to RGBA 32bit (required for texture update) <-- ISSUE
        Texture2D texture = LoadTextureFromImage(imOrigin);    // Image converted to texture, GPU memory (VRAM)

        Image imCopy = ImageCopy(imOrigin);

        ImageProcess currentProcess = ImageProcess.NONE;
        bool textureReload = false;

        Rectangle[] toggleRecs = new Rectangle[NUM_PROCESSES];
        int mouseHoverRec = -1;

        for (int i = 0; i < NUM_PROCESSES; i++) toggleRecs[i] = new Rectangle(40.0f, (float)(50 + 32*i), 150.0f, 30.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Mouse toggle group logic
            for (int i = 0; i < NUM_PROCESSES; i++)
            {
                if (CheckCollisionPointRec(GetMousePosition(), toggleRecs[i]))
                {
                    mouseHoverRec = i;

                    if (IsMouseButtonReleased(MouseButton.Left))
                    {
                        currentProcess = (ImageProcess)i;
                        textureReload = true;
                    }
                    break;
                }
                else mouseHoverRec = -1;
            }

            // Keyboard toggle group logic
            if (IsKeyPressed(Key.Down))
            {
                currentProcess++;
                if ((int)currentProcess > (NUM_PROCESSES - 1)) currentProcess = 0;
                textureReload = true;
            }
            else if (IsKeyPressed(Key.Up))
            {
                currentProcess--;
                if (currentProcess < 0) currentProcess = (ImageProcess)(NUM_PROCESSES - 1);
                textureReload = true;
            }

            // Reload texture when required
            if (textureReload)
            {
                UnloadImage(imCopy);                // Unload image-copy data
                imCopy = ImageCopy(imOrigin);     // Restore image-copy from image-origin

                // NOTE: Image processing is a costly CPU process to be done every frame,
                // If image processing is required in a frame-basis, it should be done
                // with a texture and by shaders
                switch (currentProcess)
                {
                    case ImageProcess.COLOR_GRAYSCALE: ImageColorGrayscale(ref imCopy); break;
                    case ImageProcess.COLOR_TINT: ImageColorTint(ref imCopy, Color.Green); break;
                    case ImageProcess.COLOR_INVERT: ImageColorInvert(ref imCopy); break;
                    case ImageProcess.COLOR_CONTRAST: ImageColorContrast(ref imCopy, -40); break;
                    case ImageProcess.COLOR_BRIGHTNESS: ImageColorBrightness(ref imCopy, -80); break;
                    case ImageProcess.GAUSSIAN_BLUR: ImageBlurGaussian(ref imCopy, 10); break;
                    case ImageProcess.FLIP_VERTICAL: ImageFlipVertical(ref imCopy); break;
                    case ImageProcess.FLIP_HORIZONTAL: ImageFlipHorizontal(ref imCopy); break;
                    default: break;
                }

                // Update texture with new image data, the image's own pixels, which are its colors
                UpdateTexture(texture, imCopy);

                textureReload = false;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("IMAGE PROCESSING:", 40, 30, 10, Color.DarkGray);

                // Draw rectangles
                for (int i = 0; i < NUM_PROCESSES; i++)
                {
                    bool active = (i == (int)currentProcess) || (i == mouseHoverRec);
                    DrawRectangleRec(toggleRecs[i], active ? Color.SkyBlue : Color.LightGray);
                    DrawRectangleLines((int)toggleRecs[i].X, (int) toggleRecs[i].Y, (int) toggleRecs[i].Width, (int) toggleRecs[i].Height, active ? Color.Blue : Color.Gray);
                    DrawText( processText[i], (int)( toggleRecs[i].X + toggleRecs[i].Width/2 - (float)MeasureText(processText[i], 10)/2), (int) toggleRecs[i].Y + 11, 10, active ? Color.DarkBlue : Color.DarkGray);
                }

                DrawTexture(texture, screenWidth - texture.Width - 60, screenHeight/2 - texture.Height/2, Color.White);
                DrawRectangleLines(screenWidth - texture.Width - 60, screenHeight/2 - texture.Height/2, texture.Width, texture.Height, Color.Black);

            EndDrawing();
        }

        UnloadTexture(texture);       // Unload texture from VRAM
        UnloadImage(imOrigin);        // Unload image-origin from RAM
        UnloadImage(imCopy);          // Unload image-copy from RAM

        CloseWindow();
    }
}
