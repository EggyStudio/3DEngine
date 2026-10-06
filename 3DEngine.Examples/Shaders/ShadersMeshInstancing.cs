// raylib's shaders_mesh_instancing example, Copyright (c) 2020-2025 seanpringle (@seanpringle), Max
// (@moliad) and Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.RLights;

namespace Engine.Examples;

public static class ShadersMeshInstancing
{
    private const int MAX_INSTANCES = 10000;
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] mesh instancing");

        Camera3D camera = new(new Vector3(-125.0f, 125.0f, -125.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        ModelMesh cube = GenMeshCube(1.0f, 1.0f, 1.0f);

        // Each cube placed and turned at random, the turn before the place
        Matrix4x4[] transforms = new Matrix4x4[MAX_INSTANCES];
        for (int i = 0; i < MAX_INSTANCES; i++)
        {
            // C leaves the order of a call's arguments to the compiler, and GCC, which builds the
            // raylib program this is measured against, works them out from the last, so the place
            // along z is drawn first. The axis below is a literal, worked out in order.
            float z = GetRandomValue(-50, 50), y = GetRandomValue(-50, 50), x = GetRandomValue(-50, 50);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(x, y, z);
            Vector3 axis = Vector3.Normalize(new Vector3(GetRandomValue(0, 360), GetRandomValue(0, 360), GetRandomValue(0, 360)));
            float angle = GetRandomValue(0, 180)*DEG2RAD;
            Matrix4x4 rotation = Matrix4x4.CreateFromAxisAngle(axis, angle);

            transforms[i] = rotation*translation;
        }

        // raylib's lighting_instancing.vs and lighting.fs, written in Slang for the model pass
        Shader shader = LoadShader("resources/shaders/slang/lighting_instancing.slang");
        int viewLoc = GetShaderLocation(shader, "viewPos");

        // Ambient light level
        int ambientLoc = GetShaderLocation(shader, "ambient");
        SetShaderValue(shader, ambientLoc, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));

        CreateLight(LIGHT_DIRECTIONAL, new Vector3(50.0f, 50.0f, 0.0f), Vector3.Zero, Color.White, shader);

        // The instanced copies are drawn red with the lighting shader
        ModelMaterial matInstances = LoadMaterialDefault() with { Shader = shader, Color = Color.Red };

        // The two single cubes are drawn blue with the default material
        ModelMaterial matDefault = LoadMaterialDefault() with { Color = Color.Blue };

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // The camera's position, which the highlights are seen from
            SetShaderValue(shader, viewLoc, camera.Position);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawMesh(cube, matDefault, Matrix4x4.CreateTranslation(-10.0f, 0.0f, 0.0f));

                    DrawMeshInstanced(cube, matInstances, transforms);

                    DrawMesh(cube, matDefault, Matrix4x4.CreateTranslation(10.0f, 0.0f, 0.0f));

                EndMode3D();

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadShader(shader);

        CloseWindow();
    }
}
