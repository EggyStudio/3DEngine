// CORTAO's prepass (cortao/CORTAOPrepass.h, Isheden's circular occluders): each pixel of the
// window's depth at half its size, its distance along the view and its normal in view space packed
// into one uint, written into the first level of the packed image, and into each level after it
// where the pixel is the one of its block of four that level keeps.
#define CORTAO_HLSL 1
#define CORTAO_PERSPECTIVE_PROJECTION 1
#include "cortao/CORTAOPrepass.h"
#include "ao_cortao.h"

struct CortaoPrepassPush
{
    CORTAOPrepassParameters parameters;
    // CORTAO's unprojection of a pixel's place on the depth to a way in view space, as its main
    // pass takes it, which the normal is found through.
    float4 unproject;
};

[[vk::push_constant]] ConstantBuffer<CortaoPrepassPush> push;

[[vk::binding(0, 0)]] Sampler2D depth;

// Each level of the packed image, eight at most, those past its own the first one again.
[[vk::binding(1, 0)]] [format("r32ui")] RWTexture2D<uint> levels[8];

cortao_float cortao_loadDepth(cortao_int2 pixelCoords)
{
    return depth.Load(int3(pixelCoords, 0)).r;
}

cortao_float3 cortao_loadNormal(cortao_int2 pixelCoords)
{
    return cortaoNormalAt(depth, pixelCoords, cortao_getResolution(push.parameters.packedResolution), push.unproject,
        push.parameters.depthScale, push.parameters.depthOffset);
}

void cortao_writePackedData(cortao_int2 pixelCoords, cortao_int mipLevel, cortao_uint data)
{
    levels[mipLevel][pixelCoords] = data;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    cortao_prepass(id.xy, push.parameters);
}
