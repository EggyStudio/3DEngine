// raylib's shaders_color_correction example, Copyright (c) 2025 Jordi Santonja (@JordSant), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersColorCorrection
{
    private const int MAX_TEXTURES = 4;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] color correction");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Texture2D[] texture =
        [
            LoadTexture("resources/parrots.png"),
            LoadTexture("resources/cat.png"),
            LoadTexture("resources/mandrill.png"),
            LoadTexture("resources/fudesumi.png"),
        ];

        // raylib's color_correction.fs, written in Slang
        Shader shdrColorCorrection = LoadShader("resources/shaders/slang/color_correction.slang");

        int imageIndex = 0;
        bool resetButtonClicked = false;

        float contrast = 0.0f;
        float saturation = 0.0f;
        float brightness = 0.0f;

        int contrastLoc = GetShaderLocation(shdrColorCorrection, "contrast");
        int saturationLoc = GetShaderLocation(shdrColorCorrection, "saturation");
        int brightnessLoc = GetShaderLocation(shdrColorCorrection, "brightness");

        SetShaderValue(shdrColorCorrection, contrastLoc, contrast);
        SetShaderValue(shdrColorCorrection, saturationLoc, saturation);
        SetShaderValue(shdrColorCorrection, brightnessLoc, brightness);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // 1 to 4 pick the picture,
            if (IsKeyPressed(Key.One)) imageIndex = 0;
            else if (IsKeyPressed(Key.Two)) imageIndex = 1;
            else if (IsKeyPressed(Key.Three)) imageIndex = 2;
            else if (IsKeyPressed(Key.Four)) imageIndex = 3;

            // and R or the button sets the values back to 0.
            if (IsKeyPressed(Key.R) || resetButtonClicked)
            {
                contrast = 0.0f;
                saturation = 0.0f;
                brightness = 0.0f;
            }

            SetShaderValue(shdrColorCorrection, contrastLoc, contrast);
            SetShaderValue(shdrColorCorrection, saturationLoc, saturation);
            SetShaderValue(shdrColorCorrection, brightnessLoc, brightness);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginShaderMode(shdrColorCorrection);
                    DrawTexture(texture[imageIndex], 580/2 - texture[imageIndex].Width/2, GetScreenHeight()/2 - texture[imageIndex].Height/2, Color.White);
                EndShaderMode();

                DrawLine(580, 0, 580, GetScreenHeight(), new Color(218, 218, 218, 255));
                DrawRectangle(580, 0, GetScreenWidth(), GetScreenHeight(), new Color(232, 232, 232, 255));

                DrawText("Color Correction", 585, 40, 20, Color.Gray);
                DrawText("Picture", 602, 75, 10, Color.Gray);
                DrawText("Press [1] - [4] to Change Picture", 600, 230, 8, Color.Gray);
                DrawText("Press [R] to Reset Values", 600, 250, 8, Color.Gray);

                // raygui's controls, as ImGui's, where raygui places them: its toggle group as four
                // buttons, the one picked staying down, and its sliders each named on its left.
                // The window reaches left of the panel, so the names before the sliders are not
                // cut off, ImGui's font being wider than raygui's.
                ImGui.SetNextWindowPos(new Vector2(500, 0));
                ImGui.SetNextWindowSize(new Vector2(300, screenHeight));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);

                for (int i = 0; i < MAX_TEXTURES; i++)
                {
                    ImGui.SetCursorScreenPos(new Vector2(645 + 22*i, 70));
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(i == imageIndex ? ImGuiCol.ButtonActive : ImGuiCol.Button));
                    if (ImGui.Button($"{i + 1}", new Vector2(20, 20))) imageIndex = i;
                    ImGui.PopStyleColor();
                }

                Slider(100, "Contrast", ref contrast);
                Slider(130, "Saturation", ref saturation);
                Slider(160, "Brightness", ref brightness);

                ImGui.SetCursorScreenPos(new Vector2(645, 190));
                resetButtonClicked = ImGui.Button("Reset", new Vector2(40, 20));

                ImGui.End();

                DrawFPS(710, 10);

            EndDrawing();
        }

        for (int i = 0; i < MAX_TEXTURES; i++) UnloadTexture(texture[i]);
        UnloadShader(shdrColorCorrection);

        CloseWindow();
    }

    // A slider at x 645, 120 wide, its name ending 5 pixels left of it and its value inside.
    private static void Slider(float y, string label, ref float value)
    {
        ImGui.SetCursorScreenPos(new Vector2(640 - ImGui.CalcTextSize(label).X, y));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SetCursorScreenPos(new Vector2(645, y));
        ImGui.SetNextItemWidth(120);
        ImGui.SliderFloat("##" + label, ref value, -100.0f, 100.0f, "%.0f");
    }
}
