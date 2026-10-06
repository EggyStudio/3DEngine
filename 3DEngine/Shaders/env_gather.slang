// An environment's equirectangular image resampled into a cube of linear light, the first level of
// the cube the filter of a reflection probe's capture prefilters, or the sky's cube. Each texel
// averages four samples spread over it, each read from the image's mip whose rows are as far apart
// as the samples, so every direction weighs what its texel covers. Filtering the image's own mips
// instead would weigh a row by its length in the image, which near a pole is many times what it
// covers.
import probecube;

[[vk::binding(0, 0)]] Sampler2D image;
[[vk::binding(1, 0)]] [format("rgba16f")] RWTexture2DArray<float4> cube;

// The face width of the level written, and the image's mip its samples read.
struct GatherPush
{
    uint size;
    float level;
};

[[vk::push_constant]] ConstantBuffer<GatherPush> push;

static const float Pi = 3.14159265;

// Where a direction falls in the image: longitude across from the back, latitude down from the
// top, as EnvironmentMap decodes it.
float2 equirectangular(float3 d)
{
    return float2(0.5 + atan2(d.x, -d.z) / (2.0 * Pi), acos(clamp(d.y, -1.0, 1.0)) / Pi);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.size || id.y >= push.size)
        return;
    float3 sum = float3(0.0);
    for (uint s = 0; s < 4; s++)
    {
        float2 at = (float2(id.xy) + float2(0.25 + 0.5 * float(s & 1), 0.25 + 0.5 * float(s >> 1))) / float(push.size) * 2.0 - 1.0;
        sum += image.SampleLevel(equirectangular(cubeDirection(id.z, at.x, at.y)), push.level).rgb;
    }
    cube[id] = float4(min(sum * 0.25, float3(65504.0)), 1.0);
}
