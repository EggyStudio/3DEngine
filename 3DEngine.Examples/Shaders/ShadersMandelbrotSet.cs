// raylib's shaders_mandelbrot_set example, Copyright (c) 2025 Jordi Santonja (@JordSant), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersMandelbrotSet
{
    // Places worth seeing, each where it is and how near
    private static readonly (Vector2 Offset, float Zoom)[] pointsOfInterest =
    [
        (new(-1.76826775f, -0.00422996283f), 28435.9238f),
        (new(0.322004497f, -0.0357099883f), 56499.7266f),
        (new(-0.748880744f, -0.0562955774f), 9237.59082f),
        (new(-1.78385007f, -0.0156200649f), 14599.5283f),
        (new(-0.0985441282f, -0.924688697f), 26259.8535f),
        (new(0.317785531f, -0.0322612226f), 29297.9258f),
    ];

    private const int screenWidth = 800;
    private const int screenHeight = 450;
    private const float zoomSpeed = 1.01f;
    private const float offsetSpeedMul = 2.0f;

    private const float startingZoom = 0.6f;
    private static readonly Vector2 startingOffset = new(-0.5f, 0.0f);

    public static void Run()
    {
        InitWindow(screenWidth, screenHeight, "[shaders] mandelbrot set");

        // raylib's mandelbrot_set.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/mandelbrot_set.slang");

        // The texture the set is drawn across
        RenderTexture2D target = LoadRenderTexture(GetScreenWidth(), GetScreenHeight());

        // The view's place and zoom, centered and whole to begin with
        Vector2 offset = startingOffset;
        float zoom = startingZoom;

        // The steps a point takes grow as the view zooms in, for detail, by a rough rule. UP and
        // DOWN change them where it falls short. These are raylib's numbers on the desktop.
        int maxIterations = 333;
        float maxIterationsMultiplier = 166.5f;

        int zoomLoc = GetShaderLocation(shader, "zoom");
        int offsetLoc = GetShaderLocation(shader, "offset");
        int maxIterationsLoc = GetShaderLocation(shader, "maxIterations");

        SetShaderValue(shader, zoomLoc, zoom);
        SetShaderValue(shader, offsetLoc, offset);
        SetShaderValue(shader, maxIterationsLoc, maxIterations);

        bool showControls = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            bool updateShader = false;

            // 1 to 6 go to a place worth seeing.
            Key[] numbers = [Key.One, Key.Two, Key.Three, Key.Four, Key.Five, Key.Six];
            for (int i = 0; i < numbers.Length; i++)
            {
                if (IsKeyPressed(numbers[i]))
                {
                    (offset, zoom) = pointsOfInterest[i];
                    updateShader = true;
                    break;
                }
            }

            // R centers the view and resets its zoom.
            if (IsKeyPressed(Key.R))
            {
                offset = startingOffset;
                zoom = startingZoom;
                updateShader = true;
            }

            if (IsKeyPressed(Key.F1)) showControls = !showControls;

            // More steps cost a great deal of time.
            if (IsKeyPressed(Key.Up))
            {
                maxIterationsMultiplier *= 1.4f;
                updateShader = true;
            }
            else if (IsKeyPressed(Key.Down))
            {
                maxIterationsMultiplier /= 1.4f;
                updateShader = true;
            }

            // The left button zooms in and the right out, toward the pointer.
            if (IsMouseButtonDown(MouseButton.Left) || IsMouseButtonDown(MouseButton.Right))
            {
                zoom *= IsMouseButtonDown(MouseButton.Left)? zoomSpeed : (1.0f/zoomSpeed);

                Vector2 mousePos = GetMousePosition();

                // The view moves toward the pointer, faster the farther it is from the middle and
                // slower the nearer the zoom.
                Vector2 offsetVelocity = new(
                    (mousePos.X/(float)screenWidth - 0.5f)*offsetSpeedMul/zoom,
                    (mousePos.Y/(float)screenHeight - 0.5f)*offsetSpeedMul/zoom);

                offset += GetFrameTime()*offsetVelocity;

                updateShader = true;
            }

            if (updateShader)
            {
                // More steps the nearer the zoom, by a rough rule that mostly works
                maxIterations = (int)(MathF.Sqrt(2.0f*MathF.Sqrt(MathF.Abs(1.0f - MathF.Sqrt(37.5f*zoom))))*maxIterationsMultiplier);

                SetShaderValue(shader, zoomLoc, zoom);
                SetShaderValue(shader, offsetLoc, offset);
                SetShaderValue(shader, maxIterationsLoc, maxIterations);
            }

            // A blank texture the shader is drawn across. A rectangle would not do, its texture
            // coordinates being those of the white pixel shapes are drawn with.
            BeginTextureMode(target);
                ClearBackground(Color.Black);
                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.Black);
            EndTextureMode();

            BeginDrawing();

                ClearBackground(Color.Black);

                // Drawn as it is, the shader reading its coordinates from the top as raylib's does
                BeginShaderMode(shader);
                    DrawTextureEx(target.Texture, Vector2.Zero, 0.0f, 1.0f, Color.White);
                EndShaderMode();

                if (showControls)
                {
                    DrawText("Press Mouse buttons right/left to zoom in/out and move", 10, 15, 10, Color.RayWhite);
                    DrawText("Press F1 to toggle these controls", 10, 30, 10, Color.RayWhite);
                    DrawText("Press [1 - 6] to change point of interest", 10, 45, 10, Color.RayWhite);
                    DrawText("Press UP | DOWN to change number of iterations", 10, 60, 10, Color.RayWhite);
                    DrawText("Press R to recenter the camera", 10, 75, 10, Color.RayWhite);
                }

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadRenderTexture(target);

        CloseWindow();
    }
}
