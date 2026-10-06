// raylib's shaders_lightmap_rendering example, Copyright (c) 2019-2025 Jussi Viitala (@nullstare) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersLightmapRendering
{
    private const int MAP_SIZE = 16;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);  // Enable Multi Sampling Anti Aliasing 4x (if available)
        InitWindow(screenWidth, screenHeight, "[shaders] lightmap rendering");

        // Define the camera to look into our 3d world
        Camera3D camera = default;
        camera.Position = new Vector3(4.0f, 6.0f, 8.0f);    // Camera position
        camera.Target = new Vector3(0.0f, 0.0f, 0.0f);      // Camera looking at point
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);          // Camera up vector (rotation towards target)
        camera.FovY = 45.0f;                                // Camera field-of-view Y
        camera.Projection = CameraProjection.Perspective;   // Camera projection type

        ModelMesh mesh = GenMeshPlane(MAP_SIZE, MAP_SIZE, 1, 1);

        // GenMeshPlane doesn't generate texcoords2 so we will upload them separately. A render
        // texture is upright here, where raylib's is stored bottom up, so each counts down from the
        // lightmap's top where raylib's counts up from its bottom.
        float[] texcoords2 = new float[mesh.VertexCount*2];

        // X                    // Y
        texcoords2[0] = 0.0f;   texcoords2[1] = 1.0f;
        texcoords2[2] = 1.0f;   texcoords2[3] = 1.0f;
        texcoords2[4] = 0.0f;   texcoords2[5] = 0.0f;
        texcoords2[6] = 1.0f;   texcoords2[7] = 0.0f;

        // Index 5 is for texcoords2, which the mesh is given as they are written, where raylib loads
        // a vertex buffer of its own through rlgl and sets it as the mesh's attribute 5
        UpdateMeshBuffer(mesh, 5, texcoords2, 0);

        // Load lightmap shader
        Shader shader = LoadShader("resources/shaders/slang/lightmap.slang");

        Texture2D texture = LoadTexture("resources/cubicmap_atlas.png");
        Texture2D light = LoadTexture("resources/spark_flame.png");

        GenTextureMipmaps(ref texture);
        SetTextureFilter(texture, TextureFilter.Trilinear);

        RenderTexture2D lightmap = LoadRenderTexture(MAP_SIZE, MAP_SIZE);

        ModelMaterial material = LoadMaterialDefault();
        material.Shader = shader;
        SetMaterialTexture(ref material, MaterialMapIndex.Albedo, texture);
        SetMaterialTexture(ref material, MaterialMapIndex.Metalness, lightmap.Texture);

        // Drawing to lightmap
        BeginTextureMode(lightmap);
            ClearBackground(Color.Black);

            BeginBlendMode(BlendMode.Additive);
                DrawTexturePro(
                    light,
                    new Rectangle(0, 0, light.Width, light.Height),
                    new Rectangle(0, 0, 2.0f*MAP_SIZE, 2.0f*MAP_SIZE),
                    new Vector2(MAP_SIZE, MAP_SIZE),
                    0.0f,
                    Color.Red
                );
                DrawTexturePro(
                    light,
                    new Rectangle(0, 0, light.Width, light.Height),
                    new Rectangle(MAP_SIZE*0.8f, MAP_SIZE/2.0f, 2.0f*MAP_SIZE, 2.0f*MAP_SIZE),
                    new Vector2(MAP_SIZE, MAP_SIZE),
                    0.0f,
                    Color.Blue
                );
                DrawTexturePro(
                    light,
                    new Rectangle(0, 0, light.Width, light.Height),
                    new Rectangle(MAP_SIZE*0.8f, MAP_SIZE*0.8f, MAP_SIZE, MAP_SIZE),
                    new Vector2(MAP_SIZE/2.0f, MAP_SIZE/2.0f),
                    0.0f,
                    Color.Green
                );
            BeginBlendMode(BlendMode.Alpha);
        EndTextureMode();

        // NOTE: To enable trilinear filtering we need mipmaps available for texture. A render
        // texture here keeps one level, and the lightmap is drawn larger than its pixels, so the
        // filter blends between them as raylib's does.
        Texture2D lightmapTexture = lightmap.Texture;
        GenTextureMipmaps(ref lightmapTexture);
        SetTextureFilter(lightmapTexture, TextureFilter.Trilinear);

        SetTargetFPS(60);                   // Set our game to run at 60 frames-per-second

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawMesh(mesh, material, Matrix4x4.Identity);
                EndMode3D();

                // The width negative mirrors it as raylib's does, and the height is left as it is,
                // a render texture being upright here
                DrawTexturePro(lightmap.Texture, new Rectangle(0, 0, -MAP_SIZE, MAP_SIZE),
                    new Rectangle(GetRenderWidth() - MAP_SIZE*8 - 10, 10, MAP_SIZE*8, MAP_SIZE*8),
                    new Vector2(0.0f, 0.0f), 0.0f, Color.White);

                DrawText($"LIGHTMAP: {MAP_SIZE}x{MAP_SIZE} pixels", GetRenderWidth() - 130, 20 + MAP_SIZE*8, 10, Color.Green);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadMesh(mesh);       // Unload the mesh
        UnloadShader(shader);   // Unload shader
        UnloadTexture(texture); // Unload texture
        UnloadTexture(light);   // Unload texture
        UnloadRenderTexture(lightmap); // Unload lightmap render texture

        CloseWindow();
    }
}
