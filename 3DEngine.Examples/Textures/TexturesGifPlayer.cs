// raylib's textures_gif_player example, Copyright (c) 2021-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesGifPlayer
{
    private const int MAX_FRAME_DELAY = 20;
    private const int MIN_FRAME_DELAY = 1;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] gif player");

        // Every frame of the GIF in one image, stacked from the top, where raylib's image is one
        // frame tall with the rest after it in memory. The frames lie end to end in its pixels
        // either way, so a frame's offset is the same.
        Image imScarfyAnim = LoadImageAnim("resources/scarfy_run.gif", out int animFrames);
        int frameHeight = imScarfyAnim.Height/Math.Max(animFrames, 1);
        int frameBytes = imScarfyAnim.Width*frameHeight*4;

        // A texture of the first frame, which takes the next frame's pixels when it is due. A
        // sprite sheet is the better way to animate, as textures_sprite_animation draws one.
        Texture2D texScarfyAnim = LoadTextureFromImage(ImageFromImage(imScarfyAnim, new Rectangle(0, 0, imScarfyAnim.Width, frameHeight)));

        int nextFrameDataOffset = 0;

        int currentAnimFrame = 0;
        int frameDelay = 8;
        int frameCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            frameCounter++;
            if (frameCounter >= frameDelay)
            {
                // The next frame, back to the first after the last
                currentAnimFrame++;
                if (currentAnimFrame >= animFrames) currentAnimFrame = 0;

                nextFrameDataOffset = frameBytes*currentAnimFrame;

                UpdateTextureRec(texScarfyAnim, new Rectangle(0, 0, texScarfyAnim.Width, texScarfyAnim.Height),
                    imScarfyAnim.Data.AsSpan(nextFrameDataOffset, frameBytes).ToArray());

                frameCounter = 0;
            }

            if (IsKeyPressed(Key.Right)) frameDelay++;
            else if (IsKeyPressed(Key.Left)) frameDelay--;

            if (frameDelay > MAX_FRAME_DELAY) frameDelay = MAX_FRAME_DELAY;
            else if (frameDelay < MIN_FRAME_DELAY) frameDelay = MIN_FRAME_DELAY;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText($"TOTAL GIF FRAMES:  {animFrames:D2}", 50, 30, 20, Color.LightGray);
                DrawText($"CURRENT FRAME: {currentAnimFrame:D2}", 50, 60, 20, Color.Gray);
                DrawText($"CURRENT FRAME IMAGE.DATA OFFSET: {nextFrameDataOffset:D2}", 50, 90, 20, Color.Gray);

                DrawText("FRAMES DELAY: ", 100, 305, 10, Color.DarkGray);
                DrawText($"{frameDelay:D2} frames", 620, 305, 10, Color.DarkGray);
                DrawText("PRESS RIGHT/LEFT KEYS to CHANGE SPEED!", 290, 350, 10, Color.DarkGray);

                for (int i = 0; i < MAX_FRAME_DELAY; i++)
                {
                    if (i < frameDelay) DrawRectangle(190 + 21*i, 300, 20, 20, Color.Red);
                    DrawRectangleLines(190 + 21*i, 300, 20, 20, Color.Maroon);
                }

                DrawTexture(texScarfyAnim, GetScreenWidth()/2 - texScarfyAnim.Width/2, 140, Color.White);

                DrawText("(c) Scarfy sprite by Eiden Marsal", screenWidth - 200, screenHeight - 20, 10, Color.Gray);

            EndDrawing();
        }

        UnloadTexture(texScarfyAnim);
        UnloadImage(imScarfyAnim);

        CloseWindow();
    }
}
