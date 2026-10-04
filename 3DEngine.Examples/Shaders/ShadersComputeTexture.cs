using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersComputeTexture
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] compute texture");

        // A texture a compute shader paints each frame, a thread a texel, drawn as any texture is.
        var texture = LoadTextureFromImage(GenImageColor(256, 256, Color.Black));
        var plasma = LoadComputeShader("resources/shaders/plasma.slang");
        var timeAt = GetShaderLocation(plasma, "time");
        SetShaderValueTexture(plasma, GetShaderLocation(plasma, "image"), texture);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(new Color(20, 22, 28));
            DrawTextureEx(texture, new Vector2(272, 80), 0, 1, Color.White);
            DrawRectangleLines(271, 79, 258, 258, Color.Gray);
            DrawText("A compute shader paints this texture every frame", 10, 10, 20, Color.RayWhite);
            DrawFPS(10, 420);
            EndDrawing();

            // Painted after each frame for the next, which the dispatch runs on the GPU ahead of,
            // since a texture reaches the GPU in the frame after it is loaded.
            SetShaderValue(plasma, timeAt, (float)GetTime());
            ComputeShaderDispatch(plasma, 256 / 8, 256 / 8, 1);
        }

        UnloadShader(plasma);
        UnloadTexture(texture);
        CloseWindow();
    }
}
