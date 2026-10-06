// raylib's textures_polygon_drawing example, Copyright (c) 2021-2025 Chris Camacho (@chriscamacho) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesPolygonDrawing
{
    private const int MAX_POINTS = 11;      // 10 points and back to the start
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] polygon drawing");

        // Define texture coordinates to map our texture to poly
        Vector2[] texcoords =
        [
            new(0.75f, 0.0f),
            new(0.25f, 0.0f),
            new(0.0f, 0.5f),
            new(0.0f, 0.75f),
            new(0.25f, 1.0f),
            new(0.375f, 0.875f),
            new(0.625f, 0.875f),
            new(0.75f, 1.0f),
            new(1.0f, 0.75f),
            new(1.0f, 0.5f),
            new(0.75f, 0.0f),   // Close the poly
        ];

        // Define the base poly vertices from the UV's
        // NOTE: They can be specified in any other way
        Vector2[] points = new Vector2[MAX_POINTS];
        for (int i = 0; i < MAX_POINTS; i++)
        {
            points[i].X = (texcoords[i].X - 0.5f)*256.0f;
            points[i].Y = (texcoords[i].Y - 0.5f)*256.0f;
        }

        // Define the vertices drawing position
        // NOTE: Initially same as points but updated every frame
        Vector2[] positions = new Vector2[MAX_POINTS];
        for (int i = 0; i < MAX_POINTS; i++) positions[i] = points[i];

        // Load texture to be mapped to poly
        Texture2D texture = LoadTexture("resources/cat.png");

        float angle = 0.0f;             // Rotation angle (in degrees)

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Update points rotation with an angle transform
            // NOTE: Base points position are not modified
            angle++;
            for (int i = 0; i < MAX_POINTS; i++) positions[i] = Vector2Rotate(points[i], angle*DEG2RAD);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("textured polygon", 20, 20, 20, Color.DarkGray);

                DrawTexturePoly(texture, new Vector2(GetScreenWidth()/2.0f, GetScreenHeight()/2.0f),
                                positions, texcoords, MAX_POINTS, Color.White);

            EndDrawing();
        }

        UnloadTexture(texture); // Unload texture

        CloseWindow();
    }

    // Draw textured polygon, defined by vertex and texture coordinates
    // NOTE: Polygon center must have straight line path to all points
    // without crossing perimeter, points must be in anticlockwise order
    private static void DrawTexturePoly(Texture2D texture, Vector2 center, Vector2[] points, Vector2[] texcoords, int pointCount, Color tint)
    {
        rlSetTexture(texture.Id);
        rlBegin(RlDrawMode.Triangles);

            rlColor4ub(tint.R, tint.G, tint.B, tint.A);

            for (int i = 0; i < pointCount - 1; i++)
            {
                rlTexCoord2f(0.5f, 0.5f);
                rlVertex2f(center.X, center.Y);

                rlTexCoord2f(texcoords[i].X, texcoords[i].Y);
                rlVertex2f(points[i].X + center.X, points[i].Y + center.Y);

                rlTexCoord2f(texcoords[i + 1].X, texcoords[i + 1].Y);
                rlVertex2f(points[i + 1].X + center.X, points[i + 1].Y + center.Y);
            }
        rlEnd();

        rlSetTexture(0);
    }
}
