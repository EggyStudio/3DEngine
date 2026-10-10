// Traces the rays of one cascade of light probes through the scene's distance field, a thread a
// ray. A cascade's probes lie every eight cells of the field's cascade of the same number, and each
// sends a ray along each texel of an octahedron of directions across its interval, from as far as
// the probes are apart to four times that, so the next cascade, twice as coarse, carries on from
// half way along it, and a surface nearer a probe of the next cascade than its interval begins, which
// that probe meets early, is met by this probe's own ray. A ray that meets a surface brings back the light the surface sends along it and
// stops there. One that meets nothing brings back nothing and lets what lies beyond through, but
// in the last cascade, where it brings back the light from all around. A probe inside a mesh sends
// nothing, which the merge reads as a probe to pass over.
//
// A ray is marched from its probe, since one marched from where its interval begins started under
// a floor its probe stood a little above and brought back the light under it. One that meets a
// surface before its interval begins brings back that surface's light, marked apart, alpha a half,
// which the probe's own faces take and the merge of the cascade below reads as dark, as it did
// when such a ray was blocked dark here too. Blocked, this cascade's faces lacked every surface
// nearer its probes than the interval's start, which a Cornell box probe of the second cascade
// read 63% short of a path-traced reference, and every surface past the first cascade's reach
// that read those faces for the light bounced to it.
//
// A probe takes the light that bounced to the surfaces its rays meet at the share the gather left
// it, less than the whole the frame after the light its rays bring straight from the sun, the
// lights and what gives off light fell by a fifth or more, so the light that bounced on from light
// that went out goes with it rather than over the frames each bounce takes to fade.
import gi;
import scenefield;

[[vk::binding(0, 0)]]
Sampler3D sceneField;

