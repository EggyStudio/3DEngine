// The circular occluders' filter pass, from cortao/CORTAOFilterPass.h. Each thread's block of two
// by two pixels of the main pass's occlusion is blended with its neighbors three and six pixels
// away, each weighed by how near its depth and its packed normal are to the pixel's, in the larger
// of its two patterns, each pair of opposite taps weighed alike, since the occlusion's history
// follows.
#define CORTAO_HLSL 1
#define CORTAO_PERSPECTIVE_PROJECTION 1
#define CORTAO_LARGE_FILTER 1
#define CORTAO_FILTER_SYMMETRY 1
#include "cortao/CORTAOFilterPass.h"

[[vk::push_constant]] ConstantBuffer<CORTAOFilterPassParameters> push;

[[vk::binding(0, 0)]] Texture2D<uint> packed;

// The main pass's result, the occlusion in alpha.
[[vk::binding(1, 0)]] Texture2D<float4> occlusion;

[[vk::binding(2, 0)]] SamplerState nearest;

// The filtered occlusion, in every channel.
[[vk::binding(3, 0)]] [format("rgba8")] RWTexture2D<float4> filtered;

// The four texels about a corner of texels moved by offset, gathered at the moved corner rather
// than with the offset itself, which a device needs another feature for when the offset is not a
// constant of the instruction.
float2 movedCorner(cortao_float2 texCoords, cortao_int2 offset)
{
    return texCoords + float2(offset) / float2(cortao_getResolution(push.packedResolution));
}

cortao_uint4 cortao_gatherPackedData(cortao_float2 texCoords, cortao_int2 offset)
{
    return packed.Gather(nearest, movedCorner(texCoords, offset));
}

cortao_half4 cortao_gatherSSAO(cortao_float2 texCoords, cortao_int2 offset)
{
    return occlusion.GatherAlpha(nearest, movedCorner(texCoords, offset));
}

void cortao_writeFilterOutput(cortao_int2 pixelCoords, cortao_half ssao)
{
    filtered[pixelCoords] = float4(ssao, ssao, ssao, ssao);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    cortao_filterPass(id.xy, push);
}
