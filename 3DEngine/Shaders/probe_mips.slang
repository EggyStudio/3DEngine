// A level of a reflection probe's gathered cube, each texel the average of the four of the level
// above it, so the filter reads the capture blurred to the solid angle each of its samples covers.
[[vk::binding(0, 0)]] [format("rgba16f")] RWTexture2DArray<float4> above;
[[vk::binding(1, 0)]] [format("rgba16f")] RWTexture2DArray<float4> below;

struct MipPush
{
    uint size;
};

[[vk::push_constant]] ConstantBuffer<MipPush> push;

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.size || id.y >= push.size)
        return;
    uint2 at = id.xy * 2;
    float4 sum = above[uint3(at, id.z)] + above[uint3(at + uint2(1, 0), id.z)]
        + above[uint3(at + uint2(0, 1), id.z)] + above[uint3(at + uint2(1, 1), id.z)];
    below[id] = sum * 0.25;
}
