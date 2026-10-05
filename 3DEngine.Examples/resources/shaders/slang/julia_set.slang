// raylib's julia_set.fs, written in Slang: the Julia set of z^2 + c, each point colored by how soon
// it leaves the circle of radius 2.
import engine;

uniform float2 c;               // c.x the real part, c.y the imaginary
uniform float2 offset;          // Where the view is moved to
uniform float zoom;

static const int maxIterations = 255;
static const float colorCycles = 2.0;  // How often the palette repeats, more showing more detail

float2 ComplexSquare(float2 z)
{
    return float2(z.x * z.x - z.y * z.y, z.x * z.y * 2.0);
}

float3 Hsv2rgb(float3 hsv)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(hsv.xxx + K.xyz) * 6.0 - K.www);
    return hsv.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), hsv.y);
}

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    // z^2 + c is iterated until z leaves the circle of radius 2, past which it grows without end,
    // or the iterations run out, and the count over the most is a color. The square of the length
    // is held to 4, which saves the square root.
    float2 z = float2((input.uv.x - 0.5) * 2.5, (input.uv.y - 0.5) * 1.5) / zoom;
    z.x += offset.x;
    z.y += offset.y;

    int iterations = 0;
    for (iterations = 0; iterations < maxIterations; iterations++)
    {
        z = ComplexSquare(z) + c;
        if (dot(z, z) > 4.0) break;
    }

    // Two more steps, which smooth the bands between counts
    z = ComplexSquare(z) + c;
    z = ComplexSquare(z) + c;

    float smoothVal = float(iterations) + 1.0 - (log(log(length(z))) / log(2.0));
    float norm = smoothVal / float(maxIterations);

    if (norm > 0.999) return float4(0.0, 0.0, 0.0, 1.0);
    return float4(Hsv2rgb(float3(norm * colorCycles, 1.0, 1.0)), 1.0);
}
