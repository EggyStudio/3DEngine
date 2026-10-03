using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreWindowFlags
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] window flags");
        SetWindowMinSize(320, 240);
        var resizes = 0;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.F)) ToggleFullscreen();
            if (IsKeyPressed(Key.R)) SetWindowSize(GetScreenWidth() == 800 ? 640 : 800, GetScreenHeight() == 450 ? 360 : 450);
            if (IsKeyPressed(Key.M)) MaximizeWindow();
            if (IsKeyPressed(Key.N)) RestoreWindow();
            if (IsKeyPressed(Key.C)) SetClipboardText($"{GetScreenWidth()}x{GetScreenHeight()}");
            if (IsWindowResized()) resizes++;

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            var monitor = GetCurrentMonitor();
            var position = GetWindowPosition();
            DrawText($"Window {GetScreenWidth()}x{GetScreenHeight()} at {position.X},{position.Y}, resized {resizes} time(s)", 20, 20, 20, Color.DarkGray);
            DrawText($"Monitor {monitor} of {GetMonitorCount()}: {GetMonitorName(monitor)}", 20, 60, 20, Color.DarkGray);
            DrawText($"{GetMonitorWidth(monitor)}x{GetMonitorHeight(monitor)} at {GetMonitorRefreshRate(monitor)} Hz", 20, 90, 20, Color.DarkGray);

            var y = 140;
            foreach (var (name, on) in new[]
                     {
                         ("fullscreen", IsWindowFullscreen()), ("maximized", IsWindowMaximized()), ("minimized", IsWindowMinimized()),
                         ("focused", IsWindowFocused()), ("hidden", IsWindowHidden()),
                     })
            {
                DrawText(name, 40, y, 20, on ? Color.Lime : Color.Gray);
                y += 26;
            }

            DrawText("F fullscreen, R resize, M maximize, N restore, C copies the size", 20, GetScreenHeight() - 40, 20, Color.Gray);
            EndDrawing();
        }

        CloseWindow();
    }
}
