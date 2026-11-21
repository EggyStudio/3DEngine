// Merges one cascade of light probes with the merged cascade above it by the RadianceCascades
// module's merge, a thread a texel at the side its rays were traced at, from the last cascade down
// to the first. The last cascade's texels keep their rays' light with the sky past what met nothing
// (settleTexel). A finer cascade's texel keeps what its ray met, and where it met nothing by its
// end it is handed to the probes of the cascade above about where it ended, each weighed by how
// near it stands and passed over where it stands in a mesh or does not see that point by its reach,
// after the ray marches on to open space where it ended beside a surface; where no probe above
// sees the point, the ray marches on itself to the last counted cascade's end (mergeTexel). So the
// first cascade holds the light from every distance. A cascade of 8 texels a side is traced and
// merged at 16, its four rays a texel, and each texel of 8 takes the mean of its four, which its
// readers read. A probe inside a mesh, or walled in, holds nothing, its alpha 0 where every other's
// is 1.
//
// Each texel's share of the sky is merged alongside its light the same way, every surface lit by
// none and the sky by 1, which the probes' faces and the cascade below read.
import gi;
import gi_hits;
import probelight;
import scenefield;
import rc_probe_passes;

// Each texel's ray as the trace ended it, its light and the share it carries, and where and how
// it ended (gi_trace.slang).
[[vk::binding(5, 0)]]
Texture3D<float4> probeRayLight;

[[vk::binding(8, 0)]]
Texture3D<float2> probeRayEnd;

// Where the trace moved each probe to this frame, after each probe's own light and its share, its
// move in its spacing with w 1 where it stands outside every mesh.
[[vk::binding(9, 0)]] [format("rgba16f")]
RWTexture3D<float4> held;

// The cascade above, merged at the side it was traced at, and its share of the sky, or this
// cascade's own images standing in for the last.
[[vk::binding(10, 0)]]
Texture3D<float4> above;

[[vk::binding(14, 0)]]
Texture3D<float> aboveSky;

// This cascade merged at the side it was traced at, which the cascade below reads.
[[vk::binding(16, 0)]] [format("rgba16f")]
RWTexture3D<float4> mergedTraced;

[[vk::binding(17, 0)]] [format("r16f")]
RWTexture3D<float> mergedSkyTraced;

// This cascade merged at its texels, each of an 8-side cascade the mean of its four, which the
// gather, the screen's probes and the next frame's rays read.
[[vk::binding(18, 0)]] [format("rgba16f")]
RWTexture3D<float4> merged;

[[vk::binding(19, 0)]] [format("r16f")]
RWTexture3D<float> mergedSky;

// Every cascade's probes' reach, sixteen by sixteen ways a probe, as gi_reach.slang traced it this
// frame.
[[vk::binding(20, 0)]]
Texture3D<float> reach;

// Every cascade's probes' faces' light as the gather kept it the frame before, times 1 where the
// probe holds light with that 1 in alpha (gi_ambient.slang).
[[vk::binding(21, 0)]]
Sampler3D keptLight;

// The light held for each cell of the field within a cell of a surface, as gi_hold.slang held it
// this frame, the cascades one after another along z.
[[vk::binding(22, 0)]]
Texture3D<float4> heldLight;

// The held light as the module's merge reads it at a hit (HeldHitLight), which holds none, a
// side's own light, and its probes' light times the merged probe's share, a color, as the trace's
// hits take it, so the share the module weighs it by is 1.
struct HeldCellsRead : IHeldLight
{
    float3 share;

    float3 heldAt(int cascade, int3 cell, uint side) { return heldRead(cascade, cell, side, 0u); }
    float3 heldProbeLightAt(int cascade, int3 cell, uint side) { return share * heldRead(cascade, cell, side, 1u); }
    float probeShare(float3 place, int cascade, int3 cell) { return 1.0; }
    void hold(int cascade, int3 cell, uint side, float3 own, float3 probes) {}
}

