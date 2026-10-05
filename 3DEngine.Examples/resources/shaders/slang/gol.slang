// raylib's gol.glsl, written in Slang: one step of Conway's Game of Life, each thread a cell.

#define GOL_WIDTH 768

StructuredBuffer<uint> golBuffer;           // golBuffer[x, y] = golBuffer[x + GOL_WIDTH*y]
RWStructuredBuffer<uint> golBufferDest;     // golBufferDest[x, y] = golBufferDest[x + GOL_WIDTH*y]

// A cell, or 0 past the grid's edge. raylib's test lets x and y reach GOL_WIDTH, a cell past the
// end of a row, which is the next row's first or, past the last row, after the buffer, so here
// they stop at the edge. A coordinate below 0 wraps to a large one, as a uint does.
uint fetchGol(uint x, uint y)
{
    return ((x >= GOL_WIDTH) || (y >= GOL_WIDTH)) ? 0 : golBuffer[x + GOL_WIDTH*y];
}

void setGol(uint x, uint y, uint value)
{
    golBufferDest[x + GOL_WIDTH*y] = value;
}

[shader("compute")]
[numthreads(16, 16, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint neighborCount = 0;
    uint x = id.x;
    uint y = id.y;

    neighborCount += fetchGol(x - 1, y - 1);   // Top left
    neighborCount += fetchGol(x, y - 1);       // Top middle
    neighborCount += fetchGol(x + 1, y - 1);   // Top right
    neighborCount += fetchGol(x - 1, y);       // Left
    neighborCount += fetchGol(x + 1, y);       // Right
    neighborCount += fetchGol(x - 1, y + 1);   // Bottom left
    neighborCount += fetchGol(x, y + 1);       // Bottom middle
    neighborCount += fetchGol(x + 1, y + 1);   // Bottom right

    if (neighborCount == 3) setGol(x, y, 1);
    else if (neighborCount == 2) setGol(x, y, fetchGol(x, y));
    else setGol(x, y, 0);
}
