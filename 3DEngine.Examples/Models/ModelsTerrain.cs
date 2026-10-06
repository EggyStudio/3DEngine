using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsTerrain
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] heightmap and cubicmap");

        // Lit from above over a little light from all around, where models are drawn unlit with none.
        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 0.65f);
        SetAmbientLight(Color.White, 0.35f);

        var camera = new Camera3D(new Vector3(18, 14, 18), new Vector3(0, 0, 0), Vector3.UnitY, 45);

        // Hills from a few overlapping waves, painted into an image as brightness.
        var heights = GenImageColor(64, 64, Color.Black);
        for (int z = 0; z < 64; z++)
        for (int x = 0; x < 64; x++)
        {
            var h = 0.5f + 0.25f * MathF.Sin(x * 0.15f) * MathF.Cos(z * 0.12f) + 0.2f * MathF.Sin((x + z) * 0.08f);
            var b = (byte)Math.Clamp(h * 255, 0, 255);
            ImageDrawPixel(ref heights, x, z, new Color(b, b, b));
        }
        var terrain = LoadModelFromMesh(GenMeshHeightmap(heights, new Vector3(12, 3, 12)));
        terrain.Materials[0].Texture = LoadTextureFromImage(GenImageGradientLinear(64, 64, 0, Color.DarkGreen, Color.Lime));

        // A maze: white pixels are walls.
        var plan = GenImageColor(9, 9, Color.Black);
        ImageDrawRectangleLines(ref plan, 0, 0, 9, 9, Color.White);
        ImageDrawLine(ref plan, 2, 2, 6, 2, Color.White);
        ImageDrawLine(ref plan, 2, 2, 2, 6, Color.White);
        ImageDrawLine(ref plan, 4, 4, 8, 4, Color.White);
        ImageDrawLine(ref plan, 6, 6, 6, 8, Color.White);
        ImageDrawPixel(ref plan, 0, 4, Color.Black);
        var maze = LoadModelFromMesh(GenMeshCubicmap(plan, new Vector3(1, 1.2f, 1)));
        var checker = LoadTextureFromImage(GenImageChecked(32, 32, 8, 8, Color.LightGray, Color.Gray));
        GenTextureMipmaps(ref checker);
        maze.Materials[0].Texture = checker;

        SetTargetFPS(60);
        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            BeginDrawing();
            ClearBackground(Color.SkyBlue);
            BeginMode3D(camera);
            DrawModel(terrain, new Vector3(-13, 0, -6), 1, Color.White);
            DrawModel(maze, new Vector3(2, 0, -4.5f), 1, Color.White);
            EndMode3D();
            DrawText("GenMeshHeightmap and GenMeshCubicmap, each from an image", 10, 10, 20, Color.DarkBlue);
            EndDrawing();
        }

        UnloadTexture(terrain.Materials[0].Texture);
        UnloadModel(terrain);
        UnloadTexture(checker);
        UnloadModel(maze);
        CloseWindow();
    }
}
