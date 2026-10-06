// Linked into one of raylib's examples by build/raylib-bench/compare.py, around EndDrawing through
// the linker's --wrap: at the frame named by SHOT_FRAME the frame's batch is drawn and written to
// SHOT_PATH, as this engine's capture of the same example is taken, and the program ends.
#include <stdlib.h>
#include "raylib.h"
#include "rlgl.h"

void __real_EndDrawing(void);

static int frames;

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
