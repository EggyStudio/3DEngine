// raylib's shaders_basic_pbr example, Copyright (c) 2023-2025 Afan OLOVCIC (@_DevDad), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersBasicPbr
{
    private const int MAX_LIGHTS = 4;           // Max dynamic lights supported by shader

    // Light type
    private const int LIGHT_DIRECTIONAL = 0;
    private const int LIGHT_POINT = 1;
    private const int LIGHT_SPOT = 2;

    // Light data
    private struct Light
    {
        public int type;
        public int enabled;
        public Vector3 position;
        public Vector3 target;
        public Vector4 color;
        public float intensity;

        // Shader light parameters locations
        public int typeLoc;
        public int enabledLoc;
        public int positionLoc;
        public int targetLoc;
        public int colorLoc;
        public int intensityLoc;
    }

    private static int lightCount = 0;          // Current number of dynamic lights that have been created

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] basic pbr");

        Camera3D camera = new(new Vector3(2.0f, 2.0f, 6.0f), new Vector3(0.0f, 0.5f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // raylib's pbr.vs and pbr.fs, written in Slang for the model pass. raylib's shader names
        // the locations of its maps, and here they are the material's maps the pass binds.
        Shader shader = LoadShader("resources/shaders/slang/pbr.slang");

        int viewLoc = GetShaderLocation(shader, "viewPos");
        int lightCountLoc = GetShaderLocation(shader, "numOfLights");
        int maxLightCount = MAX_LIGHTS;
        SetShaderValue(shader, lightCountLoc, maxLightCount);

        // Setup ambient color and intensity parameters
        float ambientIntensity = 0.02f;
        Color ambientColor = new(26, 32, 135, 255);
        Vector3 ambientColorNormalized = new(ambientColor.R/255.0f, ambientColor.G/255.0f, ambientColor.B/255.0f);
        SetShaderValue(shader, GetShaderLocation(shader, "ambientColor"), ambientColorNormalized);
        SetShaderValue(shader, GetShaderLocation(shader, "ambient"), ambientIntensity);

        // Get location for shader parameters that can be modified in real time
        int metallicValueLoc = GetShaderLocation(shader, "metallicValue");
        int roughnessValueLoc = GetShaderLocation(shader, "roughnessValue");
        int emissiveIntensityLoc = GetShaderLocation(shader, "emissivePower");
        int emissiveColorLoc = GetShaderLocation(shader, "emissiveColor");
        int textureTilingLoc = GetShaderLocation(shader, "tiling");

        // The old car model, a single mesh with a single material
        Model car = LoadModel("resources/models/old_car_new.glb");

        car.Materials[0].Shader = shader;

        // The material's default parameters
        car.Materials[0].Color = Color.White;
        car.Materials[0].Metallic = 1.0f;
        car.Materials[0].Roughness = 0.0f;
        car.Materials[0].OcclusionStrength = 1.0f;
        car.Materials[0].Emissive = new Color(255, 162, 0, 255);

        // The material's textures. The MRA map packs metalness, roughness and occlusion, and goes
        // in the metalness map's place, as raylib's does.
        SetMaterialTexture(ref car.Materials[0], MaterialMapIndex.Albedo, LoadTexture("resources/old_car_d.png"));
        SetMaterialTexture(ref car.Materials[0], MaterialMapIndex.Metalness, LoadTexture("resources/old_car_mra.png"));
        SetMaterialTexture(ref car.Materials[0], MaterialMapIndex.Normal, LoadTexture("resources/old_car_n.png"));
        SetMaterialTexture(ref car.Materials[0], MaterialMapIndex.Emission, LoadTexture("resources/old_car_e.png"));

        // The floor model, with the same shader
        Model floor = LoadModel("resources/models/plane.glb");

        floor.Materials[0].Shader = shader;

        floor.Materials[0].Color = Color.White;
        floor.Materials[0].Metallic = 0.8f;
        floor.Materials[0].Roughness = 0.1f;
        floor.Materials[0].OcclusionStrength = 1.0f;
        floor.Materials[0].Emissive = Color.Black;

        SetMaterialTexture(ref floor.Materials[0], MaterialMapIndex.Albedo, LoadTexture("resources/road_a.png"));
        SetMaterialTexture(ref floor.Materials[0], MaterialMapIndex.Metalness, LoadTexture("resources/road_mra.png"));
        SetMaterialTexture(ref floor.Materials[0], MaterialMapIndex.Normal, LoadTexture("resources/road_n.png"));

        Vector2 carTextureTiling = new(0.5f, 0.5f);
        Vector2 floorTextureTiling = new(0.5f, 0.5f);

        // Create some lights
        Light[] lights = new Light[MAX_LIGHTS];
        lights[0] = CreateLight(LIGHT_POINT, new Vector3(-1.0f, 1.0f, -2.0f), new Vector3(0.0f, 0.0f, 0.0f), Color.Yellow, 4.0f, shader);
        lights[1] = CreateLight(LIGHT_POINT, new Vector3(2.0f, 1.0f, 1.0f), new Vector3(0.0f, 0.0f, 0.0f), Color.Green, 3.3f, shader);
        lights[2] = CreateLight(LIGHT_POINT, new Vector3(-2.0f, 1.0f, 1.0f), new Vector3(0.0f, 0.0f, 0.0f), Color.Red, 8.3f, shader);
        lights[3] = CreateLight(LIGHT_POINT, new Vector3(1.0f, 1.0f, -2.0f), new Vector3(0.0f, 0.0f, 0.0f), Color.Blue, 2.0f, shader);

        // The texture maps are all used, by default
        int usage = 1;
        SetShaderValue(shader, GetShaderLocation(shader, "useTexAlbedo"), usage);
        SetShaderValue(shader, GetShaderLocation(shader, "useTexNormal"), usage);
        SetShaderValue(shader, GetShaderLocation(shader, "useTexMRA"), usage);
        SetShaderValue(shader, GetShaderLocation(shader, "useTexEmissive"), usage);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // The camera's position, which the highlights are seen from
            SetShaderValue(shader, viewLoc, camera.Position);

            if (IsKeyPressed(Key.Alpha1)) { lights[2].enabled = (lights[2].enabled == 0) ? 1 : 0; }
            if (IsKeyPressed(Key.Alpha2)) { lights[1].enabled = (lights[1].enabled == 0) ? 1 : 0; }
            if (IsKeyPressed(Key.Alpha3)) { lights[3].enabled = (lights[3].enabled == 0) ? 1 : 0; }
            if (IsKeyPressed(Key.Alpha4)) { lights[0].enabled = (lights[0].enabled == 0) ? 1 : 0; }

            // Update light values on shader (only whether each is on changes)
            for (int i = 0; i < MAX_LIGHTS; i++) UpdateLight(shader, lights[i]);

            BeginDrawing();

                ClearBackground(Color.Black);

                BeginMode3D(camera);

                    // The floor's texture tiling and emissive color
                    SetShaderValue(shader, textureTilingLoc, floorTextureTiling);
                    Vector4 floorEmissiveColor = ColorNormalize(floor.Materials[0].Emissive);
                    SetShaderValue(shader, emissiveColorLoc, floorEmissiveColor);

                    // The floor's metallic and roughness values
                    SetShaderValue(shader, metallicValueLoc, floor.Materials[0].Metallic);
                    SetShaderValue(shader, roughnessValueLoc, floor.Materials[0].Roughness);

                    DrawModel(floor, new Vector3(0.0f, 0.0f, 0.0f), 5.0f, Color.White);

                    // The old car's texture tiling, emissive color and emissive intensity
                    SetShaderValue(shader, textureTilingLoc, carTextureTiling);
                    Vector4 carEmissiveColor = ColorNormalize(car.Materials[0].Emissive);
                    SetShaderValue(shader, emissiveColorLoc, carEmissiveColor);
                    float emissiveIntensity = 0.01f;
                    SetShaderValue(shader, emissiveIntensityLoc, emissiveIntensity);

                    // The old car's metallic and roughness values
                    SetShaderValue(shader, metallicValueLoc, car.Materials[0].Metallic);
                    SetShaderValue(shader, roughnessValueLoc, car.Materials[0].Roughness);

                    DrawModel(car, new Vector3(0.0f, 0.0f, 0.0f), 0.25f, Color.White);

                    // Spheres where the lights are
                    for (int i = 0; i < MAX_LIGHTS; i++)
                    {
                        Color lightColor = new(
                            (byte)(lights[i].color.X*255),
                            (byte)(lights[i].color.Y*255),
                            (byte)(lights[i].color.Z*255),
                            (byte)(lights[i].color.W*255));

                        if (lights[i].enabled != 0) DrawSphereEx(lights[i].position, 0.2f, 8, 8, lightColor);
                        else DrawSphereWires(lights[i].position, 0.2f, 8, 8, ColorAlpha(lightColor, 0.3f));
                    }

                EndMode3D();

                DrawText("Toggle lights: [1][2][3][4]", 10, 40, 20, Color.LightGray);

                DrawText("(c) Old Rusty Car model by Renafox (https://skfb.ly/LxRy)", screenWidth - 320, screenHeight - 20, 10, Color.LightGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        // raylib unloads each material, and its textures with it, apart from the shader they share.
        foreach (Model model in new[] { car, floor })
        {
            ModelMaterial material = model.Materials[0];
            UnloadTexture(material.Texture);
            UnloadTexture(material.MetallicRoughnessMap);
            UnloadTexture(material.NormalMap);
            UnloadTexture(material.EmissiveMap);
            UnloadModel(model);
        }

        UnloadShader(shader);

        CloseWindow();
    }

    // Create light with provided data. It counts toward lightCount, which MAX_LIGHTS limits.
    private static Light CreateLight(int type, Vector3 position, Vector3 target, Color color, float intensity, Shader shader)
    {
        Light light = default;

        if (lightCount < MAX_LIGHTS)
        {
            light.enabled = 1;
            light.type = type;
            light.position = position;
            light.target = target;
            light.color = new Vector4(color.R/255.0f, color.G/255.0f, color.B/255.0f, color.A/255.0f);
            light.intensity = intensity;

            // The shader names them so.
            light.enabledLoc = GetShaderLocation(shader, $"lights[{lightCount}].enabled");
            light.typeLoc = GetShaderLocation(shader, $"lights[{lightCount}].type");
            light.positionLoc = GetShaderLocation(shader, $"lights[{lightCount}].position");
            light.targetLoc = GetShaderLocation(shader, $"lights[{lightCount}].target");
            light.colorLoc = GetShaderLocation(shader, $"lights[{lightCount}].color");
            light.intensityLoc = GetShaderLocation(shader, $"lights[{lightCount}].intensity");

            UpdateLight(shader, light);

            lightCount++;
        }

        return light;
    }

    // Send light properties to shader
    private static void UpdateLight(Shader shader, Light light)
    {
        SetShaderValue(shader, light.enabledLoc, light.enabled);
        SetShaderValue(shader, light.typeLoc, light.type);

        // Send to shader light position and target position values
        SetShaderValue(shader, light.positionLoc, light.position);
        SetShaderValue(shader, light.targetLoc, light.target);
        SetShaderValue(shader, light.colorLoc, light.color);
        SetShaderValue(shader, light.intensityLoc, light.intensity);
    }
}
