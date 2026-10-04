// One step of Conway's Game of Life on a grid that wraps at its edges: each cell of next is alive
// when three of its neighbors in current are, or two and it was already, and pixels takes its
// color, as four bytes packed red first.
uniform uint width;
uniform uint height;
StructuredBuffer<uint> current;
RWStructuredBuffer<uint> next;
RWStructuredBuffer<uint> pixels;

[shader("compute")]
[numthreads(16, 16, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= width || id.y >= height)
        return;

    uint neighbors = 0;
    for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0)
                continue;
            uint x = (id.x + width + dx) % width;
            uint y = (id.y + height + dy) % height;
            neighbors += current[y * width + x];
        }

    uint cell = id.y * width + id.x;
    uint alive = (neighbors == 3 || (neighbors == 2 && current[cell] == 1)) ? 1 : 0;
    next[cell] = alive;
    pixels[cell] = alive == 1 ? 0xFF46E0FFu : 0xFF281E14u;
}
