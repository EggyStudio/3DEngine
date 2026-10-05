// raylib's textures_cellular_automata example, Copyright (c) 2025 Jordi Santonja (@JordSant), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesCellularAutomata
{
    private const int screenWidth = 800;
    private const int screenHeight = 450;
    private const int imageWidth = 800;
    private const int imageHeight = 800/2;

    // The rule's buttons, their sizes and places
    private const int drawRuleStartX = 585;
    private const int drawRuleStartY = 10;
    private const int drawRuleSpacing = 15;
    private const int drawRuleGroupSpacing = 50;
    private const int drawRuleSize = 14;
    private const int drawRuleInnerSize = 10;

    // The presets' buttons
    private const int presetsSizeX = 42;
    private const int presetsSizeY = 22;

    private const int linesUpdatedPerFrame = 4;

    // A line from the one above it, by the rule. The edges stay white.
    private static void ComputeLine(ref Image image, int line, int rule)
    {
        for (int i = 1; i < imageWidth - 1; i++)
        {
            // The three pixels above, left, center and right, as the bits of a number
            int prevValue = ((GetImageColor(image, i - 1, line - 1).R < 5)? 4 : 0) +
                            ((GetImageColor(image, i, line - 1).R < 5)? 2 : 0) +
                            ((GetImageColor(image, i + 1, line - 1).R < 5)? 1 : 0);

            // whose bit of the rule says the pixel's color.
            bool currValue = (rule & (1 << prevValue)) != 0;

            ImageDrawPixel(ref image, i, line, currValue? Color.Black : Color.RayWhite);
        }
    }

    public static void Run()
    {
        InitWindow(screenWidth, screenHeight, "[textures] cellular automata");

        // The automaton's image, which starts from a black pixel at the top's middle
        Image image = GenImageColor(imageWidth, imageHeight, Color.RayWhite);

        ImageDrawPixel(ref image, imageWidth/2, 0, Color.Black);

        Texture2D texture = LoadTextureFromImage(image);

        // Some rules worth seeing
        int[] presetValues = [18, 30, 60, 86, 102, 124, 126, 150, 182, 225];
        int presetsCount = presetValues.Length;

        int rule = 30;
        int line = 1;   // The line to compute next, the first having its one point

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mouse = GetMousePosition();
            int mouseInCell = -1;   // None, a bit of the rule from 0 to 7, or a preset from 8

            for (int i = 0; i < 8; i++)
            {
                int cellX = drawRuleStartX - drawRuleGroupSpacing*i + drawRuleSpacing;
                int cellY = drawRuleStartY + drawRuleSpacing;
                if ((mouse.X >= cellX) && (mouse.X <= cellX + drawRuleSize) &&
                    (mouse.Y >= cellY) && (mouse.Y <= cellY + drawRuleSize))
                {
                    mouseInCell = i;
                    break;
                }
            }

            if (mouseInCell < 0)
            {
                for (int i = 0; i < presetsCount; i++)
                {
                    int cellX = 4 + (presetsSizeX + 2)*(i/2);
                    int cellY = 2 + (presetsSizeY + 2)*(i%2);
                    if ((mouse.X >= cellX) && (mouse.X <= cellX + presetsSizeX) &&
                        (mouse.Y >= cellY) && (mouse.Y <= cellY + presetsSizeY))
                    {
                        mouseInCell = i + 8;
                        break;
                    }
                }
            }

            if (IsMouseButtonPressed(MouseButton.Left) && (mouseInCell >= 0))
            {
                // A bit of the rule flips, or a preset becomes the rule, and the image starts again.
                if (mouseInCell < 8)
                    rule ^= (1 << mouseInCell);
                else
                    rule = presetValues[mouseInCell - 8];

                ImageClearBackground(ref image, Color.RayWhite);
                ImageDrawPixel(ref image, imageWidth/2, 0, Color.Black);
                line = 1;
            }

            if (line < imageHeight)
            {
                for (int i = 0; (i < linesUpdatedPerFrame) && (line + i < imageHeight); i++)
                    ComputeLine(ref image, line + i, rule);
                line += linesUpdatedPerFrame;

                UpdateTexture(texture, image);
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(texture, 0, screenHeight - imageHeight, Color.White);

                // The presets
                for (int i = 0; i < presetsCount; i++)
                {
                    DrawText($"{presetValues[i]}", 8 + (presetsSizeX + 2)*(i/2), 4 + (presetsSizeY + 2)*(i%2), 20, Color.Gray);
                    DrawRectangleLines(4 + (presetsSizeX + 2)*(i/2), 2 + (presetsSizeY + 2)*(i%2), presetsSizeX, presetsSizeY, Color.Blue);

                    if (mouseInCell == i + 8)
                        DrawRectangleLinesEx(new Rectangle(2 + (presetsSizeX + 2.0f)*(i/2),
                                                           (presetsSizeY + 2.0f)*(i%2),
                                                           presetsSizeX + 4.0f, presetsSizeY + 4.0f), 3, Color.Red);
                }

                // The rule's bits
                for (int i = 0; i < 8; i++)
                {
                    // The three above
                    for (int j = 0; j < 3; j++)
                    {
                        DrawRectangleLines(drawRuleStartX - drawRuleGroupSpacing*i + drawRuleSpacing*j, drawRuleStartY, drawRuleSize, drawRuleSize, Color.Gray);
                        if ((i & (4 >> j)) != 0)
                            DrawRectangle(drawRuleStartX + 2 - drawRuleGroupSpacing*i + drawRuleSpacing*j, drawRuleStartY + 2, drawRuleInnerSize, drawRuleInnerSize, Color.Black);
                    }

                    // and the one they make
                    DrawRectangleLines(drawRuleStartX - drawRuleGroupSpacing*i + drawRuleSpacing, drawRuleStartY + drawRuleSpacing, drawRuleSize, drawRuleSize, Color.Blue);
                    if ((rule & (1 << i)) != 0)
                        DrawRectangle(drawRuleStartX + 2 - drawRuleGroupSpacing*i + drawRuleSpacing, drawRuleStartY + 2 + drawRuleSpacing, drawRuleInnerSize, drawRuleInnerSize, Color.Black);

                    if (mouseInCell == i)
                        DrawRectangleLinesEx(new Rectangle(drawRuleStartX - drawRuleGroupSpacing*i + drawRuleSpacing - 2.0f,
                                                           drawRuleStartY + drawRuleSpacing - 2.0f,
                                                           drawRuleSize + 4.0f, drawRuleSize + 4.0f), 3, Color.Red);
                }

                DrawText($"RULE: {rule}", drawRuleStartX + drawRuleSpacing*4, drawRuleStartY + 1, 30, Color.Gray);

            EndDrawing();
        }

        UnloadImage(image);
        UnloadTexture(texture);

        CloseWindow();
    }
}
