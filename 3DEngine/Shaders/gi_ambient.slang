// Gathers the light each probe of a cascade receives into the six faces of a cube, a thread a
// probe's face. Each face holds the light arriving at a surface facing along its axis, every merged
// direction weighed by how squarely it falls on that surface and by the share of the sphere its
// texel spans, which the model pass and the next frame's rays read, blended between probes. The
// weights are scaled to sum to pi, the light reaching a face under a sky of 1, so a uniform sky
// reads alike on every face at any number of texels, where an equal share for every texel read a
// face along z at 0.82 of it with 4 texels a side and 0.93 with 8. Each probe's reach is laid out
// beside, how far its rays went along each of eight by eight directions before a surface stopped
// them, from this cascade's rays, each face's thread a sixth of the directions, which bouncedAt
// weighs a probe by.
import gi;

[[vk::binding(0, 0)]]
Texture3D<float4> merged;

// Every cascade's probes, each face a block of the probes along x, the cascades one after another along z.
[[vk::binding(1, 0)]] [format("rgba16f")]
RWTexture3D<float4> cubes;

// How far this cascade's rays went, as gi_trace.slang wrote them, a texel a ray.
[[vk::binding(2, 0)]]
Texture3D<float> distances;

// Every cascade's probes' reach, eight by eight directions a probe, the cascades one after another along z.
[[vk::binding(3, 0)]] [format("r16f")]
RWTexture3D<float> reach;

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
            // A probe inside a mesh holds alpha 0 in every texel, where every other's are a half or 1.
            valid = max(valid, light.a > 0.25 ? 1.0 : 0.0);
        }
    cubes[uint3(face * probes + at.x, at.y, push.cascade.x * probes + at.z)] = float4(sum * (Pi / max(weights, 1e-6)), valid);

    // This probe's reach, the nearest ray's distance along each direction where the cascade has as
    // many directions or fewer, and the mean of the rays inside it where it has more.
    uint step = max(texels / uint(ReachTexels), 1u);
    for (uint k = face; k < uint(ReachTexels * ReachTexels); k += 6u)
    {
        uint2 texel = uint2(k % uint(ReachTexels), k / uint(ReachTexels));
        uint2 first = texels >= uint(ReachTexels) ? texel * step : uint2((float2(texel) + 0.5) / float(ReachTexels) * float(texels));
        float reached = 0.0;
        for (uint v = 0u; v < step; v++)
            for (uint u = 0u; u < step; u++)
                reached += distances.Load(int4(int2(at.xy * texels + first + uint2(u, v)), int(at.z), 0));
        reach[uint3(at.xy * uint(ReachTexels) + texel, push.cascade.x * probes + at.z)] = reached / float(step * step);
    }
}
