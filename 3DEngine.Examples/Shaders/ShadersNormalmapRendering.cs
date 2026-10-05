// raylib's shaders_normalmap_rendering example, Copyright (c) 2025 Jeremy Montgomery (@Sir_Irk) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersNormalmapRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] normalmap rendering");

        Camera3D camera = new(new Vector3(0.0f, 2.0f, -4.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // raylib's normalmap.vs and normalmap.fs, written in Slang for the model pass
        Shader shader = LoadShader("resources/shaders/slang/normalmap.slang");
        int viewLoc = GetShaderLocation(shader, "viewPos");

        // One point light
        Vector3 lightPosition = new(0.0f, 1.0f, 0.0f);
        int lightPosLoc = GetShaderLocation(shader, "lightPos");

        // A plane model that has proper normals and tangents
        Model plane = LoadModel("resources/models/plane.glb");

        Texture2D diffuse = LoadTexture("resources/tiles_diffuse.png");
        Texture2D normal = LoadTexture("resources/tiles_normal.png");

        // Mipmaps and trilinear filtering to help with texture aliasing
        GenTextureMipmaps(ref diffuse);
        GenTextureMipmaps(ref normal);

        SetTextureFilter(diffuse, TextureFilter.Trilinear);
        SetTextureFilter(normal, TextureFilter.Trilinear);

        // The plane model's shader and texture maps
        plane.Materials[0].Shader = shader;
        plane.Materials[0].Texture = diffuse;
        plane.Materials[0].NormalMap = normal;

        // Specular exponent AKA shininess of the material
        float specularExponent = 8.0f;
        int specularExponentLoc = GetShaderLocation(shader, "specularExponent");

        // The normal map can be turned off and on, to compare
        int useNormalMap = 1;
        int useNormalMapLoc = GetShaderLocation(shader, "useNormalMap");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Move the light around on the X and Z axis using WASD keys
            Vector3 direction = Vector3.Zero;
            if (IsKeyDown(Key.W)) direction += new Vector3(0.0f, 0.0f, 1.0f);
            if (IsKeyDown(Key.S)) direction += new Vector3(0.0f, 0.0f, -1.0f);
            if (IsKeyDown(Key.D)) direction += new Vector3(-1.0f, 0.0f, 0.0f);
            if (IsKeyDown(Key.A)) direction += new Vector3(1.0f, 0.0f, 0.0f);

            // raymath gives a zero vector back as it is, where Normalize gives NaN.
            if (direction != Vector3.Zero) direction = Vector3.Normalize(direction);
            lightPosition += direction*(GetFrameTime()*3.0f);

            // Increase/Decrease the specular exponent(shininess)
            if (IsKeyDown(Key.Up)) specularExponent = Math.Clamp(specularExponent + 40.0f*GetFrameTime(), 2.0f, 128.0f);
            if (IsKeyDown(Key.Down)) specularExponent = Math.Clamp(specularExponent - 40.0f*GetFrameTime(), 2.0f, 128.0f);

            // Toggle normal map on and off
            if (IsKeyPressed(Key.N)) useNormalMap = (useNormalMap == 0) ? 1 : 0;

            // Spin plane model at a constant rate
            plane.Transform = Matrix4x4.CreateRotationY((float)GetTime()*0.5f);

            // Update shader values
            SetShaderValue(shader, lightPosLoc, lightPosition);
            SetShaderValue(shader, viewLoc, camera.Position);
            SetShaderValue(shader, specularExponentLoc, specularExponent);
            SetShaderValue(shader, useNormalMapLoc, useNormalMap);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    BeginShaderMode(shader);

                        DrawModel(plane, Vector3.Zero, 2.0f, Color.White);

                    EndShaderMode();

                    // A sphere where the light is
                    DrawSphereWires(lightPosition, 0.2f, 8, 8, Color.Orange);

                EndMode3D();

                Color textColor = (useNormalMap != 0) ? Color.DarkGreen : Color.Red;
                string toggleStr = (useNormalMap != 0) ? "On" : "Off";
                DrawText($"Use key [N] to toggle normal map: {toggleStr}", 10, 10, 10, textColor);

                int yOffset = 24;
                DrawText("Use keys [W][A][S][D] to move the light", 10, 10 + yOffset*1, 10, Color.Black);
                DrawText("Use keys [Up][Down] to change specular exponent", 10, 10 + yOffset*2, 10, Color.Black);
                DrawText($"Specular Exponent: {specularExponent:0.00}", 10, 10 + yOffset*3, 10, Color.Blue);

                DrawFPS(screenWidth - 90, 10);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadModel(plane);
        UnloadTexture(diffuse);
        UnloadTexture(normal);

        CloseWindow();
    }
}
