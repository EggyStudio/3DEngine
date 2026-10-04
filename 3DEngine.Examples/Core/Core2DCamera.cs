using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// raylib's 2D camera: a player walking along a street of buildings, followed by a camera that
/// turns and zooms, with a spline drawn through the rooftops.
/// </summary>
public static class Core2DCamera
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] 2d camera");

        var player = new Rectangle(400, 280, 40, 40);
        var buildings = new List<(Rectangle Rect, Color Color)>();
        var random = new Random(3);
        var spacing = 0f;
        for (int i = 0; i < 100; i++)
        {
            var width = random.Next(50, 200);
            var height = random.Next(100, 800);
            buildings.Add((new Rectangle(-6000 + spacing, 320 - height, width, height),
                new Color((byte)random.Next(200, 240), (byte)random.Next(200, 240), (byte)random.Next(200, 250))));
            spacing += width;
        }
        // A line through the rooftops, which a thick spline draws.
        var roofs = buildings.Select(b => new Vector2(b.Rect.X + b.Rect.Width / 2, b.Rect.Y)).ToArray();

        var camera = new Camera2D(new Vector2(400, 225), new Vector2(player.X + 20, player.Y + 20));

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            var step = (IsKeyDown(Key.Right) ? 1 : 0) - (IsKeyDown(Key.Left) ? 1 : 0);
            player = player with { X = player.X + step * 240 * GetFrameTime() };

            camera.Target = new Vector2(player.X + 20, player.Y + 20);
            if (IsKeyDown(Key.A)) camera.Rotation--;
            if (IsKeyDown(Key.S)) camera.Rotation++;
            camera.Rotation = Math.Clamp(camera.Rotation, -40, 40);
            camera.Zoom = Math.Clamp(MathF.Exp(MathF.Log(camera.Zoom) + GetMouseWheelMove() * 0.1f), 0.1f, 3f);
            if (IsKeyPressed(Key.R)) camera = camera with { Rotation = 0, Zoom = 1 };

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode2D(camera);
            DrawRectangle(-6000, 320, 13000, 8000, Color.DarkGray);
            foreach (var (rect, color) in buildings) DrawRectangleRec(rect, color);
            DrawSplineCatmullRom(roofs, 3, Color.SkyBlue);
            DrawRectangleRec(player, Color.Red);
            DrawLine((int)camera.Target.X, -GetScreenHeight() * 10, (int)camera.Target.X, GetScreenHeight() * 10, Color.Green);
            DrawLine(-GetScreenWidth() * 10, (int)camera.Target.Y, GetScreenWidth() * 10, (int)camera.Target.Y, Color.Green);
            EndMode2D();

            DrawText("SCREEN AREA", 640, 10, 20, Color.Red);
            DrawRectangle(0, 0, GetScreenWidth(), 5, Color.Red);
            DrawRectangle(0, 5, 5, GetScreenHeight() - 10, Color.Red);
            DrawRectangle(GetScreenWidth() - 5, 5, 5, GetScreenHeight() - 10, Color.Red);
            DrawRectangle(0, GetScreenHeight() - 5, GetScreenWidth(), 5, Color.Red);

            DrawRectangle(10, 10, 250, 113, Color.SkyBlue.Fade(0.5f));
            DrawRectangleLines(10, 10, 250, 113, Color.Blue);
            DrawText("Free 2d camera controls:", 20, 20, 10, Color.Black);
            DrawText("- Right and Left to move the player", 40, 40, 10, Color.DarkGray);
            DrawText("- Mouse wheel to zoom in and out", 40, 60, 10, Color.DarkGray);
            DrawText("- A and S to turn", 40, 80, 10, Color.DarkGray);
            DrawText("- R to reset the zoom and turn", 40, 100, 10, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
