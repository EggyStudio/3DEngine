// raylib's gol_transfert.glsl, written in Slang: the squares the mouse drew, each brought to life
// or cleared on the grid by a thread of its own.

#define GOL_WIDTH 768
#define MAX_BUFFERED_TRANSFERTS 48

// Game Of Life Update Command, as the program lays it out
struct GolUpdateCmd
{
    uint x;         // x coordinate of the gol command
    uint y;         // y coordinate of the gol command
    uint w;         // width of the filled zone
    uint enabled;   // whether to enable or disable zone
};

// The commands, after their count, as one element
struct GolUpdateSSBO
{
    uint count;
    GolUpdateCmd commands[MAX_BUFFERED_TRANSFERTS];
};

// Output game of life grid buffer
RWStructuredBuffer<uint> golBuffer;     // golBuffer[x, y] = golBuffer[x + GOL_WIDTH*y]

// Command buffer
StructuredBuffer<GolUpdateSSBO> golUpdate;

bool isInside(uint x, uint y) { return (x >= 0) && (y >= 0) && (x < GOL_WIDTH) && (y < GOL_WIDTH); }
uint getBufferIndex(uint x, uint y) { return x + GOL_WIDTH*y; }

[shader("compute")]
[numthreads(1, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint cmdIndex = id.x;
    GolUpdateCmd cmd = golUpdate[0].commands[cmdIndex];

    for (uint x = cmd.x; x < (cmd.x + cmd.w); x++)
    {
        for (uint y = cmd.y; y < (cmd.y + cmd.w); y++)
        {
            if (isInside(x, y))
            {
                if (cmd.enabled != 0) InterlockedOr(golBuffer[getBufferIndex(x, y)], 1);
                else InterlockedAnd(golBuffer[getBufferIndex(x, y)], 0);
            }
        }
    }
}
