// raylib's textures_mouse_painting example, Copyright (c) 2019-2025 Chris Dill (@MysteriousSpace) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesMousePainting
{
    private const int MAX_COLORS_COUNT = 23;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] mouse painting");

        Color[] colors =
        [
            Color.RayWhite, Color.Yellow, Color.Gold, Color.Orange, Color.Pink, Color.Red, Color.Maroon, Color.Green, Color.Lime, Color.DarkGreen,
            Color.SkyBlue, Color.Blue, Color.DarkBlue, Color.Purple, Color.Violet, Color.DarkPurple, Color.Beige, Color.Brown, Color.DarkBrown,
            Color.LightGray, Color.Gray, Color.DarkGray, Color.Black,
        ];

        // Each color's swatch
        Rectangle[] colorsRecs = new Rectangle[MAX_COLORS_COUNT];

        for (int i = 0; i < MAX_COLORS_COUNT; i++) colorsRecs[i] = new Rectangle(10 + 30.0f*i + 2*i, 10, 30, 30);

        int colorSelected = 0;
        int colorSelectedPrev = colorSelected;
        int colorMouseHover = 0;
        float brushSize = 20.0f;
        bool mouseWasPressed = false;

        Rectangle btnSaveRec = new(750, 10, 40, 30);
        bool btnSaveMouseHover = false;
        bool showSaveMessage = false;
        int saveMessageCounter = 0;

        // The canvas, cleared before painting starts
        RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);

        BeginTextureMode(target);
        ClearBackground(colors[0]);
        EndTextureMode();

        SetTargetFPS(120);

        while (!WindowShouldClose())
        {
            Vector2 mousePos = GetMousePosition();

            // The arrows move between colors,
            if (IsKeyPressed(Key.Right)) colorSelected++;
            else if (IsKeyPressed(Key.Left)) colorSelected--;

            if (colorSelected >= MAX_COLORS_COUNT) colorSelected = MAX_COLORS_COUNT - 1;
            else if (colorSelected < 0) colorSelected = 0;

            // and a click on a swatch picks one.
            for (int i = 0; i < MAX_COLORS_COUNT; i++)
            {
                if (CheckCollisionPointRec(mousePos, colorsRecs[i]))
                {
                    colorMouseHover = i;
                    break;
                }
                else colorMouseHover = -1;
            }

            if ((colorMouseHover >= 0) && IsMouseButtonPressed(MouseButton.Left))
            {
                colorSelected = colorMouseHover;
                colorSelectedPrev = colorSelected;
            }

            brushSize += GetMouseWheelMove()*5;
            if (brushSize < 2) brushSize = 2;
            if (brushSize > 50) brushSize = 50;

            if (IsKeyPressed(Key.C))
            {
                BeginTextureMode(target);
                ClearBackground(colors[0]);
                EndTextureMode();
            }

            if (IsMouseButtonDown(MouseButton.Left) || IsGestureDetected(Gesture.Drag))
            {
                // A circle a frame, which leaves gaps when the pointer moves fast
                BeginTextureMode(target);
                if (mousePos.Y > 50) DrawCircle((int)mousePos.X, (int)mousePos.Y, brushSize, colors[colorSelected]);
                EndTextureMode();
            }

            if (IsMouseButtonDown(MouseButton.Right))
            {
                if (!mouseWasPressed)
                {
                    colorSelectedPrev = colorSelected;
                    colorSelected = 0;
                }

                mouseWasPressed = true;

                // The right button paints the background back.
                BeginTextureMode(target);
                if (mousePos.Y > 50) DrawCircle((int)mousePos.X, (int)mousePos.Y, brushSize, colors[0]);
                EndTextureMode();
            }
            else if (IsMouseButtonReleased(MouseButton.Right) && mouseWasPressed)
            {
                colorSelected = colorSelectedPrev;
                mouseWasPressed = false;
            }

            btnSaveMouseHover = CheckCollisionPointRec(mousePos, btnSaveRec);

            // The painting is saved under a name of raylib's. A target is read back the right
            // way up here, so it needs none of raylib's ImageFlipVertical.
            if ((btnSaveMouseHover && IsMouseButtonReleased(MouseButton.Left)) || IsKeyPressed(Key.S))
            {
                Image image = LoadImageFromTexture(target.Texture);
                ExportImage(image, "my_amazing_texture_painting.png");
                UnloadImage(image);
                showSaveMessage = true;
            }

            if (showSaveMessage)
            {
                // A message over the screen for two seconds
                saveMessageCounter++;
                if (saveMessageCounter > 240)
                {
                    showSaveMessage = false;
                    saveMessageCounter = 0;
                }
            }

            BeginDrawing();

            ClearBackground(Color.RayWhite);

            // A target is stored the right way up, so it is drawn with its height as it is.
            DrawTextureRec(target.Texture, new Rectangle(0, 0, (float)target.Texture.Width, (float)target.Texture.Height), Vector2.Zero, Color.White);

            // The brush, where it would paint
            if (mousePos.Y > 50)
            {
                if (IsMouseButtonDown(MouseButton.Right)) DrawCircleLines((int)mousePos.X, (int)mousePos.Y, brushSize, Color.Gray);
                else DrawCircle(GetMouseX(), GetMouseY(), brushSize, colors[colorSelected]);
            }

            // The panel at the top, with its swatches
            DrawRectangle(0, 0, GetScreenWidth(), 50, Color.RayWhite);
            DrawLine(0, 50, GetScreenWidth(), 50, Color.LightGray);

            for (int i = 0; i < MAX_COLORS_COUNT; i++) DrawRectangleRec(colorsRecs[i], colors[i]);
            DrawRectangleLines(10, 10, 30, 30, Color.LightGray);

            if (colorMouseHover >= 0) DrawRectangleRec(colorsRecs[colorMouseHover], Fade(Color.White, 0.6f));

            DrawRectangleLinesEx(new Rectangle(colorsRecs[colorSelected].X - 2, colorsRecs[colorSelected].Y - 2,
                                 colorsRecs[colorSelected].Width + 4, colorsRecs[colorSelected].Height + 4), 2, Color.Black);

            DrawRectangleLinesEx(btnSaveRec, 2, btnSaveMouseHover ? Color.Red : Color.Black);
            DrawText("SAVE!", 755, 20, 10, btnSaveMouseHover ? Color.Red : Color.Black);

            if (showSaveMessage)
            {
                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Fade(Color.RayWhite, 0.8f));
                DrawRectangle(0, 150, GetScreenWidth(), 80, Color.Black);
                DrawText("IMAGE SAVED!", 150, 180, 20, Color.RayWhite);
            }

            EndDrawing();
        }

        UnloadRenderTexture(target);

        CloseWindow();
    }
}
