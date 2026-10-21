// Fills the vertex count of each indirect draw of a frame's meshes whose count a shader writes, a
// thread a draw. Each mesh's count was copied into counts, an entry a mesh, before this runs, and
// each draw's record names its mesh's entry and capacity, so one dispatch fills every draw a chunk
// of records holds however many meshes they draw, and a count past the capacity draws the capacity.

// Each mesh's count as its shader wrote it, an entry a mesh.
[[vk::binding(0, 0)]]
StructuredBuffer<uint> counts;

// The draws' records, eight uints each: the vertex count this fills, the instance count, the first
// vertex and the first instance, which the draw reads as Vulkan's VkDrawIndirectCommand, then the
// mesh's entry in counts and its capacity, which the CPU wrote.
[[vk::binding(1, 0)]]
RWStructuredBuffer<uint> records;

struct CountsPush
{
    uint records;
};

[[vk::push_constant]]
ConstantBuffer<CountsPush> push;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.records)
        return;
    uint at = id.x * 8;
    records[at] = min(counts[records[at + 4]], records[at + 5]);
}
