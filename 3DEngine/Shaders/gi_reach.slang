// Traces each probe's reach, how far its ray along each of sixteen by sixteen ways goes from the
// place the probe stands at before it meets a surface, up to its cascade's end, by the module's
// reach pass, a thread a reach texel. It runs after a cascade's rays are traced, which move a probe
// out of a surface it stood in, and before the merge, which reads this frame's reach, as the gather
// and the model pass weigh a probe by it (bouncedAt).

import gi;
import scenefield;
import rc_probe_passes;

[[vk::binding(0, 0)]]
Sampler3D sceneField;

[[vk::binding(1, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

[[vk::binding(2, 0)]]
Texture3D<float4> fieldSecond;

// Each probe's place as the trace moved it, at three times the probes along x past the slab's start
// (placed in gi_trace.slang), xyz its move in spacings.
[[vk::binding(3, 0)]] [format("rgba16f")]
RWTexture3D<float4> held;

// Every cascade's probes' reach, sixteen by sixteen ways a probe, the cascades one after another
// along z.
[[vk::binding(4, 0)]] [format("r16f")]
RWTexture3D<float> reach;

struct ReachPush
{
    // x: the cascade. y: the probes along each side. z: how many cascades. w: unused.
    uint4 cascade;
    // x: where the cascade's interval ends. y: the probes' spacing in cells. zw: unused.
    float4 interval;
};

[[vk::push_constant]]
ConstantBuffer<ReachPush> push;

// The field as the module's reach reads it (IFieldVolume), from the bound table of cascades.
struct ReachField : IFieldVolume
{
    int cascadeOf(float3 p, float margin) { return fieldCascade(fieldInfo, p, margin); }
    float4 cornerAndCell(int cascade) { return fieldInfo.cascades[cascade].originAndCell; }
    int size() { return int(fieldInfo.counts.y); }

    float4 between(int cascade, float3 cells)
    {
        float size = fieldInfo.counts.y;
        return sceneField.sampleAt(float3(cells.xy / size, (cells.z + float(cascade) * size) / (size * fieldInfo.counts.x)), 0);
    }

    float4 cellAt(int cascade, int3 cell) { return sceneField.loadAt(int3(cell.xy, cell.z + cascade * int(fieldInfo.counts.y))); }

    float3 secondAt(int cascade, int3 cell)
    {
        return SheetVolume(sceneField, fieldSecond).secondAt(int3(cell.xy, cell.z + cascade * int(fieldInfo.counts.y)));
    }
}

// The places the trace moved the cascade's probes to, each its spot on the grid and its move.
struct HeldPlaces : IProbePlaces
{
    float3 corner;
    float apart;
    int probes;
    int cascade;

    float3 placeOf(int3 probe)
    {
        float3 moved = held[int3(3 * probes + probe.x, probe.y, cascade * probes + probe.z)].xyz;
        return corner + (float3(probe) + 0.5 + moved) * apart;
    }
}

// The most a texel of the reach holds, a half float's largest, where the module's reach met nothing
// (ReachOpen), which every reader takes as seeing past any point.
static const float ReachKeptOpen = 65504.0;

// The reach as the gather and the model pass read it, each probe's sixteen by sixteen ways in a
// block, the cascade's probes one after another along z.
struct ReachKept : IReachTexels
{
    uint probes;
    uint cascade;

    void keepReach(uint index, float reached)
    {
        uint texels = ReachTexels * ReachTexels;
        uint probe = index / texels;
        uint2 texel = uint2((index % texels) % ReachTexels, (index % texels) / ReachTexels);
        uint3 at = uint3(probe % probes, (probe / probes) % probes, probe / (probes * probes));
        reach[uint3(at.xy * ReachTexels + texel, cascade * probes + at.z)] = min(reached, ReachKeptOpen);
    }
}

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    uint probes = push.cascade.y;
    float4 oc = cascadeAt(fieldInfo, push.cascade.x, int(push.cascade.z));
    // A cascade the field does not hold yet keeps the reach it has.
    if (oc.w <= 0.0)
        return;
    float apart = oc.w * push.interval.y;
    ProbeCascade cascade;
    cascade.corner = oc.xyz + 0.5 * apart;
    cascade.spacing = apart;
    cascade.count = int3(int(probes));
    cascade.texels = ReachTexels;
    cascade.start = 0.0;
    cascade.end = push.interval.x;
    cascade.stepCap = 0u;
    HeldPlaces places;
    places.corner = oc.xyz;
    places.apart = apart;
    places.probes = int(probes);
    places.cascade = int(push.cascade.x);
    ReachKept kept;
    kept.probes = probes;
    kept.cascade = push.cascade.x;
    ReachField field;
    reachTexel(field, kept, places, cascade, id.x);
}
