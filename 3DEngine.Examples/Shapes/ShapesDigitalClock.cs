// raylib's shapes_digital_clock example, Copyright (c) 2025 Hamza RAHAL (@hmz-rhl) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesDigitalClock
{
    private const int CLOCK_ANALOG = 0;
    private const int CLOCK_DIGITAL = 1;
    private const float DEG2RAD = MathF.PI/180.0f;

    private struct ClockHand
    {
        public int value;
        public float angle;
        public int length;
        public int thickness;
        public Color color;
    }

    private struct Clock
    {
        public ClockHand second;
        public ClockHand minute;
        public ClockHand hour;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] digital clock");

        int clockMode = CLOCK_DIGITAL;

        Clock clock = default;
        clock.second.angle = 45;
        clock.second.length = 140;
        clock.second.thickness = 3;
        clock.second.color = Color.Maroon;
        clock.minute.angle = 10;
        clock.minute.length = 130;
        clock.minute.thickness = 7;
        clock.minute.color = Color.DarkGray;
        clock.hour.angle = 0;
        clock.hour.length = 100;
        clock.hour.thickness = 7;
        clock.hour.color = Color.Black;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space))
            {
                if (clockMode == CLOCK_DIGITAL) clockMode = CLOCK_ANALOG;
                else if (clockMode == CLOCK_ANALOG) clockMode = CLOCK_DIGITAL;
            }

            UpdateClock(ref clock);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (clockMode == CLOCK_ANALOG) DrawClockAnalog(clock, new Vector2(400, 240));
                else if (clockMode == CLOCK_DIGITAL)
                {
                    DrawClockDigital(clock, new Vector2(30, 60));

                    string clockTime = $"{clock.hour.value:00}:{clock.minute.value:00}:{clock.second.value:00}";
                    DrawText(clockTime, GetScreenWidth()/2 - MeasureText(clockTime, 150)/2, 300, 150, Color.Black);
                }

                DrawText($"Press [SPACE] to switch clock mode: {((clockMode == CLOCK_DIGITAL) ? "DIGITAL CLOCK" : "ANALOGUE CLOCK")}", 10, 10, 20, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void UpdateClock(ref Clock clock)
    {
        var timeinfo = DateTime.Now;

        clock.second.value = timeinfo.Second;
        clock.minute.value = timeinfo.Minute;
        clock.hour.value = timeinfo.Hour;

        clock.hour.angle = (timeinfo.Hour%12)*180.0f/6.0f;
        clock.hour.angle += (timeinfo.Minute%60)*30/60.0f;
        clock.hour.angle -= 90;

        clock.minute.angle = (timeinfo.Minute%60)*6.0f;
        clock.minute.angle += (timeinfo.Second%60)*6/60.0f;
        clock.minute.angle -= 90;

        clock.second.angle = (timeinfo.Second%60)*6.0f;
        clock.second.angle -= 90;
    }

    private static void DrawClockAnalog(Clock clock, Vector2 position)
    {
        DrawCircleV(position, clock.second.length + 40.0f, Color.LightGray);
        DrawCircleV(position, 12.0f, Color.Gray);

        for (int i = 0; i < 60; i++)
        {
            DrawLineEx(new Vector2(position.X + (clock.second.length + ((i%5 != 0) ? 10 : 6))*MathF.Cos((6.0f*i - 90.0f)*DEG2RAD),
                position.Y + (clock.second.length + ((i%5 != 0) ? 10 : 6))*MathF.Sin((6.0f*i - 90.0f)*DEG2RAD)),
                new Vector2(position.X + (clock.second.length + 20)*MathF.Cos((6.0f*i - 90.0f)*DEG2RAD),
                position.Y + (clock.second.length + 20)*MathF.Sin((6.0f*i - 90.0f)*DEG2RAD)), ((i%5 != 0) ? 1.0f : 3.0f), Color.DarkGray);
        }

        DrawRectanglePro(new Rectangle(position.X, position.Y, (float)clock.second.length, (float)clock.second.thickness),
            new Vector2(0.0f, clock.second.thickness/2.0f), clock.second.angle, clock.second.color);

        DrawRectanglePro(new Rectangle(position.X, position.Y, (float)clock.minute.length, (float)clock.minute.thickness),
            new Vector2(0.0f, clock.minute.thickness/2.0f), clock.minute.angle, clock.minute.color);

        DrawRectanglePro(new Rectangle(position.X, position.Y, (float)clock.hour.length, (float)clock.hour.thickness),
            new Vector2(0.0f, clock.hour.thickness/2.0f), clock.hour.angle, clock.hour.color);
    }

    private static void DrawClockDigital(Clock clock, Vector2 position)
    {
        DrawDisplayValue(new Vector2(position.X, position.Y), clock.hour.value/10, Color.Red, Fade(Color.LightGray, 0.3f));
        DrawDisplayValue(new Vector2(position.X + 120, position.Y), clock.hour.value%10, Color.Red, Fade(Color.LightGray, 0.3f));

        DrawCircle((int)position.X + 240, (int)position.Y + 70, 12, (clock.second.value%2 != 0) ? Color.Red : Fade(Color.LightGray, 0.3f));
        DrawCircle((int)position.X + 240, (int)position.Y + 150, 12, (clock.second.value%2 != 0) ? Color.Red : Fade(Color.LightGray, 0.3f));

        DrawDisplayValue(new Vector2(position.X + 260, position.Y), clock.minute.value/10, Color.Red, Fade(Color.LightGray, 0.3f));
        DrawDisplayValue(new Vector2(position.X + 380, position.Y), clock.minute.value%10, Color.Red, Fade(Color.LightGray, 0.3f));

        DrawCircle((int)position.X + 500, (int)position.Y + 70, 12, (clock.second.value%2 != 0) ? Color.Red : Fade(Color.LightGray, 0.3f));
        DrawCircle((int)position.X + 500, (int)position.Y + 150, 12, (clock.second.value%2 != 0) ? Color.Red : Fade(Color.LightGray, 0.3f));

        DrawDisplayValue(new Vector2(position.X + 520, position.Y), clock.second.value/10, Color.Red, Fade(Color.LightGray, 0.3f));
        DrawDisplayValue(new Vector2(position.X + 640, position.Y), clock.second.value%10, Color.Red, Fade(Color.LightGray, 0.3f));
    }

    private static void DrawDisplayValue(Vector2 position, int value, Color colorOn, Color colorOff)
    {
        switch (value)
        {
            case 0: Draw7SDisplay(position, 0b00111111, colorOn, colorOff); break;
            case 1: Draw7SDisplay(position, 0b00000110, colorOn, colorOff); break;
            case 2: Draw7SDisplay(position, 0b01011011, colorOn, colorOff); break;
            case 3: Draw7SDisplay(position, 0b01001111, colorOn, colorOff); break;
            case 4: Draw7SDisplay(position, 0b01100110, colorOn, colorOff); break;
            case 5: Draw7SDisplay(position, 0b01101101, colorOn, colorOff); break;
            case 6: Draw7SDisplay(position, 0b01111101, colorOn, colorOff); break;
            case 7: Draw7SDisplay(position, 0b00000111, colorOn, colorOff); break;
            case 8: Draw7SDisplay(position, 0b01111111, colorOn, colorOff); break;
            case 9: Draw7SDisplay(position, 0b01101111, colorOn, colorOff); break;
            default: break;
        }
    }

    private static void Draw7SDisplay(Vector2 position, int segments, Color colorOn, Color colorOff)
    {
        int segmentLen = 60;
        int segmentThick = 20;
        float offsetYAdjust = segmentThick*0.3f;

        DrawDisplaySegment(new Vector2(position.X + segmentThick + segmentLen/2.0f, position.Y + segmentThick),
            segmentLen, segmentThick, false, ((segments & 0b00000001) != 0) ? colorOn : colorOff);
        DrawDisplaySegment(new Vector2(position.X + segmentThick + segmentLen + segmentThick/2.0f, position.Y + 2*segmentThick + segmentLen/2.0f - offsetYAdjust),
            segmentLen, segmentThick, true, ((segments & 0b00000010) != 0) ? colorOn : colorOff);
        DrawDisplaySegment(new Vector2(position.X + segmentThick + segmentLen + segmentThick/2.0f, position.Y + 4*segmentThick + segmentLen + segmentLen/2.0f - 3*offsetYAdjust),
            segmentLen, segmentThick, true, ((segments & 0b00000100) != 0) ? colorOn : colorOff);
        DrawDisplaySegment(new Vector2(position.X + segmentThick + segmentLen/2.0f, position.Y + 5*segmentThick + 2*segmentLen - 4*offsetYAdjust),
            segmentLen, segmentThick, false, ((segments & 0b00001000) != 0) ? colorOn : colorOff);
        DrawDisplaySegment(new Vector2(position.X + segmentThick/2.0f, position.Y + 4*segmentThick + segmentLen + segmentLen/2.0f - 3*offsetYAdjust),
            segmentLen, segmentThick, true, ((segments & 0b00010000) != 0) ? colorOn : colorOff);
        DrawDisplaySegment(new Vector2(position.X + segmentThick/2.0f, position.Y + 2*segmentThick + segmentLen/2.0f - offsetYAdjust),
            segmentLen, segmentThick, true, ((segments & 0b00100000) != 0) ? colorOn : colorOff);
        DrawDisplaySegment(new Vector2(position.X + segmentThick + segmentLen/2.0f, position.Y + 3*segmentThick + segmentLen - 2*offsetYAdjust),
            segmentLen, segmentThick, false, ((segments & 0b01000000) != 0) ? colorOn : colorOff);
    }

    private static void DrawDisplaySegment(Vector2 center, int length, int thick, bool vertical, Color color)
    {
        if (!vertical)
        {
            Vector2[] segmentPointsH =
            [
                new(center.X - length/2.0f - thick/2.0f, center.Y),
                new(center.X - length/2.0f, center.Y + thick/2.0f),
                new(center.X - length/2.0f, center.Y - thick/2.0f),
                new(center.X + length/2.0f, center.Y + thick/2.0f),
                new(center.X + length/2.0f, center.Y - thick/2.0f),
                new(center.X + length/2.0f + thick/2.0f, center.Y),
            ];

            DrawTriangleStrip(segmentPointsH, color);
        }
        else
        {
            Vector2[] segmentPointsV =
            [
                new(center.X, center.Y - length/2.0f - thick/2.0f),
                new(center.X - thick/2.0f, center.Y - length/2.0f),
                new(center.X + thick/2.0f, center.Y - length/2.0f),
                new(center.X - thick/2.0f, center.Y + length/2.0f),
                new(center.X + thick/2.0f, center.Y + length/2.0f),
                new(center.X, center.Y + (float)length/2 + thick/2.0f),
            ];

            DrawTriangleStrip(segmentPointsV, color);
        }
    }
}
