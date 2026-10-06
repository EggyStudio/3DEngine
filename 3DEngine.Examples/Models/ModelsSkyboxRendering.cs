// raylib's models_skybox_rendering example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsSkyboxRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] skybox rendering");

        Camera3D camera = new(new Vector3(1.0f, 1.0f, 1.0f), new Vector3(4.0f, 1.0f, 4.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // A cube drawn around the camera
        ModelMesh cube = GenMeshCube(1.0f, 1.0f, 1.0f);
        Model skybox = LoadModelFromMesh(cube);

        // A panorama in place of the cross of six faces, made into a cube
        bool useHDR = false;

        // raylib's skybox.vs and skybox.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/skybox.slang");
        skybox.Materials[0].Shader = shader;
        int cubemapLoc = GetShaderLocation(shader, "cubemap");
        SetShaderValue(shader, GetShaderLocation(shader, "doGamma"), useHDR ? 1 : 0);
        SetShaderValue(shader, GetShaderLocation(shader, "vflipped"), useHDR ? 1 : 0);

        string skyboxFileName = "";
        Texture2D cubemap;

        if (useHDR)
        {
            skyboxFileName = "resources/dresden_square_2k.hdr";

            // The panorama is wrapped round the cube's faces, and is not needed after
            Texture2D panorama = LoadTexture(skyboxFileName);
            cubemap = GenTextureCubemap(panorama, 1024);
            UnloadTexture(panorama);
        }
        else
        {
            Image image = LoadImage("resources/skybox.png");
            cubemap = LoadTextureCubemap(image, CubemapLayout.AutoDetect);
            UnloadImage(image);
        }

        // The sampler is given its cube, where raylib names the material's cube map
        SetShaderValueTexture(shader, cubemapLoc, cubemap);

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.FirstPerson);

            // An image dropped on the window becomes the sky
            if (IsFileDropped())
            {
                string[] droppedFiles = LoadDroppedFiles();

                if (droppedFiles.Length == 1)
                {
                    if (Path.GetExtension(droppedFiles[0]).ToLowerInvariant() is ".png" or ".jpg" or ".hdr" or ".bmp" or ".tga")
                    {
                        UnloadTexture(cubemap);

                        if (useHDR)
                        {
                            Texture2D panorama = LoadTexture(droppedFiles[0]);
                            cubemap = GenTextureCubemap(panorama, 1024);
                            UnloadTexture(panorama);
                        }
                        else
                        {
                            Image image = LoadImage(droppedFiles[0]);
                            cubemap = LoadTextureCubemap(image, CubemapLayout.AutoDetect);
                            UnloadImage(image);
                        }

                        SetShaderValueTexture(shader, cubemapLoc, cubemap);
                        skyboxFileName = droppedFiles[0];
                    }
                }

                UnloadDroppedFiles();
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    // Inside the cube, writing no depth, so the grid drawn after shows in front of it
                    rlDisableBackfaceCulling();
                    rlDisableDepthMask();
                        DrawModel(skybox, Vector3.Zero, 1.0f, Color.White);
                    rlEnableBackfaceCulling();
                    rlEnableDepthMask();

                    DrawGrid(10, 1.0f);

                EndMode3D();

                if (useHDR) DrawText($"Panorama image from hdrihaven.com: {Path.GetFileName(skyboxFileName)}", 10, GetScreenHeight() - 20, 10, Color.Black);
                else DrawText($": {Path.GetFileName(skyboxFileName)}", 10, GetScreenHeight() - 20, 10, Color.Black);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadTexture(cubemap);
        UnloadModel(skybox);

        CloseWindow();
    }

    // A cube from a panorama, each texel of each face the panorama's color in its direction, by
    // raylib's cubemap.fs. raylib draws the cube into each face through rlgl's framebuffers, which
    // the flat API does not carry, so the faces are worked out here and loaded as a vertical line.
    private static Texture2D GenTextureCubemap(Texture2D panorama, int size)
    {
        Image map = LoadImageFromTexture(panorama);
        Image faces = GenImageColor(size, size*6, Color.Black);

        for (int face = 0; face < 6; face++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Each face's texel as a direction, in the order and orientation of a cube's faces
                    float s = 2.0f*(x + 0.5f)/size - 1.0f;
                    float t = 2.0f*(y + 0.5f)/size - 1.0f;
                    Vector3 direction = face switch
                    {
                        0 => new Vector3(1.0f, -t, -s),
                        1 => new Vector3(-1.0f, -t, s),
                        2 => new Vector3(s, 1.0f, t),
                        3 => new Vector3(s, -1.0f, -t),
                        4 => new Vector3(s, -t, 1.0f),
                        _ => new Vector3(-s, -t, -1.0f),
                    };
                    direction = Vector3.Normalize(direction);

                    // cubemap.fs's SampleSphericalMap
                    Vector2 uv = new Vector2(MathF.Atan2(direction.Z, direction.X), MathF.Asin(direction.Y))*new Vector2(0.1591f, 0.3183f) + new Vector2(0.5f);
                    int px = Math.Clamp((int)(uv.X*map.Width), 0, map.Width - 1);
                    int py = Math.Clamp((int)(uv.Y*map.Height), 0, map.Height - 1);
                    ImageDrawPixel(ref faces, x, size*face + y, GetImageColor(map, px, py));
                }
            }
        }

        Texture2D cubemap = LoadTextureCubemap(faces, CubemapLayout.LineVertical);
        UnloadImage(faces);
        UnloadImage(map);

        return cubemap;
    }
}
