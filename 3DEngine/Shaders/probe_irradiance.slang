// A reflection probe's capture projected onto the nine spherical harmonics, each texel of a level
// of its gathered cube weighted by the solid angle it covers, then each band scaled by its share
// of a cosine lobe over pi (1, 2/3 and 1/4), so the sum in a direction is the light a white
// diffuse surface facing it returns, as EnvironmentMap.Irradiance holds an environment's. One
// group of 64 threads, each summing its share of the texels, then the group's sums added.
import probecube;

[[vk::binding(0, 0)]] SamplerCube source;
[[vk::binding(1, 0)]] RWStructuredBuffer<float4> irradiance;

// The face width of the level read, and the level.
struct IrradiancePush
{
    uint size;
    float level;
};

[[vk::push_constant]] ConstantBuffer<IrradiancePush> push;

static const uint Threads = 64;
groupshared float3 partial[Threads * 9];

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_GroupThreadID)
{
    float3 sums[9];
    for (uint c = 0; c < 9; c++)
        sums[c] = float3(0.0);

    uint perFace = push.size * push.size;
    for (uint k = id.x; k < 6 * perFace; k += Threads)
    {
        uint face = k / perFace;
        uint2 texel = uint2(k % push.size, (k % perFace) / push.size);
        float2 uv = faceCoordinates(texel, push.size);
        float3 d = cubeDirection(face, uv.x, uv.y);
        float3 light = source.SampleLevel(d, push.level).rgb * texelSolidAngle(uv, push.size);
        float y[9];
        harmonics(d, y);
        for (uint c = 0; c < 9; c++)
            sums[c] += light * y[c];
    }
    for (uint c = 0; c < 9; c++)
        partial[id.x * 9 + c] = sums[c];
    GroupMemoryBarrierWithGroupSync();

    if (id.x < 9)
    {
        float3 total = float3(0.0);
        for (uint t = 0; t < Threads; t++)
            total += partial[t * 9 + id.x];
        float band = id.x == 0 ? 1.0 : id.x < 4 ? 2.0 / 3.0 : 0.25;
        irradiance[id.x] = float4(total * band, 0.0);
    }
}
