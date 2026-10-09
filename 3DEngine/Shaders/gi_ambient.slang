// Gathers the light each probe of a cascade receives into the six faces of a cube, a thread a
// probe's face. Each face holds the light arriving at a surface facing along its axis, every merged
// direction weighed by how squarely it falls on that surface and by the share of the sphere its
// texel spans, which the model pass and the next frame's rays read, blended between probes. The
// weights are scaled to sum to pi, the light reaching a face under a sky of 1, so a uniform sky
// reads alike on every face at any number of texels, where an equal share for every texel read a
// face along z at 0.82 of it with 4 texels a side and 0.93 with 8.
import gi;

[[vk::binding(0, 0)]]
Texture3D<float4> merged;

// Every cascade's probes, each face a block of the probes along x, the cascades one after another along z.
[[vk::binding(1, 0)]] [format("rgba16f")]
RWTexture3D<float4> cubes;

struct AmbientPush
{
    // x: the cascade. y: the probes along each side. z: the octahedron's texels along each side. w: unused.
    uint4 cascade;
};

[[vk::push_constant]]
ConstantBuffer<AmbientPush> push;

static const float3 Axes[6] = { float3(1, 0, 0), float3(-1, 0, 0), float3(0, 1, 0), float3(0, -1, 0), float3(0, 0, 1), float3(0, 0, -1) };

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint probes = push.cascade.y;
    uint texels = push.cascade.z;
    uint face = id.x % 6u;
    uint probe = id.x / 6u;
    if (probe >= probes * probes * probes)
        return;
    uint3 at = uint3(probe % probes, (probe / probes) % probes, probe / (probes * probes));
    float3 axis = Axes[face];
    float3 sum = float3(0.0);
    float weights = 0.0;
    float valid = 0.0;
    for (uint v = 0u; v < texels; v++)
        for (uint u = 0u; u < texels; u++)
        {
            float4 light = merged.Load(int4(int2(at.xy * texels + uint2(u, v)), int(at.z), 0));
            float2 uv = (float2(u, v) + 0.5) / float(texels);
            float weight = max(dot(fromOctahedron(uv), axis), 0.0) * octahedronShare(uv);
            sum += light.rgb * weight;
            weights += weight;
            valid = max(valid, light.a);
        }
    cubes[uint3(face * probes + at.x, at.y, push.cascade.x * probes + at.z)] = float4(sum * (Pi / max(weights, 1e-6)), valid);
}
