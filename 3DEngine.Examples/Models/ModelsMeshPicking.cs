// raylib's models_mesh_picking example, Copyright (c) 2017-2025 Joel Davis (@joeld42) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsMeshPicking
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] mesh picking");

        Camera3D camera = new(new Vector3(20.0f, 20.0f, 20.0f), new Vector3(0.0f, 8.0f, 0.0f), new Vector3(0.0f, 1.6f, 0.0f), 45.0f, CameraProjection.Perspective);

        Ray ray = default;

        Model tower = LoadModel("resources/models/obj/turret.obj");
        Texture2D texture = LoadTexture("resources/models/obj/turret_diffuse.png");
        tower.Materials[0].Texture = texture;

        Vector3 towerPos = Vector3.Zero;
        BoundingBox towerBBox = GetMeshBoundingBox(tower.Meshes[0]);

        // The ground's quad
        Vector3 g0 = new(-50.0f, 0.0f, -50.0f);
        Vector3 g1 = new(-50.0f, 0.0f, 50.0f);
        Vector3 g2 = new(50.0f, 0.0f, 50.0f);
        Vector3 g3 = new(50.0f, 0.0f, -50.0f);

        // A triangle
        Vector3 ta = new(-25.0f, 0.5f, 0.0f);
        Vector3 tb = new(-4.0f, 2.5f, 1.0f);
        Vector3 tc = new(-8.0f, 6.5f, 0.0f);

        Vector3 bary = Vector3.Zero;

        // A sphere
        Vector3 sp = new(-30.0f, 5.0f, 5.0f);
        float sr = 4.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsCursorHidden()) UpdateCamera(ref camera, CameraMode.FirstPerson);

            // The right button takes and gives back the camera's control.
            if (IsMouseButtonPressed(MouseButton.Right))
            {
                if (IsCursorHidden()) EnableCursor();
                else DisableCursor();
            }

            // The nearest hit, as no hit at the greatest distance to begin with
            RayCollision collision = new(false, float.MaxValue, Vector3.Zero, Vector3.Zero);
            string hitObjectName = "None";
            Color cursorColor = Color.White;

            ray = GetScreenToWorldRay(GetMousePosition(), camera);

            // The ground,
            RayCollision groundHitInfo = GetRayCollisionQuad(ray, g0, g1, g2, g3);

            if (groundHitInfo.Hit && (groundHitInfo.Distance < collision.Distance))
            {
                collision = groundHitInfo;
                cursorColor = Color.Green;
                hitObjectName = "Ground";
            }

            // the triangle,
            RayCollision triHitInfo = GetRayCollisionTriangle(ray, ta, tb, tc);

            if (triHitInfo.Hit && (triHitInfo.Distance < collision.Distance))
            {
                collision = triHitInfo;
                cursorColor = Color.Purple;
                hitObjectName = "Triangle";

                bary = Vector3Barycenter(collision.Point, ta, tb, tc);
            }

            // the sphere,
            RayCollision sphereHitInfo = GetRayCollisionSphere(ray, sp, sr);

            if (sphereHitInfo.Hit && (sphereHitInfo.Distance < collision.Distance))
            {
                collision = sphereHitInfo;
                cursorColor = Color.Orange;
                hitObjectName = "Sphere";
            }

            // and the tower, its box before its meshes.
            RayCollision boxHitInfo = GetRayCollisionBox(ray, towerBBox);

            if (boxHitInfo.Hit && (boxHitInfo.Distance < collision.Distance))
            {
                collision = boxHitInfo;
                cursorColor = Color.Orange;
                hitObjectName = "Box";

                // Each mesh at the model's transform, which a model drawn many times would give
                // each of its transforms to in turn
                RayCollision meshHitInfo = default;
                for (int m = 0; m < tower.Meshes.Length; m++)
                {
                    meshHitInfo = GetRayCollisionMesh(ray, tower.Meshes[m], tower.Transform);
                    if (meshHitInfo.Hit)
                    {
                        // The nearest, the first mesh hit being enough
                        if (!collision.Hit || (collision.Distance > meshHitInfo.Distance)) collision = meshHitInfo;

                        break;
                    }
                }

                if (meshHitInfo.Hit)
                {
                    collision = meshHitInfo;
                    cursorColor = Color.Orange;
                    hitObjectName = "Mesh";
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    // A scale other than 1 would need the collision's transform to have it as well.
                    DrawModel(tower, towerPos, 1.0f, Color.White);

                    DrawLine3D(ta, tb, Color.Purple);
                    DrawLine3D(tb, tc, Color.Purple);
                    DrawLine3D(tc, ta, Color.Purple);

                    DrawSphereWires(sp, sr, 8, 8, Color.Purple);

                    if (boxHitInfo.Hit) DrawBoundingBox(towerBBox, Color.Lime);

                    // The cursor where the ray hits, with the surface's normal
                    if (collision.Hit)
                    {
                        DrawCube(collision.Point, 0.3f, 0.3f, 0.3f, cursorColor);
                        DrawCubeWires(collision.Point, 0.3f, 0.3f, 0.3f, Color.Red);

                        Vector3 normalEnd = collision.Point + collision.Normal;

                        DrawLine3D(collision.Point, normalEnd, Color.Red);
                    }

                    DrawRay(ray, Color.Maroon);

                    DrawGrid(10, 10.0f);

                EndMode3D();

                DrawText($"Hit Object: {hitObjectName}", 10, 50, 10, Color.Black);

                if (collision.Hit)
                {
                    int ypos = 70;

                    DrawText($"Distance: {collision.Distance,3:0.00}", 10, ypos, 10, Color.Black);

                    DrawText($"Hit Pos: {collision.Point.X,3:0.00} {collision.Point.Y,3:0.00} {collision.Point.Z,3:0.00}", 10, ypos + 15, 10, Color.Black);

                    DrawText($"Hit Norm: {collision.Normal.X,3:0.00} {collision.Normal.Y,3:0.00} {collision.Normal.Z,3:0.00}", 10, ypos + 30, 10, Color.Black);

                    if (triHitInfo.Hit && hitObjectName == "Triangle")
                        DrawText($"Barycenter: {bary.X,3:0.00} {bary.Y,3:0.00} {bary.Z,3:0.00}", 10, ypos + 45, 10, Color.Black);
                }

                DrawText("Right click mouse to toggle camera controls", 10, 430, 10, Color.Gray);

                DrawText("(c) Turret 3D model by Alberto Cano", screenWidth - 200, screenHeight - 20, 10, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadModel(tower);
        UnloadTexture(texture);

        CloseWindow();
    }
}
