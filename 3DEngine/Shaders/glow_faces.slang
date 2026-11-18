// Which faces of each glow light look out into the open, read from the scene's distance field and
// taken into the faces the light emits from (glowFaces in glow.slang), so a probe's ray's hit and a
// surface alike take no light from a face that lies against what the field holds solid, as a
// lamp's sides set into a wall. A workgroup a light's place and a thread a point, four points a face
// at the middles of its quarters, each marched out along the face's normal a cell at a time as far
// as the face's longer side and a cell. Each step reads the field at the middle of the cell it
// lies in, where no blend of the cells around mixes a surface's inside with the light's own face
// beside it. A cell within a quarter cell of the light's box is passed over; one whose distance is
// as far as the box's, within a quarter cell, steps on; one a quarter cell or more inside a surface
// buries the point; any other ends the march open. Reaching the end reads open, and a march the
// field's band cuts short, or a cell wider than the face's shorter side, reads nothing. Three
// points buried of four clear the face, two open keep it, and otherwise the face keeps what it was.
//
// A light's faces are decided again only where its place is new, it is not yet decided, a build of
// this frame changed the field within its reach, a finer cascade holds it than it was decided in,
// or its box changed size; every other frame the faces kept are applied again, the CPU writing
// every light's faces whole each frame. A light with no face open lights nothing, its reach 0.

import scenefield;
import glow;

[[vk::binding(0, 0)]]
Sampler3D sceneField;

[[vk::binding(1, 0)]]
ConstantBuffer<SceneFieldInfo> fieldInfo;

// The lights of global illumination (GiLights in gi.slang), read and written as bytes.
[[vk::binding(2, 0)]]
RWByteAddressBuffer lights;

// Each place's faces as last decided: bits 0 to 5 the faces open, bit 6 set once decided, bits 7 to
// 10 the cascade they were decided in, and the bits above a hash of the box's half sides.
[[vk::binding(3, 0)]]
RWStructuredBuffer<uint> kept;

struct FacesPush
{
    // x: the byte the glow lights' count lies at, the places whose light is the frame before's 32
    // bytes after it, a bit a place, and the lights GlowLightsHead bytes after it. y: the bytes of
    // a light. z and w: unused.
    uint4 at;
    // x: the cascades a build of this frame changed, a bit a cascade, none where no build did.
    uint4 built;
    // xyz: the least corner of the box the builds changed, against the frame's origin, and the most.
    float4 changedLow;
    float4 changedHigh;
};

[[vk::push_constant]]
ConstantBuffer<FacesPush> push;

groupshared uint buried[6];
groupshared uint shown[6];
groupshared uint decide;
groupshared uint fresher;

// How far q lies from the box about middle with the half sides given, 0 inside it.
float boxDistance(float3 q, float3 middle, float4 halves[3])
{
    float3 local = q - middle;
    float squared = 0.0;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        float side = halves[k].w;
        float along = side > 1e-6 ? abs(dot(local, halves[k].xyz)) / side : 0.0;
        float past = max(along - side, 0.0);
        squared += past * past;
    }
    return sqrt(squared);
}

// What the way out of `from` along `normal` meets: 1 open, 2 buried, 0 nothing read.
uint wayOut(float3 from, float3 normal, float reach, float3 middle, float4 halves[3], float shorter)
{
    int c = fieldCascade(fieldInfo, from, 1.0);
    if (c < 0)
        return 0u;
    float4 oc = fieldInfo.cascades[c].originAndCell;
    float cell = oc.w;
    if (cell > 2.0 * shorter)
        return 0u;
    [loop]
    for (int i = 0; i < 16; i++)
    {
        float t = (float(i) + 0.5) * cell;
        if (t > reach + cell)
            return 1u;
        float3 q = from + normal * t;
        if (fieldCascade(fieldInfo, q, 1.0) != c)
            return 0u;
        float3 at = oc.xyz + (floor((q - oc.xyz) / cell) + 0.5) * cell;
        float s = boxDistance(at, middle, halves);
        if (s < 0.25 * cell)
            continue;
        float d = fieldDistanceIn(sceneField, fieldInfo, c, at);
        if (d >= s - 0.25 * cell)
            continue;
        return d <= -0.25 * cell ? 2u : 1u;
    }
    return 1u;
}

