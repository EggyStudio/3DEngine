using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersComputeLife
{
    private const int Width = 200, Height = 112, CellSize = 4;

    public static void Run()
    {
        InitWindow(Width * CellSize, Height * CellSize, "[shaders] compute game of life");

        // Two grids the shader steps between, the one it reads and the one it writes, and the
        // pixels it colors, which reach the screen through a texture.
        var random = new Random(1);
        var start = new uint[Width * Height];
        for (int i = 0; i < start.Length; i++) start[i] = random.Next(4) == 0 ? 1u : 0u;
        var grids = new[] { LoadShaderBuffer<uint>(start), LoadShaderBuffer(Width * Height * 4) };
        var pixels = LoadShaderBuffer(Width * Height * 4);

        var life = LoadComputeShader("resources/shaders/life.slang");
        SetShaderValue(life, GetShaderLocation(life, "width"), Width);
        SetShaderValue(life, GetShaderLocation(life, "height"), Height);
        var (currentAt, nextAt) = (GetShaderLocation(life, "current"), GetShaderLocation(life, "next"));
        SetShaderValueBuffer(life, GetShaderLocation(life, "pixels"), pixels);

        var image = new Image(new byte[Width * Height * 4], Width, Height);
        var texture = LoadTextureFromImage(image);
        SetTextureFilter(texture, TextureFilter.Point);
        var step = 0;

        SetTargetFPS(30);

        while (!WindowShouldClose())
        {
            // Holding the left button brings cells to life under the pointer.
            if (IsMouseButtonDown(MouseButton.Left))
            {
                var mouse = GetMousePosition() / CellSize;
                for (int y = -2; y <= 2; y++)
                {
                    var (cx, cy) = ((int)mouse.X, (int)mouse.Y + y);
                    if (cy < 0 || cy >= Height) continue;
                    for (int x = Math.Max(0, cx - 2); x <= Math.Min(Width - 1, cx + 2); x++)
                        UpdateShaderBuffer<uint>(grids[step % 2], [1u], (cy * Width + x) * 4);
                }
            }

            SetShaderValueBuffer(life, currentAt, grids[step % 2]);
            SetShaderValueBuffer(life, nextAt, grids[(step + 1) % 2]);
            ComputeShaderDispatch(life, (Width + 15) / 16, (Height + 15) / 16, 1);
            step++;

            ReadShaderBuffer<byte>(pixels, image.Data);
            UpdateTexture(texture, image);

            BeginDrawing();
            ClearBackground(Color.Black);
            DrawTextureEx(texture, Vector2.Zero, 0, CellSize, Color.White);
            DrawRectangle(0, 0, Width * CellSize, 40, Color.Black.Fade(0.7f));
            DrawText($"Generation {step}, stepped by a compute shader. Hold the mouse to draw.", 10, 10, 20, Color.RayWhite);
            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadShaderBuffer(grids[0]);
        UnloadShaderBuffer(grids[1]);
        UnloadShaderBuffer(pixels);
        UnloadShader(life);
        CloseWindow();
    }
}
