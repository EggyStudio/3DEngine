// raylib's textures_npatch_drawing example, Copyright (c) 2018-2025 Jorge A. Gomes (@overdev) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesNpatchDrawing
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] npatch drawing");

        Texture2D nPatchTexture = LoadTexture("resources/ninepatch_button.png");

        // Pixel art, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(nPatchTexture, TextureFilter.Point);

        Vector2 mousePosition = Vector2.Zero;
        Vector2 origin = new(0.0f, 0.0f);

        // Where the patches are drawn, and their sizes
        Rectangle dstRec1 = new(480.0f, 160.0f, 32.0f, 32.0f);
        Rectangle dstRec2 = new(160.0f, 160.0f, 32.0f, 32.0f);
        Rectangle dstRecH = new(160.0f, 93.0f, 32.0f, 32.0f);
        Rectangle dstRecV = new(92.0f, 160.0f, 32.0f, 32.0f);

        // A nine-patch stretches on both axes
        NPatchInfo ninePatchInfo1 = new(new Rectangle(0.0f, 0.0f, 64.0f, 64.0f), 12, 40, 12, 12, NPatchLayout.NinePatch);
        NPatchInfo ninePatchInfo2 = new(new Rectangle(0.0f, 128.0f, 64.0f, 64.0f), 16, 16, 16, 16, NPatchLayout.NinePatch);

        // a horizontal three-patch along x only
        NPatchInfo h3PatchInfo = new(new Rectangle(0.0f, 64.0f, 64.0f, 64.0f), 8, 8, 8, 8, NPatchLayout.ThreePatchHorizontal);

        // and a vertical three-patch along y only.
        NPatchInfo v3PatchInfo = new(new Rectangle(0.0f, 192.0f, 64.0f, 64.0f), 6, 6, 6, 6, NPatchLayout.ThreePatchVertical);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            mousePosition = GetMousePosition();

            // The patches stretch to the pointer, each at least a pixel and the nine-patches at
            // most 300 wide.
            dstRec1 = dstRec1 with { Width = Math.Clamp(mousePosition.X - dstRec1.X, 1.0f, 300.0f), Height = Math.Max(mousePosition.Y - dstRec1.Y, 1.0f) };
            dstRec2 = dstRec2 with { Width = Math.Clamp(mousePosition.X - dstRec2.X, 1.0f, 300.0f), Height = Math.Max(mousePosition.Y - dstRec2.Y, 1.0f) };
            dstRecH = dstRecH with { Width = Math.Max(mousePosition.X - dstRecH.X, 1.0f) };
            dstRecV = dstRecV with { Height = Math.Max(mousePosition.Y - dstRecV.Y, 1.0f) };

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTextureNPatch(nPatchTexture, ninePatchInfo2, dstRec2, origin, 0.0f, Color.White);
                DrawTextureNPatch(nPatchTexture, ninePatchInfo1, dstRec1, origin, 0.0f, Color.White);
                DrawTextureNPatch(nPatchTexture, h3PatchInfo, dstRecH, origin, 0.0f, Color.White);
                DrawTextureNPatch(nPatchTexture, v3PatchInfo, dstRecV, origin, 0.0f, Color.White);

                // The texture the patches come from
                DrawRectangleLines(5, 88, 74, 266, Color.Blue);
                DrawTexture(nPatchTexture, 10, 93, Color.White);
                DrawText("TEXTURE", 15, 360, 10, Color.DarkGray);

                DrawText("Move the mouse to stretch or shrink the n-patches", 10, 20, 20, Color.DarkGray);

            EndDrawing();
        }

        UnloadTexture(nPatchTexture);

        CloseWindow();
    }
}