// The held light of a cell's side, its own, part 0, or its probes', part 1 (gi_hold.slang).
float3 heldRead(int cascade, int3 cell, uint side, uint part)
{
    int size = int(fieldInfo.counts.y);
    return heldLight.Load(int4(cell.x + int(side * 2u + part) * size, cell.y, cell.z + cascade * size, 0)).rgb;
}

struct MergePush
{
    // x: the cascade. y: the probes along each side. z: this cascade's octahedron's texels along
    // each side. w: the side the cascade above was merged at, or 0 for the last cascade.
    uint4 cascade;
    // x: the probes' spacing in cells. y: 1 where each cascade keeps its own rays' light alone,
    // taking nothing from the cascade above. z: the one cascade whose rays' light alone reaches the
    // first, plus one, or 0 for every cascade, the cascades above it giving nothing and those below
    // it passing on its light where their rays meet nothing and nothing where they meet a surface.
    float4 spacing;
    // x: how many cascades. y: 1 where the field holds every mesh in reach, before which a finer
    // cascade's ray that leaves the field brings no sky. z: how many times its merge's side the
    // cascade's rays are traced at (TracedSide). w: unused.
    uint4 counts;
    // x: where this cascade's interval ends. y: where the cascade above's ends. z: where the last
    // counted cascade's ends, which a ray no probe above sees the end of marches on to. w: unused.
    float4 ends;
};

[[vk::push_constant]]
ConstantBuffer<MergePush> push;


// The places the trace moved a cascade's probes to, each its spot on the grid and its move.
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

// The cascade's rays as the trace kept them, their light none where the cascade passes on another's
// light alone or where its share of the sky is merged.
struct KeptRays : IProbeRays
{
    uint side;
    uint probes;
    bool unlit;

    ProbeRay rayAt(uint index)
    {
        uint texels = side * side;
        uint probe = index / texels;
        uint2 texel = uint2((index % texels) % side, (index % texels) / side);
        int4 from = int4(int2(uint2(probe % probes, (probe / probes) % probes) * side + texel), int(probe / (probes * probes)), 0);
        float4 light = probeRayLight.Load(from);
        float2 end = probeRayEnd.Load(from);
        ProbeRay ray;
        ray.ended = uint(end.y + 0.5);
        ray.t = end.x;
        ray.carried = light.a;
        ray.light = unlit ? float3(0.0) : light.rgb;
        return ray;
    }

    void keepRay(uint index, ProbeRay ray) {}
}

// The cascade above's merged light and its share of the sky, and its probes' reach, none for a
// probe walled in, which so sees no point.
struct AboveProbes : IFarSkyProbes
{
    uint side;
    int probes;
    int cascade;

    float3 lightAt(int3 probe, uint2 texel)
    {
        return above.Load(int4(probe.xy * int(side) + int2(texel), probe.z, 0)).rgb;
    }

    float skyShareAt(int3 probe, uint2 texel)
    {
        return aboveSky.Load(int4(probe.xy * int(side) + int2(texel), probe.z, 0));
    }

    float reachAt(int3 probe, uint2 texel)
    {
        if (held[int3(3 * probes + probe.x, probe.y, cascade * probes + probe.z)].w < 0.5)
            return 0.0;
        // A texel kept at a half float's largest met nothing, the module's ReachOpen
        // (gi_reach.slang).
        float reached = reach.Load(int4(probe.xy * int(ReachTexels) + int2(texel), cascade * probes + probe.z, 0));
        return reached >= 65504.0 ? ReachOpen : reached;
    }
}

// The light and the share of the sky a texel merged, kept for its thread.
static float3 laneMerged;
static float laneSkyShare;

struct LaneMerged : IMergedSkyLight
{
    void keepMerged(uint index, float3 light) { laneMerged = light; }
    void keepSkyShare(uint index, float share) { laneSkyShare = share; }
}

// The field as the merge's marches read it, every surface they meet lit by the light held for its
// cell (HeldHitLight), or in the build with KEPT_HIT by the probes about it as the gather kept them
// (keptHitLight).
struct MarchedField : IProbeField
{
    ShadedField field;

