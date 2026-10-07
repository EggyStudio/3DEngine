// Turns the codes field_splat.slang left in one cascade's cells into distances in world units,
// below zero behind a face, and writes them into the cascade's place in the field the meshes alone
// make and in the field the passes read, a thread a cell.

[[vk::binding(0, 0)]]
StructuredBuffer<uint> cells;

[[vk::binding(1, 0)]] [format("r16f")]
RWTexture3D<float> stillField;

[[vk::binding(2, 0)]] [format("r16f")]
RWTexture3D<float> field;

struct ResolvePush
{
    // x: the cascade. y: the cells along each side. z: the width of a cell. w: the band in cells.
    float4 cascade;
};

[[vk::push_constant]]
ConstantBuffer<ResolvePush> push;

[shader("compute")]
[numthreads(4, 4, 4)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint size = uint(push.cascade.y);
    if (any(id >= size))
        return;
    uint code = cells[(id.z * size + id.y) * size + id.x];
    float d = min(float(code >> 1) / 1024.0, push.cascade.w) * push.cascade.z;
    if ((code & 1u) == 0u)
        d = -d;
    uint3 at = uint3(id.xy, id.z + uint(push.cascade.x) * size);
    stillField[at] = d;
    field[at] = d;
}
