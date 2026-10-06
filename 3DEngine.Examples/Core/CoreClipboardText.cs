// raylib's core_clipboard_text example, Copyright (c) 2025 Ananth S (@Ananth1839), under the zlib license,
// written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreClipboardText
{
    private const int MAX_TEXT_SAMPLES = 5;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] clipboard text");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        string[] sampleTexts =
        [
            "Hello from raylib!",
            "The quick brown fox jumps over the lazy dog",
            "Clipboard operations are useful!",
            "raylib is a simple and easy-to-use library",
            "Copy and paste me!",
        ];

        string? clipboardText = null;
        string inputBuffer = "Hello from raylib!";

        bool btnCutPressed = false;
        bool btnCopyPressed = false;
        bool btnPastePressed = false;
        bool btnClearPressed = false;
        bool btnRandomPressed = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (btnCutPressed)
            {
                SetClipboardText(inputBuffer);
                clipboardText = GetClipboardText();
                inputBuffer = "";
            }

            if (btnCopyPressed)
            {
                SetClipboardText(inputBuffer);
                clipboardText = GetClipboardText();
            }

            if (btnPastePressed)
            {
                clipboardText = GetClipboardText();
                if (clipboardText != null) inputBuffer = clipboardText;
            }

            if (btnClearPressed) inputBuffer = "";

            if (btnRandomPressed) inputBuffer = sampleTexts[GetRandomValue(0, MAX_TEXT_SAMPLES - 1)];

            // Cut, copy and paste from the keyboard.
            if (IsKeyDown(Key.LeftControl) || IsKeyDown(Key.RightControl))
            {
                if (IsKeyPressed(Key.X))
                {
                    SetClipboardText(inputBuffer);
                    inputBuffer = "";
                }

                if (IsKeyPressed(Key.C)) SetClipboardText(inputBuffer);

                if (IsKeyPressed(Key.V))
                {
                    clipboardText = GetClipboardText();
                    if (clipboardText != null) inputBuffer = clipboardText;
                }
            }

            BeginDrawing();

            ClearBackground(Color.RayWhite);

            DrawText("[CTRL+X] - CUT | [CTRL+C] COPY | [CTRL+V] | PASTE", 50, 60, 20, Color.Maroon);

            // raygui's controls, as ImGui's, where raygui places them, at raygui's text size of 20.
            // raygui's icons on the buttons are left out, ImGui's font having none, and the random
            // text button says so instead.
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new Vector2(screenWidth, screenHeight));
            ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
            ImGui.SetWindowFontScale(20.0f/13.0f);

            Label(new Rectangle(50, 20, 700, 36), "Use the BUTTONS or KEY SHORTCUTS:");

            ImGui.SetCursorScreenPos(new Vector2(50, 120));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, (40 - ImGui.GetFontSize())/2));
            ImGui.SetNextItemWidth(652);
            ImGui.InputText("##input", ref inputBuffer, 256);
            ImGui.PopStyleVar();

            ImGui.SetCursorScreenPos(new Vector2(50 + 652 + 8, 120));
            btnRandomPressed = ImGui.Button("RND", new Vector2(40, 40));

            ImGui.SetCursorScreenPos(new Vector2(50, 180));
            btnCutPressed = ImGui.Button("CUT", new Vector2(158, 40));
            ImGui.SetCursorScreenPos(new Vector2(50 + 165, 180));
            btnCopyPressed = ImGui.Button("COPY", new Vector2(158, 40));
            ImGui.SetCursorScreenPos(new Vector2(50 + 165*2, 180));
            btnPastePressed = ImGui.Button("PASTE", new Vector2(158, 40));
            ImGui.SetCursorScreenPos(new Vector2(50 + 165*3, 180));
            btnClearPressed = ImGui.Button("CLEAR", new Vector2(158, 40));

            ImGui.BeginDisabled();
            Label(new Rectangle(50, 260, 700, 40), "Clipboard current text data:");
            string shown = clipboardText ?? "";
            ImGui.SetCursorScreenPos(new Vector2(50, 300));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, (40 - ImGui.GetFontSize())/2));
            ImGui.SetNextItemWidth(700);
            ImGui.InputText("##clipboard", ref shown, 256, ImGuiInputTextFlags.ReadOnly);
            ImGui.PopStyleVar();
            Label(new Rectangle(50, 360, 700, 40), "Try copying text from other applications and pasting here!");
            ImGui.EndDisabled();

            ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }

    // raygui's label, its text centered on the rectangle's height.
    private static void Label(Rectangle bounds, string text)
    {
        ImGui.SetCursorScreenPos(new Vector2(bounds.X, bounds.Y + (bounds.Height - ImGui.GetTextLineHeight())/2));
        ImGui.TextUnformatted(text);
    }
}
