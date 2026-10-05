// raylib's spotlight.fs, written in Slang: darkness over the screen, clear in a few spots and fading
// out around them, and dim rather than dark over its right half.
import engine;

static const int MAX_SPOTS = 3;

struct Spot
{
    float2 pos;         // In window pixels, from the bottom as GLSL counts them
    float inner;        // Clear within this
    float radius;       // Fading out to this
};

uniform Spot spots[MAX_SPOTS];
uniform float screenWidth;

// GLSL's gl_FragCoord counts from the bottom and SV_Position from the top, so the screen's height,
// which raylib's shader has no need of, turns it.
uniform float screenHeight;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float alpha = 1.0;

    float2 pos = float2(input.position.x, screenHeight - input.position.y);

    // The nearest spot, counting each spot's size
    float d = 65000;
    int fi = -1;
    for (int i = 0; i < MAX_SPOTS; i++)
    {
        for (int j = 0; j < MAX_SPOTS; j++)
        {
            float dj = distance(pos, spots[j].pos) - spots[j].radius + spots[i].radius;
            if (d > dj)
            {
                d = dj;
                fi = i;
            }
        }
    }

    if (fi != -1)
    {
        if (d > spots[fi].radius) alpha = 1.0;
        else
        {
            if (d < spots[fi].inner) alpha = 0.0;
            else alpha = (d - spots[fi].inner) / (spots[fi].radius - spots[fi].inner);
        }
    }

    // The right half is dim rather than dark.
    if ((pos.x > screenWidth / 2.0) && (alpha > 0.9)) alpha = 0.9;

    return float4(0, 0, 0, alpha);
}
