// raylib's shaders_palette_switch example, Copyright (c) 2019-2025 Marco Lizza (@MarcoLizza) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersPaletteSwitch
{
    private const int MAX_PALETTES = 3;
    private const int COLORS_PER_PALETTE = 8;

    // A palette's color, three whole numbers as the shader's int3 takes them
    private readonly record struct Int3(int R, int G, int B);

    private static readonly Int3[][] palettes =
    [
        [   // 3-BIT RGB
            new(0, 0, 0),
            new(255, 0, 0),
            new(0, 255, 0),
            new(0, 0, 255),
            new(0, 255, 255),
            new(255, 0, 255),
            new(255, 255, 0),
            new(255, 255, 255),
        ],
        [   // AMMO-8 (GameBoy-like)
            new(4, 12, 6),
            new(17, 35, 24),
            new(30, 58, 41),
            new(48, 93, 66),
            new(77, 128, 97),
            new(137, 162, 87),
            new(190, 220, 127),
            new(238, 255, 204),
        ],
        [   // RKBV (2-strip film)
            new(21, 25, 26),
            new(138, 76, 88),
            new(217, 98, 117),
            new(230, 184, 193),
            new(69, 107, 115),
            new(75, 151, 166),
            new(165, 189, 194),
            new(255, 245, 247),
        ],
    ];

    private static readonly string[] paletteText =
    [
        "3-BIT RGB",
        "AMMO-8 (GameBoy-like)",
        "RKBV (2-strip film)",
    ];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] palette switch");

        // raylib's palette_switch.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/palette_switch.slang");

        // The palette's place in the shader, -1 where it has none
        int paletteLoc = GetShaderLocation(shader, "palette");

        int currentPalette = 0;
        int lineHeight = screenHeight/COLORS_PER_PALETTE;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Right)) currentPalette++;
            else if (IsKeyPressed(Key.Left)) currentPalette--;

            if (currentPalette >= MAX_PALETTES) currentPalette = 0;
            else if (currentPalette < 0) currentPalette = MAX_PALETTES - 1;

            // The palette's colors, red, green and blue without alpha
            SetShaderValueV<Int3>(shader, paletteLoc, palettes[currentPalette]);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginShaderMode(shader);

                    for (int i = 0; i < COLORS_PER_PALETTE; i++)
                    {
                        // A band across the screen for each index, the index drawn as the color
                        DrawRectangle(0, lineHeight*i, GetScreenWidth(), lineHeight, new Color((byte)i, (byte)i, (byte)i, 255));
                    }

                EndShaderMode();

                DrawText("< >", 10, 10, 30, Color.DarkBlue);
                DrawText("CURRENT PALETTE:", 60, 15, 20, Color.RayWhite);
                DrawText(paletteText[currentPalette], 300, 15, 20, Color.Red);

                DrawFPS(700, 15);

            EndDrawing();
        }

        UnloadShader(shader);

        CloseWindow();
    }
}
