// Merges one cascade of light probes with the merged cascade above it, a thread a probe's
// direction, from the last cascade down to the first. What a probe's ray brought back is kept, and
// where the ray met nothing the light from beyond is added, the cascade above's probes around this
// one blended by how near each is, each one's texels of the directions inside this texel's
// averaged, so the first cascade holds the light from every distance. A probe inside a mesh, or
// one a surface hides from this probe, as a probe above a ceiling is hidden from one under it, is
// passed over in the blend, and a probe inside a mesh holds nothing itself, its alpha 0 where every
// other's is 1.
import gi;
import scenefield;

[[vk::binding(0, 0)]]
Texture3D<float4> radiance;

// The cascade above, merged, or this cascade's own radiance for the last.
[[vk::binding(1, 0)]]
Texture3D<float4> above;

[[vk::binding(2, 0)]] [format("rgba16f")]
RWTexture3D<float4> merged;

[[vk::binding(3, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

[[vk::binding(4, 0)]]
Sampler3D sceneField;

struct MergePush
{
    // x: the cascade. y: the probes along each side. z: this cascade's octahedron's texels along
    // each side. w: the cascade above's, or 0 for the last cascade.
    uint4 cascade;
    // x: the probes' spacing in cells.
    float4 spacing;
};

[[vk::push_constant]]
ConstantBuffer<MergePush> push;

// Whether each probe of the group sees each of the eight probes of the cascade above around it, a
// probe's eight together, for every thread of the probe to read. A group holds four probes at most,
// as a probe has sixteen threads at fewest.
groupshared uint seen[32];

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID, uint3 local : SV_GroupThreadID)
{
    uint probes = push.cascade.y;
    uint texels = push.cascade.z;
    uint rays = texels * texels;
    uint probe = id.x / rays;
    uint ray = id.x % rays;
    uint3 at = uint3(probe % probes, (probe / probes) % probes, probe / (probes * probes));
    uint2 texel = uint2(ray % texels, ray / texels);
    uint3 written = uint3(at.xy * texels + texel, at.z);

    uint c = push.cascade.x;
    uint upper = push.cascade.w;
    float4 oc = fieldInfo.cascades[c].originAndCell;
    float3 origin = oc.xyz + (float3(at) + 0.5) * (oc.w * push.spacing.x);
    float4 ac = fieldInfo.cascades[min(c + 1u, 7u)].originAndCell;
    float3 q = (origin - ac.xyz) / (ac.w * push.spacing.x) - 0.5;
    int3 base = int3(floor(q));

    // The first eight threads of each probe in the group trace whether it sees each of the probes
    // above around it, every thread of the group reaching the barrier before any returns.
    uint inGroup = min(rays, 64u);
    uint slot = local.x / inGroup;
    uint traced = local.x % inGroup;
    if (traced < 8u)
    {
        int3 offset = int3(traced & 1u, (traced >> 1) & 1u, traced >> 2);
        int3 neighbor = clamp(base + offset, int3(0), int3(int(probes) - 1));
        float3 upperProbe = ac.xyz + (float3(neighbor) + 0.5) * (ac.w * push.spacing.x);
        seen[slot * 8u + traced] = upper > 0u && probe < probes * probes * probes && oc.w > 0.0
            && seesThrough(sceneField, fieldInfo, origin, upperProbe, oc.w * 0.5) ? 1u : 0u;
    }
    GroupMemoryBarrierWithGroupSync();

    if (probe >= probes * probes * probes)
        return;
    if (oc.w <= 0.0 || fieldDistance(sceneField, fieldInfo, origin) < 0.0)
    {
        merged[written] = float4(0.0);
        return;
    }
    float4 own = radiance[written];
    if (upper == 0u || own.a < 0.5)
    {
        merged[written] = float4(own.rgb, 1.0);
        return;
    }

    // The cascade above's probes around this one, eight of them weighed by how near each is, and
    // passed over where it is inside a mesh or a surface hides it from this one.
    float3 f = q - float3(base);
    uint factor = upper / texels;
    float3 sum = float3(0.0);
    float weights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 neighbor = clamp(base + offset, int3(0), int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float weight = lerps.x * lerps.y * lerps.z * float(seen[slot * 8u + uint(corner)]);
        // The texels of the cascade above that lie inside this one's, one or four of them.
        float3 light = float3(0.0);
        float valid = 0.0;
        for (uint v = 0u; v < factor; v++)
            for (uint u = 0u; u < factor; u++)
            {
                float4 child = above.Load(int4(int2(uint2(neighbor.xy) * upper + texel * factor + uint2(u, v)), neighbor.z, 0));
                light += child.rgb;
                valid += child.a;
            }
        valid /= float(factor * factor);
        weight *= valid;
        sum += light / float(factor * factor) * weight;
        weights += weight;
    }
    float3 beyond = weights > 1e-4 ? sum / weights : float3(0.0);
    merged[written] = float4(own.rgb + beyond, 1.0);
}
