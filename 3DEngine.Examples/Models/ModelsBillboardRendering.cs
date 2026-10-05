// raylib's models_billboard_rendering example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsBillboardRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] billboard rendering");

        Camera3D camera = new(new Vector3(5.0f, 4.0f, 5.0f), new Vector3(0.0f, 2.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Texture2D bill = LoadTexture("resources/billboard.png");
        Vector3 billPositionStatic = new(0.0f, 2.0f, 0.0f);
        Vector3 billPositionRotating = new(1.0f, 2.0f, 1.0f);

        // The whole texture, where a source would take part of a larger one
        Rectangle source = new(0.0f, 0.0f, (float)bill.Width, (float)bill.Height);

        // The turning billboard keeps y up,
        Vector3 billUp = Vector3.UnitY;

        // is one unit tall at the texture's aspect,
        Vector2 size = new(source.Width/source.Height, 1.0f);

        // and turns about its center.
        Vector2 origin = size*0.5f;

        // The farther billboard is drawn first, so the nearer covers it.
        float distanceStatic;
        float distanceRotating;
        float rotation = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            rotation += 0.4f;
            distanceStatic = Vector3.Distance(camera.Position, billPositionStatic);
            distanceRotating = Vector3.Distance(camera.Position, billPositionRotating);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawGrid(10, 1.0f);

                    if (distanceStatic > distanceRotating)
                    {
                        DrawBillboard(camera, bill, billPositionStatic, 2.0f, Color.White);
                        DrawBillboardPro(camera, bill, source, billPositionRotating, billUp, size, origin, rotation, Color.White);
                    }
                    else
                    {
                        DrawBillboardPro(camera, bill, source, billPositionRotating, billUp, size, origin, rotation, Color.White);
                        DrawBillboard(camera, bill, billPositionStatic, 2.0f, Color.White);
                    }

                EndMode3D();

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(bill);

        CloseWindow();
    }
}
