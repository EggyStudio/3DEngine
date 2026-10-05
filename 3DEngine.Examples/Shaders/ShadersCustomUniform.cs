// raylib's shaders_custom_uniform example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersCustomUniform
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] custom uniform");

        Camera3D camera = new(new Vector3(8.0f, 8.0f, 8.0f), new Vector3(0.0f, 1.5f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Model model = LoadModel("resources/models/barracks.obj");
        Texture2D texture = LoadTexture("resources/models/barracks_diffuse.png");
        model.Materials[0].Texture = texture;
        Vector3 position = Vector3.Zero;

        // raylib's swirl.fs, written in Slang, for the scene drawn after into a texture
        Shader shader = LoadShader("resources/shaders/slang/swirl.slang");

        // The swirl's center, found by name, -1 where the shader has none
        int swirlCenterLoc = GetShaderLocation(shader, "center");

        Vector2 swirlCenter = new((float)screenWidth/2, (float)screenHeight/2);

        RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // The swirl follows the pointer, its height counted from the bottom as raylib's shader does.
            Vector2 mousePosition = GetMousePosition();

            swirlCenter.X = mousePosition.X;
            swirlCenter.Y = screenHeight - mousePosition.Y;

            SetShaderValue(shader, swirlCenterLoc, swirlCenter);

            BeginTextureMode(target);

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawModel(model, position, 0.5f, Color.White);
                    DrawGrid(10, 1.0f);
                EndMode3D();

                DrawText("TEXT DRAWN IN RENDER TEXTURE", 200, 10, 30, Color.Red);

            EndTextureMode();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // The scene through the swirl. A target is stored the right way up here, so it is
                // drawn with its height as it is, and the shader turns its coordinates as raylib's
                // turned target has them.
                BeginShaderMode(shader);
                    DrawTextureRec(target.Texture, new Rectangle(0, 0, (float)target.Texture.Width, (float)target.Texture.Height), Vector2.Zero, Color.White);
                EndShaderMode();

                DrawText("(c) Barracks 3D model by Alberto Cano", screenWidth - 220, screenHeight - 20, 10, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadTexture(texture);
        UnloadModel(model);
        UnloadRenderTexture(target);

        CloseWindow();
    }
}
