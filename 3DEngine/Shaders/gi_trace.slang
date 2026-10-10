// Traces the rays of one cascade of light probes through the scene's distance field, a thread a
// ray. A cascade's probes lie every eight cells of the field's cascade of the same number, and each
// sends a ray along each texel of an octahedron of directions across its interval, from as far as
// the probes are apart to twice that, so the next cascade, twice as coarse, carries on where this
// one stops. A ray that meets a surface brings back the light the surface sends along it and
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

struct TracePush
{
    // x: the cascade. y: the probes along each side. z: the octahedron's texels along each side.
    // w: 1 for the last cascade.
    uint4 cascade;
    // x: where the interval starts. y: where it ends. z: the probes' spacing in cells. w: how many cascades.
    float4 interval;
};

[[vk::push_constant]]
ConstantBuffer<TracePush> push;

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint probes = push.cascade.y;
    uint texels = push.cascade.z;
    uint rays = texels * texels;
    uint probe = id.x / rays;
    if (probe >= probes * probes * probes)
        return;
    uint ray = id.x % rays;
    uint3 at = uint3(probe % probes, (probe / probes) % probes, probe / (probes * probes));
    uint2 texel = uint2(ray % texels, ray / texels);
    uint3 written = uint3(at.xy * texels + texel, at.z);

    float4 oc = fieldInfo.cascades[push.cascade.x].originAndCell;
    if (oc.w <= 0.0)
    {
        radiance[written] = float4(0.0, 0.0, 0.0, 1.0);
        distances[written] = 0.0;
        return;
    }
    float3 origin = oc.xyz + (float3(at) + 0.5) * (oc.w * push.interval.z);
    if (fieldDistance(sceneField, fieldInfo, origin) < 0.0)
    {
        radiance[written] = float4(0.0);
        distances[written] = 0.0;
        return;
    }

    float3 way = fromOctahedron((float2(texel) + 0.5) / float(texels));
    float t = traceField(sceneField, fieldInfo, origin, way, 0.0, push.interval.y);
    distances[written] = t >= 0.0 ? t : push.interval.y;
    if (t >= 0.0)
    {
        radiance[written] = float4(shadeProbeHit(sceneField, fieldInfo, fieldAlbedo, fieldGlow, lights, bounced, reach, origin + way * t,
            float(probes), push.interval.z, push.interval.w, true), t < push.interval.x ? 0.5 : 0.0);
        return;
    }
    if (push.cascade.w != 0u)
    {
        float3 sky = lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb;
        radiance[written] = float4(sky, 0.0);
        return;
    }
    radiance[written] = float4(0.0, 0.0, 0.0, 1.0);
}
