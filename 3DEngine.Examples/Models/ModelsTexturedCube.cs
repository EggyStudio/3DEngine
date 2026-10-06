// raylib's models_textured_cube example, Copyright (c) 2022-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsTexturedCube
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] textured cube");

        Camera3D camera = new(new Vector3(0.0f, 10.0f, 10.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // Load texture to be applied to the cubes sides
        Texture2D texture = LoadTexture("resources/cubicmap_atlas.png");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    // Draw cube with an applied texture
                    DrawCubeTexture(texture, new Vector3(-2.0f, 2.0f, 0.0f), 2.0f, 4.0f, 2.0f, Color.White);

                    // Draw cube with an applied texture, but only a defined rectangle piece of the texture
                    DrawCubeTextureRec(texture, new Rectangle(0.0f, texture.Height/2.0f, texture.Width/2.0f, texture.Height/2.0f),
                        new Vector3(2.0f, 1.0f, 0.0f), 2.0f, 2.0f, 2.0f, Color.White);

                    DrawGrid(10, 1.0f);        // Draw a grid

                EndMode3D();

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(texture); // Unload texture

        CloseWindow();
    }

    // Draw cube textured
    // NOTE: Cube position is the center position
    private static void DrawCubeTexture(Texture2D texture, Vector3 position, float width, float height, float length, Color color)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z;

        // Set desired texture to be enabled while drawing following vertex data
        rlSetTexture(texture.Id);

        // Vertex data transformation can be defined with the commented lines,
        // but in this example we calculate the transformed vertex data directly when calling rlVertex3f()
        //rlPushMatrix();
            // NOTE: Transformation is applied in inverse order (scale -> rotate -> translate)
            //rlTranslatef(2.0f, 0.0f, 0.0f);
            //rlRotatef(45, 0, 1, 0);
            //rlScalef(2.0f, 2.0f, 2.0f);

            rlBegin(RlDrawMode.Quads);
                rlColor4ub(color.R, color.G, color.B, color.A);
                // Front Face
                rlNormal3f(0.0f, 0.0f, 1.0f);       // Normal Pointing Towards Viewer
                rlTexCoord2f(0.0f, 0.0f); rlVertex3f(x - width/2, y - height/2, z + length/2);  // Bottom Left Of The Texture and Quad
                rlTexCoord2f(1.0f, 0.0f); rlVertex3f(x + width/2, y - height/2, z + length/2);  // Bottom Right Of The Texture and Quad
                rlTexCoord2f(1.0f, 1.0f); rlVertex3f(x + width/2, y + height/2, z + length/2);  // Top Right Of The Texture and Quad
                rlTexCoord2f(0.0f, 1.0f); rlVertex3f(x - width/2, y + height/2, z + length/2);  // Top Left Of The Texture and Quad
                // Back Face
                rlNormal3f(0.0f, 0.0f, - 1.0f);     // Normal Pointing Away From Viewer
                rlTexCoord2f(1.0f, 0.0f); rlVertex3f(x - width/2, y - height/2, z - length/2);  // Bottom Right Of The Texture and Quad
                rlTexCoord2f(1.0f, 1.0f); rlVertex3f(x - width/2, y + height/2, z - length/2);  // Top Right Of The Texture and Quad
                rlTexCoord2f(0.0f, 1.0f); rlVertex3f(x + width/2, y + height/2, z - length/2);  // Top Left Of The Texture and Quad
                rlTexCoord2f(0.0f, 0.0f); rlVertex3f(x + width/2, y - height/2, z - length/2);  // Bottom Left Of The Texture and Quad
                // Top Face
                rlNormal3f(0.0f, 1.0f, 0.0f);       // Normal Pointing Up
                rlTexCoord2f(0.0f, 1.0f); rlVertex3f(x - width/2, y + height/2, z - length/2);  // Top Left Of The Texture and Quad
                rlTexCoord2f(0.0f, 0.0f); rlVertex3f(x - width/2, y + height/2, z + length/2);  // Bottom Left Of The Texture and Quad
                rlTexCoord2f(1.0f, 0.0f); rlVertex3f(x + width/2, y + height/2, z + length/2);  // Bottom Right Of The Texture and Quad
                rlTexCoord2f(1.0f, 1.0f); rlVertex3f(x + width/2, y + height/2, z - length/2);  // Top Right Of The Texture and Quad
                // Bottom Face
                rlNormal3f(0.0f, - 1.0f, 0.0f);     // Normal Pointing Down
                rlTexCoord2f(1.0f, 1.0f); rlVertex3f(x - width/2, y - height/2, z - length/2);  // Top Right Of The Texture and Quad
                rlTexCoord2f(0.0f, 1.0f); rlVertex3f(x + width/2, y - height/2, z - length/2);  // Top Left Of The Texture and Quad
                rlTexCoord2f(0.0f, 0.0f); rlVertex3f(x + width/2, y - height/2, z + length/2);  // Bottom Left Of The Texture and Quad
                rlTexCoord2f(1.0f, 0.0f); rlVertex3f(x - width/2, y - height/2, z + length/2);  // Bottom Right Of The Texture and Quad
                // Right face
                rlNormal3f(1.0f, 0.0f, 0.0f);       // Normal Pointing Right
                rlTexCoord2f(1.0f, 0.0f); rlVertex3f(x + width/2, y - height/2, z - length/2);  // Bottom Right Of The Texture and Quad
                rlTexCoord2f(1.0f, 1.0f); rlVertex3f(x + width/2, y + height/2, z - length/2);  // Top Right Of The Texture and Quad
                rlTexCoord2f(0.0f, 1.0f); rlVertex3f(x + width/2, y + height/2, z + length/2);  // Top Left Of The Texture and Quad
                rlTexCoord2f(0.0f, 0.0f); rlVertex3f(x + width/2, y - height/2, z + length/2);  // Bottom Left Of The Texture and Quad
                // Left Face
                rlNormal3f( - 1.0f, 0.0f, 0.0f);    // Normal Pointing Left
                rlTexCoord2f(0.0f, 0.0f); rlVertex3f(x - width/2, y - height/2, z - length/2);  // Bottom Left Of The Texture and Quad
                rlTexCoord2f(1.0f, 0.0f); rlVertex3f(x - width/2, y - height/2, z + length/2);  // Bottom Right Of The Texture and Quad
                rlTexCoord2f(1.0f, 1.0f); rlVertex3f(x - width/2, y + height/2, z + length/2);  // Top Right Of The Texture and Quad
                rlTexCoord2f(0.0f, 1.0f); rlVertex3f(x - width/2, y + height/2, z - length/2);  // Top Left Of The Texture and Quad
            rlEnd();
        //rlPopMatrix();

        rlSetTexture(0);
    }

    // Draw cube with texture piece applied to all faces
    private static void DrawCubeTextureRec(Texture2D texture, Rectangle source, Vector3 position, float width, float height, float length, Color color)
    {
        float x = position.X;
        float y = position.Y;
        float z = position.Z;
        float texWidth = (float)texture.Width;
        float texHeight = (float)texture.Height;

        // Set desired texture to be enabled while drawing following vertex data
        rlSetTexture(texture.Id);

        // We calculate the normalized texture coordinates for the desired texture-source-rectangle
        // It means converting from (tex.width, tex.height) coordinates to [0.0f, 1.0f] equivalent
        rlBegin(RlDrawMode.Quads);
            rlColor4ub(color.R, color.G, color.B, color.A);

            // Front face
            rlNormal3f(0.0f, 0.0f, 1.0f);
            rlTexCoord2f(source.X/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x - width/2, y - height/2, z + length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x + width/2, y - height/2, z + length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, source.Y/texHeight);
            rlVertex3f(x + width/2, y + height/2, z + length/2);
            rlTexCoord2f(source.X/texWidth, source.Y/texHeight);
            rlVertex3f(x - width/2, y + height/2, z + length/2);

            // Back face
            rlNormal3f(0.0f, 0.0f, - 1.0f);
            rlTexCoord2f((source.X + source.Width)/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x - width/2, y - height/2, z - length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, source.Y/texHeight);
            rlVertex3f(x - width/2, y + height/2, z - length/2);
            rlTexCoord2f(source.X/texWidth, source.Y/texHeight);
            rlVertex3f(x + width/2, y + height/2, z - length/2);
            rlTexCoord2f(source.X/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x + width/2, y - height/2, z - length/2);

            // Top face
            rlNormal3f(0.0f, 1.0f, 0.0f);
            rlTexCoord2f(source.X/texWidth, source.Y/texHeight);
            rlVertex3f(x - width/2, y + height/2, z - length/2);
            rlTexCoord2f(source.X/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x - width/2, y + height/2, z + length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x + width/2, y + height/2, z + length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, source.Y/texHeight);
            rlVertex3f(x + width/2, y + height/2, z - length/2);

            // Bottom face
            rlNormal3f(0.0f, - 1.0f, 0.0f);
            rlTexCoord2f((source.X + source.Width)/texWidth, source.Y/texHeight);
            rlVertex3f(x - width/2, y - height/2, z - length/2);
            rlTexCoord2f(source.X/texWidth, source.Y/texHeight);
            rlVertex3f(x + width/2, y - height/2, z - length/2);
            rlTexCoord2f(source.X/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x + width/2, y - height/2, z + length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x - width/2, y - height/2, z + length/2);

            // Right face
            rlNormal3f(1.0f, 0.0f, 0.0f);
            rlTexCoord2f((source.X + source.Width)/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x + width/2, y - height/2, z - length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, source.Y/texHeight);
            rlVertex3f(x + width/2, y + height/2, z - length/2);
            rlTexCoord2f(source.X/texWidth, source.Y/texHeight);
            rlVertex3f(x + width/2, y + height/2, z + length/2);
            rlTexCoord2f(source.X/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x + width/2, y - height/2, z + length/2);

            // Left face
            rlNormal3f( - 1.0f, 0.0f, 0.0f);
            rlTexCoord2f(source.X/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x - width/2, y - height/2, z - length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, (source.Y + source.Height)/texHeight);
            rlVertex3f(x - width/2, y - height/2, z + length/2);
            rlTexCoord2f((source.X + source.Width)/texWidth, source.Y/texHeight);
            rlVertex3f(x - width/2, y + height/2, z + length/2);
            rlTexCoord2f(source.X/texWidth, source.Y/texHeight);
            rlVertex3f(x - width/2, y + height/2, z - length/2);

        rlEnd();

        rlSetTexture(0);
    }
}
