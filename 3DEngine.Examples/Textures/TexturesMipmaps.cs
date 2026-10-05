using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesMipmaps
{
    public static void Run()
    {
        InitWindow(800, 450, "[textures] mipmaps");

        var camera = new Camera3D(new Vector3(0, 1.2f, 6), new Vector3(0, 0, -20), Vector3.UnitY, 60);

        // A fine checkerboard, the worst case for a texture drawn smaller than its size. Without
        // mip levels its far rows break into noise, and with them they fade to gray.
        var image = GenImageChecked(512, 512, 8, 8, Color.Black, Color.White);
        var plain = LoadTextureFromImage(image);
        var mipmapped = LoadTextureFromImage(image);
        GenTextureMipmaps(ref mipmapped);

        // Trilinear blends the two nearest levels, where bilinear would step from one to the next.
        SetTextureFilter(mipmapped, TextureFilter.Trilinear);

        var left = LoadModelFromMesh(GenMeshPlane(8, 60, 1, 1));
        var right = LoadModelFromMesh(GenMeshPlane(8, 60, 1, 1));
        left.Materials[0].Texture = plain;
        right.Materials[0].Texture = mipmapped;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Color.SkyBlue);

            BeginMode3D(camera);
            DrawModel(left, new Vector3(-4.1f, 0, -25), 1, Color.White);
            DrawModel(right, new Vector3(4.1f, 0, -25), 1, Color.White);
            EndMode3D();

            DrawText("1 mip level", 140, 20, 20, Color.DarkGray);
            DrawText($"{mipmapped.Mipmaps} mip levels", 520, 20, 20, Color.DarkGray);
            EndDrawing();
        }

        UnloadModel(left);
        UnloadModel(right);
        UnloadTexture(plain);
        UnloadTexture(mipmapped);
        CloseWindow();
    }
}
