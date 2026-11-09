// Merges one cascade of light probes with the merged cascade above it, a thread a probe's
// direction, from the last cascade down to the first. What a probe's ray brought back is kept, and
// where the ray met nothing the light from beyond is added, the cascade above's probes around this
// one blended by how near each is, each one's texels of the directions inside this texel's
// averaged, so the first cascade holds the light from every distance. A probe inside a mesh, or one
// a surface hides from this probe, as a probe above a ceiling is hidden from one under it, is
// passed over in the blend, and a probe inside a mesh holds nothing itself, its alpha 0 where every
// other's is 1. A ray of the cascade above that met a surface before its interval began keeps that
// surface's light for its own probe's faces, its alpha a half, and the blend reads it as dark,
// since the surface lies nearer that probe than this one's ray reaches, so the light it sees there
// is not the light along this ray, and taken it read a ceiling beside a lamp for one two units off
// in a Cornell box. Each probe above is read toward the point of this probe's ray nearest where its
// own ray along the way met a surface the frame before (its reach), and not along the same way, so
// a probe above standing elsewhere lends the light of what this ray would meet and not of what it
// sees from where it stands, the merge's parallax. Read along the same way, the first cascade's
// probe in a Cornell box's pocket between the floor and the small block took for its upward face
// the ceiling nearer the lamp that the probe above meets. A trace from this ray toward each probe
// above, the bilinear fix, mends that less than the parallax and costs a trace for each.
//
// A probe above stands a spacing from this one and may see past what this ray would meet beyond
// its interval, or meet what this ray passes. Where the cascade above's interval ends at twice this
// one's, this ray is marched on past its interval to that end through the field, across surfaces
// that let light through, and each probe above whose own hit along the way lies off where the march
// met a surface, by two of its cells or a quarter of the way, or that met one where the march met
// none, is passed over, those that agree taken alone, and where none agrees and the march met a
// surface, the light from beyond is none. A probe above that is carried past its interval with its
// rays moving through their sixteen places is read by its texels' middle ways' hits, which the
// trace keeps for it (gi_trace.slang), since its rays' own met a surface in some frames and saw
// past the end in others, and passed over in some frames and not others, a probe below it by the
// door view's end wall flickered with the view held. In Sponza's door view the probes above the
// vestibule saw the sky and the court past its arch along ways whose rays meet its underside, and
// in the lion view the probes above under the arcade met its arch along a way the probe below sees
// the sky along. The cascade whose above is the last counted is not marched, since its march runs
// to the field's edge, where nearly every way that takes the cascade above has a probe above that
// sees past it, so the march changes little there for its cost. The probes above that met a
// surface before their interval are read as dark and not passed over, since read as dark they stand
// in for this ray's own meeting with the roof, and passed over without the march they lit the floor
// under a solid canopy from the probes at its edge.
import gi;
import scenefield;
import lightthrough;

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

// Where the trace moved each probe to this frame, after each probe's own light, its share and the
// frame before's, its move in its spacing with w 1 where it stands outside every mesh.
[[vk::binding(5, 0)]] [format("rgba16f")]
RWTexture3D<float4> held;

// The share of each merged direction that reaches the sky, 1 where a ray of the last cascade met
// nothing, carried down as the light from beyond is, and the cascade above's, read where this
// cascade's ray met nothing; the last cascade reads none.
[[vk::binding(6, 0)]] [format("r16f")]
RWTexture3D<float> mergedSky;

[[vk::binding(7, 0)]]
Texture3D<float> aboveSky;

// The cascade above's probes' reach, the nearest ray's distance along each of their eight by eight
// directions, as the gather wrote it the frame before.
[[vk::binding(8, 0)]]
Texture3D<float> reachAbove;

// The field's colors, whose alpha says where a surface lets light through (seesPastLeaves).
[[vk::binding(9, 0)]]
Sampler3D fieldAlbedo;

// A carried probe's cycle of the sixteen places its rays move through (gi_trace.slang), the mean
// gathered from the cycle's first frame with its count, and the last whole cycle's mean, as the
// frame before left them, and as this frame leaves them.
[[vk::binding(10, 0)]]
Texture3D<float4> partialBefore;
[[vk::binding(11, 0)]]
Texture3D<float4> fullBefore;
[[vk::binding(12, 0)]] [format("rgba16f")]
RWTexture3D<float4> partialNow;
[[vk::binding(13, 0)]] [format("rgba16f")]
RWTexture3D<float4> fullNow;

