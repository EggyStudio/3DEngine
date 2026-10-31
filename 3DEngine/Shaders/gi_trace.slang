// Traces the rays of one cascade of light probes through the scene's distance field, a thread a
// ray. A cascade's probes lie every eight cells of the field's cascade of the same number, or of
// its far cascade for the last where it has one (cascadeAt), and each
// sends a ray along each texel of an octahedron of directions across its interval, from as far as
// the probes are apart to four times that, the last cascade's on to the field's edge, so the next
// cascade, twice as coarse, carries on from half way along it, and a surface nearer a probe of the
// next cascade than its interval begins, which that probe meets early, is met by this probe's own
// ray. A ray that meets a surface brings back the light the surface sends along it and stops there.
// One that meets nothing brings back nothing and lets what lies beyond through, but in the last
// cascade, where it brings back the light from all around. A probe inside a mesh sends nothing,
// which the merge reads as a probe to pass over.
//
// A ray is marched from its probe, since one marched from where its interval begins started under
// a floor its probe stood a little above and brought back the light under it. One that meets a
// surface before its interval begins brings back that surface's light, marked apart, alpha a half,
// which the probe's own faces take and the merge of the cascade below reads as dark, as it did
// when such a ray was blocked dark here too. Blocked, this cascade's faces lacked every surface
// nearer its probes than the interval's start, which a Cornell box probe of the second cascade
// read 63% short of a path-traced reference, and every surface past the first cascade's reach
// that read those faces for the indirect light reaching it.
//
// A probe nearer a surface than a tenth of its spacing, or inside a mesh, is moved off it along the
// field's normal before its rays leave, as DDGI moves its probes, so the probes in the plane of a
// wall or a ceiling stand in the room before it, and where it stands is left for the merge and the
// gather, which writes it into the probe's faces for every pass that reads them.
//
// A probe takes the indirect light reaching the surfaces its rays meet at the share the gather left
// it, other than the whole the frame after the light its rays bring straight from the sun, the
// lights and emissive surfaces fell by a fifth or rose by a quarter, so the indirect light from
// light that went out goes with it rather than over the frames each bounce takes to fade.
import gi;
import scenefield;
import lightthrough;

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

// What each texel's rays brought back, rgb the light of those that met a surface or the sky over
// how many it sends, and a what most of them met, 1 nothing, which lets what lies beyond through, a
// half a surface before their interval began, 0 one inside it, and a quarter the sky, where a ray of
// the last cascade met nothing and brought its light, which the merge takes as it takes 0 and
// counts as the sky the probe sees, moved a sixteenth on for each ray that met nothing (texelMark).
// A texel a direction, each probe a tile of the octahedron's texels.
[[vk::binding(5, 0)]] [format("rgba16f")]
RWTexture3D<float4> radiance;

// The indirect light reaching each probe the frame before, as gi_ambient.slang left it, for the
// light a surface sends on from it.
[[vk::binding(6, 0)]]
Sampler3D bounced;

// The light from all around, for a ray of the last cascade that meets nothing, or a black cube.
[[vk::binding(7, 0)]]
SamplerCube environment;

// x: how far each ray went before it met a surface, or its interval's end where it met none, below
// zero where it met a face from behind, which gi_ambient.slang lays out as each probe's reach and
// counts to find a probe inside a solid. y: the share of light the ray kept across surfaces that
// let light through before its interval began, which the probe's own faces take its light times,
// where the cascade below, whose interval that is, takes the light from the interval on whole.
[[vk::binding(8, 0)]] [format("rg16f")]
RWTexture3D<float2> distances;

// Each probe's own light as gi_ambient.slang left it, the frame before's and the one before that
// side by side, the share of the indirect light each probe takes, and where this trace moved
// each probe to, its move in its spacing with w 1 where it stands outside every mesh, every
// cascade.
[[vk::binding(9, 0)]] [format("rgba16f")]
RWTexture3D<float4> held;

