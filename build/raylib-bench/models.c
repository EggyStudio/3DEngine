// Cubes on a square spiral 1.5 apart, each turning every frame and drawn with DrawModelEx, as
// 3DEngine's models_stress places its entities, with the search its StressRamp makes. raylib's
// default shader is unlit and casts no shadow, so each cube costs it less drawing than the lit,
// shadowed entities of models_stress, and none of the eight skinned arms is drawn.
#include "raylib.h"
#include "raymath.h"
#include <math.h>
#include <stdio.h>
#include <stdlib.h>

#define MAX 2000000

// The next place on the spiral, walking on from where the one before left it.
static Vector3 Spot(int *x, int *z, int *dx, int *dz, int *leg, int *step, int *turns)
{
    *x += *dx; *z += *dz;
    if (++*step >= *leg) { *step = 0; int t = *dx; *dx = -*dz; *dz = t; if (++*turns % 2 == 0) (*leg)++; }
    return (Vector3){ *x * 1.5f, 0, *z * 1.5f };
}

int main(void)
{
    SetConfigFlags(FLAG_WINDOW_HIDDEN);
    InitWindow(800, 450, "raylib models");
    SetTargetFPS(0);
    Model cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
    Color colors[4] = { { 200, 196, 186, 255 }, { 190, 120, 60, 255 }, { 90, 200, 120, 255 }, { 230, 200, 90, 255 } };

    Vector3 *spots = (Vector3 *)malloc(MAX * sizeof(Vector3));
    float *angles = (float *)calloc(MAX, sizeof(float));
    int x = 0, z = 0, dx = 1, dz = 0, leg = 1, step = 0, turns = 0;
    spots[0] = (Vector3){ 0, 0, 0 };
    for (int i = 1; i < MAX; i++) spots[i] = Spot(&x, &z, &dx, &dz, &leg, &step, &turns);

    int count = 500, wanted = 500, held = 0, failed = -1, limit = 0, settleFrames = 0, frames = 0, doubted = 0;
    double settled = 0, seconds = 0, frameMs = 0;

    while (!WindowShouldClose() && limit == 0)
    {
        count = wanted;
        float turn = GetFrameTime() * RAD2DEG;
        for (int i = 0; i < count; i++) angles[i] = fmodf(angles[i] + turn, 360);

        float side = sqrtf((float)(count > 1 ? count : 1)) * 1.5f;
        Camera3D camera = { { 0, side * 0.8f + 4, side * 0.9f + 6 }, { 0, 0, 0 }, { 0, 1, 0 }, 45, CAMERA_PERSPECTIVE };

        BeginDrawing();
        ClearBackground((Color){ 30, 34, 44, 255 });
        BeginMode3D(camera);
        DrawCube((Vector3){ 0, -0.6f, 0 }, side + 4, 0.2f, side + 4, (Color){ 120, 124, 130, 255 });
        for (int i = 0; i < count; i++)
            DrawModelEx(cube, spots[i], (Vector3){ 0, 1, 0 }, angles[i], (Vector3){ 1, 1, 1 }, colors[i % 4]);
        EndMode3D();
        DrawRectangle(0, 0, GetScreenWidth(), 40, BLACK);
        DrawText(TextFormat("models: %i", count), 10, 10, 20, GREEN);
        DrawFPS(GetScreenWidth() - 110, 10);
        EndDrawing();

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
    UnloadModel(cube);
    CloseWindow();
    return 0;
}