// The same cycle's share of the sky and mark of what the rays met, the merged light's alpha, the
// mean of each gathered from the cycle's first frame and the last whole cycle's, x and y the share's
// and z and w the mark's, as the frame before left them, and as this frame leaves them.
[[vk::binding(14, 0)]]
Texture3D<float4> skyMarkBefore;
[[vk::binding(15, 0)]] [format("rgba16f")]
RWTexture3D<float4> skyMarkNow;

// The texel of an octahedron of `texels` along each side at t, folded across its edges.
int2 foldedAt(int2 t, int texels)
{
    if (t.x < 0)
        t = int2(0, texels - 1 - t.y);
    else if (t.x >= texels)
        t = int2(texels - 1, texels - 1 - t.y);
    if (t.y < 0)
        t = int2(texels - 1 - t.x, 0);
    else if (t.y >= texels)
        t = int2(texels - 1 - t.x, texels - 1);
    return t;
}

float4 placeOf(int3 at, uint c, uint probes)
{
    return held[int3(3 * int(probes) + at.x, at.y, int(c * probes) + at.z)];
}

struct MergePush
{
    // x: the cascade. y: the probes along each side. z: this cascade's octahedron's texels along
    // each side. w: the cascade above's, or 0 for the last cascade.
    uint4 cascade;
    // x: the probes' spacing in cells. y: 1 where each cascade keeps its own rays' light alone, taking
    // nothing from the cascade above. z: the one cascade whose rays' light alone reaches the first,
    // plus one, or 0 for every cascade, the cascades above it giving nothing and those below it
    // passing on its light where their rays meet nothing and nothing where they meet a surface.
    // w: where the cascade above's interval ends, which its ray that met nothing reached.
    float4 spacing;
    // x: how many cascades. y: how far the probes moved since the frame before, three signed bytes,
    // with 1 << 24 where a cycle lies before to read and 1 << 25 where carried probes keep one. z:
    // the frames since the lights or the field changed. w: where the cycle stands, on sixteen.
    uint4 counts;
};

[[vk::push_constant]]
ConstantBuffer<MergePush> push;

