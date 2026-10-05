// raylib's models_tesseract_view example, Copyright (c) 2024-2025 Timothy van der Valk (@arceryz) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsTesseractView
{
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] tesseract view");

        Camera3D camera = new(new Vector3(4.0f, 4.0f, 4.0f), Vector3.Zero, new Vector3(0.0f, 0.0f, 1.0f), 50.0f, CameraProjection.Perspective);

        // The corners, each of X, Y, Z and W at +1 or -1
        Vector4[] tesseract =
        [
            new( 1,  1,  1, 1), new( 1,  1,  1, -1),
            new( 1,  1, -1, 1), new( 1,  1, -1, -1),
            new( 1, -1,  1, 1), new( 1, -1,  1, -1),
            new( 1, -1, -1, 1), new( 1, -1, -1, -1),
            new(-1,  1,  1, 1), new(-1,  1,  1, -1),
            new(-1,  1, -1, 1), new(-1,  1, -1, -1),
            new(-1, -1,  1, 1), new(-1, -1,  1, -1),
            new(-1, -1, -1, 1), new(-1, -1, -1, -1),
        ];

        float rotation = 0.0f;
        Vector3[] transformed = new Vector3[16];
        float[] wValues = new float[16];

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            rotation = DEG2RAD*45.0f*(float)GetTime();

            for (int i = 0; i < 16; i++)
            {
                Vector4 p = tesseract[i];

                // Turned in the plane of X and W
                Vector2 rotXW = Vector2Rotate(new Vector2(p.X, p.W), rotation);
                p.X = rotXW.X;
                p.W = rotXW.Y;

                // Seen from (0, 0, 0, 3): the ray to the corner carried on until W is 0
                float c = 3.0f/(3.0f - p.W);
                p.X = c*p.X;
                p.Y = c*p.Y;
                p.Z = c*p.Z;

                // The point in space, and its W kept for the drawing
                transformed[i] = new Vector3(p.X, p.Y, p.Z);
                wValues[i] = p.W;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    for (int i = 0; i < 16; i++)
                    {
                        // A sphere as large as the corner's W
                        DrawSphere(transformed[i], MathF.Abs(wValues[i]*0.1f), Color.Red);

                        for (int j = 0; j < 16; j++)
                        {
                            // Corners joined by an edge differ in one coordinate, so no list of
                            // edges is kept, and each is drawn once, from its lower corner.
                            Vector4 v1 = tesseract[i];
                            Vector4 v2 = tesseract[j];
                            int diff = (v1.X == v2.X ? 1 : 0) + (v1.Y == v2.Y ? 1 : 0) + (v1.Z == v2.Z ? 1 : 0) + (v1.W == v2.W ? 1 : 0);

                            if (diff == 3 && i < j) DrawLine3D(transformed[i], transformed[j], Color.Maroon);
                        }
                    }

                EndMode3D();

            EndDrawing();
        }

        CloseWindow();
    }
}