    int cascadeOf(float3 p, float margin) { return field.cascadeOf(p, margin); }
    float4 cornerAndCell(int cascade) { return field.cornerAndCell(cascade); }
    int size() { return field.size(); }
    float4 between(int cascade, float3 cells) { return field.between(cascade, cells); }
    float4 cellAt(int cascade, int3 cell) { return field.cellAt(cascade, cell); }
    float3 secondAt(int cascade, int3 cell) { return field.secondAt(cascade, cell); }
    float throughAt(int cascade, int3 cell) { return field.throughAt(cascade, cell); }

    float3 hitLight(float3 place, float3 way, int cascade, int3 cell) { return keptHitLight(place, way, cascade, cell, field.share); }
}

// The light a surface a march meets at p sends back along way: the light reaching it past the first
// bounce read from the probes about it as the gather kept them the frame before, their face toward
// the surface's normal over those that hold light in one filtered read half a spacing off the
// surface toward the ray, which weighs the probes past a wall thinner than that less, times the
// probe's share, and the sun by one read of its map at p, the field marched toward it where the map
// does not hold p, the normal and the gloss its cell's face's, turned toward the ray, and its color
// the field's there, with its own glow. No lamp lights it, which the trace's hits each take.
float3 keptHitLight(float3 p, float3 way, int cascade, int3 cell, float3 share)
{
    uint bits = fieldFaces.Load(int4(cell.xy, cell.z + cascade * int(fieldInfo.counts.y), 0));
    float3 n = bits != 0u ? faceWay(bits) : -way;
    if (dot(n, way) > 0.0)
        n = -n;
    float3 reflected = reflectedShare(surfaceAlbedo(fieldAlbedo, fieldInfo, p), faceGloss(bits), saturate(-dot(n, way)));
    float3 own = surfaceGlowUncarried(fieldGlow, fieldAlbedo, fieldInfo, p);
    int cascades = int(push.counts.x);
    SceneFieldInfo gathered = gatheredAt(cascades);
    int c = fieldCascade(gathered, p, 1.0, cascades);
    if (c < 0 || c >= cascades)
        return own;
    float probes = float(push.cascade.y);
    float4 oc = cascadeAt(gathered, c, cascades);
    float3 q = clamp((p + n * (0.5 * oc.w * push.spacing.x) - oc.xyz) / (oc.w * push.spacing.x) - 0.5, 0.0, probes - 1.0) + 0.5;
    float3 a = abs(n);
    int axis = a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
    float face = float(axis * 2 + (n[axis] < 0.0 ? 1 : 0));
    float3 whole = float3(6.0 * probes, probes, probes * float(cascades));
    float4 light = keptLight.SampleLevel(float3(face * probes + q.x, q.y, float(c) * probes + q.z) / whole, 0.0);
    float3 indirect = light.a > 1e-3 ? light.rgb / light.a : float3(0.0);
    return own + reflected * (sunAt(p, n) + indirect / Pi * (lights.counts.z * share));
}

// The sun's light reaching a face at p facing n, the share of it reaching p by one read of the
// sun's map, or where the map does not hold p by the field marched toward the sun, none where it
// faces away or there is no sun.
float3 sunAt(float3 p, float3 n)
{
    float3 l = lights.sunDirection.xyz;
    float facing = dot(n, l);
    if (lights.sunDirection.w <= 0.5 || facing <= 0.0)
        return float3(0.0);
    float cell = fieldCell(fieldInfo, p);
    float mapped = SunShadowTap(sunShadow).reached(lights, p, n, cell);
    float reaches = mapped >= 0.0 ? mapped : traceField(sceneField, fieldInfo, p + n * (cell * 1.5), l, cell, 1e5) < 0.0 ? 1.0 : 0.0;
    return lights.sunColor.rgb * (facing * reaches);
}

// The sun's map read as SunShadowMap reads it with one read in place of nine.
struct SunShadowTap : ISunShadow
{
    Sampler2D map;

