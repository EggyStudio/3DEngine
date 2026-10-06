// raylib's shaders_shadowmap_rendering example, Copyright (c) 2023-2025 TheManTheMythTheGameDev
// (@TheManTheMythTheGameDev), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersShadowmapRendering
{
    private const int SHADOWMAP_RESOLUTION = 1024;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        // Shadows are a HUGE topic, and this example shows an extremely simple implementation of the shadowmapping algorithm,
        // which is the industry standard for shadows. This algorithm can be extended in a ridiculous number of ways to improve
        // realism and also adapt it for different scenes. This is pretty much the simplest possible implementation

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] shadowmap rendering");

        Camera3D camera = new(new Vector3(10.0f, 10.0f, 10.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // raylib's shadowmap.vs and shadowmap.fs, written in Slang for the model pass
        Shader shadowShader = LoadShader("resources/shaders/slang/shadowmap.slang");
        int viewLoc = GetShaderLocation(shadowShader, "viewPos");

        Vector3 lightDir = Vector3.Normalize(new Vector3(0.35f, -1.0f, -0.35f));
        Color lightColor = Color.White;
        Vector4 lightColorNormalized = ColorNormalize(lightColor);
        int lightDirLoc = GetShaderLocation(shadowShader, "lightDir");
        int lightColLoc = GetShaderLocation(shadowShader, "lightColor");
        SetShaderValue(shadowShader, lightDirLoc, lightDir);
        SetShaderValue(shadowShader, lightColLoc, lightColorNormalized);
        int ambientLoc = GetShaderLocation(shadowShader, "ambient");
        SetShaderValue(shadowShader, ambientLoc, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
        int lightVPLoc = GetShaderLocation(shadowShader, "lightVP");
        int shadowMapLoc = GetShaderLocation(shadowShader, "shadowDepth");
        int shadowMapResolution = SHADOWMAP_RESOLUTION;
        SetShaderValue(shadowShader, GetShaderLocation(shadowShader, "shadowMapResolution"), shadowMapResolution);

        Model cube = LoadModelFromMesh(GenMeshCube(1.0f, 1.0f, 1.0f));
        cube.Materials[0].Shader = shadowShader;
        Model robot = LoadModel("resources/models/robot.glb");
        for (int i = 0; i < robot.Materials.Length; i++) robot.Materials[i].Shader = shadowShader;

        ModelAnimation[] anims = LoadModelAnimations("resources/models/robot.glb");

        RenderTexture2D shadowMap = LoadShadowmapRenderTexture(SHADOWMAP_RESOLUTION, SHADOWMAP_RESOLUTION);

        // For the shadowmapping algorithm, we will be rendering everything from the light's point of view
        Camera3D lightCamera = new(lightDir*-15.0f, Vector3.Zero, Vector3.UnitY, 20.0f, CameraProjection.Orthographic);

        int frameCounter = 0;

        // Store the light matrices
        Matrix4x4 lightView;
        Matrix4x4 lightProj;
        Matrix4x4 lightViewProj;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float deltaTime = GetFrameTime();

            UpdateCamera(ref camera, CameraMode.Orbital);

            // The camera's position, which the shader works out the light seen from it with
            SetShaderValue(shadowShader, viewLoc, camera.Position);

            frameCounter++;
            frameCounter %= anims[0].KeyframeCount;
            UpdateModelAnimation(robot, anims[0], (float)frameCounter);

            // Move light with arrow keys
            const float cameraSpeed = 0.05f;
            if (IsKeyDown(Key.Left))
            {
                if (lightDir.X < 0.6f) lightDir.X += cameraSpeed*60.0f*deltaTime;
            }
            if (IsKeyDown(Key.Right))
            {
                if (lightDir.X > -0.6f) lightDir.X -= cameraSpeed*60.0f*deltaTime;
            }
            if (IsKeyDown(Key.Up))
            {
                if (lightDir.Z < 0.6f) lightDir.Z += cameraSpeed*60.0f*deltaTime;
            }
            if (IsKeyDown(Key.Down))
            {
                if (lightDir.Z > -0.6f) lightDir.Z -= cameraSpeed*60.0f*deltaTime;
            }

            lightDir = Vector3.Normalize(lightDir);
            lightCamera.Position = lightDir*-15.0f;
            SetShaderValue(shadowShader, lightDirLoc, lightDir);

            // PASS 01: Render all objects into the shadowmap render texture
            // We record all the objects' depths (as rendered from the light source's point of view) in a buffer
            // Anything that is "visible" to the light is in light, anything that isn't is in shadow
            // We can later use the depth buffer when rendering everything from the player's point of view
            // to determine whether a given point is "visible" to the light
            BeginTextureMode(shadowMap);
                ClearBackground(Color.White);

                BeginMode3D(lightCamera);
                    // The light camera's matrices, as the engine draws through them, which raylib
                    // reads back from rlgl
                    lightView = GetCameraViewMatrix(lightCamera);
                    lightProj = GetCameraProjectionMatrix(lightCamera, (float)SHADOWMAP_RESOLUTION/SHADOWMAP_RESOLUTION);
                    DrawScene(cube, robot);
                EndMode3D();

            EndTextureMode();

            lightViewProj = lightView*lightProj;

            // PASS 02: Draw the scene into main framebuffer, using the generated shadowmap
            BeginDrawing();
                ClearBackground(Color.RayWhite);

                // The shadow map's depth, which raylib binds to a texture slot of its own through rlgl
                SetShaderValueMatrix(shadowShader, lightVPLoc, lightViewProj);
                SetShaderValueTexture(shadowShader, shadowMapLoc, shadowMap.Depth);

                BeginMode3D(camera);
                    DrawScene(cube, robot); // The same things as were drawn into the shadow map
                EndMode3D();

                // Unbound again, so the next frame's first pass does not read the depth it draws
                SetShaderValueTexture(shadowShader, shadowMapLoc, default);

                DrawText("Use the arrow keys to rotate the light!", 10, 10, 30, Color.Red);
                DrawText("Shadows in raylib using the shadowmapping algorithm!", screenWidth - 280, screenHeight - 20, 10, Color.Gray);

            EndDrawing();

            if (IsKeyPressed(Key.F)) TakeScreenshot("shaders_shadowmap.png");
        }

        UnloadShader(shadowShader);
        UnloadModel(cube);
        UnloadModel(robot);
        UnloadModelAnimations(anims);
        UnloadShadowmapRenderTexture(shadowMap);

        CloseWindow();
    }

    // A render texture for the shadow map. raylib makes a framebuffer with a depth texture alone,
    // through rlgl, and a render texture here has a color image as well, which nothing reads.
    private static RenderTexture2D LoadShadowmapRenderTexture(int width, int height) => LoadRenderTexture(width, height);

    private static void UnloadShadowmapRenderTexture(RenderTexture2D target) => UnloadRenderTexture(target);

    // Draw full scene projecting shadows, once for each pass
    private static void DrawScene(Model cube, Model robot)
    {
        DrawModelEx(cube, Vector3.Zero, new Vector3(0.0f, 1.0f, 0.0f), 0.0f, new Vector3(10.0f, 1.0f, 10.0f), Color.Blue);
        DrawModelEx(cube, new Vector3(1.5f, 1.0f, -1.5f), new Vector3(0.0f, 1.0f, 0.0f), 0.0f, Vector3.One, Color.White);
        DrawModelEx(robot, new Vector3(0.0f, 0.5f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f), 0.0f, new Vector3(1.0f, 1.0f, 1.0f), Color.Red);
    }
}
