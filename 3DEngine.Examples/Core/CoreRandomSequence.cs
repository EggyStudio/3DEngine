// raylib's core_random_sequence example, Copyright (c) 2023-2025 Dalton Overmyer (@REDl3east), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreRandomSequence
{
    private struct ColorRect
    {
        public Color Color;
        public Rectangle Rect;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] random sequence");

        int rectCount = 20;
        float rectSize = (float)screenWidth/rectCount;
        ColorRect[] rectangles = GenerateRandomColorRectSequence(rectCount, rectSize, screenWidth, 0.75f*screenHeight);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space)) ShuffleColorRectSequence(rectangles, rectCount);

            if (IsKeyPressed(Key.Up))
            {
                rectCount++;
                rectSize = (float)screenWidth/rectCount;
                rectangles = GenerateRandomColorRectSequence(rectCount, rectSize, screenWidth, 0.75f*screenHeight);
            }

            if (IsKeyPressed(Key.Down))
            {
                if (rectCount >= 4)
                {
                    rectCount--;
                    rectSize = (float)screenWidth/rectCount;
                    rectangles = GenerateRandomColorRectSequence(rectCount, rectSize, screenWidth, 0.75f*screenHeight);
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < rectCount; i++)
                {
                    DrawRectangleRec(rectangles[i].Rect, rectangles[i].Color);

                    DrawText("Press SPACE to shuffle the current sequence", 10, screenHeight - 96, 20, Color.Black);
                    DrawText("Press UP to add a rectangle and generate a new sequence", 10, screenHeight - 64, 20, Color.Black);
                    DrawText("Press DOWN to remove a rectangle and generate a new sequence", 10, screenHeight - 32, 20, Color.Black);
                }

                DrawText($"Count: {rectCount} rectangles", 10, 10, 20, Color.Maroon);

                DrawFPS(screenWidth - 80, 10);

            EndDrawing();
        }

        CloseWindow();
    }

    private static Color GenerateRandomColor() =>
        new((byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), 255);

    private static ColorRect[] GenerateRandomColorRectSequence(float rectCount, float rectWidth, float screenWidth, float screenHeight)
    {
        var rectangles = new ColorRect[(int)rectCount];

        int[] seq = LoadRandomSequence((int)rectCount, 0, (int)rectCount - 1);
        float rectSeqWidth = rectCount*rectWidth;
        float startX = (screenWidth - rectSeqWidth)*0.5f;

        for (int i = 0; i < rectCount; i++)
        {
            // raymath's Remap, from the sequence's range to the height.
            int rectHeight = (int)(seq[i]/(rectCount - 1)*screenHeight);

            rectangles[i].Color = GenerateRandomColor();
            rectangles[i].Rect = new Rectangle(startX + i*rectWidth, screenHeight - rectHeight, rectWidth, rectHeight);
        }

        return rectangles;
    }

    private static void ShuffleColorRectSequence(ColorRect[] rectangles, int rectCount)
    {
        int[] seq = LoadRandomSequence(rectCount, 0, rectCount - 1);

        for (int i1 = 0; i1 < rectCount; i1++)
        {
            ref ColorRect r1 = ref rectangles[i1];
            ref ColorRect r2 = ref rectangles[seq[i1]];

            // Only the color and the height are swapped.
            ColorRect tmp = r1;
            r1.Color = r2.Color;
            r1.Rect = r1.Rect with { Height = r2.Rect.Height, Y = r2.Rect.Y };
            r2.Color = tmp.Color;
            r2.Rect = r2.Rect with { Height = tmp.Rect.Height, Y = tmp.Rect.Y };
        }
    }
}