    float reached(GiLights lights, float3 p, float3 n, float cell)
    {
        for (int c = 0; c < int(lights.sunMap.x); c++)
        {
            float4 clip = mul(lights.sunCascades[c], float4(p + n * (lights.sunTexels[c] * 1.5) + lights.sunDirection.xyz * cell, 1.0));
            float3 q = clip.xyz / clip.w;
            if (any(abs(q.xy) > 0.995) || q.z > 1.0 || q.z < 0.0)
                continue;
            float2 uv = (q.xy * 0.5 + 0.5 + float2(c % 2, c / 2)) * 0.5;
            return q.z - 0.0005 <= map.SampleLevel(uv, 0.0).r ? 1.0 : 0.0;
        }
        return -1.0;
    }
}

// The sky along a way, and the share of a sky of 1 a ray leaving the field takes, both none before
// the field holds every mesh in reach for a cascade below the last, so a finer cascade's ray lends
// no sky through a wall the field has not built.
struct MergedSky : ISkyShare
{
    float on;

    float3 skyAlong(float3 way) { return on * ShadedSky().skyAlong(way); }
    float shareAlong(float3 way) { return on; }
}

// The light a texel merges and in w its share of the sky, through one merge, the cascade's own
// alone where the cascade keeps its own or the last does.
float4 mergedOf(MarchedField field, KeptRays rays, HeldPlaces places, AboveProbes far, HeldPlaces farPlaces,
    ProbeCascade cascade, ProbeCascade farCascade, bool own, uint index, float3 way)
{
    bool last = push.cascade.w == 0u;
    MergedSky sky;
    sky.on = last || push.counts.y != 0u ? 1.0 : 0.0;
    LaneMerged kept;
    if (last)
        settleTexel(rays, kept, cascade, sky, index);
    else if (own)
    {
        ProbeRay ray = rays.rayAt(index);
        laneMerged = ray.ended == RayLeft ? lastCascadeLight(ray, way, sky) : ray.light;
        laneSkyShare = lastCascadeSkyShare(ray, way, sky);
    }
    else
    {
        NoSteps none;
#if defined(KEPT_HIT)
        mergeTexel(field, rays, places, far, farPlaces, kept, cascade, farCascade, push.ends.z, sky, none, index);
#else
        HeldHitLight<MarchedField, HeldCellsRead> held = { field, { field.field.share } };
        mergeTexel(held, rays, places, far, farPlaces, kept, cascade, farCascade, push.ends.z, sky, none, index);
#endif
    }
    return float4(laneMerged, laneSkyShare);
}

