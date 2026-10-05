// raylib's core_input_gestures example, Copyright (c) 2016-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputGestures
{
    private const int MAX_GESTURE_STRINGS = 20;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input gestures");

        Vector2 touchPosition = new(0, 0);
        Rectangle touchArea = new(220, 10, screenWidth - 230.0f, screenHeight - 20.0f);

        int gesturesCount = 0;
        string[] gestureStrings = new string[MAX_GESTURE_STRINGS];

        Gesture currentGesture = Gesture.None;
        Gesture lastGesture = Gesture.None;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            lastGesture = currentGesture;
            currentGesture = GetGestureDetected();
            touchPosition = GetTouchPosition(0);

            if (CheckCollisionPointRec(touchPosition, touchArea) && (currentGesture != Gesture.None))
            {
                if (currentGesture != lastGesture)
                {
                    switch (currentGesture)
                    {
                        case Gesture.Tap: gestureStrings[gesturesCount] = "GESTURE TAP"; break;
                        case Gesture.DoubleTap: gestureStrings[gesturesCount] = "GESTURE DOUBLETAP"; break;
                        case Gesture.Hold: gestureStrings[gesturesCount] = "GESTURE HOLD"; break;
                        case Gesture.Drag: gestureStrings[gesturesCount] = "GESTURE DRAG"; break;
                        case Gesture.SwipeRight: gestureStrings[gesturesCount] = "GESTURE SWIPE RIGHT"; break;
                        case Gesture.SwipeLeft: gestureStrings[gesturesCount] = "GESTURE SWIPE LEFT"; break;
                        case Gesture.SwipeUp: gestureStrings[gesturesCount] = "GESTURE SWIPE UP"; break;
                        case Gesture.SwipeDown: gestureStrings[gesturesCount] = "GESTURE SWIPE DOWN"; break;
                        case Gesture.PinchIn: gestureStrings[gesturesCount] = "GESTURE PINCH IN"; break;
                        case Gesture.PinchOut: gestureStrings[gesturesCount] = "GESTURE PINCH OUT"; break;
                        default: break;
                    }

                    gesturesCount++;

                    if (gesturesCount >= MAX_GESTURE_STRINGS)
                    {
                        for (int i = 0; i < MAX_GESTURE_STRINGS; i++) gestureStrings[i] = "";

                        gesturesCount = 0;
                    }
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectangleRec(touchArea, Color.Gray);
                DrawRectangle(225, 15, screenWidth - 240, screenHeight - 30, Color.RayWhite);

                DrawText("GESTURES TEST AREA", screenWidth - 270, screenHeight - 40, 20, Fade(Color.Gray, 0.5f));

                for (int i = 0; i < gesturesCount; i++)
                {
                    if (i%2 == 0) DrawRectangle(10, 30 + 20*i, 200, 20, Fade(Color.LightGray, 0.5f));
                    else DrawRectangle(10, 30 + 20*i, 200, 20, Fade(Color.LightGray, 0.3f));

                    if (i < gesturesCount - 1) DrawText(gestureStrings[i], 35, 36 + 20*i, 10, Color.DarkGray);
                    else DrawText(gestureStrings[i], 35, 36 + 20*i, 10, Color.Maroon);
                }

                DrawRectangleLines(10, 29, 200, screenHeight - 50, Color.Gray);
                DrawText("DETECTED GESTURES", 50, 15, 10, Color.Gray);

                if (currentGesture != Gesture.None) DrawCircleV(touchPosition, 30, Color.Maroon);

            EndDrawing();
        }

        CloseWindow();
    }
}
