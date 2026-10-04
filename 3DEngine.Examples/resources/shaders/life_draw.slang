// Draws the grid life.slang steps straight from its buffer, a square of cellSize pixels for each
// cell, found from the pixel being drawn, so the grid never goes through the CPU.
import engine;

uniform uint width;
uniform uint height;
uniform float cellSize;
StructuredBuffer<uint> cells;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    uint x = min(uint(input.position.x / cellSize), width - 1);
    uint y = min(uint(input.position.y / cellSize), height - 1);
    return cells[y * width + x] == 1 ? float4(1.0, 0.878, 0.275, 1) : float4(0.078, 0.118, 0.157, 1);
}
