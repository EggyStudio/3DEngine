using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersBloom
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] bloom");

        // Glowing orbs brighter than white over a dim floor, which bloom spreads into the dark
        // around them, and a white cube lit by a lamp, which stays sharp. B turns bloom off and on,
        // and the other effects over the frame have a key each.
        SetBloom(0.8f);
        var bloom = true;
        var (curve, vignette, graded, fxaa, bright, focus, blur) = (Tonemap.Engine, false, false, false, false, false, false);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.5f)), new Color(110, 120, 150), 0.4f);
        CreatePointLight(new Vector3(0, 2.5f, 1.5f), new Color(255, 210, 160), 6, range: 12);

        var ground = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));
        var cube = LoadModelFromMesh(GenMeshCube(1.2f, 1.2f, 1.2f));
        var orb = LoadModelFromMesh(GenMeshSphere(0.45f, 32, 32));
        Color[] glows = [new Color(255, 120, 40), new Color(60, 200, 255), new Color(255, 60, 200)];
        var camera = new Camera3D(new Vector3(0, 3.5f, 8), new Vector3(0, 0.8f, 0), Vector3.UnitY, 45);
        var orbit = 0f;
        var t = 0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            t += GetFrameTime();
            if (IsKeyPressed(Key.B)) SetBloom((bloom = !bloom) ? 0.8f : 0);
            if (IsKeyPressed(Key.T)) SetTonemap(curve = (Tonemap)(((int)curve + 1) % Enum.GetValues<Tonemap>().Length));
            if (IsKeyPressed(Key.V)) SetVignette((vignette = !vignette) ? 0.6f : 0);
            if (IsKeyPressed(Key.G)) SetColorGrading(1, (graded = !graded) ? 0.3f : 1, graded ? new Color(255, 225, 190) : Color.White);
            if (IsKeyPressed(Key.F)) SetFxaa(fxaa = !fxaa);
            if (IsKeyPressed(Key.E)) SetExposure((bright = !bright) ? 1.8f : 1);
            // Depth of field sharp on the cube, about eight units away, and motion blur, which the
            // camera swinging round while it is on shows.
            if (IsKeyPressed(Key.D)) SetDepthOfField(8.3f, 2, (focus = !focus) ? 0.025f : 0);
            if (IsKeyPressed(Key.M)) SetMotionBlur((blur = !blur) ? 0.6f : 0);
            if (blur) orbit += GetFrameTime() * 1.2f;
            camera.Position = new Vector3(MathF.Sin(orbit) * 8, 3.5f, MathF.Cos(orbit) * 8);

            BeginDrawing();
            ClearBackground(new Color(12, 12, 18));

            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(70, 70, 80));
            DrawModel(cube, new Vector3(0, 0.6f, 0), 1, Color.White);
            for (int i = 0; i < glows.Length; i++)
            {
                var angle = t * 0.5f + i * MathF.Tau / glows.Length;
                orb.Materials[0] = new ModelMaterial(Color.Black) { Emissive = glows[i], EmissiveIntensity = 4 };
                DrawModel(orb, new Vector3(MathF.Cos(angle) * 2.8f, 1.2f + MathF.Sin(t + i) * 0.3f, MathF.Sin(angle) * 2.8f), 1, Color.White);
            }
            EndMode3D();

            DrawText(bloom ? "Bloom on: light past white glows. B turns it off." : "Bloom off. B turns it on.", 10, 10, 20, Color.RayWhite);
            DrawText($"T curve {curve}, V vignette {(vignette ? "on" : "off")}, G grading {(graded ? "on" : "off")}, " +
                     $"F FXAA {(fxaa ? "on" : "off")}, E exposure {(bright ? 1.8f : 1)}", 10, 36, 16, Color.LightGray);
            DrawText($"D depth of field {(focus ? "on" : "off")}, M motion blur {(blur ? "on, the camera swinging" : "off")}", 10, 56, 16, Color.LightGray);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(ground);
        UnloadModel(cube);
        UnloadModel(orb);
        CloseWindow();
    }
}
