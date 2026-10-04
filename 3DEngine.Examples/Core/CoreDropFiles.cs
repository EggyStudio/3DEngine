using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreDropFiles
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] drop files");

        // The paths dropped so far. A drop is kept by the engine until it is unloaded, so the
        // program takes each one into a list of its own.
        var files = new List<string>();
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsFileDropped())
            {
                files.AddRange(LoadDroppedFiles());
                UnloadDroppedFiles();
            }

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            if (files.Count == 0)
                DrawText("Drop your files to this window!", 100, 40, 20, Color.DarkGray);
            else
            {
                DrawText("Dropped files:", 100, 40, 20, Color.DarkGray);
                for (int i = 0; i < files.Count; i++)
                {
                    DrawRectangle(0, 85 + 40 * i, GetScreenWidth(), 40, Color.LightGray.Fade(i % 2 == 0 ? 0.5f : 0.3f));
                    DrawText(files[i], 120, 100 + 40 * i, 10, Color.Gray);
                }
                DrawText("Drop new files...", 100, 110 + 40 * files.Count, 20, Color.DarkGray);
            }

            EndDrawing();
        }

        CloseWindow();
    }
}
