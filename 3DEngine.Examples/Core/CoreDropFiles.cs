// raylib's core_drop_files example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreDropFiles
{
    private const int MAX_FILEPATH_RECORDED = 4096;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] drop files");

        int filePathCounter = 0;
        string[] filePaths = new string[MAX_FILEPATH_RECORDED];

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsFileDropped())
            {
                string[] droppedFiles = LoadDroppedFiles();

                for (int i = 0, offset = filePathCounter; i < droppedFiles.Length; i++)
                {
                    if (filePathCounter < (MAX_FILEPATH_RECORDED - 1))
                    {
                        filePaths[offset + i] = droppedFiles[i];
                        filePathCounter++;
                    }
                }

                UnloadDroppedFiles();
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (filePathCounter == 0) DrawText("Drop your files to this window!", 100, 40, 20, Color.DarkGray);
                else
                {
                    DrawText("Dropped files:", 100, 40, 20, Color.DarkGray);

                    for (int i = 0; i < filePathCounter; i++)
                    {
                        if (i%2 == 0) DrawRectangle(0, 85 + 40*i, screenWidth, 40, Fade(Color.LightGray, 0.5f));
                        else DrawRectangle(0, 85 + 40*i, screenWidth, 40, Fade(Color.LightGray, 0.3f));

                        DrawText(filePaths[i], 120, 100 + 40*i, 10, Color.Gray);
                    }

                    DrawText("Drop new files...", 100, 110 + 40*filePathCounter, 20, Color.DarkGray);
                }

            EndDrawing();
        }

        CloseWindow();
    }
}
