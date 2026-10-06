// raylib's shaders_postprocessing example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersPostprocessing
{
    private const int MAX_POSTPRO_SHADERS = 12;

    private const int FX_GRAYSCALE = 0;

    private static readonly string[] postproShaderText =
    [
        "GRAYSCALE",
        "POSTERIZATION",
        "DREAM_VISION",
        "PIXELIZER",
        "CROSS_HATCHING",
        "CROSS_STITCHING",
        "PREDATOR_VIEW",
        "SCANLINES",
        "FISHEYE",
        "SOBEL",
        "BLOOM",
        "BLUR",
    ];

    // raylib's fragment shaders of the same names, written in Slang, in the order of the names above
    private static readonly string[] postproShaderFile =
    [
        "grayscale", "posterization", "dream_vision", "pixelizer", "cross_hatching", "cross_stitching",
        "predator", "scanlines", "fisheye", "sobel", "bloom", "blur",
    ];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);

        InitWindow(screenWidth, screenHeight, "[shaders] postprocessing");

        Camera3D camera = new(new Vector3(2.0f, 3.0f, 2.0f), new Vector3(0.0f, 1.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Model model = LoadModel("resources/models/church.obj");
        Texture2D texture = LoadTexture("resources/models/church_diffuse.png");
        model.Materials[0].Texture = texture;

        Vector3 position = Vector3.Zero;

        // Every postprocessing shader, each a fragment stage drawn with the engine's vertex stage
        Shader[] shaders = new Shader[MAX_POSTPRO_SHADERS];
        for (int i = 0; i < MAX_POSTPRO_SHADERS; i++) shaders[i] = LoadShader($"resources/shaders/slang/{postproShaderFile[i]}.slang");

        int currentShader = FX_GRAYSCALE;

        RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            if (IsKeyPressed(Key.Right)) currentShader++;
            else if (IsKeyPressed(Key.Left)) currentShader--;

            if (currentShader >= MAX_POSTPRO_SHADERS) currentShader = 0;
            else if (currentShader < 0) currentShader = MAX_POSTPRO_SHADERS - 1;

            BeginTextureMode(target);
                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawModel(model, position, 0.1f, Color.White);
                    DrawGrid(10, 1.0f);
                EndMode3D();
            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                // The texture through the shader chosen, drawn upright as it is stored here
                BeginShaderMode(shaders[currentShader]);
                    DrawTextureRec(target.Texture, new Rectangle(0, 0, target.Texture.Width, target.Texture.Height), Vector2.Zero, Color.White);
                EndShaderMode();

                // 2D shapes and text over the texture
                DrawRectangle(0, 9, 580, 30, Fade(Color.LightGray, 0.7f));

                DrawText("(c) Church 3D model by Alberto Cano", screenWidth - 200, screenHeight - 20, 10, Color.Gray);
                DrawText("CURRENT POSTPRO SHADER:", 10, 15, 20, Color.Black);
                DrawText(postproShaderText[currentShader], 330, 15, 20, Color.Red);
                DrawText("< >", 540, 10, 30, Color.DarkBlue);
                DrawFPS(700, 15);
            EndDrawing();
        }

        for (int i = 0; i < MAX_POSTPRO_SHADERS; i++) UnloadShader(shaders[i]);

        UnloadTexture(texture);
        UnloadModel(model);
        UnloadRenderTexture(target);

        CloseWindow();
    }
}