// A carried probe's light, the mean of the cycle of sixteen places its rays move through, as the
// screen's probes keep theirs (gi_screen_filter.slang), gathered from the cycle's first frame and
// shown as it grows until a whole cycle has been gathered since the lights or the field changed,
// then the last whole cycle's. Read from the fixed middles alone, the vault of Sponza's vestibule
// took none of the court's sunlit light that a path-traced reference sees past its arch's edges.
// Its share of the sky and its mark of what the rays met are kept the same way, which the cascade
// below and the probe's faces read, since taken from each frame's places alone they moved with the
// rays, and every probe whose hits read them flickered with them, as the edges of Sponza's balcony
// did, and a probe below took the sky through a texel above in some frames and not others.
float3 settled(float3 light, inout float sky, inout float mark, uint3 at, uint2 texel, uint probes, uint texels, uint c)
{
    uint3 written = uint3(at.xy * texels + texel, at.z);
    if ((push.counts.y & 0x2000000u) == 0u)
        return light;
    if (placeOf(int3(at), c, probes).w < 1.5)
    {
        partialNow[written] = float4(0.0);
        fullNow[written] = float4(0.0);
        skyMarkNow[written] = float4(0.0);
        return light;
    }
    // The cycle as the frame before left it at the probe's place then, begun again where the
    // lights or the field changed this frame (counts.z, the frames since) or the probe's own
    // light fell, as its kept share says.
    uint packed = push.counts.y;
    float4 partial = float4(0.0), full = float4(0.0);
    float4 skyMark = float4(0.0);
    int3 shift = int3(int(packed << 24u) >> 24, int(packed << 16u) >> 24, int(packed << 8u) >> 24);
    int3 from = int3(at) + shift;
    if ((packed & 0x1000000u) != 0u && all(from >= int3(0)) && all(from < int3(int(probes))))
    {
        int4 then = int4(from.xy * int(texels) + int2(texel), from.z, 0);
        partial = partialBefore.Load(then);
        full = fullBefore.Load(then);
        skyMark = skyMarkBefore.Load(then);
    }
    float4 share = held[int3(2 * int(probes) + int(at.x), int(at.y), int(c * probes) + int(at.z))];
    bool begun = push.counts.z == 0u || share.r >= 0.0 && any(share.rgb < 0.99);
    uint phase = push.counts.w & 15u;
    bool going = partial.a > 0.5 && !begun;
    float count = going ? partial.a : 0.0;
    float3 mean = phase == 0u || !going ? light : lerp(partial.rgb, light, 1.0 / (count + 1.0));
    float2 means = phase == 0u || !going ? float2(sky, mark) : lerp(skyMark.xz, float2(sky, mark), 1.0 / (count + 1.0));
    bool whole = !begun && (phase == 0u ? going && count >= 15.5 : full.a > 0.5);
    float3 wholeMean = phase == 0u ? partial.rgb : full.rgb;
    float2 wholeMeans = phase == 0u ? skyMark.xz : skyMark.yw;
    partialNow[written] = float4(mean, phase == 0u || !going ? 1.0 : count + 1.0);
    fullNow[written] = whole ? float4(wholeMean, 1.0) : float4(0.0);
    float2 lastWhole = whole ? wholeMeans : float2(0.0);
    skyMarkNow[written] = float4(means.x, lastWhole.x, means.y, lastWhole.y);
    sky = whole ? wholeMeans.x : means.x;
    mark = whole ? wholeMeans.y : means.y;
    return whole ? wholeMean : mean;
}

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
    // The probes where the field's cascades they stand in lie, the far one's for the last.
    float4 oc = cascadeAt(fieldInfo, c, int(push.counts.x));
    float3 spot = oc.xyz + (float3(at) + 0.5) * (oc.w * push.spacing.x);
    float4 here = probe < probes * probes * probes ? placeOf(int3(at), c, probes) : float4(0.0);
    float3 origin = spot + here.xyz * (oc.w * push.spacing.x);
    uint upperCascade = min(c + 1u, push.counts.x - 1u);
    float4 ac = cascadeAt(fieldInfo, upperCascade, int(push.counts.x));
    float3 q = (spot - ac.xyz) / (ac.w * push.spacing.x) - 0.5;
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
        float3 upperProbe = ac.xyz + (float3(neighbor) + 0.5 + placeOf(neighbor, upperCascade, probes).xyz) * (ac.w * push.spacing.x);
        seen[slot * 8u + traced] = upper > 0u && probe < probes * probes * probes && oc.w > 0.0
            && seesPastLeaves(sceneField, fieldAlbedo, fieldInfo, origin, upperProbe, oc.w * 0.5) ? 1u : 0u;
    }
    GroupMemoryBarrierWithGroupSync();

    if (probe >= probes * probes * probes)
        return;
    if (oc.w <= 0.0 || here.w < 0.5)
    {
        merged[written] = float4(0.0);
        mergedSky[written] = 0.0;
        if ((push.counts.y & 0x2000000u) != 0u)
        {
            partialNow[written] = float4(0.0);
            fullNow[written] = float4(0.0);
            skyMarkNow[written] = float4(0.0);
        }
        return;
    }
    float4 own = radiance[written];
    // What most of the texel's rays met, and the share that met nothing, which takes the light from
    // beyond, the light of the rest in own's color (gi_trace.slang).
    float nothing = float(texelNothing(own.a, RaysAt(texels))) / float(RaysAt(texels));
    // Rays of the last cascade that met nothing, the sky's.
    float ownSky = abs(own.a - 0.25) < 0.1 ? 1.0 : 0.0;
    uint alone = uint(push.spacing.z + 0.5);
    if (alone > 0u && c >= alone)
    {
        merged[written] = float4(0.0, 0.0, 0.0, 1.0);
        mergedSky[written] = 0.0;
        if ((push.counts.y & 0x2000000u) != 0u)
        {
            partialNow[written] = float4(0.0);
            fullNow[written] = float4(0.0);
            skyMarkNow[written] = float4(0.0);
        }
        return;
    }
    if (alone > 0u && c + 1u < alone)
        own.rgb = float3(0.0);
    // A ray that met a surface keeps its light, marked a half where most met it before its interval.
    float kept = own.a > 0.25 && own.a < 0.75 ? 0.5 : 1.0;
    if (upper == 0u || nothing <= 0.0 || push.spacing.y > 0.5 || alone == c + 1u)
    {
        float ownMark = own.a < 0.75 ? kept : 1.0;
        float3 ownLight = settled(own.rgb, ownSky, ownMark, at, texel, probes, texels, c);
        merged[written] = float4(ownLight, ownMark);
        mergedSky[written] = ownSky;
        return;
    }

    // The cascade above's probes around this one, eight of them weighed by how near each is, and
    // passed over where it is inside a mesh or a surface hides it from this one.
    float3 f = q - float3(base);
    uint factor = upper / texels;
    float3 d = fromOctahedron((float2(texel) + 0.5) / float(texels));
    // Where this ray meets a surface past its interval, which ends at four times the probes'
    // spacing below the last cascade, up to where the cascade above's ends, or -1 where it meets
    // none or the cascade above's runs on to the field's edge.
    float end = 4.0 * oc.w * push.spacing.x;
    float limit = push.spacing.w;
    bool marched = limit < 2.5 * end;
    float occluder = marched ? metPastLeaves(sceneField, fieldAlbedo, fieldInfo, origin, d, end, limit) : -1.0;
    float3 sum = float3(0.0), agreedSum = float3(0.0);
    float sky = 0.0, agreedSky = 0.0;
    float weights = 0.0, agreedWeights = 0.0;
    for (int corner = 0; corner < 8; corner++)
    {
        int3 offset = int3(corner & 1, (corner >> 1) & 1, corner >> 2);
        int3 neighbor = clamp(base + offset, int3(0), int3(int(probes) - 1));
        float3 lerps = lerp(1.0 - f, f, float3(offset));
        float weight = lerps.x * lerps.y * lerps.z * float(seen[slot * 8u + uint(corner)]) / float(factor * factor);
        // The probe above read toward the point of this ray nearest where its own ray along the way
        // met a surface, and along the way itself where it met nothing.
        float3 upperAt = ac.xyz + (float3(neighbor) + 0.5 + placeOf(neighbor, upperCascade, probes).xyz) * (ac.w * push.spacing.x);
        // Its reach along this texel's middle way, which lies on the corner its rays' cells meet at
        // where its cascade sends four rays a texel, read as the mean of the four. Read from the
        // one cell past the corner, the probe in the lintel's hall of GlobalIlluminationTests read
        // its face toward the opening well under a path-traced reference.
        uint cells = max(uint(ReachTexels) / upper, 1u);
        int2 rt = min(int2(octahedron(d) * float(ReachTexels / int(cells))), int2(ReachTexels / int(cells) - 1)) * int(cells);
        float r = 0.0;
        for (uint k = 0u; k < cells * cells; k++)
            r += reachAbove.Load(int4(neighbor.xy * ReachTexels + rt + int2(k % cells, k / cells), int(c + 1u) * int(probes) + neighbor.z, 0));
        r /= float(cells * cells);
        float3 way = d;
        if (r > 0.0 && r < push.spacing.w * 0.999)
        {
            float3 met = upperAt + d * r;
            float3 toward = origin + d * max(dot(met - origin, d), 0.0) - upperAt;
            if (dot(toward, toward) > 1e-8)
                way = normalize(toward);
        }
        int2 first = int2(floor(octahedron(way) * float(upper) - float(factor) * 0.5 + 0.5));
        // Where along this ray the probe above's own hit lies, or past the limit where it met none.
        float along = r > 0.0 && r < limit * 0.999 ? dot(upperAt + d * r - origin, d) : 1e30;
        bool agrees = !marched || (occluder < 0.0 ? along >= limit * 0.999 : abs(along - occluder) <= max(2.0 * ac.w, 0.25 * occluder));
        // The texels of the cascade above that lie inside this one's, one or four of them, those
        // that met a surface before their interval dark, and those inside a mesh passed over.
        for (uint v = 0u; v < factor; v++)
            for (uint u = 0u; u < factor; u++)
            {
                int2 tt = foldedAt(first + int2(int(u), int(v)), int(upper));
                int4 from = int4(int2(neighbor.xy) * int(upper) + tt, neighbor.z, 0);
                float4 child = above.Load(from);
                if (child.a > 0.75)
                {
                    float itsSky = aboveSky.Load(from);
                    sum += child.rgb * weight;
                    sky += itsSky * weight;
                    weights += weight;
                    if (agrees)
                    {
                        agreedSum += child.rgb * weight;
                        agreedSky += itsSky * weight;
                        agreedWeights += weight;
                    }
                }
                else if (child.a > 0.25)
                {
                    weights += weight;
                    if (agrees)
                        agreedWeights += weight;
                }
            }
    }
    if (agreedWeights > 1e-4)
    {
        sum = agreedSum;
        sky = agreedSky;
        weights = agreedWeights;
    }
    else if (occluder >= 0.0)
    {
        sum = float3(0.0);
        sky = 0.0;
    }
    float3 beyond = weights > 1e-4 ? sum / weights : float3(0.0);
    float shareOfSky = weights > 1e-4 ? sky / weights * nothing : 0.0;
    float mark = own.a < 0.75 ? kept : 1.0;
    float3 light = settled(own.rgb + beyond * nothing, shareOfSky, mark, at, texel, probes, texels, c);
    merged[written] = float4(light, mark);
    mergedSky[written] = shareOfSky;
}