// What each workgroup of a probe's rays, 64 of them, brought back straight from the sun, the lights
// and emissive surfaces, summed, with in a the luminance of all they brought back, each ray
// weighed by the share of the sphere its texel spans, four workgroups a probe at most, which
// gi_ambient.slang sums a probe at a time.
[[vk::binding(10, 0)]] [format("rgba32f")]
RWTexture3D<float4> partial;

// The way each cell's nearest face looks, which a hit takes its color from cells by.
[[vk::binding(11, 0)]] [format("r8ui")]
Texture3D<uint> fieldFaces;

// The second plane each cell of the field keeps where two double-sided faces lie as near it, which
// the rays read with the field (SheetVolume).
[[vk::binding(12, 0)]]
Texture3D<float4> fieldSecond;

struct TracePush
{
    // x: the cascade. y: the probes along each side. z: the octahedron's texels along each side.
    // w: 1 for the last cascade.
    uint4 cascade;
    // x: where the interval starts. y: where it ends. z: the probes' spacing in cells. w: how many cascades.
    float4 interval;
    // x: 1 where each probe's share holds for the probe, its cascade lying where it lay the frame
    // before. y: the bits of the last cascade's interval's end, the field's edge, which a probe
    // that sees no probe of the cascade above carries its rays on to. z: the frame's parity.
    uint4 follow;
};

[[vk::push_constant]]
ConstantBuffer<TracePush> push;

groupshared float4 place;

// Whether the gather found the frame before that the probe's rays met more faces from behind than
// from in front (gi_ambient.slang), so it stands inside a solid.
groupshared bool walledIn;

// Whether the probe sees any of the eight probes of the cascade above around it, as the merge asks
// (gi_merge.slang), set by the group's first eight rays for every ray of it.
groupshared uint seesAbove;

// The field's layout with each cascade at the corner it lay at the frame before, where the faces of
// the indirect light, which the rays read, were gathered. Read at this frame's corners, a
// cascade that moved with the eye read each probe's faces from the probe a step or more beside it,
// and the light past the first bounce, fed from those faces each frame, took ten frames and more to
// find itself again after a turn moved every cascade.
SceneFieldInfo gatheredAt()
{
    int cascades = int(push.interval.w);
    SceneFieldInfo then = fieldInfo;
    int far = int(fieldInfo.counts.w);
    // Written where cascadeAt reads each cascade of probes from, the last at the far cascade's place
    // where the field has one.
    for (int c = 0; c < min(cascades, MostProbeCascades); c++)
        then.cascades[far > 0 && c == cascades - 1 ? far : c].originAndCell.xyz = lights.before[c].xyz;
    return then;
}

// Whether the open space at p is narrower than `apart` along two axes or three, a surface within it
// either way, as a corridor's inside is across it and up it.
bool narrowAt(float3 p, float apart)
{
    int narrow = 0;
    for (int axis = 0; axis < 3; axis++)
    {
        float3 way = float3(0.0);
        way[axis] = 1.0;
        if (traceFieldSheets(SheetVolume(sceneField, fieldSecond), fieldInfo, p, way, 0.0, apart) >= 0.0
            && traceFieldSheets(SheetVolume(sceneField, fieldSecond), fieldInfo, p, -way, 0.0, apart) >= 0.0)
            narrow++;
    }
    return narrow >= 2;
}

