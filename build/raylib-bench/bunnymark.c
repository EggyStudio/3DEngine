// raylib's textures_bunnymark with the search 3DEngine's StressRamp makes: the count grows by half
// until a frame takes longer than a sixtieth of a second, then the distance between the last count
// that held and the first that did not halves until they are within about 3 percent.
#include "raylib.h"
#include <stdio.h>
#include <stdlib.h>

#define MAX 2000000
typedef struct { Vector2 position, speed; Color color; } Bunny;

int main(int argc, char **argv)
{
    SetConfigFlags(FLAG_WINDOW_HIDDEN);
    InitWindow(800, 450, "raylib bunnymark");
    SetTargetFPS(0);
    Image image = LoadImage(argc > 1 ? argv[1] : "logo.png");
    ImageResize(&image, 32, 32);
    Texture2D texture = LoadTextureFromImage(image);
    UnloadImage(image);

    Bunny *bunnies = (Bunny *)malloc(MAX * sizeof(Bunny));
    srand(1);
    int count = 0, wanted = 1000, held = 0, failed = -1, limit = 0, settleFrames = 0, frames = 0, doubted = 0;
    double settled = 0, seconds = 0, frameMs = 0;

    while (!WindowShouldClose() && limit == 0)
    {
        while (count < wanted)
        {
            bunnies[count].position = (Vector2){ (float)(rand() % 768), (float)(rand() % 418) };
            bunnies[count].speed = (Vector2){ (float)(rand() % 500 - 250) / 60.0f, (float)(rand() % 500 - 250) / 60.0f };
            bunnies[count].color = (Color){ 50 + rand() % 190, 80 + rand() % 160, 100 + rand() % 140, 255 };
            count++;
        }
        if (count > wanted) count = wanted;

        for (int i = 0; i < count; i++)
        {
            bunnies[i].position.x += bunnies[i].speed.x;
            bunnies[i].position.y += bunnies[i].speed.y;
            if (bunnies[i].position.x < 0 || bunnies[i].position.x > GetScreenWidth() - 32) bunnies[i].speed.x *= -1;
            if (bunnies[i].position.y < 40 || bunnies[i].position.y > GetScreenHeight() - 32) bunnies[i].speed.y *= -1;
        }

        BeginDrawing();
        ClearBackground(RAYWHITE);
        for (int i = 0; i < count; i++)
            DrawTexture(texture, (int)bunnies[i].position.x, (int)bunnies[i].position.y, bunnies[i].color);
        DrawRectangle(0, 0, GetScreenWidth(), 40, BLACK);
        DrawText(TextFormat("sprites: %i", count), 10, 10, 20, GREEN);
        DrawFPS(GetScreenWidth() - 110, 10);
        EndDrawing();

        // StressRamp.Measure, line for line.
        double frame = GetFrameTime();
        if (settled < 0.25 || settleFrames < 10) { settled += frame; settleFrames++; continue; }
        frames++;
        seconds += frame;
        if (frames < 20 || seconds < 0.5) continue;
        frameMs = seconds / frames * 1000;
        frames = settleFrames = 0;
        seconds = settled = 0;
        if (frameMs > 1000.0 / 60 && !doubted) { doubted = 1; continue; }
        doubted = 0;
        if (frameMs <= 1000.0 / 60) held = wanted; else failed = wanted;
        if (failed < 0) wanted = wanted * 3 / 2;
        else if (failed - held <= (failed / 32 > 1 ? failed / 32 : 1)) limit = held > 1 ? held : 1;
        else wanted = (held + failed) / 2;
        printf("count %d measured %.2f ms\n", wanted, frameMs);
        fflush(stdout);
    }
    printf("limit %d held 60 frames a second, %d did not, last %.2f ms\n", limit, failed, frameMs);
    UnloadTexture(texture);
    CloseWindow();
    free(bunnies);
    return 0;
}
