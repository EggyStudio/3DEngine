using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// raylib's bunnymark: textured sprites bouncing around the window, as many as a frame holds at 60
/// frames a second, found by <see cref="StressRamp"/>. <c>e3d command profile</c> reports where the
/// time goes.
/// </summary>
public static class TexturesBunnymark
{
    public static void Run()
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
