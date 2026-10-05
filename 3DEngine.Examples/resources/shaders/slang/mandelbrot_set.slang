// raylib's mandelbrot_set.fs, written in Slang: the Mandelbrot set, each point colored by how many
// steps of z^2 + c it takes to leave.
import engine;

static const float PI = 3.1415926535897932384626433832795;

uniform float2 offset;          // Where the view is moved to
uniform float zoom;
uniform int maxIterations;      // The most steps a point takes

// A point that reaches 4 is taken to go on to infinity, compared squared to save the square root.
static const float max = 4.0;
static const float max2 = max * max;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // The point's place in the set's plane, the texture coordinates being 0 to 1 across the screen
    float2 c = float2((input.uv.x - 0.5) * 2.5, (input.uv.y - 0.5) * 1.5) / zoom;
    c.x += offset.x;
    c.y += offset.y;

    // z = a + bi, from 0, stepped by z^2 + c = (a^2 - b^2 + c.x) + (2ab + c.y)i
    float a = 0.0;
    float b = 0.0;

    int iter = 0;
    for (iter = 0; iter < maxIterations; iter++)
    {
        float aa = a * a;
        float bb = b * b;
        if (aa + bb > max2) break;

        float twoab = 2.0 * a * b;
        a = aa - bb + c.x;
        b = twoab + c.y;
    }

    if (iter >= maxIterations) return float4(0.0, 0.0, 0.0, 1.0);

    float normR = float(iter % 55) / 55.0;
    float normG = float(iter % 69) / 69.0;
    float normB = float(iter % 40) / 40.0;
    return float4(sin(normR * PI), sin(normG * PI), sin(normB * PI), 1.0);
}
