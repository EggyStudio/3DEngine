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
//
// The first face's thread also adds up the light the probe's rays brought straight from the sun,
// the lights and what gives off light, its own light, and all they brought, as the trace summed
// them a workgroup at a time, and sets the share of the
// light that bounced the probe's next rays take (gi_trace.slang): where its own light is a quarter
// of all it brings or more and fell by a fifth or more against the frame before's, the share it
// kept, channel by channel, so the light that bounced on from light that went out goes with it
// rather than fading over the frames each bounce takes; where its own light is less, a mark that
// the probe is judged by the probes around it (gi_trace.slang).
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

// What each workgroup of a probe's rays brought back straight from the sun, the lights and what
// gives off light, and the luminance of all they brought back, summed, as gi_trace.slang wrote it.
[[vk::binding(4, 0)]]
Texture3D<float4> partial;

// Each probe's own light and the luminance of all its rays brought, the frames of each parity side
// by side, after them the share of the light that bounced each probe's next rays take, a negative
// one where the probe is judged by the probes around it, and after that where the trace moved each
// probe to, every cascade.
[[vk::binding(5, 0)]] [format("rgba16f")]
RWTexture3D<float4> held;

struct AmbientPush
{
    // x: the cascade. y: the probes along each side. z: the octahedron's texels along each side.
    // w: the frame's parity, plus 2 where the cascade lies where it lay the frame before, so the
    // frame before's own light is the same probes'.
    uint4 cascade;
};

[[vk::push_constant]]
ConstantBuffer<AmbientPush> push;

static const float3 Axes[6] = { float3(1, 0, 0), float3(-1, 0, 0), float3(0, 1, 0), float3(0, -1, 0), float3(0, 0, 1), float3(0, 0, -1) };

// Adds up the probe's own light and all its rays brought, over the shares of the sphere its texels
// span, and sets the share of the light that bounced its next rays take.
void follow(uint3 at, uint probes, uint texels, float shares)
{
    uint parity = push.cascade.w & 1u;
    int p = int(probes);
    int z = int(push.cascade.x) * p + int(at.z);
    float4 sum = float4(0.0);
    uint rays = texels * texels;
    for (uint g = 0u; g < max(rays / 64u, 1u); g++)
        sum += partial.Load(int4(int(at.x) * 4 + int(g), int(at.y), z, 0));
    float3 own = sum.rgb / max(shares, 1e-6);
    float all = sum.a / max(shares, 1e-6);

    float4 before = held[int3(int(parity ^ 1u) * p + int(at.x), int(at.y), z)];
    float3 kept = float3(1.0);
    float was = luminance(before.rgb);
    if ((push.cascade.w & 2u) != 0u && before.a >= 0.0)
    {
        if (was > 1e-4 && was >= OwnShare * before.a)
        {
            // Each channel's, a channel the frame before's light held next to none of kept whole.
            kept = select(before.rgb > was * 1e-3, followed(own, before.rgb), float3(1.0));
        }
        else
            kept = float3(-1.0);
    }
    held[int3(int(parity) * p + int(at.x), int(at.y), z)] = float4(own, all);
    held[int3(2 * p + int(at.x), int(at.y), z)] = float4(kept, 0.0);
}

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
    float shares = 0.0;
    float valid = 0.0;
    for (uint v = 0u; v < texels; v++)
        for (uint u = 0u; u < texels; u++)
        {
            float4 light = merged.Load(int4(int2(at.xy * texels + uint2(u, v)), int(at.z), 0));
            float2 uv = (float2(u, v) + 0.5) / float(texels);
            float share = octahedronShare(uv);
            float weight = max(dot(fromOctahedron(uv), axis), 0.0) * share;
            shares += share;
            sum += light.rgb * weight;
            weights += weight;
            // A probe inside a mesh holds alpha 0 in every texel, where every other's are a half or 1.
            valid = max(valid, light.a > 0.25 ? 1.0 : 0.0);
        }
    // Its alpha 0 where the probe holds no light, and two more than its move along the face's axis
    // where it does (faceHolds and faceMove in gi.slang).
    float4 place = held[int3(3 * int(probes) + int(at.x), int(at.y), int(push.cascade.x * probes + at.z))];
    float holds = valid > 0.5 && place.w > 0.5 ? 1.0 : 0.0;
    cubes[uint3(face * probes + at.x, at.y, push.cascade.x * probes + at.z)] = float4(sum * (Pi / max(weights, 1e-6)), holds * (2.0 + place[face / 2u]));
    if (face == 0u)
        follow(at, probes, texels, shares);

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
