using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreBasicWindow
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] basic window");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);
            DrawText("Congrats! You created your first window!", 190, 200, 20, Color.LightGray);
            EndDrawing();
        }

        CloseWindow();
    }
}
