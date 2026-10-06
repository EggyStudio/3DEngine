// A level of a reflection probe's cube, its capture as a surface of the level's roughness
// reflects it, as EnvironmentMap prefilters an environment: the first level a mirror's, read
// blurred to the solid angle its texel covers, and each level after by GGX importance sampling
// on a Hammersley set around the direction it is looked up by, each sample read from the level
// of the gathered cube its own solid angle blurs over, as Brian Karis's split sum takes it.
import probecube;

[[vk::binding(0, 0)]] SamplerCube source;
[[vk::binding(1, 0)]] [format("rgba16f")] RWTexture2DArray<float4> target;

// The level's face width and roughness, the solid angle a texel of the gathered cube's first
// level covers, and the level of it a mirror's texel reads.
struct PrefilterPush
{
    uint size;
    float roughness;
    float sourceTexel;
    float mirrorLevel;
};

[[vk::push_constant]] ConstantBuffer<PrefilterPush> push;

static const uint Samples = 64;

[shader("compute")]
[numthreads(8, 8, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= push.size || id.y >= push.size)
        return;
    float2 uv = faceCoordinates(id.xy, push.size);
    float3 n = cubeDirection(id.z, uv.x, uv.y);
    if (push.roughness <= 0.0)
    {
        target[id] = float4(source.SampleLevel(n, push.mirrorLevel).rgb, 1.0);
        return;
    }

    float3 up = abs(n.z) < 0.999 ? float3(0.0, 0.0, 1.0) : float3(1.0, 0.0, 0.0);
    float3 tangent = normalize(cross(up, n));
    float3 bitangent = cross(n, tangent);
    float a = max(push.roughness * push.roughness, 1e-3);
    float a2 = a * a;

    float3 sum = float3(0.0);
    float weight = 0.0;
    for (uint i = 0; i < Samples; i++)
    {
        float x = float(i) / float(Samples);
        float y = float(reversebits(i)) * 2.3283064365386963e-10;
        float phi = 2.0 * 3.14159265 * x;
        float cosTheta = sqrt((1.0 - y) / (1.0 + (a2 - 1.0) * y));
        float sinTheta = sqrt(1.0 - cosTheta * cosTheta);
        float3 h = float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
        float3 l = 2.0 * h.z * h - float3(0.0, 0.0, 1.0);
        if (l.z <= 0.0)
            continue;

        // With the eye along the normal the sample's pdf is D / 4.
        float d = a2 / (3.14159265 * pow(h.z * h.z * (a2 - 1.0) + 1.0, 2.0));
        float solidAngle = 1.0 / (float(Samples) * d / 4.0 + 1e-4);
        float level = max(0.0, 0.5 * log2(solidAngle / push.sourceTexel) + 1.0);
        float3 direction = tangent * l.x + bitangent * l.y + n * l.z;
        sum += source.SampleLevel(direction, level).rgb * l.z;
        weight += l.z;
    }
    target[id] = float4(weight > 0.0 ? sum / weight : source.SampleLevel(n, 0.0).rgb, 1.0);
}
