// raylib's core_input_gestures_testbed example, Copyright (c) 2023-2025 ubkp (@ubkp), under the zlib
// license, written again for the flat API.

using System.Globalization;
using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputGesturesTestbed
{
    private const int GESTURE_LOG_SIZE = 20;
    private const int MAX_TOUCH_COUNT = 32;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input gestures testbed");

        Vector2 messagePosition = new(160, 7);

        int lastGesture = 0;
        Vector2 lastGesturePosition = new(165, 130);

        string[] gestureLog = new string[GESTURE_LOG_SIZE];
        Array.Fill(gestureLog, "");
        int gestureLogIndex = GESTURE_LOG_SIZE;
        int previousGesture = 0;

        int logMode = 1;

        Color gestureColor = new(0, 0, 0, 255);
        Rectangle logButton1 = new(53, 7, 48, 26);
        Rectangle logButton2 = new(108, 7, 36, 26);
        Vector2 gestureLogPosition = new(10, 10);

        float angleLength = 90.0f;
        float currentAngleDegrees = 0.0f;
        Vector2 finalVector = new(0.0f, 0.0f);
        Vector2 protractorPosition = new(266.0f, 315.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            int currentGesture = (int)GetGestureDetected();
            float currentDragDegrees = GetGestureDragAngle();
            float currentPitchDegrees = GetGesturePinchAngle();
            int touchCount = GetTouchPointCount();

            if ((currentGesture != 0) && (currentGesture != 4) && (currentGesture != previousGesture))
                lastGesture = currentGesture;

            if (IsMouseButtonReleased(MouseButton.Left))
            {
                if (CheckCollisionPointRec(GetMousePosition(), logButton1))
                {
                    switch (logMode)
                    {
                        case 3: logMode = 2; break;
                        case 2: logMode = 3; break;
                        case 1: logMode = 0; break;
                        default: logMode = 1; break;
                    }
                }
                else if (CheckCollisionPointRec(GetMousePosition(), logButton2))
                {
                    switch (logMode)
                    {
                        case 3: logMode = 1; break;
                        case 2: logMode = 0; break;
                        case 1: logMode = 3; break;
                        default: logMode = 2; break;
                    }
                }
            }

            bool fillLog = false;
            if (currentGesture != 0)
            {
                if (logMode == 3)
                {
                    if (((currentGesture != 4) && (currentGesture != previousGesture)) || (currentGesture < 3)) fillLog = true;
                }
                else if (logMode == 2)
                {
                    if (currentGesture != 4) fillLog = true;
                }
                else if (logMode == 1)
                {
                    if (currentGesture != previousGesture) fillLog = true;
                }
                else
                {
                    fillLog = true;
                }
            }

            if (fillLog)
            {
                previousGesture = currentGesture;
                gestureColor = GetGestureColor(currentGesture);
                if (gestureLogIndex <= 0) gestureLogIndex = GESTURE_LOG_SIZE;
                gestureLogIndex--;

                gestureLog[gestureLogIndex] = GetGestureName(currentGesture);
            }

            if (currentGesture > 255) currentAngleDegrees = currentPitchDegrees;
            else if (currentGesture > 15) currentAngleDegrees = currentDragDegrees;
            else if (currentGesture > 0) currentAngleDegrees = 0.0f;

            float currentAngleRadians = ((currentAngleDegrees + 90.0f)*MathF.PI/180);

            finalVector = new Vector2((angleLength*MathF.Sin(currentAngleRadians)) + protractorPosition.X,
                (angleLength*MathF.Cos(currentAngleRadians)) + protractorPosition.Y);

            Vector2[] touchPosition = new Vector2[MAX_TOUCH_COUNT];
            Vector2 mousePosition = default;
            if (currentGesture != (int)Gesture.None)
            {
                if (touchCount != 0)
                {
                    for (int i = 0; i < touchCount; i++) touchPosition[i] = GetTouchPosition(i);
                }
                else mousePosition = GetMousePosition();
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("*", (int)messagePosition.X + 5, (int)messagePosition.Y + 5, 10, Color.Black);
                DrawText("Example optimized for Web/HTML5\non Smartphones with Touch Screen.", (int)messagePosition.X + 15, (int)messagePosition.Y + 5, 10, Color.Black);
                DrawText("*", (int)messagePosition.X + 5, (int)messagePosition.Y + 35, 10, Color.Black);
                DrawText("While running on Desktop Web Browsers,\ninspect and turn on Touch Emulation.", (int)messagePosition.X + 15, (int)messagePosition.Y + 35, 10, Color.Black);

                DrawText("Last gesture", (int)lastGesturePosition.X + 33, (int)lastGesturePosition.Y - 47, 20, Color.Black);
                DrawText("Swipe         Tap       Pinch  Touch", (int)lastGesturePosition.X + 17, (int)lastGesturePosition.Y - 18, 10, Color.Black);
                DrawRectangle((int)lastGesturePosition.X + 20, (int)lastGesturePosition.Y, 20, 20, lastGesture == (int)Gesture.SwipeUp ? Color.Red : Color.LightGray);
                DrawRectangle((int)lastGesturePosition.X, (int)lastGesturePosition.Y + 20, 20, 20, lastGesture == (int)Gesture.SwipeLeft ? Color.Red : Color.LightGray);
                DrawRectangle((int)lastGesturePosition.X + 40, (int)lastGesturePosition.Y + 20, 20, 20, lastGesture == (int)Gesture.SwipeRight ? Color.Red : Color.LightGray);
                DrawRectangle((int)lastGesturePosition.X + 20, (int)lastGesturePosition.Y + 40, 20, 20, lastGesture == (int)Gesture.SwipeDown ? Color.Red : Color.LightGray);
                DrawCircle((int)lastGesturePosition.X + 80, (int)lastGesturePosition.Y + 16, 10, lastGesture == (int)Gesture.Tap ? Color.Blue : Color.LightGray);
                DrawRing(new Vector2(lastGesturePosition.X + 103, lastGesturePosition.Y + 16), 6.0f, 11.0f, 0.0f, 360.0f, 0, lastGesture == (int)Gesture.Drag ? Color.Lime : Color.LightGray);
                DrawCircle((int)lastGesturePosition.X + 80, (int)lastGesturePosition.Y + 43, 10, lastGesture == (int)Gesture.DoubleTap ? Color.SkyBlue : Color.LightGray);
                DrawCircle((int)lastGesturePosition.X + 103, (int)lastGesturePosition.Y + 43, 10, lastGesture == (int)Gesture.DoubleTap ? Color.SkyBlue : Color.LightGray);
                DrawTriangle(new Vector2(lastGesturePosition.X + 122, lastGesturePosition.Y + 16), new Vector2(lastGesturePosition.X + 137, lastGesturePosition.Y + 26), new Vector2(lastGesturePosition.X + 137, lastGesturePosition.Y + 6), lastGesture == (int)Gesture.PinchOut ? Color.Orange : Color.LightGray);
                DrawTriangle(new Vector2(lastGesturePosition.X + 147, lastGesturePosition.Y + 6), new Vector2(lastGesturePosition.X + 147, lastGesturePosition.Y + 26), new Vector2(lastGesturePosition.X + 162, lastGesturePosition.Y + 16), lastGesture == (int)Gesture.PinchOut ? Color.Orange : Color.LightGray);
                DrawTriangle(new Vector2(lastGesturePosition.X + 125, lastGesturePosition.Y + 33), new Vector2(lastGesturePosition.X + 125, lastGesturePosition.Y + 53), new Vector2(lastGesturePosition.X + 140, lastGesturePosition.Y + 43), lastGesture == (int)Gesture.PinchIn ? Color.Violet : Color.LightGray);
                DrawTriangle(new Vector2(lastGesturePosition.X + 144, lastGesturePosition.Y + 43), new Vector2(lastGesturePosition.X + 159, lastGesturePosition.Y + 53), new Vector2(lastGesturePosition.X + 159, lastGesturePosition.Y + 33), lastGesture == (int)Gesture.PinchIn ? Color.Violet : Color.LightGray);
                for (int i = 0; i < 4; i++) DrawCircle((int)lastGesturePosition.X + 180, (int)lastGesturePosition.Y + 7 + i*15, 5, touchCount <= i ? Color.LightGray : gestureColor);

                DrawText("Log", (int)gestureLogPosition.X, (int)gestureLogPosition.Y, 20, Color.Black);

                // Before the first gesture the index is one past the log. raylib's C reads past its
                // array's end there for an empty line, and the wrapped index reads an empty line too.
                for (int i = 0, ii = gestureLogIndex; i < GESTURE_LOG_SIZE; i++, ii = (ii + 1)%GESTURE_LOG_SIZE) DrawText(gestureLog[ii%GESTURE_LOG_SIZE], (int)gestureLogPosition.X, (int)gestureLogPosition.Y + 410 - i*20, 20, (i == 0 ? gestureColor : Color.LightGray));
                Color logButton1Color, logButton2Color;
                switch (logMode)
                {
                    case 3:  logButton1Color = Color.Maroon; logButton2Color = Color.Maroon; break;
                    case 2:  logButton1Color = Color.Gray;   logButton2Color = Color.Maroon; break;
                    case 1:  logButton1Color = Color.Maroon; logButton2Color = Color.Gray;   break;
                    default: logButton1Color = Color.Gray;   logButton2Color = Color.Gray;   break;
                }
                DrawRectangleRec(logButton1, logButton1Color);
                DrawText("Hide", (int)logButton1.X + 7, (int)logButton1.Y + 3, 10, Color.White);
                DrawText("Repeat", (int)logButton1.X + 7, (int)logButton1.Y + 13, 10, Color.White);
                DrawRectangleRec(logButton2, logButton2Color);
                DrawText("Hide", (int)logButton1.X + 62, (int)logButton1.Y + 3, 10, Color.White);
                DrawText("Hold", (int)logButton1.X + 62, (int)logButton1.Y + 13, 10, Color.White);

                DrawText("Angle", (int)protractorPosition.X + 55, (int)protractorPosition.Y + 76, 10, Color.Black);
                string angleString = currentAngleDegrees.ToString("F6", CultureInfo.InvariantCulture);
                int angleStringDot = angleString.IndexOf('.');
                string angleStringTrim = angleString[..(angleStringDot + 3)];
                DrawText(angleStringTrim, (int)protractorPosition.X + 55, (int)protractorPosition.Y + 92, 20, gestureColor);
                DrawCircleV(protractorPosition, 80.0f, Color.White);
                DrawLineEx(new Vector2(protractorPosition.X - 90, protractorPosition.Y), new Vector2(protractorPosition.X + 90, protractorPosition.Y), 3.0f, Color.LightGray);
                DrawLineEx(new Vector2(protractorPosition.X, protractorPosition.Y - 90), new Vector2(protractorPosition.X, protractorPosition.Y + 90), 3.0f, Color.LightGray);
                DrawLineEx(new Vector2(protractorPosition.X - 80, protractorPosition.Y - 45), new Vector2(protractorPosition.X + 80, protractorPosition.Y + 45), 3.0f, Color.Green);
                DrawLineEx(new Vector2(protractorPosition.X - 80, protractorPosition.Y + 45), new Vector2(protractorPosition.X + 80, protractorPosition.Y - 45), 3.0f, Color.Green);
                DrawText("0", (int)protractorPosition.X + 96, (int)protractorPosition.Y - 9, 20, Color.Black);
                DrawText("30", (int)protractorPosition.X + 74, (int)protractorPosition.Y - 68, 20, Color.Black);
                DrawText("90", (int)protractorPosition.X - 11, (int)protractorPosition.Y - 110, 20, Color.Black);
                DrawText("150", (int)protractorPosition.X - 100, (int)protractorPosition.Y - 68, 20, Color.Black);
                DrawText("180", (int)protractorPosition.X - 124, (int)protractorPosition.Y - 9, 20, Color.Black);
                DrawText("210", (int)protractorPosition.X - 100, (int)protractorPosition.Y + 50, 20, Color.Black);
                DrawText("270", (int)protractorPosition.X - 18, (int)protractorPosition.Y + 92, 20, Color.Black);
                DrawText("330", (int)protractorPosition.X + 72, (int)protractorPosition.Y + 50, 20, Color.Black);
                if (currentAngleDegrees != 0.0f) DrawLineEx(protractorPosition, finalVector, 3.0f, gestureColor);

                if (currentGesture != (int)Gesture.None)
                {
                    if (touchCount != 0)
                    {
                        for (int i = 0; i < touchCount; i++)
                        {
                            DrawCircleV(touchPosition[i], 50.0f, Fade(gestureColor, 0.5f));
                            DrawCircleV(touchPosition[i], 5.0f, gestureColor);
                        }

                        if (touchCount == 2) DrawLineEx(touchPosition[0], touchPosition[1], ((currentGesture == 512) ? 8.0f : 12.0f), gestureColor);
                    }
                    else
                    {
                        DrawCircleV(mousePosition, 35.0f, Fade(gestureColor, 0.5f));
                        DrawCircleV(mousePosition, 5.0f, gestureColor);
                    }
                }

            EndDrawing();
        }

        CloseWindow();
    }

    private static string GetGestureName(int gesture) => gesture switch
    {
        0 => "None",
        1 => "Tap",
        2 => "Double Tap",
        4 => "Hold",
        8 => "Drag",
        16 => "Swipe Right",
        32 => "Swipe Left",
        64 => "Swipe Up",
        128 => "Swipe Down",
        256 => "Pinch In",
        512 => "Pinch Out",
        _ => "Unknown",
    };

    private static Color GetGestureColor(int gesture) => gesture switch
    {
        0 => Color.Black,
        1 => Color.Blue,
        2 => Color.SkyBlue,
        4 => Color.Black,
        8 => Color.Lime,
        16 => Color.Red,
        32 => Color.Red,
        64 => Color.Red,
        128 => Color.Red,
        256 => Color.Violet,
        512 => Color.Orange,
        _ => Color.Black,
    };
}
