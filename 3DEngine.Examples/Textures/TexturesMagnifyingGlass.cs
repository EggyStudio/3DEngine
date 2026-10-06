// raylib's textures_magnifying_glass example, Copyright (c) 2026 Luke Vaughan (@badram), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesMagnifyingGlass
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] magnifying glass");

        Texture2D bunny = LoadTexture("resources/raybunny.png");
        Texture2D parrots = LoadTexture("resources/parrots.png");

        // Use image draw to generate a mask texture instead of loading it from a file.
        Image circle = GenImageColor(256, 256, Color.Blank);
        ImageDrawCircle(ref circle, 128, 128, 128, Color.White);
        Texture2D mask = LoadTextureFromImage(circle); // Copy the mask image from RAM to VRAM
        UnloadImage(circle); // Unload the image from RAM

        RenderTexture2D magnifiedWorld = LoadRenderTexture(256, 256);

        Camera2D camera = default;
        // Set magnifying glass zoom
        camera.Zoom = 2;
        // Offset by half the size of the magnifying glass to counteract drawing the texture centered on the mouse position
        camera.Offset = new Vector2(128, 128);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mPos = GetMousePosition();
            camera.Target = mPos;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // Draw the normal version of the world
                DrawTexture(parrots, 144, 33, Color.White);
                DrawText("Use the magnifying glass to find hidden bunnies!", 154, 6, 20, Color.Black);

                // Render to a the magnifying glass
                BeginTextureMode(magnifiedWorld);
                    ClearBackground(Color.RayWhite);

                    BeginMode2D(camera);
                        // Draw the same things in the magnified world as were in the normal version
                        DrawTexture(parrots, 144, 33, Color.White);
                        DrawText("Use the magnifying glass to find hidden bunnies!", 154, 6, 20, Color.Black);

                        // Draw bunnies only in the magnified world.
                        // BlendMode.Multiplied lets them take on the color of the image below them.
                        BeginBlendMode(BlendMode.Multiplied);
                            DrawTexture(bunny, 250, 350, Color.White);
                            DrawTexture(bunny, 500, 100, Color.White);
                            DrawTexture(bunny, 420, 300, Color.White);
                            DrawTexture(bunny, 650, 10, Color.White);
                        EndBlendMode();
                    EndMode2D();

                    // Mask the magnifying glass view texture to a circle
                    // To make the mask affect only alpha, a CUSTOM blend mode is used with SEPARATE color/alpha functions
                    BeginBlendMode(BlendMode.CustomSeparate);
                        // C is color and A alpha, s the source drawn and d the destination drawn into:
                        //   source color, zero (Cs * 0 = 0), so the mask's colors are not drawn at all
                        //   destination color, one (Cd * 1 = Cd), so the colors drawn stay as they are
                        //   source alpha, one (As * 1 = As), so the mask's alpha is taken as it is
                        //   destination alpha, zero (Ad * 0 = 0), so the alpha drawn is let go
                        //   both added (Cs(0) + Cd = Cd, As + Ad(0) = As), so the color is unchanged and the alpha is the mask's
                        rlSetBlendFactorsSeparate(RlBlendFactor.Zero, RlBlendFactor.One, RlBlendFactor.One, RlBlendFactor.Zero, RlBlendEquation.FuncAdd, RlBlendEquation.FuncAdd);
                        DrawTexture(mask, 0, 0, Color.White);
                    EndBlendMode();
                EndTextureMode();

                // Draw magnifiedWorld to screen, centered on cursor. A render texture is stored
                // the right way up, so it is drawn with its height as it is, where raylib's is turned.
                DrawTextureRec(magnifiedWorld.Texture, new Rectangle(0, 0, 256, 256), new Vector2(mPos.X - 128, mPos.Y - 128), Color.White);

                // Draw the outer ring of the magnifying glass
                DrawRing(mPos, 126, 130, 0, 360, 64, Color.Black);

                // Draw floating specular highlight on the glass
                float rx = mPos.X/800;
                float ry = mPos.Y/800;
                DrawCircle((int)(mPos.X - 64*rx) - 32, (int)(mPos.Y - 64*ry) - 32, 4, ColorAlpha(Color.White, 0.5f));

            EndDrawing();
        }

        UnloadTexture(parrots);
        UnloadTexture(bunny);
        UnloadTexture(mask);
        UnloadRenderTexture(magnifiedWorld);

        CloseWindow();
    }
}
