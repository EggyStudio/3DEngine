// raylib's models_point_rendering example, Copyright (c) 2024 Reese Gallagher (@satchelfrost), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsPointRendering
{
    private const int MAX_POINTS = 10000000;     // 10 million
    private const int MIN_POINTS = 1000;         // 1 thousand

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] point rendering");

        Camera3D camera = new()
        {
            Position = new Vector3(3.0f, 3.0f, 3.0f),
            Target = new Vector3(0.0f, 0.0f, 0.0f),
            Up = new Vector3(0.0f, 1.0f, 0.0f),
            FovY = 45.0f,
            Projection = CameraProjection.Perspective,
        };

        Vector3 position = new(0.0f, 0.0f, 0.0f);
        bool useDrawModelPoints = true;
        bool numPointsChanged = false;
        int numPoints = 1000;

        // Each point's color, which a mesh here does not keep, is drawn by this shader from the
        // hue the point carries.
        Shader pointColors = LoadShader("resources/shaders/slang/point_colors.slang");

        var (mesh, vertices, colors) = GenMeshPoints(numPoints);
        Model model = LoadModelFromMesh(mesh);
        model.Materials[0].Shader = pointColors;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            if (IsKeyPressed(Key.Space)) useDrawModelPoints = !useDrawModelPoints;
            if (IsKeyPressed(Key.Up))
            {
                numPoints = (numPoints*10 > MAX_POINTS)? MAX_POINTS : numPoints*10;
                numPointsChanged = true;
            }
            if (IsKeyPressed(Key.Down))
            {
                numPoints = (numPoints/10 < MIN_POINTS)? MIN_POINTS : numPoints/10;
                numPointsChanged = true;
            }

            // Upload a different point cloud size
            if (numPointsChanged)
            {
                UnloadModel(model);
                (mesh, vertices, colors) = GenMeshPoints(numPoints);
                model = LoadModelFromMesh(mesh);
                model.Materials[0].Shader = pointColors;
                numPointsChanged = false;
            }

            BeginDrawing();

                ClearBackground(Color.Black);

                BeginMode3D(camera);
                    // The new method only uploads the points once to the GPU
                    if (useDrawModelPoints) DrawModelPoints(model, position, 1.0f, Color.White);
                    else
                    {
                        // The old method must continually draw the "points" (lines)
                        for (int i = 0; i < numPoints; i++)
                        {
                            DrawPoint3D(vertices[i], colors[i]);
                        }
                    }

                    // Draw a unit sphere for reference
                    DrawSphereWires(position, 1.0f, 10, 10, Color.Yellow);
                EndMode3D();

                // Draw UI text
                DrawText($"Point Count: {numPoints}", 10, screenHeight - 50, 40, Color.White);
                DrawText("UP - Increase points", 10, 40, 20, Color.White);
                DrawText("DOWN - Decrease points", 10, 70, 20, Color.White);
                DrawText("SPACE - Drawing function", 10, 100, 20, Color.White);

                if (useDrawModelPoints) DrawText("Using: DrawModelPoints()", 10, 130, 20, Color.Green);
                else DrawText("Using: DrawPoint3D()", 10, 130, 20, Color.Red);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadModel(model);
        UnloadShader(pointColors);

        CloseWindow();
    }

    // Generate a spherical point cloud, its positions and colors kept beside the mesh for the old
    // method, which reads raylib's from the mesh.
    private static (ModelMesh Mesh, Vector3[] Vertices, Color[] Colors) GenMeshPoints(int numPoints)
    {
        var vertices = new Vector3[numPoints];
        var colors = new Color[numPoints];
        var points = new ModelVertex[numPoints];

        // REF: https://en.wikipedia.org/wiki/Spherical_coordinate_system
        for (int i = 0; i < numPoints; i++)
        {
            float theta = MathF.PI*Random.Shared.NextSingle();
            float phi = 2.0f*MathF.PI*Random.Shared.NextSingle();
            float r = 10.0f*Random.Shared.NextSingle();

            vertices[i] = new Vector3(r*MathF.Sin(theta)*MathF.Cos(phi), r*MathF.Sin(theta)*MathF.Sin(phi), r*MathF.Cos(theta));
            colors[i] = ColorFromHSV(r*360.0f, 1.0f, 1.0f);

            // The hue as a fraction of a turn, which the shader colors the point by
            points[i] = new ModelVertex(vertices[i], Vector3.UnitY, new Vector2(r - MathF.Floor(r), 0.0f));
        }

        // raylib draws the vertices three at a time as triangles, a point at each corner in point
        // mode, so a last one or two short of a triangle are not drawn.
        var indices = new uint[numPoints/3*3];
        for (int i = 0; i < indices.Length; i++) indices[i] = (uint)i;

        return (UploadMesh(points, indices), vertices, colors);
    }

    // Draw a model points
    private static void DrawModelPoints(Model model, Vector3 position, float scale, Color tint)
    {
        rlEnablePointMode();
        rlDisableBackfaceCulling();

        DrawModel(model, position, scale, tint);

        rlEnableBackfaceCulling();
        rlDisablePointMode();
    }
}
