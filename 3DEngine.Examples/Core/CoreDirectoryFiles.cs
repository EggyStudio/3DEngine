// raylib's core_directory_files example, Copyright (c) 2025 Hugo ARNAL (@hugoarnal), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreDirectoryFiles
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] directory files");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        string directory = Directory.GetCurrentDirectory();

        // raylib's LoadDirectoryFilesEx with the filter "DIRS*;.png;.c" is C#'s Directory here.
        string[] files = LoadDirectoryFiles(directory);

        bool btnBackPressed = false;

        int listItemActive = -1;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (btnBackPressed)
            {
                directory = Path.GetDirectoryName(directory) ?? directory;
                files = LoadDirectoryFiles(directory);

                listItemActive = -1;
            }

            if ((listItemActive >= 0) && (listItemActive < files.Length) && Directory.Exists(files[listItemActive]))
            {
                directory = files[listItemActive];
                files = LoadDirectoryFiles(directory);

                listItemActive = -1;
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                // raygui's controls, as ImGui's, where raygui places them, the directory at twice
                // the text's size as raygui draws it, and the list a list box as tall as the rest
                // of the window, each item's text 40 pixels in.
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);

                ImGui.SetCursorScreenPos(new Vector2(40, 10));
                btnBackPressed = ImGui.Button("<", new Vector2(48, 28));

                ImGui.SetWindowFontScale(2);
                ImGui.SetCursorScreenPos(new Vector2(40 + 48 + 10, 10 + (28 - ImGui.GetTextLineHeight())/2));
                ImGui.TextUnformatted(directory);
                ImGui.SetWindowFontScale(1);

                ImGui.SetCursorScreenPos(new Vector2(0, 50));
                if (ImGui.BeginListBox("##files", new Vector2(GetScreenWidth(), GetScreenHeight() - 50)))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        float y = ImGui.GetCursorPosY();
                        if (ImGui.Selectable($"##{i}", i == listItemActive, ImGuiSelectableFlags.None, new Vector2(0, 28))) listItemActive = i;
                        ImGui.SameLine(40);
                        ImGui.SetCursorPosY(y + (28 - ImGui.GetTextLineHeight())/2);
                        ImGui.TextUnformatted(files[i]);
                    }
                    ImGui.EndListBox();
                }

                ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }

    // The directories and the .png and .c files in a directory, by name, where raylib lists them in
    // the order the file system gives.
    private static string[] LoadDirectoryFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(directory)
                .Where(path => Directory.Exists(path) || Path.GetExtension(path) is ".png" or ".c")
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            TraceLog(LogLevel.Warning, $"FILEIO: Directory cannot be opened ({directory})");
            return [];
        }
    }
}
