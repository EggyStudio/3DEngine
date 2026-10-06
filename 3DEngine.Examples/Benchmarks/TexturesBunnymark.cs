// raylib's textures_bunnymark example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API, and with --stress the benchmark beside raylib's.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// raylib's bunnymark: a hundred bunnies added at the mouse while its button is held, bouncing
/// around the window. With <c>--stress</c> it is the benchmark build/raylib-bench/run.sh measures,
/// as many sprites as a frame holds at 60 frames a second, found by <see cref="StressRamp"/>, and
/// <c>e3d command profile</c> reports where the time goes.
/// </summary>
public static class TexturesBunnymark
{
    private const int MAX_BUNNIES = 80000;

    // The quads rlgl draws in one batch, which raylib's count of draw calls divides by.
    private const int MAX_BATCH_ELEMENTS = 8192;

    private struct Bunny
    {
        public Vector2 position;
        public Vector2 speed;
        public Color color;
    }

    public static void Run()
    {
        if (Environment.GetCommandLineArgs().Contains("--stress"))
        {
            RunStress();
            return;
        }

        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] bunnymark");

        Texture2D texBunny = LoadTexture("resources/raybunny.png");

        Bunny[] bunnies = new Bunny[MAX_BUNNIES];
        int bunniesCount = 0;
        bool paused = false;

        SetTargetFPS(0);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonDown(MouseButton.Left))
            {
                // A hundred more each frame the button is held
                for (int i = 0; i < 100; i++)
                {
                    if (bunniesCount < MAX_BUNNIES)
                    {
                        bunnies[bunniesCount].position = GetMousePosition();
                        bunnies[bunniesCount].speed.X = (float)GetRandomValue(-250, 250);
                        bunnies[bunniesCount].speed.Y = (float)GetRandomValue(-250, 250);
                        bunnies[bunniesCount].color = new Color((byte)GetRandomValue(50, 240),
                                                                (byte)GetRandomValue(80, 240),
                                                                (byte)GetRandomValue(100, 240), (byte)255);
                        bunniesCount++;
                    }
                }
            }

            if (IsKeyPressed(Key.P)) paused = !paused;

            if (!paused)
            {
                for (int i = 0; i < bunniesCount; i++)
                {
                    bunnies[i].position.X += bunnies[i].speed.X*GetFrameTime();
                    bunnies[i].position.Y += bunnies[i].speed.Y*GetFrameTime();

                    if (((bunnies[i].position.X + (float)texBunny.Width/2) > GetScreenWidth()) ||
                        ((bunnies[i].position.X + (float)texBunny.Width/2) < 0)) bunnies[i].speed.X *= -1;
                    if (((bunnies[i].position.Y + (float)texBunny.Height/2) > GetScreenHeight()) ||
                        ((bunnies[i].position.Y + (float)texBunny.Height/2 - 40) < 0)) bunnies[i].speed.Y *= -1;
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < bunniesCount; i++)
                    DrawTexture(texBunny, (int)bunnies[i].position.X, (int)bunnies[i].position.Y, bunnies[i].color);

                DrawRectangle(0, 0, screenWidth, 40, Color.Black);
                DrawText($"bunnies: {bunniesCount}", 120, 10, 20, Color.Green);
                DrawText($"batched draw calls: {1 + bunniesCount/MAX_BATCH_ELEMENTS}", 320, 10, 20, Color.Maroon);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(texBunny);

        CloseWindow();
    }

    // The benchmark: textured sprites bouncing around the window, as many as a frame holds at 60
    // frames a second, a fixed step a frame as the measured raylib program's are.
    private static void RunStress()
    {
        InitWindow(800, 450, "[textures] bunnymark");
        SetTargetFPS(0);

        var image = LoadImage("resources/logo.png");
        ImageResize(ref image, 32, 32);
        var bunny = LoadTextureFromImage(image);

        var random = new Random(1);
        var positions = new List<Vector2>();
        var speeds = new List<Vector2>();
        var tints = new List<Color>();
        var ramp = new StressRamp(1000);

        while (!WindowShouldClose())
        {
            while (positions.Count < ramp.Count)
            {
                positions.Add(new Vector2(random.Next(0, 768), random.Next(0, 418)));
                speeds.Add(new Vector2(random.Next(-250, 250), random.Next(-250, 250)) / 60);
                tints.Add(new Color((byte)random.Next(50, 240), (byte)random.Next(80, 240), (byte)random.Next(100, 240)));
            }
            if (positions.Count > ramp.Count)
            {
                positions.RemoveRange(ramp.Count, positions.Count - ramp.Count);
                speeds.RemoveRange(ramp.Count, speeds.Count - ramp.Count);
                tints.RemoveRange(ramp.Count, tints.Count - ramp.Count);
            }

            // Bounced off the window's edges, a fixed step a frame as raylib's does.
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i] + speeds[i];
                var s = speeds[i];
                if (p.X < 0 || p.X > GetScreenWidth() - 32) s.X = -s.X;
                if (p.Y < 40 || p.Y > GetScreenHeight() - 32) s.Y = -s.Y;
                positions[i] = p;
                speeds[i] = s;
            }

            BeginDrawing();
            ClearBackground(Color.RayWhite);
            for (int i = 0; i < positions.Count; i++)
                DrawTexture(bunny, (int)positions[i].X, (int)positions[i].Y, tints[i]);

            DrawRectangle(0, 0, GetScreenWidth(), 40, Color.Black);
            DrawText(ramp.Limit > 0 ? $"{ramp.Limit} sprites hold 60 frames a second" : $"sprites: {ramp.Count}, {ramp.FrameMs:0.0} ms",
                10, 10, 20, Color.Green);
            DrawFPS(GetScreenWidth() - 110, 10);
            EndDrawing();
            ramp.Measure();
        }

        UnloadTexture(bunny);
        CloseWindow();
    }
}