[shader("compute")]
[numthreads(32, 1, 1)]
void computeMain(uint3 group : SV_GroupID, uint lane : SV_GroupIndex)
{
    uint place = group.x;
    uint count = uint(asfloat(lights.Load(push.at.x)));
    if (place >= min(count, uint(MostGlowLights)))
        return;
    uint at = push.at.x + uint(GlowLightsHead) + place * push.at.y;
    float4 middleAndReach = asfloat(lights.Load4(at));
    if (middleAndReach.w <= 0.0)
        return;
    float4 halves[3] = { asfloat(lights.Load4(at + 16u)), asfloat(lights.Load4(at + 32u)), asfloat(lights.Load4(at + 48u)) };
    float3 middle = middleAndReach.xyz;
    float glowW = asfloat(lights.Load(at + 76u));
    uint given = uint(floor(glowW * 0.5));
    given = given == 0u ? 63u : given;
    float share = glowW - 2.0 * floor(glowW * 0.5);

    // Whether to decide the faces again, which the first lane works out for all.
    uint state = kept[place];
    uint hash = (asuint(halves[0].w) * 73856093u ^ asuint(halves[1].w) * 19349663u ^ asuint(halves[2].w) * 83492791u) >> 11u;
    if (lane == 0u)
    {
        bool fresh = ((lights.Load(push.at.x + 32u + (place >> 5u) * 4u) >> (place & 31u)) & 1u) == 0u;
        int c = fieldCascade(fieldInfo, middle, 1.0);
        float grow = max(halves[0].w, max(halves[1].w, halves[2].w)) * 2.0 + (c >= 0 ? fieldInfo.cascades[c].originAndCell.w : 0.0);
        bool changed = push.built.x != 0u && all(middle + grow >= push.changedLow.xyz) && all(middle - grow <= push.changedHigh.xyz);
        bool finer = c >= 0 && (state & 64u) != 0u && uint(c) < ((state >> 7u) & 15u);
        decide = fresh || (state & 64u) == 0u || changed || finer || (state >> 11u) != hash ? 1u : 0u;
        fresher = fresh ? 1u : 0u;
        for (int f = 0; f < 6; f++)
        {
            buried[f] = 0u;
            shown[f] = 0u;
        }
    }
    GroupMemoryBarrierWithGroupSync();

    if (decide != 0u && lane < 24u)
    {
        uint f = lane / 4u, p = lane % 4u;
        uint k = f / 2u;
        float4 h = halves[k];
        if (((given >> f) & 1u) != 0u && h.w > 1e-6)
        {
            float3 normal = h.xyz / h.w * ((f & 1u) != 0u ? 1.0 : -1.0);
            float4 u = halves[(k + 1u) % 3u], v = halves[(k + 2u) % 3u];
            float3 point = middle + normal * h.w + u.xyz * ((p & 1u) != 0u ? 0.5 : -0.5) + v.xyz * ((p & 2u) != 0u ? 0.5 : -0.5);
            uint met = wayOut(point, normal, 2.0 * max(u.w, v.w), middle, halves, min(u.w, v.w));
            if (met == 2u)
                InterlockedAdd(buried[f], 1u);
            else if (met == 1u)
                InterlockedAdd(shown[f], 1u);
        }
    }
    GroupMemoryBarrierWithGroupSync();

    if (lane != 0u)
        return;
    uint open = given;
    if (decide != 0u)
    {
        // A face three of whose points are buried is closed and one two of whose points are open
        // is kept; any other keeps what was decided before, or the CPU's faces where none was or
        // the place holds another light than the one decided.
        uint before = (state & 64u) != 0u && fresher == 0u ? state & 63u : given;
        uint decided = 0u;
        for (uint f = 0u; f < 6u; f++)
        {
            bool keep = buried[f] >= 3u ? false : shown[f] >= 2u ? true : ((before >> f) & 1u) != 0u;
            if (keep)
                decided |= 1u << f;
        }
        int c = fieldCascade(fieldInfo, middle, 1.0);
        kept[place] = decided | 64u | (uint(max(c, 0)) & 15u) << 7u | hash << 11u;
        open = given & decided;
    }
    else
        open = given & (state & 63u);
    // The faces written, every face where all six are open, and a light with none open lights nothing.
    if (open == 0u)
        lights.Store(at + 12u, asuint(0.0));
    else
        lights.Store(at + 76u, asuint(share + 2.0 * float(open == 63u ? 0u : open)));
}
