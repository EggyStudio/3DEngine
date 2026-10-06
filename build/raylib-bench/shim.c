// Linked into one of raylib's examples by build/raylib-bench/compare.py, around BeginDrawing,
// EndDrawing, GetFrameTime, GetTime and InitWindow through the linker's --wrap, which reaches the
// example's own calls and leaves raylib's calls within its own files as they are.
//
// At the frame named by SHOT_FRAME the frame's batch is drawn and written to SHOT_PATH, as this
// engine's capture of the same example is taken, and the program ends. SHOT_FRAME_TIME makes each
// frame last that many seconds to the example, as this engine's E3D_FRAME_TIME does, where time
// moves on as a frame begins. No time has passed before the first BeginDrawing, and k frames' time
// once the frame numbered k from 1 has begun. SHOT_SEED seeds the random generator once the window
// is open, as E3D_SEED does there, so the two programs step and place things alike.
#include <stdlib.h>
#include "raylib.h"
#include "rlgl.h"

void __real_BeginDrawing(void);
void __real_EndDrawing(void);
float __real_GetFrameTime(void);
double __real_GetTime(void);
void __real_InitWindow(int width, int height, const char *title);

// Frames begun and frames ended.
static int begun;
static int frames;

static double FrameSeconds(void)
{
    const char *seconds = getenv("SHOT_FRAME_TIME");
    return seconds != NULL ? atof(seconds) : 0.0;
}

void __wrap_BeginDrawing(void)
{
    begun++;
    __real_BeginDrawing();
}

void __wrap_EndDrawing(void)
{
    frames++;
    const char *at = getenv("SHOT_FRAME");
    const char *path = getenv("SHOT_PATH");
    if (at != NULL && path != NULL && frames >= atoi(at))
    {
        rlDrawRenderBatchActive();
        TakeScreenshot(path);
        exit(0);
    }
    __real_EndDrawing();
}

float __wrap_GetFrameTime(void)
{
    double seconds = FrameSeconds();
    if (seconds <= 0.0) return __real_GetFrameTime();
    return begun > 0 ? (float)seconds : 0.0f;
}

double __wrap_GetTime(void)
{
    double seconds = FrameSeconds();
    return seconds > 0.0 ? begun*seconds : __real_GetTime();
}

void __wrap_InitWindow(int width, int height, const char *title)
{
    __real_InitWindow(width, height, title);
    const char *seed = getenv("SHOT_SEED");
    if (seed != NULL) SetRandomSeed((unsigned int)strtoul(seed, NULL, 10));
}
