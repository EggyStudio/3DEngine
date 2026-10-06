// raylib's core_monitor_detector example, Copyright (c) 2025 Maicon Santana (@maiconpintoabreu), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreMonitorDetector
{
    private const int MAX_MONITORS = 10;

    // Monitor info
    private struct MonitorInfo
    {
        public Vector2 position;
        public string name;
        public int width;
        public int height;
        public int physicalWidth;
        public int physicalHeight;
        public int refreshRate;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] monitor detector");

        MonitorInfo[] monitors = new MonitorInfo[MAX_MONITORS];
        int currentMonitorIndex = GetCurrentMonitor();
        int monitorCount = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Variables to find the max x and Y to calculate the scale
            int maxWidth = 1;
            int maxHeight = 1;

            // Monitor offset is to fix when monitor position x is negative
            int monitorOffsetX = 0;

            // Rebuild monitors array every frame
            monitorCount = Math.Min(GetMonitorCount(), MAX_MONITORS);
            for (int i = 0; i < monitorCount; i++)
            {
                monitors[i] = new MonitorInfo
                {
                    position = GetMonitorPosition(i),
                    name = GetMonitorName(i),
                    width = GetMonitorWidth(i),
                    height = GetMonitorHeight(i),
                    physicalWidth = GetMonitorPhysicalWidth(i),
                    physicalHeight = GetMonitorPhysicalHeight(i),
                    refreshRate = GetMonitorRefreshRate(i),
                };

                if (monitors[i].position.X < monitorOffsetX) monitorOffsetX = -(int)monitors[i].position.X;

                int width = (int)monitors[i].position.X + monitors[i].width;
                int height = (int)monitors[i].position.Y + monitors[i].height;

                if (maxWidth < width) maxWidth = width;
                if (maxHeight < height) maxHeight = height;
            }

            if (IsKeyPressed(Key.Enter) && (monitorCount > 1))
            {
                currentMonitorIndex += 1;

                // Set index to 0 if the last one
                if (currentMonitorIndex == monitorCount) currentMonitorIndex = 0;

                SetWindowMonitor(currentMonitorIndex); // Move window to currentMonitorIndex
            }
            else currentMonitorIndex = GetCurrentMonitor(); // Get currentMonitorIndex if manually moved

            float monitorScale = 0.6f;

            if (maxHeight > (maxWidth + monitorOffsetX)) monitorScale *= ((float)screenHeight/(float)maxHeight);
            else monitorScale *= ((float)screenWidth/(float)(maxWidth + monitorOffsetX));

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("Press [Enter] to move window to next monitor available", 20, 20, 20, Color.DarkGray);

                DrawRectangleLines(20, 60, screenWidth - 40, screenHeight - 100, Color.DarkGray);

                // Draw Monitor Rectangles with information inside
                for (int i = 0; i < monitorCount; i++)
                {
                    // Calculate rectangle position and size using monitorScale
                    Rectangle rec = new(
                        (monitors[i].position.X + monitorOffsetX)*monitorScale + 140,
                        monitors[i].position.Y*monitorScale + 80,
                        monitors[i].width*monitorScale,
                        monitors[i].height*monitorScale);

                    // Draw monitor name and information inside the rectangle
                    DrawText($"[{i}] {monitors[i].name}", (int)rec.X + 10, (int)rec.Y + (int)(100*monitorScale), (int)(120*monitorScale), Color.Blue);
                    DrawText(
                        $"Resolution: [{monitors[i].width}px x {monitors[i].height}px]\nRefreshRate: [{monitors[i].refreshRate}hz]\nPhysical Size: [{monitors[i].physicalWidth}mm x {monitors[i].physicalHeight}mm]\nPosition: {monitors[i].position.X,3:0} x {monitors[i].position.Y,3:0}",
                        (int)rec.X + 10, (int)rec.Y + (int)(200*monitorScale), (int)(120*monitorScale), Color.DarkGray);

                    // Highlight current monitor
                    if (i == currentMonitorIndex)
                    {
                        DrawRectangleLinesEx(rec, 5, Color.Red);
                        Vector2 windowPosition = new((GetWindowPosition().X + monitorOffsetX)*monitorScale  + 140, GetWindowPosition().Y*monitorScale + 80);

                        // Draw window position based on monitors
                        DrawRectangleV(windowPosition, new Vector2(screenWidth*monitorScale, screenHeight*monitorScale), Fade(Color.Green, 0.5f));
                    }
                    else DrawRectangleLinesEx(rec, 5, Color.Gray);
                }

            EndDrawing();
        }

        CloseWindow();
    }
}
