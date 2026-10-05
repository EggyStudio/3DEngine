// raylib's core_viewport_scaling example, Copyright (c) 2025 Agnis Aldiņš (@nezvers), under the zlib
// license, written again for the flat API.
//
// A render texture is upright here, where raylib's is read with its height negative to turn it, so
// each source rectangle starts at the top with its height as it is, and what raylib's example shows
// of the source's height it shows without the sign. A texture loads bilinear here and with the point
// filter in raylib, so the target is set to point, as raylib's is without a call.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreViewportScaling
{
    private const int RESOLUTION_COUNT = 4;

    private enum ViewportType
    {
        KEEP_ASPECT_INTEGER,
        KEEP_HEIGHT_INTEGER,
        KEEP_WIDTH_INTEGER,
        KEEP_ASPECT,
        KEEP_HEIGHT,
        KEEP_WIDTH,
        VIEWPORT_TYPE_COUNT,
    }

    private static readonly string[] ViewportTypeNames =
    [
        "KEEP_ASPECT_INTEGER",
        "KEEP_HEIGHT_INTEGER",
        "KEEP_WIDTH_INTEGER",
        "KEEP_ASPECT",
        "KEEP_HEIGHT",
        "KEEP_WIDTH",
    ];

    public static void Run()
    {
        int screenWidth = 800;
        int screenHeight = 450;

        SetConfigFlags(ConfigFlags.WindowResizable);
        InitWindow(screenWidth, screenHeight, "[core] viewport scaling");

        Vector2[] resolutionList =
        [
            new(64, 64),
            new(256, 240),
            new(320, 180),
            new(3840, 2160),
        ];

        int resolutionIndex = 0;
        int gameWidth = 64;
        int gameHeight = 64;

        RenderTexture2D target = default;
        Rectangle sourceRect = default;
        Rectangle destRect = default;

        ViewportType viewportType = ViewportType.KEEP_ASPECT_INTEGER;
        ResizeRenderSize(viewportType, ref screenWidth, ref screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect, ref target);

        Rectangle decreaseResolutionButton = new(200, 30, 10, 10);
        Rectangle increaseResolutionButton = new(215, 30, 10, 10);
        Rectangle decreaseTypeButton = new(200, 45, 10, 10);
        Rectangle increaseTypeButton = new(215, 45, 10, 10);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsWindowResized()) ResizeRenderSize(viewportType, ref screenWidth, ref screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect, ref target);

            Vector2 mousePosition = GetMousePosition();
            bool mousePressed = IsMouseButtonPressed(MouseButton.Left);

            if (CheckCollisionPointRec(mousePosition, decreaseResolutionButton) && mousePressed)
            {
                resolutionIndex = (resolutionIndex + RESOLUTION_COUNT - 1)%RESOLUTION_COUNT;
                gameWidth = (int)resolutionList[resolutionIndex].X;
                gameHeight = (int)resolutionList[resolutionIndex].Y;
                ResizeRenderSize(viewportType, ref screenWidth, ref screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect, ref target);
            }

            if (CheckCollisionPointRec(mousePosition, increaseResolutionButton) && mousePressed)
            {
                resolutionIndex = (resolutionIndex + 1)%RESOLUTION_COUNT;
                gameWidth = (int)resolutionList[resolutionIndex].X;
                gameHeight = (int)resolutionList[resolutionIndex].Y;
                ResizeRenderSize(viewportType, ref screenWidth, ref screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect, ref target);
            }

            if (CheckCollisionPointRec(mousePosition, decreaseTypeButton) && mousePressed)
            {
                viewportType = (ViewportType)(((int)viewportType + (int)ViewportType.VIEWPORT_TYPE_COUNT - 1)%(int)ViewportType.VIEWPORT_TYPE_COUNT);
                ResizeRenderSize(viewportType, ref screenWidth, ref screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect, ref target);
            }

            if (CheckCollisionPointRec(mousePosition, increaseTypeButton) && mousePressed)
            {
                viewportType = (ViewportType)(((int)viewportType + 1)%(int)ViewportType.VIEWPORT_TYPE_COUNT);
                ResizeRenderSize(viewportType, ref screenWidth, ref screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect, ref target);
            }

            Vector2 textureMousePosition = Screen2RenderTexturePosition(mousePosition, sourceRect, destRect);

            BeginTextureMode(target);
                ClearBackground(Color.White);
                DrawCircleV(textureMousePosition, 20.0f, Color.Lime);
            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.Black);

                DrawTexturePro(target.Texture, sourceRect, destRect, new Vector2(0.0f, 0.0f), 0.0f, Color.White);

                Rectangle infoRect = new(5, 5, 330, 105);
                DrawRectangleRec(infoRect, Fade(Color.LightGray, 0.7f));
                DrawRectangleLinesEx(infoRect, 1, Color.Blue);

                DrawText($"Window Resolution: {screenWidth} x {screenHeight}", 15, 15, 10, Color.Black);
                DrawText($"Game Resolution: {gameWidth} x {gameHeight}", 15, 30, 10, Color.Black);

                DrawText($"Type: {ViewportTypeNames[(int)viewportType]}", 15, 45, 10, Color.Black);
                Vector2 scaleRatio = new(destRect.Width/sourceRect.Width, destRect.Height/sourceRect.Height);
                if (scaleRatio.X < 0.001f || scaleRatio.Y < 0.001f) DrawText("Scale ratio: INVALID", 15, 60, 10, Color.Black);
                else DrawText($"Scale ratio: {scaleRatio.X:0.00} x {scaleRatio.Y:0.00}", 15, 60, 10, Color.Black);

                DrawText($"Source size: {sourceRect.Width:0.00} x {sourceRect.Height:0.00}", 15, 75, 10, Color.Black);
                DrawText($"Destination size: {destRect.Width:0.00} x {destRect.Height:0.00}", 15, 90, 10, Color.Black);

                DrawRectangleRec(decreaseTypeButton, Color.SkyBlue);
                DrawRectangleRec(increaseTypeButton, Color.SkyBlue);
                DrawRectangleRec(decreaseResolutionButton, Color.SkyBlue);
                DrawRectangleRec(increaseResolutionButton, Color.SkyBlue);
                DrawText("<", (int)decreaseTypeButton.X + 3, (int)decreaseTypeButton.Y + 1, 10, Color.Black);
                DrawText(">", (int)increaseTypeButton.X + 3, (int)increaseTypeButton.Y + 1, 10, Color.Black);
                DrawText("<", (int)decreaseResolutionButton.X + 3, (int)decreaseResolutionButton.Y + 1, 10, Color.Black);
                DrawText(">", (int)increaseResolutionButton.X + 3, (int)increaseResolutionButton.Y + 1, 10, Color.Black);

            EndDrawing();
        }

        UnloadRenderTexture(target);
        CloseWindow();
    }

    private static void KeepAspectCenteredInteger(int screenWidth, int screenHeight, int gameWidth, int gameHeight, ref Rectangle sourceRect, ref Rectangle destRect)
    {
        sourceRect = new Rectangle(0.0f, 0.0f, (float)gameWidth, (float)gameHeight);

        int ratio_x = (screenWidth/gameWidth);
        int ratio_y = (screenHeight/gameHeight);
        float resizeRatio = (float)((ratio_x < ratio_y)? ratio_x : ratio_y);

        destRect = new Rectangle(
            (float)(int)((screenWidth - (gameWidth*resizeRatio))*0.5f),
            (float)(int)((screenHeight - (gameHeight*resizeRatio))*0.5f),
            (float)(int)(gameWidth*resizeRatio),
            (float)(int)(gameHeight*resizeRatio));
    }

    private static void KeepHeightCenteredInteger(int screenWidth, int screenHeight, int gameWidth, int gameHeight, ref Rectangle sourceRect, ref Rectangle destRect)
    {
        float resizeRatio = (float)screenHeight/gameHeight;
        sourceRect = new Rectangle(0.0f, 0.0f, (float)(int)(screenWidth/resizeRatio), (float)gameHeight);

        destRect = new Rectangle(
            (float)(int)((screenWidth - (sourceRect.Width*resizeRatio))*0.5f),
            (float)(int)((screenHeight - (gameHeight*resizeRatio))*0.5f),
            (float)(int)(sourceRect.Width*resizeRatio),
            (float)(int)(gameHeight*resizeRatio));
    }

    private static void KeepWidthCenteredInteger(int screenWidth, int screenHeight, int gameWidth, int gameHeight, ref Rectangle sourceRect, ref Rectangle destRect)
    {
        float resizeRatio = (float)screenWidth/gameWidth;
        sourceRect = new Rectangle(0.0f, 0.0f, (float)gameWidth, (float)(int)(screenHeight/resizeRatio));

        destRect = new Rectangle(
            (float)(int)((screenWidth - (gameWidth*resizeRatio))*0.5f),
            (float)(int)((screenHeight - (sourceRect.Height*resizeRatio))*0.5f),
            (float)(int)(gameWidth*resizeRatio),
            (float)(int)(sourceRect.Height*resizeRatio));
    }

    private static void KeepAspectCentered(int screenWidth, int screenHeight, int gameWidth, int gameHeight, ref Rectangle sourceRect, ref Rectangle destRect)
    {
        sourceRect = new Rectangle(0.0f, 0.0f, (float)gameWidth, (float)gameHeight);

        float ratio_x = ((float)screenWidth/(float)gameWidth);
        float ratio_y = ((float)screenHeight/(float)gameHeight);
        float resizeRatio = (ratio_x < ratio_y ? ratio_x : ratio_y);

        destRect = new Rectangle(
            (float)(int)((screenWidth - (gameWidth*resizeRatio))*0.5f),
            (float)(int)((screenHeight - (gameHeight*resizeRatio))*0.5f),
            (float)(int)(gameWidth*resizeRatio),
            (float)(int)(gameHeight*resizeRatio));
    }

    private static void KeepHeightCentered(int screenWidth, int screenHeight, int gameWidth, int gameHeight, ref Rectangle sourceRect, ref Rectangle destRect)
    {
        float resizeRatio = ((float)screenHeight/(float)gameHeight);
        sourceRect = new Rectangle(0.0f, 0.0f, (float)(int)((float)screenWidth/resizeRatio), (float)gameHeight);

        destRect = new Rectangle(
            (float)(int)((screenWidth - (sourceRect.Width*resizeRatio))*0.5f),
            (float)(int)((screenHeight - (gameHeight*resizeRatio))*0.5f),
            (float)(int)(sourceRect.Width*resizeRatio),
            (float)(int)(gameHeight*resizeRatio));
    }

    private static void KeepWidthCentered(int screenWidth, int screenHeight, int gameWidth, int gameHeight, ref Rectangle sourceRect, ref Rectangle destRect)
    {
        float resizeRatio = ((float)screenWidth/(float)gameWidth);
        sourceRect = new Rectangle(0.0f, 0.0f, (float)gameWidth, (float)(int)((float)screenHeight/resizeRatio));

        destRect = new Rectangle(
            (float)(int)((screenWidth - (gameWidth*resizeRatio))*0.5f),
            (float)(int)((screenHeight - (sourceRect.Height*resizeRatio))*0.5f),
            (float)(int)(gameWidth*resizeRatio),
            (float)(int)(sourceRect.Height*resizeRatio));
    }

    private static void ResizeRenderSize(ViewportType viewportType, ref int screenWidth, ref int screenHeight, int gameWidth, int gameHeight, ref Rectangle sourceRect, ref Rectangle destRect, ref RenderTexture2D target)
    {
        screenWidth = GetScreenWidth();
        screenHeight = GetScreenHeight();

        switch (viewportType)
        {
            case ViewportType.KEEP_ASPECT_INTEGER: KeepAspectCenteredInteger(screenWidth, screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect); break;
            case ViewportType.KEEP_HEIGHT_INTEGER: KeepHeightCenteredInteger(screenWidth, screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect); break;
            case ViewportType.KEEP_WIDTH_INTEGER: KeepWidthCenteredInteger(screenWidth, screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect); break;
            case ViewportType.KEEP_ASPECT: KeepAspectCentered(screenWidth, screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect); break;
            case ViewportType.KEEP_HEIGHT: KeepHeightCentered(screenWidth, screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect); break;
            case ViewportType.KEEP_WIDTH: KeepWidthCentered(screenWidth, screenHeight, gameWidth, gameHeight, ref sourceRect, ref destRect); break;
            default: break;
        }

        UnloadRenderTexture(target);
        target = LoadRenderTexture((int)sourceRect.Width, (int)sourceRect.Height);
        SetTextureFilter(target.Texture, TextureFilter.Point);
    }

    private static Vector2 Screen2RenderTexturePosition(Vector2 point, Rectangle textureRect, Rectangle scaledRect)
    {
        Vector2 relativePosition = new(point.X - scaledRect.X, point.Y - scaledRect.Y);
        Vector2 ratio = new(textureRect.Width/scaledRect.Width, textureRect.Height/scaledRect.Height);

        return new Vector2(relativePosition.X*ratio.X, relativePosition.Y*ratio.X);
    }
}
