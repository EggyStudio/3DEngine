// raylib's models_rlgl_solar_system example, Copyright (c) 2018-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsRlglSolarSystem
{
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        const float sunRadius = 4.0f;
        const float earthRadius = 0.6f;
        const float earthOrbitRadius = 8.0f;
        const float moonRadius = 0.16f;
        const float moonOrbitRadius = 1.5f;

        InitWindow(screenWidth, screenHeight, "[models] rlgl solar system");

        Camera3D camera = new(new Vector3(16.0f, 16.0f, 16.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        float rotationSpeed = 0.2f;         // General system rotation speed

        float earthRotation = 0.0f;         // Rotation of earth around itself (days) in degrees
        float earthOrbitRotation = 0.0f;    // Rotation of earth around the Sun (years) in degrees
        float moonRotation = 0.0f;          // Rotation of moon around itself
        float moonOrbitRotation = 0.0f;     // Rotation of moon around earth in degrees

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            earthRotation += (5.0f*rotationSpeed);
            earthOrbitRotation += (365/360.0f*(5.0f*rotationSpeed)*rotationSpeed);
            moonRotation += (2.0f*rotationSpeed);
            moonOrbitRotation += (8.0f*rotationSpeed);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    rlPushMatrix();
                        rlScalef(sunRadius, sunRadius, sunRadius);          // Scale Sun
                        DrawSphereBasic(Color.Gold);                        // Draw the Sun
                    rlPopMatrix();

                    rlPushMatrix();
                        rlRotatef(earthOrbitRotation, 0.0f, 1.0f, 0.0f);    // Rotation for Earth orbit around Sun
                        rlTranslatef(earthOrbitRadius, 0.0f, 0.0f);         // Translation for Earth orbit

                        rlPushMatrix();
                            rlRotatef(earthRotation, 0.25f, 1.0f, 0.0f);    // Rotation for Earth itself
                            rlScalef(earthRadius, earthRadius, earthRadius);// Scale Earth

                            DrawSphereBasic(Color.Blue);                    // Draw the Earth
                        rlPopMatrix();

                        rlRotatef(moonOrbitRotation, 0.0f, 1.0f, 0.0f);     // Rotation for Moon orbit around Earth
                        rlTranslatef(moonOrbitRadius, 0.0f, 0.0f);          // Translation for Moon orbit
                        rlRotatef(moonRotation, 0.0f, 1.0f, 0.0f);          // Rotation for Moon itself
                        rlScalef(moonRadius, moonRadius, moonRadius);       // Scale Moon

                        DrawSphereBasic(Color.LightGray);                   // Draw the Moon
                    rlPopMatrix();

                    // Some reference elements (not affected by previous matrix transformations)
                    DrawCircle3D(new Vector3(0.0f, 0.0f, 0.0f), earthOrbitRadius, new Vector3(1, 0, 0), 90.0f, Fade(Color.Red, 0.5f));
                    DrawGrid(20, 1.0f);

                EndMode3D();

                DrawText("EARTH ORBITING AROUND THE SUN!", 400, 10, 20, Color.Maroon);
                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }

    // Draw sphere without any matrix transformation
    // NOTE: Sphere is drawn in world position ( 0, 0, 0 ) with radius 1.0f
    private static void DrawSphereBasic(Color color)
    {
        int rings = 16;
        int slices = 16;

        // Make sure there is enough space in the internal render batch
        // buffer to store all required vertex, batch is reseted if required
        rlCheckRenderBatchLimit((rings + 2)*slices*6);

        rlBegin(RlDrawMode.Triangles);
            rlColor4ub(color.R, color.G, color.B, color.A);

            for (int i = 0; i < (rings + 2); i++)
            {
                for (int j = 0; j < slices; j++)
                {
                    rlVertex3f(MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*i))*MathF.Sin(DEG2RAD*(j*360.0f/slices)),
                               MathF.Sin(DEG2RAD*(270+(180.0f/(rings + 1))*i)),
                               MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*i))*MathF.Cos(DEG2RAD*(j*360.0f/slices)));
                    rlVertex3f(MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1)))*MathF.Sin(DEG2RAD*((j+1)*360.0f/slices)),
                               MathF.Sin(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1))),
                               MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1)))*MathF.Cos(DEG2RAD*((j+1)*360.0f/slices)));
                    rlVertex3f(MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1)))*MathF.Sin(DEG2RAD*(j*360.0f/slices)),
                               MathF.Sin(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1))),
                               MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1)))*MathF.Cos(DEG2RAD*(j*360.0f/slices)));

                    rlVertex3f(MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*i))*MathF.Sin(DEG2RAD*(j*360.0f/slices)),
                               MathF.Sin(DEG2RAD*(270+(180.0f/(rings + 1))*i)),
                               MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*i))*MathF.Cos(DEG2RAD*(j*360.0f/slices)));
                    rlVertex3f(MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i)))*MathF.Sin(DEG2RAD*((j+1)*360.0f/slices)),
                               MathF.Sin(DEG2RAD*(270+(180.0f/(rings + 1))*(i))),
                               MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i)))*MathF.Cos(DEG2RAD*((j+1)*360.0f/slices)));
                    rlVertex3f(MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1)))*MathF.Sin(DEG2RAD*((j+1)*360.0f/slices)),
                               MathF.Sin(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1))),
                               MathF.Cos(DEG2RAD*(270+(180.0f/(rings + 1))*(i+1)))*MathF.Cos(DEG2RAD*((j+1)*360.0f/slices)));
                }
            }
        rlEnd();
    }
}
