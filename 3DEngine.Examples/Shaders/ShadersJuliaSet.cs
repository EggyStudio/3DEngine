// raylib's shaders_julia_set example, Copyright (c) 2019-2025 Josh Colclough (@joshcol9232) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersJuliaSet
{
    // A few c worth seeing
    private static readonly Vector2[] pointsOfInterest =
    [
        new(-0.348827f, 0.607167f),
        new(-0.786268f, 0.169728f),
        new(-0.8f, 0.156f),
        new(0.285f, 0.0f),
        new(-0.835f, -0.2321f),
        new(-0.70176f, -0.3842f),
    ];

    private const int screenWidth = 800;
    private const int screenHeight = 450;
    private const float zoomSpeed = 1.01f;
    private const float offsetSpeedMul = 2.0f;

    private const float startingZoom = 0.75f;

    public static void Run()
    {
        InitWindow(screenWidth, screenHeight, "[shaders] julia set");

        // raylib's julia_set.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/julia_set.slang");

        // The texture the set is drawn across
        RenderTexture2D target = LoadRenderTexture(GetScreenWidth(), GetScreenHeight());

        // The c of z^2 + c
        Vector2 c = pointsOfInterest[0];

        // The view's place and zoom, centered and whole to begin with
        Vector2 offset = Vector2.Zero;
        float zoom = startingZoom;

        // The values' places in the shader, -1 for one it has none of
        int cLoc = GetShaderLocation(shader, "c");
        int zoomLoc = GetShaderLocation(shader, "zoom");
        int offsetLoc = GetShaderLocation(shader, "offset");

        SetShaderValue(shader, cLoc, c);
        SetShaderValue(shader, zoomLoc, zoom);
        SetShaderValue(shader, offsetLoc, offset);

        int incrementSpeed = 0;             // How fast c moves
        bool showControls = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // 1 to 6 put c at a point worth seeing.
            Key[] numbers = [Key.Alpha1, Key.Alpha2, Key.Alpha3, Key.Alpha4, Key.Alpha5, Key.Alpha6];
            for (int i = 0; i < numbers.Length; i++)
            {
                if (IsKeyPressed(numbers[i]))
                {
                    c = pointsOfInterest[i];
                    SetShaderValue(shader, cLoc, c);
                    break;
                }
            }

            // R centers the view and resets its zoom.
            if (IsKeyPressed(Key.R))
            {
                zoom = startingZoom;
                offset = Vector2.Zero;
                SetShaderValue(shader, zoomLoc, zoom);
                SetShaderValue(shader, offsetLoc, offset);
            }

            if (IsKeyPressed(Key.Space)) incrementSpeed = 0;
            if (IsKeyPressed(Key.F1)) showControls = !showControls;

            if (IsKeyPressed(Key.Right)) incrementSpeed++;
            else if (IsKeyPressed(Key.Left)) incrementSpeed--;

            // The left button zooms in and the right out, toward the pointer.
            if (IsMouseButtonDown(MouseButton.Left) || IsMouseButtonDown(MouseButton.Right))
            {
                zoom *= IsMouseButtonDown(MouseButton.Left)? zoomSpeed : 1.0f/zoomSpeed;

                Vector2 mousePos = GetMousePosition();

                // The view moves toward the pointer, faster the farther it is from the middle and
                // slower the nearer the zoom.
                Vector2 offsetVelocity = new(
                    (mousePos.X/(float)screenWidth - 0.5f)*offsetSpeedMul/zoom,
                    (mousePos.Y/(float)screenHeight - 0.5f)*offsetSpeedMul/zoom);

                offset += GetFrameTime()*offsetVelocity;

                SetShaderValue(shader, zoomLoc, zoom);
                SetShaderValue(shader, offsetLoc, offset);
            }

            // c moves with time.
            float dc = GetFrameTime()*(float)incrementSpeed*0.0005f;
            c += new Vector2(dc, dc);
            SetShaderValue(shader, cLoc, c);

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
                    DrawText("Press KEY_F1 to toggle these controls", 10, 30, 10, Color.RayWhite);
                    DrawText("Press KEYS [1 - 6] to change point of interest", 10, 45, 10, Color.RayWhite);
                    DrawText("Press KEY_LEFT | KEY_RIGHT to change speed", 10, 60, 10, Color.RayWhite);
                    DrawText("Press KEY_SPACE to stop movement animation", 10, 75, 10, Color.RayWhite);
                    DrawText("Press KEY_R to recenter the camera", 10, 90, 10, Color.RayWhite);
                }

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadRenderTexture(target);

        CloseWindow();
    }
}
