// raylib's gol_render.glsl, written in Slang: the grid gol.slang steps, white for a living cell and
// black for a dead one, read straight from its buffer.
import engine;

#define GOL_WIDTH 768

// Input game of life grid
StructuredBuffer<uint> golBuffer;

// Output resolution
uniform float2 resolution;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    int2 coords = int2(input.uv*resolution);

    if ((golBuffer[coords.x + coords.y*uint2(resolution).x]) == 1) return float4(1.0);
    else return float4(0.0, 0.0, 0.0, 1.0);
}