[[vk::binding(1, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

[[vk::binding(2, 0)]]
Sampler3D fieldAlbedo;

[[vk::binding(3, 0)]]
Sampler3D fieldGlow;

[[vk::binding(4, 0)]]
ConstantBuffer<GiLights> lights;

// What each ray brought back: rgb the light, a 1 where it met nothing and lets what lies beyond
// through, a half where it met a surface before its interval began, 0 where it met one inside it.
// A texel a direction, each probe a tile of the octahedron's texels.
[[vk::binding(5, 0)]] [format("rgba16f")]
RWTexture3D<float4> radiance;

// The light that bounced to each probe the frame before, as gi_ambient.slang left it, for the
// light a surface sends on from it.
[[vk::binding(6, 0)]]
Sampler3D bounced;

// The light from all around, for a ray of the last cascade that meets nothing, or a black cube.
[[vk::binding(7, 0)]]
SamplerCube environment;

// How far each ray went before it met a surface, or its interval's end where it met none, which
// gi_ambient.slang lays out as each probe's reach.
[[vk::binding(8, 0)]] [format("r16f")]
RWTexture3D<float> distances;

// Each probe's reach the frame before, as gi_ambient.slang left it, which weighs the light a surface
// a ray meets takes from the probes around it.
[[vk::binding(9, 0)]]
Sampler3D reach;

// Each probe's own light as gi_ambient.slang left it, the frame before's and the one before that
// side by side, and the share of the light that bounced each probe takes, every cascade.
[[vk::binding(10, 0)]] [format("rgba16f")]
RWTexture3D<float4> held;

// Every probe's own light summed, in 1024ths, the frame before's and the one before that, by the
// frame's parity, as gi_ambient.slang adds it.
[[vk::binding(11, 0)]]
RWStructuredBuffer<uint> summed;

// What each workgroup of a probe's rays, 64 of them, brought back straight from the sun, the lights
// and what gives off light, summed, with in a the luminance of all they brought back, each ray
// weighed by the share of the sphere its texel spans, four workgroups a probe at most, which
// gi_ambient.slang sums a probe at a time.
[[vk::binding(12, 0)]] [format("rgba32f")]
RWTexture3D<float4> partial;

struct TracePush
{
    // x: the cascade. y: the probes along each side. z: the octahedron's texels along each side.
    // w: 1 for the last cascade.
    uint4 cascade;
    // x: where the interval starts. y: where it ends. z: the probes' spacing in cells. w: how many cascades.
    float4 interval;
    // x: 1 where each probe's share holds for the probe, its cascade lying where it lay the frame
    // before. y: 1 where the sums of the two frames before cover the same probes, no cascade
    // having moved between them. z: the frame's parity.
    uint4 follow;
};

[[vk::push_constant]]
ConstantBuffer<TracePush> push;

// The share of the light that bounced the probe at `at` of cascade c takes: the one the gather
// left it, or for a probe its own light lights too little of to judge by, the share every probe's
// own light summed kept of the frame before's where that fell by a fifth or more.
float3 kept(uint3 at, int c, int probes)
{
    if (push.follow.x == 0u)
        return float3(1.0);
    float4 share = held[int3(2 * probes + int(at.x), int(at.y), c * probes + int(at.z))];
    if (share.r >= 0.0)
        return share.rgb;
    uint parity = push.follow.z & 1u;
    float now = float(summed[parity ^ 1u]), before = float(summed[parity]);
    return push.follow.y != 0u && before > 0.0 ? followed(float3(now), float3(before)) : float3(1.0);
}

groupshared float4 brought[64];

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID, uint lane : SV_GroupIndex)
{
    uint probes = push.cascade.y;
    uint texels = push.cascade.z;
    uint rays = texels * texels;
    uint probe = id.x / rays;
    // A probe's rays are 64 or 256, so every workgroup is one probe's, and one past the last
    // probes leaves whole, before the sum below every ray of a group reaches.
    if (probe >= probes * probes * probes)
        return;
    uint ray = id.x % rays;
    uint3 at = uint3(probe % probes, (probe / probes) % probes, probe / (probes * probes));
    uint2 texel = uint2(ray % texels, ray / texels);
    uint3 written = uint3(at.xy * texels + texel, at.z);

    float4 oc = fieldInfo.cascades[push.cascade.x].originAndCell;
    float3 origin = oc.xyz + (float3(at) + 0.5) * (oc.w * push.interval.z);
    float4 light = float4(0.0, 0.0, 0.0, 1.0);
    float3 own = float3(0.0);
    float reached = 0.0;
    if (oc.w <= 0.0)
    {
    }
    else if (fieldDistance(sceneField, fieldInfo, origin) < 0.0)
        light = float4(0.0);
    else
    {
        float3 way = fromOctahedron((float2(texel) + 0.5) / float(texels));
        float t = traceField(sceneField, fieldInfo, origin, way, 0.0, push.interval.y);
        reached = t >= 0.0 ? t : push.interval.y;
        if (t >= 0.0)
            light = float4(shadeProbeHit(sceneField, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, reach, origin + way * t,
                float(probes), push.interval.z, push.interval.w, true, kept(at, int(push.cascade.x), int(probes)), own), t < push.interval.x ? 0.5 : 0.0);
        else if (push.cascade.w != 0u)
        {
            float3 sky = lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb;
            light = float4(sky, 0.0);
            own = sky;
        }
    }
    radiance[written] = light;
    distances[written] = reached;

    // The group's own light and all it brought back summed, each ray weighed by the share of the
    // sphere its texel spans, as the gather weighs them, halving the rays that add each step.
    brought[lane] = float4(own, luminance(light.rgb)) * octahedronShare((float2(texel) + 0.5) / float(texels));
    GroupMemoryBarrierWithGroupSync();
    for (uint half = 32u; half > 0u; half >>= 1)
    {
        if (lane < half)
            brought[lane] += brought[lane + half];
        GroupMemoryBarrierWithGroupSync();
    }
    if (lane == 0u)
        partial[int3(int(at.x) * 4 + int(ray / 64u), int(at.y), int(push.cascade.x) * int(probes) + int(at.z))] = brought[0];
}
