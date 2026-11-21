// The circular occluders' main pass, from cortao/CORTAOMainPass.h. Around each pixel of the
// window's depth at half its size, CORTAO_SAMPLE_COUNT points of the packed image are read as
// circles facing the way their normals do, and sixteen rays over the pixel's hemisphere are traced
// against them through the occluder table, the share of the rays no circle meets the pixel's
// occlusion and their mean way its bent normal. Built at 16 samples, and at 8 where EIGHT_OCCLUDERS
// is defined.
#define CORTAO_HLSL 1
#define CORTAO_PERSPECTIVE_PROJECTION 1
#define CORTAO_RAY_COUNT 16
#define CORTAO_OCCLUDER_LOOKUP_TABLE 1
#ifdef EIGHT_OCCLUDERS
#define CORTAO_SAMPLE_COUNT 8
#else
#define CORTAO_SAMPLE_COUNT 16
#endif
#include "cortao/CORTAOMainPass.h"
#include "ao_cortao.h"

struct CortaoMainPush
{
    CORTAOMainPassParameters parameters;
    // xy: the depth's scale and offset to its distance along the view, as the prepass takes them.
    float4 depthToLinear;
};

[[vk::push_constant]] ConstantBuffer<CortaoMainPush> push;

// The packed image the prepass wrote, every level, read through the nearest sampler, a place
// outside it read as nothing, a border of zero.
[[vk::binding(0, 0)]] Texture2D<uint> packed;
[[vk::binding(1, 0)]] SamplerState nearest;

// The occluder table for sixteen rays, 24 by 24 ways by 16 sizes of a sixteen-bit entry, two to a
// uint (cortao/occluders_24x24x16x16.bin).
[[vk::binding(2, 0)]] StructuredBuffer<uint> occluders;

[[vk::binding(3, 0)]] Sampler2D depth;

// The bent normal in view space as 0.5 + 0.5 of it, and the share of the light from all around
// that reaches the pixel in alpha.
[[vk::binding(4, 0)]] [format("rgba8")] RWTexture2D<float4> result;

cortao_half3 cortao_loadNormal(cortao_int2 pixelCoords)
{
    return cortaoNormalAt(depth, pixelCoords, cortao_getResolution(push.parameters.packedResolution), push.parameters.unprojectParams,
        push.depthToLinear.x, push.depthToLinear.y);
}

cortao_uint cortao_samplePackedData(cortao_float2 texCoords, cortao_float mipmap)
{
    if (any(texCoords < 0.0) || any(texCoords > 1.0))
        return 0u;
    return packed.SampleLevel(nearest, texCoords, mipmap);
}

// The nearest entry, the edge's where a place lies past it.
cortao_uint cortao_sampleOccluderLookupTable(cortao_float3 texCoords)
{
    uint3 at = uint3(clamp(texCoords * float3(24.0, 24.0, 16.0), float3(0.0), float3(23.0, 23.0, 15.0)));
    uint index = (at.z * 24u + at.y) * 24u + at.x;
    return (occluders[index >> 1u] >> ((index & 1u) * 16u)) & 0xFFFFu;
}

void cortao_writeResult(cortao_int2 pixelCoords, cortao_half occlusion, cortao_half3 bentNormal, cortao_half3x3 tangentSpaceTransform, cortao_uint rayBits)
{
    result[pixelCoords] = float4(bentNormal * 0.5 + 0.5, occlusion);
}

[shader("compute")]
[numthreads(16, 16, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    cortao_mainPass(id.xy, push.parameters);
}
