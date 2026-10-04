using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputGestures
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] input gestures");

        // The gestures seen, newest first, which a mouse makes as well as a finger.
        var seen = new List<string>();
        var area = new Rectangle(20, 60, 500, 360);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            var gesture = GetGestureDetected();
            if (gesture != Gesture.None && gesture != Gesture.Hold && gesture != Gesture.Drag && (seen.Count == 0 || seen[0] != gesture.ToString()))
            {
                seen.Insert(0, gesture.ToString());
                if (seen.Count > 14) seen.RemoveAt(seen.Count - 1);
            }

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            DrawRectangleRec(area, Color.LightGray.Fade(0.4f));
            DrawRectangleLinesEx(area, 2, Color.Gray);
            DrawText("Tap, hold, drag, swipe or pinch here", 40, 80, 20, Color.Gray);
            for (int i = 0; i < GetTouchPointCount(); i++)
                DrawCircleV(GetTouchPosition(i), 24, Color.Maroon.Fade(0.6f));

            var current = gesture switch
            {
                Gesture.Hold => $"Hold, {GetGestureHoldDuration():0.0} s",
                Gesture.Drag => $"Drag, {GetGestureDragAngle():0} degrees",
                _ => gesture.ToString(),
            };
            DrawText(current, 40, 380, 24, Color.DarkBlue);

            DrawText("Gestures seen", 550, 60, 20, Color.DarkGray);
            for (int i = 0; i < seen.Count; i++)
                DrawText(seen[i], 550, 90 + i * 24, 20, i == 0 ? Color.Maroon : Color.Gray);

            EndDrawing();
        }

        CloseWindow();
    }
}
