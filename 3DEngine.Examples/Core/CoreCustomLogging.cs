// raylib's core_custom_logging example, Copyright (c) 2018-2025 Pablo Marcos Oltra (@pamarcos) and Ramon Santamaria (@raysan5),
// under the zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreCustomLogging
{
    // Each line the engine logs, with the time and its level in front, as raylib's example prints it.
    private static void CustomTraceLog(LogLevel msgType, string text)
    {
        Console.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");

        switch (msgType)
        {
            case LogLevel.Info: Console.Write("[INFO] : "); break;
            case LogLevel.Error: Console.Write("[ERROR]: "); break;
            case LogLevel.Warning: Console.Write("[WARN] : "); break;
            case LogLevel.Debug: Console.Write("[DEBUG]: "); break;
            default: break;
        }

        Console.WriteLine(text);
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetTraceLogCallback(CustomTraceLog);

        InitWindow(screenWidth, screenHeight, "[core] custom logging");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

            ClearBackground(Color.RayWhite);

            DrawText("Check out the console output to see the custom logger in action!", 60, 200, 20, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