groupshared float4 quarters[64];
groupshared float quarterSkies[64];

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID, uint lane : SV_GroupIndex)
{
    uint probes = push.cascade.y;
    uint texels = push.cascade.z;
    // A thread a ray, a texel's rays (TracedRays) side by side in a workgroup, as the trace lays
    // them.
    uint spread = push.counts.z;
    uint each = TracedRays(texels, spread);
    uint side = TracedSide(texels, spread);
    uint probe = id.x / (side * side);
    uint index = id.x % (side * side);
    uint quarter = index % each;
    uint2 texel = uint2((index / each) % texels, (index / each) / texels);
    uint2 mergedAt;
    uint2 traced = tracedTexel(texel, quarter, texels, spread, mergedAt);
    bool inGrid = probe < probes * probes * probes;
    uint3 at = uint3(probe % probes, (probe / probes) % probes, probe / (probes * probes));
    uint c = push.cascade.x;
    float4 oc = cascadeAt(fieldInfo, c, int(push.counts.x));
    float apart = oc.w * push.spacing.x;
    float4 here = inGrid ? held[int3(3 * int(probes) + int(at.x), int(at.y), int(c * probes) + int(at.z))] : float4(0.0);
    uint alone = uint(push.spacing.z + 0.5);

    float4 light = float4(0.0);
    float sky = 0.0;
    if (inGrid && oc.w > 0.0 && here.w > 0.5 && alone > 0u && c >= alone)
        light = float4(0.0, 0.0, 0.0, 1.0);
    else if (inGrid && oc.w > 0.0 && here.w > 0.5)
    {
        ProbeCascade cascade;
        cascade.corner = oc.xyz + 0.5 * apart;
        cascade.spacing = apart;
        cascade.count = int3(int(probes));
        cascade.texels = side;
        cascade.start = 0.0;
        cascade.end = push.ends.x;
        cascade.stepCap = StepCap;
        HeldPlaces places;
        places.corner = oc.xyz;
        places.apart = apart;
        places.probes = int(probes);
        places.cascade = int(c);
        KeptRays rays;
        rays.side = side;
        rays.probes = probes;
        rays.unlit = alone > 0u && c + 1u < alone;
        // The cascade above, the far one's place for the last counted's where the field has one.
        uint up = min(c + 1u, push.counts.x - 1u);
        float4 ac = cascadeAt(fieldInfo, up, int(push.counts.x));
        float farApart = ac.w * push.spacing.x;
        ProbeCascade farCascade;
        farCascade.corner = ac.xyz + 0.5 * farApart;
        farCascade.spacing = farApart;
        farCascade.count = int3(int(probes));
        farCascade.texels = max(push.cascade.w, 1u);
        farCascade.start = 0.0;
        farCascade.end = push.ends.y;
        farCascade.stepCap = StepCap;
        HeldPlaces farPlaces;
        farPlaces.corner = ac.xyz;
        farPlaces.apart = farApart;
        farPlaces.probes = int(probes);
        farPlaces.cascade = int(up);
        AboveProbes far;
        far.side = max(push.cascade.w, 1u);
        far.probes = int(probes);
        far.cascade = int(up);
        // A surface a march meets lit by the probes kept about it, with the probe's share of the
        // indirect light as its trace took it (kept in gi_trace.slang), or the whole for a probe it
        // has not traced since the gather judged it by the probes around it.
        float4 shared = held[int3(2 * int(probes) + int(at.x), int(at.y), int(c * probes) + int(at.z))];
        MarchedField field;
        field.field.share = shared.r >= 0.0 ? shared.rgb : float3(1.0);
        field.field.widen = texelWidening(texels);
        field.field.probes = float(probes);
        field.field.spacing = push.spacing.x;
        field.field.cascades = float(push.counts.x);
        bool own = push.spacing.y > 0.5 || alone == c + 1u || oc.w <= 0.0 || ac.w <= 0.0;
        uint kept = (probe * side + traced.y) * side + traced.x;
        float3 way = texelWay(side, traced);
        float4 both = mergedOf(field, rays, places, far, farPlaces, cascade, farCascade, own, kept, way);
        light = float4(both.rgb, 1.0);
        sky = both.w;
    }
    // Each texel at the merge's side the mean of the traced texels it holds, for the cascade
    // below, and each texel at the cascade's own the mean of all its rays, for the gather, which
    // lie side by side in the group.
    quarters[lane] = light;
    quarterSkies[lane] = sky;
    GroupMemoryBarrierWithGroupSync();
    uint quad = spread * spread;
    if (inGrid && quarter % quad == 0u)
    {
        float4 sum = float4(0.0);
        float skySum = 0.0;
        for (uint q = 0u; q < quad; q++)
        {
            sum += quarters[lane + q];
            skySum += quarterSkies[lane + q];
        }
        mergedTraced[uint3(at.xy * MergeSide(texels) + mergedAt, at.z)] = sum / float(quad);
        mergedSkyTraced[uint3(at.xy * MergeSide(texels) + mergedAt, at.z)] = skySum / float(quad);
    }
    if (inGrid && quarter == 0u)
    {
        float4 sum = float4(0.0);
        float skySum = 0.0;
        for (uint q = 0u; q < each; q++)
        {
            sum += quarters[lane + q];
            skySum += quarterSkies[lane + q];
        }
        merged[uint3(at.xy * texels + texel, at.z)] = sum / float(each);
        mergedSky[uint3(at.xy * texels + texel, at.z)] = skySum / float(each);
    }
}
