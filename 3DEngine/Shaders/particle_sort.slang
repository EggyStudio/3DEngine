// Sorts one emitter's particles from the farthest from the eye to the nearest, for an emitter laid
// over by alpha, in the emitter's own buffer after its particles, one key a float4: x the key, y
// the particle's slot. The first dispatch writes a key for every slot, the negative of the squared
// distance from the eye for one alive, so ascending order is far to near, a large number for one
// dead, and infinity for the padding up to a power of two, so the first slots of the sorted keys
// are always the emitter's own. It also marks the header as sorted, which particles.slang reads
// the keys by.
//
// The sort is Batcher's bitonic sort, which orders each key with the one j away by which half of a
// run of k it is in. The steps within a block of 512 keys are done in a workgroup's shared memory,
// all of them in one dispatch, since a dispatch a step with a barrier between each took half a
// millisecond for 512 keys. Only the steps across blocks, for an emitter of more than 512, are
// dispatches of their own, each followed by the block's remaining steps in shared memory.

[[vk::binding(0, 0)]]
RWStructuredBuffer<float4> slots;

struct SortPush
{
    // x: j, the distance between the keys compared. y: k, the run they are ordered within. z: the
    // keys, a power of two. w: 0 to write the keys, 1 for one step across blocks, 2 to sort each
    // block whole, 3 for a block's steps within a run of k past a block.
    uint4 step;
    // xyz: the eye. w: how many particles the emitter holds.
    float4 eyeAndCapacity;
};

[[vk::push_constant]]
ConstantBuffer<SortPush> push;

static const uint Header = 4;
static const uint Threads = 256;
static const uint Block = 512;

groupshared float2 block[Block];

uint keysAt()
{
    return Header + uint(push.eyeAndCapacity.w) * 2;
}

// Orders the pair of keys in shared memory that a thread is given for a step of j within a run of
// k, by the keys' places in the whole list.
void orderPair(uint thread, uint base, uint j, uint k, uint size)
{
    if (thread < size / 2)
    {
        uint i = 2 * j * (thread / j) + (thread % j);
        uint other = i + j;
        float2 a = block[i];
        float2 b = block[other];
        bool ascending = ((base + i) & k) == 0;
        if ((a.x > b.x) == ascending)
        {
            block[i] = b;
            block[other] = a;
        }
    }
    GroupMemoryBarrierWithGroupSync();
}

[shader("compute")]
[numthreads(256, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID, uint3 group : SV_GroupID, uint3 local : SV_GroupThreadID)
{
    uint count = push.step.z;
    uint mode = push.step.w;
    uint keys = keysAt();

    if (mode == 0)
    {
        uint i = id.x;
        if (i >= count)
            return;
        uint capacity = uint(push.eyeAndCapacity.w);
        if (i == 0)
            slots[3].y = 1.0;
        float key = asfloat(0x7f800000u);
        if (i < capacity)
        {
            float4 positionAge = slots[Header + i * 2];
            float4 velocityLife = slots[Header + i * 2 + 1];
            float3 away = positionAge.xyz - push.eyeAndCapacity.xyz;
            key = velocityLife.w > 0.0 ? -dot(away, away) : 1e30;
        }
        slots[keys + i] = float4(key, float(i), 0.0, 0.0);
        return;
    }

    if (mode == 1)
    {
        uint i = id.x;
        uint j = push.step.x;
        uint k = push.step.y;
        uint other = i ^ j;
        if (i >= count || other <= i)
            return;
        float4 a = slots[keys + i];
        float4 b = slots[keys + other];
        bool ascending = (i & k) == 0;
        if ((a.x > b.x) == ascending)
        {
            slots[keys + i] = b;
            slots[keys + other] = a;
        }
        return;
    }

    // A block of keys in shared memory, two a thread.
    uint size = min(Block, count);
    uint base = group.x * Block;
    uint t = local.x;
    if (t < size)
        block[t] = slots[keys + base + t].xy;
    if (t + Threads < size)
        block[t + Threads] = slots[keys + base + t + Threads].xy;
    GroupMemoryBarrierWithGroupSync();

    if (mode == 2)
    {
        for (uint k = 2; k <= size; k <<= 1)
            for (uint j = k >> 1; j > 0; j >>= 1)
                orderPair(t, base, j, k, size);
    }
    else
    {
        for (uint j = Block >> 1; j > 0; j >>= 1)
            orderPair(t, base, j, push.step.y, size);
    }

    if (t < size)
        slots[keys + base + t] = float4(block[t], 0.0, 0.0);
    if (t + Threads < size)
        slots[keys + base + t + Threads] = float4(block[t + Threads], 0.0, 0.0);
}