// Where the probe whose spot in the grid is at stands, moved off a surface nearer than Clearance of
// the spacing, or out of a mesh, along the field's normal, by MostMoved of the spacing at most, as
// its move in its spacing, with w 1 where it then stands outside every mesh. Above the first
// cascade, a spot in or beside a wall between a space narrower than the spacing and a wider one
// goes into the narrow one, through the wall's farther face, since the wider holds the probes
// beside it and the narrow one would hold none. A corridor two units wide and two and a half high
// in walls 0.3 thick held no probe of the cascades spaced 2.4 and more, the spots of the second in
// its walls moved out through their nearer faces, so the walls by its open end, past the first
// cascade's reach, took no light past the first bounce. Moved in, the corridor's view came 18%
// nearer its path-traced reference at High and 5% at Low, every other room of shaders_bounce_rooms
// as it was. Moving the first cascade's probes so as well took the Cornell box's view at Low 1.4%
// further from its reference, and moving them into a space narrow along one axis alone the strip
// room's 0.3%.
float4 placed(float3 at, float apart)
{
    SheetVolume sheets = SheetVolume(sceneField, fieldSecond);
    float3 p = at;
    float d = fieldDistanceSheet(sheets, fieldInfo, p);
    float3 normal = fieldNormalSheet(sheets, fieldInfo, p);
    bool near = d < Clearance * apart;
    for (int step = 0; step < 4 && d < Clearance * apart; step++)
    {
        p += fieldNormalSheet(sheets, fieldInfo, p) * (Clearance * apart - d);
        float3 move = p - at;
        float length = length(move);
        if (length > MostMoved * apart)
            p = at + move * (MostMoved * apart / length);
        d = fieldDistanceSheet(sheets, fieldInfo, p);
    }
    if (near && push.cascade.x >= 1u)
    {
        // Past the farther side of what the spot stands in or beside, the first point clear by
        // Clearance once the way has crossed a surface.
        float least = fieldDistanceSheet(sheets, fieldInfo, at);
        for (int k = 1; k <= 8; k++)
        {
            float3 q = at - normal * (MostMoved * apart * float(k) / 8.0);
            float dq = fieldDistanceSheet(sheets, fieldInfo, q);
            least = min(least, dq);
            if (least <= 0.0 && dq >= Clearance * apart)
            {
                if (narrowAt(q, apart) && !narrowAt(p, apart))
                {
                    p = q;
                    d = dq;
                }
                break;
            }
        }
    }
    return float4((p - at) / apart, d >= 0.0 ? 1.0 : 0.0);
}

groupshared float3 nearNow[32];
groupshared float3 nearBefore[32];

// The share of the indirect light the probe at `at` of cascade c takes, the one the gather left
// it, or for a probe its own light lights too little of to judge by, the share the own light
// of the 27 probes of its cascade within one of it kept of the frame before's, so a room that goes
// dark takes its walls' indirect light with it, and a lamp in a room far off, judged by every
// probe's own light, does not dim or brighten the passages lit by indirect light alone with it.
// Each of the first 27 rays of the probe's workgroup reads one probe, and every ray of the group
// reaches the sum, the share being the probe's.
float3 kept(uint3 at, int c, int probes, uint lane)
{
    if (push.follow.x == 0u)
        return float3(1.0);
    float4 share = held[int3(2 * probes + int(at.x), int(at.y), c * probes + int(at.z))];
    if (share.r >= 0.0)
        return share.rgb;
    uint parity = push.follow.z & 1u;
    if (lane < 32u)
    {
        float3 now = float3(0.0), before = float3(0.0);
        int3 near = int3(at) + int3(int(lane % 3u) - 1, int((lane / 3u) % 3u) - 1, int(lane / 9u) - 1);
        if (lane < 27u && all(near >= int3(0)) && all(near < int3(probes)))
        {
            float4 last = held[int3(int(parity ^ 1u) * probes + near.x, near.y, c * probes + near.z)];
            float4 earlier = held[int3(int(parity) * probes + near.x, near.y, c * probes + near.z)];
            if (last.a >= 0.0 && earlier.a >= 0.0)
            {
                now = last.rgb;
                before = earlier.rgb;
            }
        }
        nearNow[lane] = now;
        nearBefore[lane] = before;
    }
    GroupMemoryBarrierWithGroupSync();
    for (uint half = 16u; half > 0u; half >>= 1)
    {
        if (lane < half)
        {
            nearNow[lane] += nearNow[lane + half];
            nearBefore[lane] += nearBefore[lane + half];
        }
        GroupMemoryBarrierWithGroupSync();
    }
    float3 now = nearNow[0], before = nearBefore[0];
    float was = luminance(before);
    return was > 1e-4 ? select(before > was * 1e-3, followed(now, before), float3(1.0)) : float3(1.0);
}

