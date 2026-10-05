// raylib's shapes_clock_of_clocks example, Copyright (c) 2025 JP Mortiboys (@themushroompirates), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesClockOfClocks
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] clock of clocks");

        Color bgColor = ColorLerp(Color.DarkBlue, Color.Black, 0.75f);
        Color handsColor = ColorLerp(Color.Yellow, Color.RayWhite, .25f);

        const float clockFaceSize = 24;
        const float clockFaceSpacing = 8.0f;
        const float sectionSpacing = 16.0f;

        Vector2 TL = new(0.0f, 90.0f);
        Vector2 TR = new(90.0f, 180.0f);
        Vector2 BR = new(180.0f, 270.0f);
        Vector2 BL = new(0.0f, 270.0f);
        Vector2 HH = new(0.0f, 180.0f);
        Vector2 VV = new(90.0f, 270.0f);
        Vector2 ZZ = new(135.0f, 135.0f);

        Vector2[][] digitAngles =
        [
            /* 0 */ [TL,HH,HH,TR, /* */ VV,TL,TR,VV,/* */ VV,VV,VV,VV,/* */ VV,VV,VV,VV,/* */ VV,BL,BR,VV,/* */ BL,HH,HH,BR],
            /* 1 */ [TL,HH,TR,ZZ, /* */ BL,TR,VV,ZZ,/* */ ZZ,VV,VV,ZZ,/* */ ZZ,VV,VV,ZZ,/* */ TL,BR,BL,TR,/* */ BL,HH,HH,BR],
            /* 2 */ [TL,HH,HH,TR, /* */ BL,HH,TR,VV,/* */ TL,HH,BR,VV,/* */ VV,TL,HH,BR,/* */ VV,BL,HH,TR,/* */ BL,HH,HH,BR],
            /* 3 */ [TL,HH,HH,TR, /* */ BL,HH,TR,VV,/* */ TL,HH,BR,VV,/* */ BL,HH,TR,VV,/* */ TL,HH,BR,VV,/* */ BL,HH,HH,BR],
            /* 4 */ [TL,TR,TL,TR, /* */ VV,VV,VV,VV,/* */ VV,BL,BR,VV,/* */ BL,HH,TR,VV,/* */ ZZ,ZZ,VV,VV,/* */ ZZ,ZZ,BL,BR],
            /* 5 */ [TL,HH,HH,TR, /* */ VV,TL,HH,BR,/* */ VV,BL,HH,TR,/* */ BL,HH,TR,VV,/* */ TL,HH,BR,VV,/* */ BL,HH,HH,BR],
            /* 6 */ [TL,HH,HH,TR, /* */ VV,TL,HH,BR,/* */ VV,BL,HH,TR,/* */ VV,TL,TR,VV,/* */ VV,BL,BR,VV,/* */ BL,HH,HH,BR],
            /* 7 */ [TL,HH,HH,TR, /* */ BL,HH,TR,VV,/* */ ZZ,ZZ,VV,VV,/* */ ZZ,ZZ,VV,VV,/* */ ZZ,ZZ,VV,VV,/* */ ZZ,ZZ,BL,BR],
            /* 8 */ [TL,HH,HH,TR, /* */ VV,TL,TR,VV,/* */ VV,BL,BR,VV,/* */ VV,TL,TR,VV,/* */ VV,BL,BR,VV,/* */ BL,HH,HH,BR],
            /* 9 */ [TL,HH,HH,TR, /* */ VV,TL,TR,VV,/* */ VV,BL,BR,VV,/* */ BL,HH,TR,VV,/* */ TL,HH,BR,VV,/* */ BL,HH,HH,BR],
        ];

        const float handsMoveDuration = 0.5f;

        int prevSeconds = -1;
        Vector2[,] currentAngles = new Vector2[6, 24];
        Vector2[,] srcAngles = new Vector2[6, 24];
        Vector2[,] dstAngles = new Vector2[6, 24];

        float handsMoveTimer = 0.0f;
        int hourMode = 24;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            var timeinfo = DateTime.Now;

            if (timeinfo.Second != prevSeconds)
            {
                prevSeconds = timeinfo.Second;

                string clockDigits = $"{timeinfo.Hour%hourMode:00}{timeinfo.Minute:00}{timeinfo.Second:00}";

                for (int digit = 0; digit < 6; digit++)
                {
                    for (int cell = 0; cell < 24; cell++)
                    {
                        srcAngles[digit, cell] = currentAngles[digit, cell];
                        dstAngles[digit, cell] = digitAngles[clockDigits[digit] - '0'][cell];

                        if ((digit == 0) && (hourMode == 12) && (clockDigits[0] == '0')) dstAngles[digit, cell] = ZZ;
                        if (srcAngles[digit, cell].X > dstAngles[digit, cell].X) srcAngles[digit, cell].X -= 360.0f;
                        if (srcAngles[digit, cell].Y > dstAngles[digit, cell].Y) srcAngles[digit, cell].Y -= 360.0f;
                    }
                }

                handsMoveTimer = -GetFrameTime();
            }

            if (handsMoveTimer < handsMoveDuration)
            {
                handsMoveTimer = Math.Clamp(handsMoveTimer + GetFrameTime(), 0, handsMoveDuration);

                float t = handsMoveTimer/handsMoveDuration;

                t = t*t*(3.0f - 2.0f*t);

                for (int digit = 0; digit < 6; digit++)
                {
                    for (int cell = 0; cell < 24; cell++)
                    {
                        currentAngles[digit, cell].X = float.Lerp(srcAngles[digit, cell].X, dstAngles[digit, cell].X, t);
                        currentAngles[digit, cell].Y = float.Lerp(srcAngles[digit, cell].Y, dstAngles[digit, cell].Y, t);
                    }
                }
            }

            if (IsKeyPressed(Key.Space)) hourMode = 36 - hourMode;

            BeginDrawing();

                ClearBackground(bgColor);

                DrawText($"{hourMode}-h mode, space to change", 10, 30, 20, Color.RayWhite);

                float xOffset = 4.0f;

                for (int digit = 0; digit < 6; digit++)
                {
                    for (int row = 0; row < 6; row++)
                    {
                        for (int col = 0; col < 4; col++)
                        {
                            Vector2 center = new(
                                xOffset + col*(clockFaceSize + clockFaceSpacing) + clockFaceSize*0.5f,
                                100 + row*(clockFaceSize + clockFaceSpacing) + clockFaceSize*0.5f);

                            DrawRing(center, clockFaceSize*0.5f - 2.0f, clockFaceSize*0.5f, 0, 360, 24, Color.DarkGray);

                            DrawRectanglePro(
                                new Rectangle(center.X, center.Y, clockFaceSize*0.5f + 4.0f, 4.0f),
                                new Vector2(2.0f, 2.0f),
                                currentAngles[digit, row*4 + col].X,
                                handsColor);

                            DrawRectanglePro(
                                new Rectangle(center.X, center.Y, clockFaceSize*0.5f + 2.0f, 4.0f),
                                new Vector2(2.0f, 2.0f),
                                currentAngles[digit, row*4 + col].Y,
                                handsColor);
                        }
                    }

                    xOffset += (clockFaceSize + clockFaceSpacing)*4;

                    if (digit%2 == 1)
                    {
                        DrawRing(new Vector2(xOffset + 4.0f, 160.0f), 6.0f, 8.0f, 0.0f, 360.0f, 24, handsColor);
                        DrawRing(new Vector2(xOffset + 4.0f, 225.0f), 6.0f, 8.0f, 0.0f, 360.0f, 24, handsColor);
                        xOffset += sectionSpacing;
                    }
                }

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
