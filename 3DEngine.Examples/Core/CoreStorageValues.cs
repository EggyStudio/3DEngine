// raylib's core_storage_values example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreStorageValues
{
    private const string STORAGE_DATA_FILE = "storage.data";

    private const int STORAGE_POSITION_SCORE = 0;
    private const int STORAGE_POSITION_HISCORE = 1;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] storage values");

        int score = 0;
        int hiscore = 0;
        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.R))
            {
                score = GetRandomValue(1000, 2000);
                hiscore = GetRandomValue(2000, 4000);
            }

            if (IsKeyPressed(Key.Enter))
            {
                SaveStorageValue(STORAGE_POSITION_SCORE, score);
                SaveStorageValue(STORAGE_POSITION_HISCORE, hiscore);
            }
            else if (IsKeyPressed(Key.Space))
            {
                score = LoadStorageValue(STORAGE_POSITION_SCORE);
                hiscore = LoadStorageValue(STORAGE_POSITION_HISCORE);
            }

            framesCounter++;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText($"SCORE: {score}", 280, 130, 40, Color.Maroon);
                DrawText($"HI-SCORE: {hiscore}", 210, 200, 50, Color.Black);

                DrawText($"frames: {framesCounter}", 10, 10, 20, Color.Lime);

                DrawText("Press R to generate random numbers", 220, 40, 20, Color.LightGray);
                DrawText("Press ENTER to SAVE values", 250, 310, 20, Color.LightGray);
                DrawText("Press SPACE to LOAD values", 252, 350, 20, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }

    // The file is the values one after another, four bytes each, grown to hold the position written.
    private static bool SaveStorageValue(int position, int value)
    {
        byte[]? fileData = LoadFileData(STORAGE_DATA_FILE);
        if (fileData is null) TraceLog(LogLevel.Info, $"FILEIO: [{STORAGE_DATA_FILE}] File created successfully");

        var data = fileData ?? [];
        if (data.Length < (position + 1)*sizeof(int)) Array.Resize(ref data, (position + 1)*sizeof(int));
        BitConverter.TryWriteBytes(data.AsSpan(position*sizeof(int)), value);

        bool success = SaveFileData(STORAGE_DATA_FILE, data);
        TraceLog(LogLevel.Info, $"FILEIO: [{STORAGE_DATA_FILE}] Saved storage value: {value}");
        return success;
    }

    private static int LoadStorageValue(int position)
    {
        int value = 0;
        byte[]? fileData = LoadFileData(STORAGE_DATA_FILE);

        if (fileData is not null)
        {
            if (fileData.Length < (position + 1)*sizeof(int)) TraceLog(LogLevel.Warning, $"FILEIO: [{STORAGE_DATA_FILE}] Failed to find storage position: {position}");
            else value = BitConverter.ToInt32(fileData, position*sizeof(int));

            TraceLog(LogLevel.Info, $"FILEIO: [{STORAGE_DATA_FILE}] Loaded storage value: {value}");
        }

        return value;
    }
}