groupshared float4 brought[64];

// Each ray's light and mark, its own light with how far it went, below zero where it met a face
// from behind, and the share of it that met nothing, for the first ray of its texel to take together.
groupshared float4 rayLight[64];
groupshared float4 rayOwn[64];
groupshared float rayNothing[64];
groupshared float rayEarly[64];

[shader("compute")]
[numthreads(64, 1, 1)]
void computeMain(uint3 id : SV_DispatchThreadID, uint lane : SV_GroupIndex)
{
    uint probes = push.cascade.y;
    uint texels = push.cascade.z;
    // A thread a ray, a texel's rays (RaysAt) side by side in a workgroup.
    uint each = RaysAt(texels);
    uint rays = texels * texels * each;
    uint probe = id.x / rays;
    // A probe's rays are 256, so every workgroup is one probe's, and one past the last probes
    // leaves whole, before the sum below every ray of a group reaches.
    if (probe >= probes * probes * probes)
        return;
    uint index = id.x % rays;
    uint ray = index / each;
    uint quarter = index % each;
    uint3 at = uint3(probe % probes, (probe / probes) % probes, probe / (probes * probes));
    uint2 texel = uint2(ray % texels, ray / texels);
    uint3 written = uint3(at.xy * texels + texel, at.z);

    // The probe's share, before any ray of the group leaves the rest.
    float3 share = kept(at, int(push.cascade.x), int(probes), lane);

    float4 oc = cascadeAt(fieldInfo, push.cascade.x, int(push.interval.w));
    float apart = oc.w * push.interval.z;
    float3 spot = oc.xyz + (float3(at) + 0.5) * apart;
    // The probe moved into the open by the group's first ray, for every ray of it. One the gather
    // found inside a solid is left for the merge and the gather as one inside a mesh, its rays
    // traced all the same, so it is found again each frame; found for a probe that stood elsewhere,
    // its cascade moved since, the finding is not this probe's.
    if (lane == 0u)
    {
        place = oc.w > 0.0 ? placed(spot, apart) : float4(0.0);
        int z = int(push.cascade.x) * int(probes) + int(at.z);
        walledIn = place.w > 0.5 && push.follow.x != 0u && held[int3(2 * int(probes) + int(at.x), int(at.y), z)].w > 0.5;
        held[int3(3 * int(probes) + int(at.x), int(at.y), z)] = walledIn ? float4(place.xyz, 0.0) : place;
    }
    if (lane == 0u)
        seesAbove = 0u;
    GroupMemoryBarrierWithGroupSync();
    float3 origin = spot + place.xyz * apart;
    // A probe below the last cascade that sees none of the probes above it standing outside every
    // mesh has no probe to give it what lies past its interval, so its rays go on to the field's
    // edge and bring the light of what they meet there. One that meets nothing brings nothing, its
    // reach its interval's end, since bringing the sky as the last cascade's rays do lit a closed
    // room's floor in its first frames through the cascades not yet built, 0.060 in linear light
    // where it settles under half that (CascadeEdgeTests). With the second cascade's probes placed
    // inside the corridor of shaders_bounce_rooms, this took its walls at High from 67% under a
    // path-traced reference to 60 and 62%, its view 25% nearer the reference where 18% without.
    if (lane < 8u && push.cascade.w == 0u && oc.w > 0.0 && place.w > 0.5)
    {
        uint upperCascade = push.cascade.x + 1u;
        float4 ac = cascadeAt(fieldInfo, upperCascade, int(push.interval.w));
        float upperApart = ac.w * push.interval.z;
        int3 base = int3(floor((spot - ac.xyz) / upperApart - 0.5));
        int3 neighbor = clamp(base + int3(int(lane & 1u), int((lane >> 1) & 1u), int(lane >> 2)), int3(0), int3(int(probes) - 1));
        float4 upperPlace = held[int3(3 * int(probes) + neighbor.x, neighbor.y, int(upperCascade) * int(probes) + neighbor.z)];
        float3 upperProbe = ac.xyz + (float3(neighbor) + 0.5 + upperPlace.xyz) * upperApart;
        if (ac.w > 0.0 && upperPlace.w > 0.5 && seesPastLeaves(sceneField, fieldAlbedo, fieldInfo, origin, upperProbe, oc.w * 0.5))
            InterlockedOr(seesAbove, 1u);
    }
    GroupMemoryBarrierWithGroupSync();
    bool carried = push.cascade.w == 0u && seesAbove == 0u;
    float end = carried ? asfloat(push.follow.y) : push.interval.y;
    float4 light = float4(0.0, 0.0, 0.0, 1.0);
    float3 own = float3(0.0);
    float reached = 0.0;
    bool behind = false;
    // The share of the ray's light still to come past the surfaces that let light through it
    // crossed, which a ray that met nothing hands on to the cascade above, and the share it kept
    // across those before its interval began, which its probe's own faces alone take.
    float through = 1.0;
    float keptBefore = 1.0;
    if (oc.w <= 0.0)
    {
    }
    else if (place.w < 0.5)
        light = float4(0.0);
    else
    {
        // A cascade of 8 texels a side sends a ray from the middle of each quarter of its texel,
        // and the texel takes their light together (below). One from the texel's middle took a
        // lamp's bright spot under a ceiling for the whole texel, which in a Cornell box's pocket
        // between the floor and a block held a sixty-fourth of the sphere, and the first cascade's
        // probe there read 93% over a path-traced reference in its upward face and the block's
        // front 65% over its own; four rays read 18% and 39% over (gi.probe, gi.compare). Low's and
        // Medium's second cascade has 8 a side as well, and with one ray a texel the screen's
        // probes in a room twelve units wide read the light beyond their reach from it from 44%
        // under the reference to 9% over along their ways at one bounce, where with 16 a side they
        // read it as High's do (gi.screenprobe).
        float2 within = each == 1u ? float2(0.5) : float2(float(quarter & 1u), float(quarter >> 1u)) * 0.5 + 0.25;
        float3 way = fromOctahedron((float2(texel) + within) / float(texels));
        float t = traceFieldSheets(SheetVolume(sceneField, fieldSecond), fieldInfo, origin, way, 0.0, end);
        reached = t >= 0.0 ? t : push.interval.y;
        // A ray that came on a face from behind it stops half a cell or more behind the face,
        // having stepped from a cell no face comes within the band of, which the field holds as
        // the open, straight into the cells behind it, where a ray from in front, slowing as it
        // nears the face, stops within a tenth of a cell of it.
        behind = t >= 0.0 && fieldDistance(sceneField, fieldInfo, origin + way * t) < -0.5 * fieldCell(fieldInfo, origin + way * t);
#if defined(NO_LIGHT_THROUGH)
        // A frame where no surface lets light through traces with this build, whose ray stops at
        // the first surface it meets and takes its light. With the light taken as the crossing
        // takes it, summed over what the ray met and set after, global illumination took 7% more
        // at Low in the bounce rooms, though the ray met one surface as here.
        if (t >= 0.0)
        {
            // The indirect light reaching the hit from the probes a march reaches, so the probes'
            // reach goes unread and the faces stand in its place.
            light = float4(shadeProbeHit(sceneField, SheetVolume(sceneField, fieldSecond), fieldInfo, gatheredAt(), fieldAlbedo, fieldFaces, fieldGlow, lights, bounced, bounced, origin + way * t, way,
                float(probes), push.interval.z, push.interval.w, true, share, own), t < push.interval.x ? 0.5 : 0.0);
            through = 0.0;
        }
        else if (push.cascade.w != 0u)
        {
            float3 sky = lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb;
            light = float4(sky, 0.25);
            own = sky;
            through = 0.0;
        }
#else
        float3 crossed = float3(0.0), crossedOwn = float3(0.0);
        // A surface that lets a share of light through, as a canopy's leaves, is crossed, the ray
        // keeping that share for each unit of its depth and the rest taking the surface's light as
        // a solid's would, up to four of them before the ray stops at the next as a solid. One
        // crossed before the interval began is the cascade below's, whose ray takes this one's
        // light from the interval on whole where it met nothing, so it takes nothing of the light
        // and keeps its share for the probe's own faces alone. Taken off the light, the floor under
        // a slab letting half the light through read the sky's light twice halved, once below and
        // once above, and dark where the merge read such a ray as one that met a surface early. The
        // first surface letting light through that the ray meets stands for every one it crosses,
        // as a canopy's leaves a block apart take much the same light, shaded once after the march
        // for the share of the ray's light they take together, and the surface it stops at for the
        // share that reaches it, so the build holds one copy of the shading and none in the march.
        // Shaded where the ray crossed each, the leaves of the voxel game's cherry grove at seed 1
        // cost global illumination 2.6 to 2.7 ms at High at 1920 by 1080 and 1.5 to 1.7 at Low,
        // where shaded once they cost 2.0 to 2.1 and 1.1 (./e3d command gi.toggle through off).
        float firstAt = -1.0;
        float leafAt = -1.0, leafShare = 0.0, stopAt = -1.0, stopShare = 0.0;
        [loop]
        for (int layer = 0; layer < 5 && t >= 0.0; layer++)
        {
            float3 at = origin + way * t;
            float leaf = lightThroughAt(fieldAlbedo, fieldInfo, at);
            float passing = layer < 4 ? leaf : 0.0;
            bool inside = false;
            float depth = passing > 0.0 ? lightThroughDepth(sceneField, fieldInfo, origin, way, t, end - t, inside) : -1.0;
            float passes = depth >= 0.0 ? lightThroughKept(passing, depth, fieldCell(fieldInfo, at), inside) : 0.0;
            if (depth >= 0.0 && t < push.interval.x)
                keptBefore *= passes;
            else
            {
                float taken = through * (1.0 - passes);
                if (leaf > 0.0)
                {
                    leafAt = leafAt < 0.0 ? t : leafAt;
                    leafShare += taken;
                }
                else
                {
                    stopAt = t;
                    stopShare = taken;
                }
                through *= passes;
                firstAt = firstAt < 0.0 ? t : firstAt;
            }
            if (depth < 0.0)
                break;
            // A ray still within at its interval's end met nothing past it, its share handed on.
            t = inside ? -1.0 : traceFieldSheets(SheetVolume(sceneField, fieldSecond), fieldInfo, origin, way, t + depth, end);
        }
        [loop]
        for (int shaded = 0; shaded < 2; shaded++)
        {
            float shadedAt = shaded == 0 ? leafAt : stopAt;
            if (shadedAt < 0.0)
                continue;
            // As the build without the crossing shades the hit.
            float3 itsOwn;
            float3 itsLight = shadeProbeHit(sceneField, SheetVolume(sceneField, fieldSecond), fieldInfo, gatheredAt(), fieldAlbedo, fieldFaces, fieldGlow, lights, bounced, bounced, origin + way * shadedAt, way,
                float(probes), push.interval.z, push.interval.w, true, share, itsOwn);
            float weight = shaded == 0 ? leafShare : stopShare;
            crossed += itsLight * weight;
            crossedOwn += itsOwn * weight;
        }
        // How far it went is where it stopped, past what it crossed, which the screen's probes read
        // the cascades' light beyond their reach by (probeReaches in gi_screen.slang).
        reached = t >= 0.0 ? t : push.interval.y;
        // Marked by what the ray met first that counts, a surface crossed in its interval or one it
        // stopped at, before its interval began or in it.
        float mark = firstAt >= 0.0 ? (firstAt < push.interval.x ? 0.5 : 0.0) : 1.0;
        if (t < 0.0 && push.cascade.w != 0u)
        {
            float3 sky = lights.sky.w > 0.5 ? environment.SampleLevel(way, 0).rgb * lights.sky.rgb : lights.sky.rgb;
            light = float4(crossed + through * sky, firstAt >= 0.0 ? mark : 0.25);
            own = crossedOwn + through * sky;
            through = 0.0;
        }
        else if (firstAt >= 0.0)
        {
            // A surface it stopped at, or those it crossed with nothing past them, the share of
            // the light it kept left for the cascade above to give.
            light = float4(crossed, mark);
            own = crossedOwn;
        }
#endif
    }
    if (walledIn)
    {
        light = float4(0.0);
        own = float3(0.0);
    }
    // A ray that met nothing at all, and one that crossed surfaces letting light through and met
    // nothing past them, hand on the share they kept.
    float nothingShare = light.a >= 0.75 ? 1.0 : place.w < 0.5 || walledIn ? 0.0 : through;

    // A texel's rays taken together by its first, the light of those that met a surface or the
    // sky over how many there are, marked with what most met and how many met nothing (texelMark),
    // their own light and their reach their means, and a face met from behind where most met one so.
    rayLight[lane] = light;
    rayOwn[lane] = float4(own, behind ? -reached : reached);
    rayNothing[lane] = nothingShare;
    rayEarly[lane] = keptBefore;
    GroupMemoryBarrierWithGroupSync();
    float4 texelLight = float4(0.0);
    float3 texelOwn = float3(0.0);
    if (quarter == 0u)
    {
        float3 met = float3(0.0);
        float reach = 0.0;
        // How many met a surface, one before their interval began and the sky, and the share that
        // met nothing, fractions of a ray where one crossed surfaces letting light through.
        uint surfaces = 0u, early = 0u, skies = 0u;
        float nothing = 0.0, before = 0.0;
        uint behinds = 0u;
        for (uint q = 0u; q < each; q++)
        {
            float4 one = rayLight[lane + q];
            float4 itsOwn = rayOwn[lane + q];
            surfaces += one.a < 0.125 ? 1u : 0u;
            skies += one.a >= 0.125 && one.a < 0.375 ? 1u : 0u;
            early += one.a >= 0.375 && one.a < 0.75 ? 1u : 0u;
            nothing += rayNothing[lane + q];
            before += rayEarly[lane + q];
            met += one.a < 0.75 ? one.rgb : float3(0.0);
            texelOwn += itsOwn.rgb;
            reach += abs(itsOwn.w);
            behinds += itsOwn.w < 0.0 ? 1u : 0u;
        }
        float most = nothing * 2.0 > float(each) ? 1.0 : early > surfaces && early >= skies ? 0.5 : skies > surfaces ? 0.25 : 0.0;
        texelLight = float4(met / float(each), texelMark(most, nothing, each));
        texelOwn /= float(each);
        reach /= float(each);
        radiance[written] = texelLight;
        // Below zero where the rays met a face from behind, which the gather counts.
        distances[written] = float2(behinds * 2u > each ? -reach : reach, before / float(each));
    }

    // The group's own light and all it brought back summed, each ray weighed by the share of the
    // sphere its texel spans, as the gather weighs them, halving the rays that add each step.
    brought[lane] = quarter == 0u ? float4(texelOwn, luminance(texelLight.rgb)) * octahedronShare((float2(texel) + 0.5) / float(texels)) : float4(0.0);
    GroupMemoryBarrierWithGroupSync();
    for (uint half = 32u; half > 0u; half >>= 1)
    {
        if (lane < half)
            brought[lane] += brought[lane + half];
        GroupMemoryBarrierWithGroupSync();
    }
    if (lane == 0u)
        partial[int3(int(at.x) * 4 + int(index / 64u), int(at.y), int(push.cascade.x) * int(probes) + int(at.z))] = brought[0];
}
